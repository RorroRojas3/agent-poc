using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;

namespace RR.Agent.Service.Executors;

/// <summary>
/// Executor that uses an AI agent to evaluate execution results.
/// </summary>
public sealed class EvaluatorExecutor
{
    private readonly AgentService _agentService;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<EvaluatorExecutor> _logger;

    private const string AgentName = "Evaluator";

    public EvaluatorExecutor(
        AgentService agentService,
        IOptions<AgentOptions> agentOptions,
        ILogger<EvaluatorExecutor> logger)
    {
        _agentService = agentService;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates the execution result of a task step.
    /// </summary>
    public async Task<EvaluatorOutput> ExecuteAsync(EvaluatorInput input, CancellationToken cancellationToken = default)
    {
        var step = input.Step;
        step.Status = TaskStatuses.Evaluating;

        try
        {
            _logger.LogInformation("Evaluating step {StepNumber}", step.StepNumber);

            // Create the evaluator agent (optionally with structured output)
            var responseFormat = _agentOptions.UseStructuredOutput
                ? ResponseSchemas.EvaluatorResponseSchema
                : null;

            var agent = await _agentService.GetOrCreateAgentAsync(
                AgentName,
                AgentPrompts.EvaluatorSystemPrompt,
                responseFormat: responseFormat,
                cancellationToken: cancellationToken);

            // Create a thread for this evaluation
            var thread = await _agentService.CreateThreadAsync(
                $"evaluator-step{step.StepNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                cancellationToken);

            // Build the prompt
            var prompt = AgentPrompts.GetEvaluatorPrompt(
                step.Description,
                step.ExpectedOutput,
                input.ExecutionResult.StandardOutput,
                input.ExecutionResult.StandardError,
                input.ExecutionResult.ExitCode,
                step.AttemptCount,
                _agentOptions.MaxRetryAttempts);

            // Send message and run agent
            var run = await _agentService.SendMessageAndRunAsync(
                thread.Id,
                agent.Id,
                prompt,
                cancellationToken);

            // Wait for completion
            var completedRun = await _agentService.WaitForRunCompletionAsync(
                thread.Id,
                run.Id,
                cancellationToken: cancellationToken);

            if (completedRun.Status != Azure.AI.Agents.Persistent.RunStatus.Completed)
            {
                return CreateErrorOutput(input, $"Agent run failed with status: {completedRun.Status}");
            }

            // Get the response
            var response = await _agentService.GetLatestAssistantMessageAsync(
                thread.Id,
                cancellationToken);

            if (string.IsNullOrEmpty(response))
            {
                return CreateErrorOutput(input, "No response from evaluator agent");
            }

            // Parse the evaluation
            var evaluation = ParseEvaluationResponse(response);
            if (evaluation == null)
            {
                return CreateErrorOutput(input, "Failed to parse evaluation from agent response");
            }

            // Apply retry logic based on attempt count
            if (!evaluation.IsSuccessful && step.AttemptCount >= _agentOptions.MaxRetryAttempts)
            {
                evaluation.ShouldRetry = false;
                if (!evaluation.IsImpossible)
                {
                    evaluation.IsImpossible = true;
                    evaluation.Reasoning += $" (Max retry attempts ({_agentOptions.MaxRetryAttempts}) reached)";
                }
            }

            // Update step with evaluation
            step.Evaluation = evaluation;
            step.Status = evaluation.IsSuccessful
                ? Model.Enums.TaskStatuses.Completed
                : evaluation.IsImpossible
                    ? Model.Enums.TaskStatuses.Impossible
                    : Model.Enums.TaskStatuses.Failed;

            // Update context
            input.Context.LastEvaluation = evaluation;
            input.Context.AddMessage(AgentRole.Evaluator, response);
            input.Context.IterationCount++;

            // Determine workflow continuation
            bool shouldContinue;
            bool isTaskComplete;
            bool needsReplan;

            if (evaluation.IsSuccessful)
            {
                // Step succeeded, check if there are more steps
                var plan = input.Context.Plan;
                if (plan.MoveToNextStep())
                {
                    input.Context.CurrentStep = plan.CurrentStep;
                    shouldContinue = true;
                    isTaskComplete = false;
                    needsReplan = false;
                }
                else
                {
                    // All steps completed
                    plan.Status = TaskStatuses.Completed;
                    plan.CompletedAt = DateTime.UtcNow;
                    shouldContinue = false;
                    isTaskComplete = true;
                    needsReplan = false;
                }
            }
            else if (evaluation.IsImpossible)
            {
                // Task is impossible
                input.Context.Plan.Status = TaskStatuses.Impossible;
                shouldContinue = false;
                isTaskComplete = true;
                needsReplan = false;
            }
            else if (evaluation.ShouldRetry)
            {
                // Retry needed
                if (evaluation.RevisedApproach != null)
                {
                    // Need to replan
                    needsReplan = true;
                    shouldContinue = true;
                }
                else
                {
                    // Retry same step
                    needsReplan = false;
                    shouldContinue = true;
                }
                isTaskComplete = false;
            }
            else
            {
                // Failed but no retry
                input.Context.Plan.Status = TaskStatuses.Failed;
                shouldContinue = false;
                isTaskComplete = true;
                needsReplan = false;
            }

            // Update plan iteration count
            input.Context.Plan.TotalIterations = input.Context.IterationCount;

            // Check max iterations
            if (input.Context.IterationCount >= _agentOptions.MaxIterations)
            {
                _logger.LogWarning("Max iterations ({Max}) reached", _agentOptions.MaxIterations);
                input.Context.Plan.Status = TaskStatuses.Failed;
                shouldContinue = false;
                isTaskComplete = true;
            }

            _logger.LogInformation(
                "Step {StepNumber} evaluation: Success={Success}, ShouldContinue={Continue}, IsComplete={Complete}, NeedsReplan={Replan}",
                step.StepNumber, evaluation.IsSuccessful, shouldContinue, isTaskComplete, needsReplan);

            return new EvaluatorOutput
            {
                Context = input.Context,
                Evaluation = evaluation,
                ShouldContinue = shouldContinue,
                IsTaskComplete = isTaskComplete,
                NeedsReplan = needsReplan
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating step {StepNumber}", step.StepNumber);
            return CreateErrorOutput(input, $"Evaluation error: {ex.Message}");
        }
    }

    private EvaluationResult? ParseEvaluationResponse(string response)
    {
        try
        {
            // Clean up the response (remove markdown code blocks if present)
            var json = response.Trim();
            if (json.StartsWith("```json"))
            {
                json = json[7..];
            }
            else if (json.StartsWith("```"))
            {
                json = json[3..];
            }
            if (json.EndsWith("```"))
            {
                json = json[..^3];
            }
            json = json.Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var evaluation = new EvaluationResult
            {
                IsSuccessful = root.TryGetProperty("isSuccessful", out var success) && success.GetBoolean(),
                IsImpossible = root.TryGetProperty("isImpossible", out var impossible) && impossible.GetBoolean(),
                Reasoning = root.TryGetProperty("reasoning", out var reasoning)
                    ? reasoning.GetString() ?? ""
                    : "",
                ShouldRetry = root.TryGetProperty("shouldRetry", out var retry) && retry.GetBoolean(),
                RevisedApproach = root.TryGetProperty("revisedApproach", out var revised) &&
                    revised.ValueKind == JsonValueKind.String
                    ? revised.GetString()
                    : null,
                ConfidenceScore = root.TryGetProperty("confidenceScore", out var confidence)
                    ? confidence.GetDouble()
                    : 0.5
            };

            // Parse issues
            if (root.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
            {
                evaluation.Issues = issues.EnumerateArray()
                    .Select(i => i.GetString())
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Cast<string>()
                    .ToList();
            }

            // Parse suggestions
            if (root.TryGetProperty("suggestions", out var suggestions) && suggestions.ValueKind == JsonValueKind.Array)
            {
                evaluation.Suggestions = suggestions.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Cast<string>()
                    .ToList();
            }

            return evaluation;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON evaluation response: {Response}", TruncateForLog(response));
            return null;
        }
    }

    private EvaluatorOutput CreateErrorOutput(EvaluatorInput input, string error)
    {
        var evaluation = EvaluationResult.FailedWithRetry(
            error,
            [error],
            ["Check agent connectivity and try again"]);

        input.Step.Evaluation = evaluation;
        input.Context.LastEvaluation = evaluation;

        return new EvaluatorOutput
        {
            Context = input.Context,
            Evaluation = evaluation,
            ShouldContinue = false,
            IsTaskComplete = false,
            NeedsReplan = false
        };
    }

    private static string TruncateForLog(string message, int maxLength = 200)
    {
        if (message.Length <= maxLength)
        {
            return message;
        }
        return message[..maxLength] + "...";
    }
}
