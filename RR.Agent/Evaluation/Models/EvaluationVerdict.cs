namespace RR.Agent.Evaluation.Models;

/// <summary>
/// The verdict from evaluating an execution result.
/// </summary>
public enum EvaluationVerdict
{
    /// <summary>Step succeeded, proceed to next.</summary>
    Success,

    /// <summary>Step failed, should retry.</summary>
    Retry,

    /// <summary>Step failed permanently, task is impossible.</summary>
    Impossible,

    /// <summary>Step requires plan modification.</summary>
    RequiresPlanChange
}
