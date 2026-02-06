using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;
using RR.Agent.Service.Executors;
using RR.Agent.Service.Python;

namespace RR.Agent.Service.Workflows;

/// <summary>
/// Orchestrates the multi-agent workflow for task execution.
/// Implements the Plan -> Execute -> Evaluate loop.
/// </summary>
public sealed class AgentWorkflow
{
    private readonly PlannerExecutor _planner;
    private readonly CodeExecutor _codeExecutor;
    private readonly EvaluatorExecutor _evaluator;
    private readonly IPythonEnvironmentService _pythonEnv;
    private readonly AgentService _agentService;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentWorkflow> _logger;

    public AgentWorkflow(
        PlannerExecutor planner,
        CodeExecutor codeExecutor,
        EvaluatorExecutor evaluator,
        IPythonEnvironmentService pythonEnv,
        AgentService agentService,
        IOptions<AgentOptions> options,
        ILogger<AgentWorkflow> logger)
    {
        _planner = planner;
        _codeExecutor = codeExecutor;
        _evaluator = evaluator;
        _pythonEnv = pythonEnv;
        _agentService = agentService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when workflow state changes.
    /// </summary>
    public event EventHandler<WorkflowStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Executes the complete workflow for a given task.
    /// </summary>
    public async Task<WorkflowContext> ExecuteAsync(string task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow for task: {Task}", TruncateForLog(task));

        // Initialize Python environment
        RaiseStateChanged("Initializing", "Setting up Python environment...");
        var envInitialized = await _pythonEnv.InitializeEnvironmentAsync(
            _options.WorkspaceDirectory,
            cancellationToken);

        if (!envInitialized)
        {
            throw new InvalidOperationException("Failed to initialize Python environment");
        }

        // Phase 1: Planning
        RaiseStateChanged("Planning", "Creating execution plan...");
        var plannerInput = new PlannerInput { Task = task };
        var plannerOutput = await _planner.ExecuteAsync(plannerInput, cancellationToken);

        if (!plannerOutput.Success)
        {
            _logger.LogError("Planning failed: {Error}", plannerOutput.Error);
            throw new InvalidOperationException($"Planning failed: {plannerOutput.Error}");
        }

        var context = plannerOutput.Context;
        var plan = plannerOutput.Plan;

        _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);

        // Install required packages
        if (plan.RequiredPackages.Count > 0)
        {
            RaiseStateChanged("Installing", $"Installing packages: {string.Join(", ", plan.RequiredPackages)}");
            var packagesInstalled = await _pythonEnv.InstallPackagesAsync(
                plan.RequiredPackages,
                cancellationToken);

            if (packagesInstalled)
            {
                context.InstalledPackages.AddRange(plan.RequiredPackages);
            }
            else
            {
                _logger.LogWarning("Some packages may have failed to install");
            }
        }

        // Phase 2 & 3: Execute and Evaluate loop
        while (!cancellationToken.IsCancellationRequested)
        {
            var currentStep = context.CurrentStep ?? plan.CurrentStep;
            if (currentStep == null)
            {
                _logger.LogWarning("No current step found");
                break;
            }

            // Install step-specific packages
            if (currentStep.RequiredPackages.Count > 0)
            {
                var uninstalledPackages = currentStep.RequiredPackages
                    .Except(context.InstalledPackages)
                    .ToList();

                if (uninstalledPackages.Count > 0)
                {
                    await _pythonEnv.InstallPackagesAsync(uninstalledPackages, cancellationToken);
                    context.InstalledPackages.AddRange(uninstalledPackages);
                }
            }

            // Execute step
            RaiseStateChanged("Executing",
                $"Step {currentStep.StepNumber}/{plan.Steps.Count}: {TruncateForLog(currentStep.Description, 50)}");

            var codeInput = new CodeExecutorInput
            {
                Context = context,
                Step = currentStep
            };
            var codeOutput = await _codeExecutor.ExecuteAsync(codeInput, cancellationToken);

            // Evaluate result
            RaiseStateChanged("Evaluating", $"Evaluating step {currentStep.StepNumber} results...");

            var evalInput = new EvaluatorInput
            {
                Context = codeOutput.Context,
                Step = currentStep,
                ExecutionResult = codeOutput.ExecutionResult
            };
            var evalOutput = await _evaluator.ExecuteAsync(evalInput, cancellationToken);

            context = evalOutput.Context;

            _logger.LogInformation(
                "Step {StepNumber} evaluation: Success={Success}, Continue={Continue}, Complete={Complete}, Replan={Replan}",
                currentStep.StepNumber,
                evalOutput.Evaluation.IsSuccessful,
                evalOutput.ShouldContinue,
                evalOutput.IsTaskComplete,
                evalOutput.NeedsReplan);

            // Check if complete
            if (evalOutput.IsTaskComplete)
            {
                if (plan.Status == TaskStatuses.Completed)
                {
                    RaiseStateChanged("Completed", "Task completed successfully!");
                }
                else if (plan.Status == TaskStatuses.Impossible)
                {
                    RaiseStateChanged("Impossible", "Task determined to be impossible");
                }
                else
                {
                    RaiseStateChanged("Failed", "Task failed");
                }
                break;
            }

            // Check if should continue
            if (!evalOutput.ShouldContinue)
            {
                RaiseStateChanged("Stopped", "Workflow stopped");
                break;
            }

            // Check if needs replan
            if (evalOutput.NeedsReplan)
            {
                RaiseStateChanged("Replanning", "Creating revised plan...");

                var replanInput = new PlannerInput
                {
                    Task = task,
                    Context = context,
                    PreviousEvaluation = evalOutput.Evaluation,
                    IsRetry = true
                };
                var replanOutput = await _planner.ExecuteAsync(replanInput, cancellationToken);

                if (!replanOutput.Success)
                {
                    _logger.LogError("Replanning failed: {Error}", replanOutput.Error);
                    plan.Status = TaskStatuses.Failed;
                    break;
                }

                context = replanOutput.Context;
                plan = replanOutput.Plan;
                context.CurrentStep = plan.CurrentStep;

                _logger.LogInformation("Replanned with {StepCount} steps", plan.Steps.Count);
            }

            // Update current step reference
            context.CurrentStep = context.Plan.CurrentStep;

            // Safety check for max iterations
            if (context.IterationCount >= _options.MaxIterations)
            {
                _logger.LogWarning("Max iterations reached ({Max})", _options.MaxIterations);
                plan.Status = TaskStatuses.Failed;
                RaiseStateChanged("Failed", "Max iterations reached");
                break;
            }
        }

        // Generate final result summary
        plan.FinalResult = GenerateResultSummary(context);

        // Cleanup agents
        await _agentService.CleanupAsync(cancellationToken);

        _logger.LogInformation("Workflow completed with status: {Status}", plan.Status);

        return context;
    }

