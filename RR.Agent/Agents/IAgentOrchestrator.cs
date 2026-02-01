namespace RR.Agent.Agents;

using RR.Agent.Execution.Models;
using RR.Agent.Planning.Models;

/// <summary>
/// Orchestrates the complete agent workflow: planning, execution, and evaluation.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Processes a user request through the complete agent pipeline.
    /// </summary>
    /// <param name="userRequest">The user's natural language request.</param>
    /// <param name="inputFilePaths">Optional paths to input files that scripts can read/modify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final result with all step outputs.</returns>
    Task<OrchestratorResult> ProcessRequestAsync(
        string userRequest,
        IEnumerable<string>? inputFilePaths = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a request with streaming updates.
    /// </summary>
    /// <param name="userRequest">The user's request.</param>
    /// <param name="inputFilePaths">Optional paths to input files that scripts can read/modify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream of progress updates.</returns>
    IAsyncEnumerable<OrchestratorUpdate> ProcessRequestStreamingAsync(
        string userRequest,
        IEnumerable<string>? inputFilePaths = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Final result from the orchestrator.
/// </summary>
/// <param name="Success">Whether the overall request succeeded.</param>
/// <param name="Plan">The executed plan.</param>
/// <param name="StepResults">Results from each step.</param>
/// <param name="FinalOutput">Consolidated final output.</param>
/// <param name="FailureReason">Reason if the request failed.</param>
public sealed record OrchestratorResult(
    bool Success,
    ExecutionPlan Plan,
    IReadOnlyList<ExecutionResult> StepResults,
    string? FinalOutput,
    string? FailureReason);

/// <summary>
/// Progress update during orchestration.
/// </summary>
/// <param name="Phase">Current phase (Planning, Executing, Evaluating).</param>
/// <param name="Message">Human-readable progress message.</param>
/// <param name="CurrentStep">Current step being processed.</param>
/// <param name="TotalSteps">Total steps in plan.</param>
public sealed record OrchestratorUpdate(
    OrchestratorPhase Phase,
    string Message,
    int? CurrentStep = null,
    int? TotalSteps = null);

/// <summary>
/// Phases of orchestration.
/// </summary>
public enum OrchestratorPhase
{
    PreparingFiles,
    Planning,
    PlanPresentation,
    Executing,
    Evaluating,
    Retrying,
    Completed,
    Failed
}
