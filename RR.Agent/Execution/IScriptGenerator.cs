namespace RR.Agent.Execution;

using RR.Agent.Execution.Models;
using RR.Agent.Planning.Models;

/// <summary>
/// Generates executable scripts from plan steps.
/// </summary>
public interface IScriptGenerator
{
    /// <summary>
    /// Generates a script for the given step.
    /// </summary>
    /// <param name="step">The step requiring script generation.</param>
    /// <param name="context">Context from previous executions.</param>
    /// <param name="inputFiles">Input files available for the script to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Script information including content and metadata.</returns>
    Task<ScriptInfo> GenerateScriptAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles = null,
        CancellationToken cancellationToken = default);
}
