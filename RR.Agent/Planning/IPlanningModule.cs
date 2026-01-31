namespace RR.Agent.Planning;

using RR.Agent.Planning.Models;

/// <summary>
/// Parses user requests and decomposes them into actionable execution plans.
/// </summary>
public interface IPlanningModule
{
    /// <summary>
    /// Analyzes a user request and creates a structured execution plan.
    /// </summary>
    /// <param name="userRequest">The user's natural language request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An execution plan with ordered steps.</returns>
    Task<ExecutionPlan> CreatePlanAsync(
        string userRequest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a plan is executable and well-formed.
    /// </summary>
    /// <param name="plan">The plan to validate.</param>
    /// <returns>True if the plan is valid, false otherwise.</returns>
    bool ValidatePlan(ExecutionPlan plan);

    /// <summary>
    /// Refines an existing plan based on execution feedback.
    /// </summary>
    /// <param name="originalPlan">The original plan.</param>
    /// <param name="feedback">Feedback from execution attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A refined execution plan.</returns>
    Task<ExecutionPlan> RefinePlanAsync(
        ExecutionPlan originalPlan,
        string feedback,
        CancellationToken cancellationToken = default);
}
