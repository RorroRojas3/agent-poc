namespace RR.Agent.Planning.Models;

/// <summary>
/// Represents a complete execution plan decomposed from a user request.
/// </summary>
/// <param name="OriginalRequest">The original user request that was analyzed.</param>
/// <param name="Summary">Brief summary of what the plan accomplishes.</param>
/// <param name="Steps">Ordered list of steps to execute.</param>
/// <param name="EstimatedComplexity">Estimated complexity level (1-10).</param>
public sealed record ExecutionPlan(
    string OriginalRequest,
    string Summary,
    IReadOnlyList<PlanStep> Steps,
    int EstimatedComplexity);
