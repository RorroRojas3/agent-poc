namespace RR.Agent.Evaluation.Models;

using RR.Agent.Execution.Models;

/// <summary>
/// Result of evaluating a step execution.
/// </summary>
/// <param name="Verdict">The evaluation verdict.</param>
/// <param name="Reasoning">Explanation of the evaluation decision.</param>
/// <param name="RetryContext">Retry context if verdict is Retry.</param>
/// <param name="OriginalResult">The original execution result.</param>
public sealed record EvaluationResult(
    EvaluationVerdict Verdict,
    string Reasoning,
    RetryContext? RetryContext,
    ExecutionResult OriginalResult)
{
    /// <summary>
    /// Returns true if execution should continue to next step.
    /// </summary>
    public bool ShouldProceed => Verdict == EvaluationVerdict.Success;

    /// <summary>
    /// Returns true if execution should retry the current step.
    /// </summary>
    public bool ShouldRetry => Verdict == EvaluationVerdict.Retry && (RetryContext?.CanRetry ?? false);
}
