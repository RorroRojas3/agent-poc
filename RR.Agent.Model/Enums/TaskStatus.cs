namespace RR.Agent.Model.Enums;

/// <summary>
/// Represents the current status of a task or task step in the workflow.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task has been created but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Task is currently being planned/decomposed into steps.
    /// </summary>
    Planning,

    /// <summary>
    /// Task is currently being executed.
    /// </summary>
    Executing,

    /// <summary>
    /// Task execution results are being evaluated.
    /// </summary>
    Evaluating,

    /// <summary>
    /// Task has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Task has failed after all retry attempts.
    /// </summary>
    Failed,

    /// <summary>
    /// Task has been determined to be impossible to complete.
    /// </summary>
    Impossible,

    /// <summary>
    /// Task was cancelled by the user or system.
    /// </summary>
    Cancelled
}
