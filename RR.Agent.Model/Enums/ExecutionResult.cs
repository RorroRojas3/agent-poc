namespace RR.Agent.Model.Enums;

/// <summary>
/// Represents the outcome of a Python script execution.
/// </summary>
public enum ExecutionResult
{
    /// <summary>
    /// Script executed successfully with exit code 0.
    /// </summary>
    Success,

    /// <summary>
    /// Script completed but with warnings or partial success.
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// Script execution failed with non-zero exit code.
    /// </summary>
    Failure,

    /// <summary>
    /// Script execution exceeded the timeout limit.
    /// </summary>
    Timeout,

    /// <summary>
    /// An error occurred before or during script execution (e.g., file not found).
    /// </summary>
    Error,

    /// <summary>
    /// Script execution was cancelled.
    /// </summary>
    Cancelled
}
