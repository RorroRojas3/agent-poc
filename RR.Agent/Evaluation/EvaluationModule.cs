namespace RR.Agent.Evaluation;

using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Evaluation.Models;
using RR.Agent.Execution.Models;
using RR.Agent.Infrastructure;
using RR.Agent.Planning.Models;
using RR.Agent.Tools;

/// <summary>
/// Evaluates execution results and determines next actions using AI analysis.
/// </summary>
public sealed class EvaluationModule : IEvaluationModule
{
    private const string EvaluationPrompt = """
        You are an evaluation agent that analyzes execution results and determines next actions.

        Analyze the execution result and provide:
        1. Whether the step succeeded in achieving its goal
        2. If it failed, whether it's retryable or impossible
        3. A suggested adjustment for retry if applicable

        Common patterns that indicate impossibility:
        - Missing dependencies that cannot be installed
        - Access denied or permission errors
        - Resource limits exceeded
        - Logical impossibility in the request

        Respond in JSON format:
        {
            "success": true/false,
            "retryable": true/false,
            "impossible": true/false,
            "reasoning": "explanation",
            "suggestedAdjustment": "what to try differently (if retryable)"
        }
        """;

    private readonly PersistentAgentsClient _client;
    private readonly AzureAIFoundryOptions _aiOptions;
    private readonly AgentOptions _agentOptions;
    private readonly IToolProvider _toolProvider;
    private readonly IRunPoller _runPoller;
    private readonly IMessageProcessor _messageProcessor;
    private readonly IRetryStrategy _retryStrategy;
    private readonly ILogger<EvaluationModule> _logger;

    private string? _evaluationAgentId;

    public EvaluationModule(
        PersistentAgentsClient client,
        IOptions<AzureAIFoundryOptions> aiOptions,
        IOptions<AgentOptions> agentOptions,
        IToolProvider toolProvider,
        IRunPoller runPoller,
        IMessageProcessor messageProcessor,
        IRetryStrategy retryStrategy,
        ILogger<EvaluationModule> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(aiOptions);
        ArgumentNullException.ThrowIfNull(agentOptions);
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(runPoller);
        ArgumentNullException.ThrowIfNull(messageProcessor);
        ArgumentNullException.ThrowIfNull(retryStrategy);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _aiOptions = aiOptions.Value;
        _agentOptions = agentOptions.Value;
        _toolProvider = toolProvider;
        _runPoller = runPoller;
        _messageProcessor = messageProcessor;
        _retryStrategy = retryStrategy;
        _logger = logger;
    }

    public async Task<EvaluationResult> EvaluateAsync(
        PlanStep step,
        ExecutionResult result,
        RetryContext? retryContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(result);

        _logger.LogDebug(
            "Evaluating step {Order} with status {Status}",
            step.Order,
            result.Status);

        // Initialize retry context if not provided
        retryContext ??= _retryStrategy.CreateInitialContext(_agentOptions.MaxRetryAttempts);

        // Quick path for clear success
        if (result.IsSuccess)
        {
            _logger.LogInformation("Step {Order} succeeded", step.Order);

            return new EvaluationResult(
                Verdict: EvaluationVerdict.Success,
                Reasoning: "Step executed successfully",
                RetryContext: null,
                OriginalResult: result);
        }

        // Quick path for cancellation
        if (result.Status == ExecutionStatus.Cancelled)
        {
            return new EvaluationResult(
                Verdict: EvaluationVerdict.Impossible,
                Reasoning: "Execution was cancelled",
                RetryContext: null,
                OriginalResult: result);
        }

        // For failures, use AI to analyze whether to retry
        var aiEvaluation = await AnalyzeWithAIAsync(step, result, retryContext, cancellationToken);

        if (aiEvaluation.IsImpossible)
        {
            _logger.LogWarning(
                "Step {Order} determined to be impossible: {Reason}",
                step.Order,
                aiEvaluation.Reasoning);

            return new EvaluationResult(
                Verdict: EvaluationVerdict.Impossible,
                Reasoning: aiEvaluation.Reasoning,
                RetryContext: null,
                OriginalResult: result);
        }

        if (aiEvaluation.IsRetryable && _retryStrategy.ShouldRetry(retryContext))
        {
            var newContext = retryContext.Next(
                result.ErrorMessage ?? "Unknown error",
                aiEvaluation.SuggestedAdjustment);

            _logger.LogInformation(
                "Step {Order} will be retried (attempt {Attempt}/{Max}): {Adjustment}",
                step.Order,
                newContext.AttemptNumber,
                newContext.MaxAttempts,
                aiEvaluation.SuggestedAdjustment);

            return new EvaluationResult(
                Verdict: EvaluationVerdict.Retry,
                Reasoning: aiEvaluation.Reasoning,
                RetryContext: newContext,
                OriginalResult: result);
        }

        // No more retries available
        _logger.LogWarning(
            "Step {Order} failed after {Attempts} attempts",
            step.Order,
            retryContext.AttemptNumber);

        return new EvaluationResult(
            Verdict: EvaluationVerdict.Impossible,
            Reasoning: $"Failed after {retryContext.AttemptNumber} attempts: {aiEvaluation.Reasoning}",
            RetryContext: retryContext,
            OriginalResult: result);
    }

