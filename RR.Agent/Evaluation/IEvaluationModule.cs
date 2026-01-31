namespace RR.Agent.Evaluation;

using RR.Agent.Evaluation.Models;
using RR.Agent.Execution.Models;
using RR.Agent.Planning.Models;

/// <summary>
/// Evaluates execution results and determines next actions.
/// </summary>
public interface IEvaluationModule
{
    /// <summary>
    /// Evaluates the result of a step execution.
    /// </summary>
    /// <param name="step">The step that was executed.</param>
    /// <param name="result">The execution result.</param>
    /// <param name="retryContext">Current retry context if retrying.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation result with verdict and next action.</returns>
    Task<EvaluationResult> EvaluateAsync(
        PlanStep step,
        ExecutionResult result,
        RetryContext? retryContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a task is impossible based on accumulated failures.
    /// </summary>
    /// <param name="failures">List of failure messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if task appears impossible, with explanation.</returns>
    Task<(bool IsImpossible, string Explanation)> AnalyzeImpossibilityAsync(
        IReadOnlyList<string> failures,
        CancellationToken cancellationToken = default);
}
