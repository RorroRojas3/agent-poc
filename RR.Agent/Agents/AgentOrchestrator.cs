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
    private readonly IFileManager _fileManager;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IPlanningModule planningModule,
        IExecutionEngine executionEngine,
        IEvaluationModule evaluationModule,
        IRetryStrategy retryStrategy,
        IFileManager fileManager,
        IOptions<AgentOptions> options,
        ILogger<AgentOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(planningModule);
        ArgumentNullException.ThrowIfNull(executionEngine);
        ArgumentNullException.ThrowIfNull(evaluationModule);
        ArgumentNullException.ThrowIfNull(retryStrategy);
        ArgumentNullException.ThrowIfNull(fileManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _planningModule = planningModule;
        _executionEngine = executionEngine;
        _evaluationModule = evaluationModule;
        _retryStrategy = retryStrategy;
        _fileManager = fileManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OrchestratorResult> ProcessRequestAsync(
        string userRequest,
        IEnumerable<string>? inputFilePaths = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteFullPipelineAsync(userRequest, inputFilePaths, cancellationToken);
    }

    public async IAsyncEnumerable<OrchestratorUpdate> ProcessRequestStreamingAsync(
        string userRequest,
        IEnumerable<string>? inputFilePaths = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);

        _logger.LogInformation("Starting orchestration for request: {Request}", userRequest);

        var stepResults = new Dictionary<int, ExecutionResult>();
        IReadOnlyList<InputFile> inputFiles = [];

        // Phase 0: Prepare input files (if any)
        var filePaths = inputFilePaths?.ToList() ?? [];
        if (filePaths.Count > 0)
        {
            yield return new OrchestratorUpdate(
                OrchestratorPhase.PreparingFiles,
                $"Preparing {filePaths.Count} input file(s)...");

            var (preparedFiles, fileError) = await PrepareFilesSafelyAsync(filePaths, cancellationToken);

            if (fileError is not null)
            {
                _logger.LogError(fileError, "File preparation failed");
                yield return new OrchestratorUpdate(
                    OrchestratorPhase.Failed,
                    $"File preparation failed: {fileError.Message}");
                yield break;
            }

            inputFiles = preparedFiles ?? [];

            yield return new OrchestratorUpdate(
                OrchestratorPhase.PreparingFiles,
                $"Prepared {inputFiles.Count} file(s): {string.Join(", ", inputFiles.Select(f => f.FileName))}");
        }

        // Phase 1: Planning
        yield return new OrchestratorUpdate(
            OrchestratorPhase.Planning,
            "Analyzing request and creating execution plan...");

        // Create plan (handle exceptions outside the try-catch to enable yielding)
        var (plan, planError) = await CreatePlanSafelyAsync(userRequest, inputFiles, cancellationToken);

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
                    inputFiles,
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
                    // Success - download any generated files
                    if (result.GeneratedFiles.Count > 0)
                    {
                        // Exclude input files and scripts from download
                        var excludeFileIds = inputFiles
                            .Where(f => f.IsUploaded)
                            .Select(f => f.AgentFileId!)
                            .ToHashSet();

                        // Also exclude scripts if we have the ID
                        if (result.Script?.AgentFileId is not null)
                        {
                            excludeFileIds.Add(result.Script.AgentFileId);
                        }

                        var downloadedFiles = await _fileManager.DownloadGeneratedFilesAsync(
                            result.GeneratedFiles,
                            excludeFileIds,
                            cancellationToken);

                        if (downloadedFiles.Count > 0)
                        {
                            yield return new OrchestratorUpdate(
                                OrchestratorPhase.Executing,
                                $"Downloaded {downloadedFiles.Count} output file(s): {string.Join(", ", downloadedFiles.Select(Path.GetFileName))}",
                                step.Order,
                                plan.Steps.Count);
                        }
                    }

                    // Move to next step
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

    private async Task<(IReadOnlyList<InputFile>? Files, Exception? Error)> PrepareFilesSafelyAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await _fileManager.PrepareInputFilesAsync(filePaths, cancellationToken);
            return (files, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private async Task<(ExecutionPlan? Plan, Exception? Error)> CreatePlanSafelyAsync(
        string userRequest,
        IReadOnlyList<InputFile> inputFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _planningModule.CreatePlanAsync(userRequest, inputFiles, cancellationToken);
            return (plan, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private async Task<OrchestratorResult> ExecuteFullPipelineAsync(
        string userRequest,
        IEnumerable<string>? inputFilePaths,
        CancellationToken cancellationToken)
    {
        var stepResults = new Dictionary<int, ExecutionResult>();
        ExecutionPlan plan;
        IReadOnlyList<InputFile> inputFiles = [];

        // Prepare input files if any
        var filePaths = inputFilePaths?.ToList() ?? [];
        if (filePaths.Count > 0)
        {
            inputFiles = await _fileManager.PrepareInputFilesAsync(filePaths, cancellationToken);
        }

        try
        {
            plan = await _planningModule.CreatePlanAsync(userRequest, inputFiles, cancellationToken);

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
                    inputFiles,
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
