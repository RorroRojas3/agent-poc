namespace RR.Agent.Execution.Models;

/// <summary>
/// Status of a step execution attempt.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Step completed successfully.</summary>
    Success,

    /// <summary>Step failed but may be retried.</summary>
    Failed,

    /// <summary>Step was cancelled.</summary>
    Cancelled,

    /// <summary>Step timed out.</summary>
    TimedOut,

    /// <summary>Task is impossible to complete.</summary>
    Impossible
}