    private string GenerateResultSummary(WorkflowContext context)
    {
        var plan = context.Plan;
        var completedSteps = plan.Steps.Count(s => s.Status == TaskStatuses.Completed);

        var summary = $"Task: {plan.OriginalTask}\n";
        summary += $"Status: {plan.Status}\n";
        summary += $"Completed Steps: {completedSteps}/{plan.Steps.Count}\n";
        summary += $"Total Iterations: {plan.TotalIterations}\n";

        if (context.CreatedFiles.Count > 0)
        {
            summary += $"Created Files: {string.Join(", ", context.CreatedFiles)}\n";
        }

        foreach (var step in plan.Steps)
        {
            summary += $"\nStep {step.StepNumber}: {step.Description}\n";
            summary += $"  Status: {step.Status}\n";
            if (step.Evaluation != null)
            {
                summary += $"  Result: {(step.Evaluation.IsSuccessful ? "Success" : "Failed")}\n";
                if (!string.IsNullOrEmpty(step.Evaluation.Reasoning))
                {
                    summary += $"  Reasoning: {TruncateForLog(step.Evaluation.Reasoning, 100)}\n";
                }
            }
        }

        return summary;
    }

    private void RaiseStateChanged(string state, string message)
    {
        _logger.LogInformation("[{State}] {Message}", state, message);
        StateChanged?.Invoke(this, new WorkflowStateChangedEventArgs(state, message));
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

/// <summary>
/// Event arguments for workflow state changes.
/// </summary>
public sealed class WorkflowStateChangedEventArgs : EventArgs
{
    public string State { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public WorkflowStateChangedEventArgs(string state, string message)
    {
        State = state;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}
