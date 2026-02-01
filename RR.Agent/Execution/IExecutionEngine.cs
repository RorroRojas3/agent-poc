namespace RR.Agent.Execution;

using RR.Agent.Execution.Models;
using RR.Agent.Planning.Models;

/// <summary>
/// Executes plan steps using Azure AI Agent's code interpreter.
/// </summary>
public interface IExecutionEngine
{
    /// <summary>
    /// Executes a single step from the plan.
    /// </summary>
    /// <param name="step">The step to execute.</param>
    /// <param name="context">Context from previous step executions.</param>
    /// <param name="inputFiles">Input files available for script execution.</param>
    /// <param name="retryContext">Optional context for retry attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<ExecutionResult> ExecuteStepAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles = null,
        string? retryContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file content from agent storage.
    /// </summary>
    /// <param name="fileId">The file ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content as bytes.</returns>
    Task<byte[]> GetFileContentAsync(
        string fileId,
        CancellationToken cancellationToken = default);
}