    public async Task<(bool IsImpossible, string Explanation)> AnalyzeImpossibilityAsync(
        IReadOnlyList<string> failures,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (failures.Count == 0)
        {
            return (false, "No failures to analyze");
        }

        await EnsureEvaluationAgentExistsAsync(cancellationToken);

        var failureList = string.Join("\n- ", failures);
        var prompt = $"""
            Analyze these failure messages and determine if the task is impossible:

            Failures:
            {failureList}

            Is this task fundamentally impossible, or just encountering temporary issues?
            Respond with a JSON object with "impossible" (boolean) and "explanation" (string) properties.
            """;

        var threadResponse = await _client.Threads.CreateThreadAsync();
        var thread = threadResponse.Value;

        await _client.Messages.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: prompt);

        var runResponse = await _client.Runs.CreateRunAsync(
            thread.Id,
            _evaluationAgentId!);
        var run = runResponse.Value;

        var completedRun = await _runPoller.WaitForCompletionAsync(
            thread.Id,
            run.Id,
            cancellationToken);

        var response = await _messageProcessor.GetLatestAssistantMessageAsync(thread.Id, cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            return (false, "Could not analyze failures");
        }

        // Parse the response
        try
        {
            var json = ExtractJson(response);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var impossible = doc.RootElement.GetProperty("impossible").GetBoolean();
            var explanation = doc.RootElement.GetProperty("explanation").GetString() ?? "";

            return (impossible, explanation);
        }
        catch
        {
            return (false, response);
        }
    }

    private async Task<AIEvaluationResult> AnalyzeWithAIAsync(
        PlanStep step,
        ExecutionResult result,
        RetryContext retryContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureEvaluationAgentExistsAsync(cancellationToken);

            var previousErrors = retryContext.PreviousErrors.Count > 0
                ? string.Join("\n- ", retryContext.PreviousErrors)
                : "None";

            var prompt = $"""
                Evaluate this execution result:

                Step: {step.Description}
                Expected output: {step.ExpectedOutput}
                Status: {result.Status}
                Output: {result.Output ?? "None"}
                Error: {result.ErrorMessage ?? "None"}
                Attempt: {retryContext.AttemptNumber}/{retryContext.MaxAttempts}

                Previous errors in this step:
                {previousErrors}

                Analyze and provide your evaluation in JSON format.
                """;

            var threadResponse = await _client.Threads.CreateThreadAsync();
            var thread = threadResponse.Value;

            await _client.Messages.CreateMessageAsync(
                threadId: thread.Id,
                role: MessageRole.User,
                content: prompt);

            var runResponse = await _client.Runs.CreateRunAsync(
                thread.Id,
                _evaluationAgentId!);
            var run = runResponse.Value;

            var completedRun = await _runPoller.WaitForCompletionAsync(
                thread.Id,
                run.Id,
                cancellationToken);

            var response = await _messageProcessor.GetLatestAssistantMessageAsync(thread.Id, cancellationToken);

            return ParseEvaluationResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI evaluation failed, using heuristic evaluation");

            // Fallback to heuristic evaluation
            return new AIEvaluationResult(
                IsSuccess: false,
                IsRetryable: result.CanRetry,
                IsImpossible: false,
                Reasoning: result.ErrorMessage ?? "Execution failed",
                SuggestedAdjustment: null);
        }
    }

    private static AIEvaluationResult ParseEvaluationResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new AIEvaluationResult(
                IsSuccess: false,
                IsRetryable: true,
                IsImpossible: false,
                Reasoning: "No response from evaluation agent",
                SuggestedAdjustment: null);
        }

        try
        {
            var json = ExtractJson(response);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new AIEvaluationResult(
                IsSuccess: root.TryGetProperty("success", out var s) && s.GetBoolean(),
                IsRetryable: root.TryGetProperty("retryable", out var r) && r.GetBoolean(),
                IsImpossible: root.TryGetProperty("impossible", out var i) && i.GetBoolean(),
                Reasoning: root.TryGetProperty("reasoning", out var reason)
                    ? reason.GetString() ?? "No reasoning provided"
                    : "No reasoning provided",
                SuggestedAdjustment: root.TryGetProperty("suggestedAdjustment", out var adj)
                    ? adj.GetString()
                    : null);
        }
        catch
        {
            // If parsing fails, extract what we can from the text
            var lowerResponse = response.ToLowerInvariant();
            var isSuccess = lowerResponse.Contains("success") && !lowerResponse.Contains("not success");
            var isImpossible = lowerResponse.Contains("impossible");

            return new AIEvaluationResult(
                IsSuccess: isSuccess,
                IsRetryable: !isSuccess && !isImpossible,
                IsImpossible: isImpossible,
                Reasoning: response,
                SuggestedAdjustment: null);
        }
    }

    private static string ExtractJson(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)];
        }

        return response;
    }

    private async Task EnsureEvaluationAgentExistsAsync(CancellationToken cancellationToken)
    {
        if (_evaluationAgentId is not null)
        {
            return;
        }

        _logger.LogDebug("Creating evaluation agent");

        var tools = _toolProvider.GetToolDefinitions().ToList();
        var agentResponse = await _client.Administration.CreateAgentAsync(
            model: _aiOptions.DefaultModel,
            name: "EvaluationAgent",
            instructions: EvaluationPrompt,
            tools: tools,
            toolResources: _toolProvider.GetToolResources());

        _evaluationAgentId = agentResponse.Value.Id;
        _logger.LogInformation("Created evaluation agent with ID: {AgentId}", _evaluationAgentId);
    }

    private sealed record AIEvaluationResult(
        bool IsSuccess,
        bool IsRetryable,
        bool IsImpossible,
        string Reasoning,
        string? SuggestedAdjustment);
}
