using RR.Agent.Model.Dtos;

namespace RR.Agent.Service.Executors;

/// <summary>
/// Input message for the Planner executor.
/// </summary>
public sealed class PlannerInput
{
    /// <summary>
    /// The task to plan. Either the original task or a retry prompt.
    /// </summary>
    public required string Task { get; set; }

    /// <summary>
    /// Existing execution context (for retries).
    /// </summary>
    public WorkflowContext? Context { get; set; }

    /// <summary>
    /// Previous evaluation result (for retries).
    /// </summary>
    public EvaluationResult? PreviousEvaluation { get; set; }

    /// <summary>
    /// Whether this is a retry/replan request.
    /// </summary>
    public bool IsRetry { get; set; }
}

/// <summary>
/// Output message from the Planner executor.
/// </summary>
public sealed class PlannerOutput
{
    /// <summary>
    /// The generated task plan.
    /// </summary>
    public required TaskPlan Plan { get; set; }

    /// <summary>
    /// The updated execution context.
    /// </summary>
    public required WorkflowContext Context { get; set; }

    /// <summary>
    /// Whether planning was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if planning failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Input message for the Code executor.
/// </summary>
public sealed class CodeExecutorInput
{
    /// <summary>
    /// The current execution context.
    /// </summary>
    public required WorkflowContext Context { get; set; }

    /// <summary>
    /// The step to execute.
    /// </summary>
    public required TaskStep Step { get; set; }
}

/// <summary>
/// Output message from the Code executor.
/// </summary>
public sealed class CodeExecutorOutput
{
    /// <summary>
    /// The updated execution context.
    /// </summary>
    public required WorkflowContext Context { get; set; }

    /// <summary>
    /// The Python execution result.
    /// </summary>
    public required PythonExecutionResult ExecutionResult { get; set; }

    /// <summary>
    /// The tool response.
    /// </summary>
    public required ToolResponseDto ToolResponse { get; set; }

    /// <summary>
    /// Whether execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Input message for the Evaluator executor.
/// </summary>
public sealed class EvaluatorInput
{
    /// <summary>
    /// The current execution context.
    /// </summary>
    public required WorkflowContext Context { get; set; }

    /// <summary>
    /// The step that was executed.
    /// </summary>
    public required TaskStep Step { get; set; }

    /// <summary>
    /// The execution result to evaluate.
    /// </summary>
    public required PythonExecutionResult ExecutionResult { get; set; }

    public required ToolResponseDto ToolResponse { get; set; }
}

/// <summary>
/// Output message from the Evaluator executor.
/// </summary>
public sealed class EvaluatorOutput
{
    /// <summary>
    /// The updated execution context.
    /// </summary>
    public required WorkflowContext Context { get; set; }

    /// <summary>
    /// The evaluation result.
    /// </summary>
    public required EvaluationResult Evaluation { get; set; }

    /// <summary>
    /// Whether the workflow should continue.
    /// </summary>
    public bool ShouldContinue { get; set; }

    /// <summary>
    /// Whether the task is complete (all steps done).
    /// </summary>
    public bool IsTaskComplete { get; set; }

    /// <summary>
    /// Whether a replan is needed.
    /// </summary>
    public bool NeedsReplan { get; set; }
}
