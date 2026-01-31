namespace RR.Agent.Execution.Models;

/// <summary>
/// Result of executing a single plan step.
/// </summary>
/// <param name="StepOrder">The order of the executed step.</param>
/// <param name="Status">The execution status.</param>
/// <param name="Output">The output or result from execution.</param>
/// <param name="ErrorMessage">Error message if execution failed.</param>
/// <param name="GeneratedFiles">List of file IDs created during execution.</param>
/// <param name="ExecutionTimeMs">Execution time in milliseconds.</param>
/// <param name="Script">The script that was executed (if applicable).</param>
public sealed record ExecutionResult(
    int StepOrder,
    ExecutionStatus Status,
    string? Output,
    string? ErrorMessage,
    IReadOnlyList<string> GeneratedFiles,
    long ExecutionTimeMs,
    ScriptInfo? Script = null)
{
    /// <summary>
    /// Returns true if the execution was successful.
    /// </summary>
    public bool IsSuccess => Status == ExecutionStatus.Success;

    /// <summary>
    /// Returns true if the execution can be retried.
    /// </summary>
    public bool CanRetry => Status == ExecutionStatus.Failed || Status == ExecutionStatus.TimedOut;
}
