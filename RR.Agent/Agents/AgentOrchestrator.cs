namespace RR.Agent.Agents;

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Evaluation;
using RR.Agent.Evaluation.Models;
using RR.Agent.Exceptions;
using RR.Agent.Execution;
using RR.Agent.Execution.Models;
using RR.Agent.Planning;
using RR.Agent.Planning.Models;

/// <summary>
/// Orchestrates the complete agent workflow: planning, execution, and evaluation.
/// </summary>
public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IPlanningModule _planningModule;
    private readonly IExecutionEngine _executionEngine;
    private readonly IEvaluationModule _evaluationModule;
    private readonly IRetryStrategy _retryStrategy;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IPlanningModule planningModule,
        IExecutionEngine executionEngine,
        IEvaluationModule evaluationModule,
        IRetryStrategy retryStrategy,
        IOptions<AgentOptions> options,
        ILogger<AgentOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(planningModule);
        ArgumentNullException.ThrowIfNull(executionEngine);
        ArgumentNullException.ThrowIfNull(evaluationModule);
        ArgumentNullException.ThrowIfNull(retryStrategy);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _planningModule = planningModule;
        _executionEngine = executionEngine;
        _evaluationModule = evaluationModule;
        _retryStrategy = retryStrategy;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OrchestratorResult> ProcessRequestAsync(
        string userRequest,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteFullPipelineAsync(userRequest, cancellationToken);
    }

    public async IAsyncEnumerable<OrchestratorUpdate> ProcessRequestStreamingAsync(
        string userRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);

        _logger.LogInformation("Starting orchestration for request: {Request}", userRequest);

        var stepResults = new Dictionary<int, ExecutionResult>();

        // Phase 1: Planning
        yield return new OrchestratorUpdate(
            OrchestratorPhase.Planning,
            "Analyzing request and creating execution plan...");

        // Create plan (handle exceptions outside the try-catch to enable yielding)
        var (plan, planError) = await CreatePlanSafelyAsync(userRequest, cancellationToken);

        if (planError is not null)
        {
            _logger.LogError(planError, "Planning failed");
            yield return new OrchestratorUpdate(
                OrchestratorPhase.Failed,
                $"Planning failed: {planError.Message}");
            yield break;
        }

        if (plan is null || !_planningModule.ValidatePlan(plan))
        {
            yield return new OrchestratorUpdate(
                OrchestratorPhase.Failed,
                "Generated plan is invalid");
            yield break;
        }

        // Phase 2: Present Plan
        yield return new OrchestratorUpdate(
            OrchestratorPhase.PlanPresentation,
            FormatPlanPresentation(plan));

        // Phase 3: Execute Steps
        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new OrchestratorUpdate(
                OrchestratorPhase.Executing,
                $"Step {step.Order}: {step.Description}",
                step.Order,
                plan.Steps.Count);

            var retryContext = _retryStrategy.CreateInitialContext(_options.MaxRetryAttempts);
            ExecutionResult? result = null;
            EvaluationResult? evaluation = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Execute the step
                var retryHint = evaluation?.RetryContext?.SuggestedAdjustment;
                result = await _executionEngine.ExecuteStepAsync(
                    step,
                    stepResults,
                    retryHint,
                    cancellationToken);

                // Evaluate the result
                yield return new OrchestratorUpdate(
                    OrchestratorPhase.Evaluating,
                    $"Evaluating step {step.Order} result...",
                    step.Order,
                    plan.Steps.Count);

                evaluation = await _evaluationModule.EvaluateAsync(
                    step,
                    result,
                    retryContext,
                    cancellationToken);

                if (evaluation.ShouldProceed)
                {
                    // Success - move to next step
                    stepResults[step.Order] = result;
                    break;
                }

                if (evaluation.ShouldRetry)
                {
                    // Retry with new context
                    retryContext = evaluation.RetryContext!;

                    var delay = _retryStrategy.GetDelay(retryContext.AttemptNumber);

                    yield return new OrchestratorUpdate(
                        OrchestratorPhase.Retrying,
                        $"Retrying step {step.Order} (attempt {retryContext.AttemptNumber}/{retryContext.MaxAttempts}) after {delay.TotalSeconds}s delay...",
                        step.Order,
                        plan.Steps.Count);

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                // Cannot proceed - failure or impossible
                _logger.LogWarning(
                    "Step {Order} failed: {Reason}",
                    step.Order,
                    evaluation.Reasoning);

                yield return new OrchestratorUpdate(
                    OrchestratorPhase.Failed,
                    $"Step {step.Order} failed: {evaluation.Reasoning}",
                    step.Order,
                    plan.Steps.Count);

                yield break;
            }
        }

        // Phase 4: Complete
        var finalOutput = GenerateFinalOutput(plan, stepResults);

        yield return new OrchestratorUpdate(
            OrchestratorPhase.Completed,
            finalOutput);

        _logger.LogInformation("Orchestration completed successfully");
    }

    private async Task<(ExecutionPlan? Plan, Exception? Error)> CreatePlanSafelyAsync(
        string userRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _planningModule.CreatePlanAsync(userRequest, cancellationToken);
            return (plan, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private async Task<OrchestratorResult> ExecuteFullPipelineAsync(
        string userRequest,
        CancellationToken cancellationToken)
    {
        var stepResults = new Dictionary<int, ExecutionResult>();
        ExecutionPlan plan;

        try
        {
            plan = await _planningModule.CreatePlanAsync(userRequest, cancellationToken);

            if (!_planningModule.ValidatePlan(plan))
            {
                return new OrchestratorResult(
                    Success: false,
                    Plan: plan,
                    StepResults: [],
                    FinalOutput: null,
                    FailureReason: "Generated plan is invalid");
            }
        }
        catch (PlanningException ex)
        {
            throw new AgentException($"Planning failed: {ex.Message}", ex);
        }

        foreach (var step in plan.Steps)
        {
            var retryContext = _retryStrategy.CreateInitialContext(_options.MaxRetryAttempts);
            ExecutionResult? result = null;
            EvaluationResult? evaluation = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var retryHint = evaluation?.RetryContext?.SuggestedAdjustment;
                result = await _executionEngine.ExecuteStepAsync(
                    step,
                    stepResults,
                    retryHint,
                    cancellationToken);

                evaluation = await _evaluationModule.EvaluateAsync(
                    step,
                    result,
                    retryContext,
                    cancellationToken);

                if (evaluation.ShouldProceed)
                {
                    stepResults[step.Order] = result;
                    break;
                }

                if (evaluation.ShouldRetry)
                {
                    retryContext = evaluation.RetryContext!;
                    var delay = _retryStrategy.GetDelay(retryContext.AttemptNumber);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return new OrchestratorResult(
                    Success: false,
                    Plan: plan,
                    StepResults: stepResults.Values.ToList(),
                    FinalOutput: null,
                    FailureReason: $"Step {step.Order} failed: {evaluation.Reasoning}");
            }
        }

        var finalOutput = GenerateFinalOutput(plan, stepResults);

        return new OrchestratorResult(
            Success: true,
            Plan: plan,
            StepResults: stepResults.Values.ToList(),
            FinalOutput: finalOutput,
            FailureReason: null);
    }

    private static string FormatPlanPresentation(ExecutionPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution Plan: {plan.Summary}");
        sb.AppendLine($"Complexity: {plan.EstimatedComplexity}/10");
        sb.AppendLine($"Steps: {plan.Steps.Count}");
        sb.AppendLine();

        foreach (var step in plan.Steps)
        {
            sb.AppendLine($"  {step.Order}. [{step.Type}] {step.Description}");
            sb.AppendLine($"     Expected: {step.ExpectedOutput}");

            if (step.Dependencies.Count > 0)
            {
                sb.AppendLine($"     Depends on: {string.Join(", ", step.Dependencies)}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateFinalOutput(
        ExecutionPlan plan,
        Dictionary<int, ExecutionResult> stepResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Execution Complete ===");
        sb.AppendLine($"Plan: {plan.Summary}");
        sb.AppendLine();

        foreach (var step in plan.Steps)
        {
            if (stepResults.TryGetValue(step.Order, out var result))
            {
                sb.AppendLine($"Step {step.Order}: {(result.IsSuccess ? "SUCCESS" : "FAILED")}");

                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    sb.AppendLine($"  Output: {TruncateOutput(result.Output, 500)}");
                }

                if (result.GeneratedFiles.Count > 0)
                {
                    sb.AppendLine($"  Files: {string.Join(", ", result.GeneratedFiles)}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (output.Length <= maxLength)
        {
            return output;
        }

        return output[..maxLength] + "... (truncated)";
    }
}
