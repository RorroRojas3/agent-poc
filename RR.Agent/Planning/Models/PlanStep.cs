namespace RR.Agent.Planning.Models;

/// <summary>
/// Represents a single executable step in an execution plan.
/// </summary>
/// <param name="Order">The sequence order of this step (1-based).</param>
/// <param name="Description">Human-readable description of what this step does.</param>
/// <param name="Type">The type of action this step performs.</param>
/// <param name="ExpectedOutput">Description of the expected output or result.</param>
/// <param name="Dependencies">List of step orders this step depends on.</param>
/// <param name="ScriptHint">Optional hint for script generation (language preference, approach).</param>
public sealed record PlanStep(
    int Order,
    string Description,
    StepType Type,
    string ExpectedOutput,
    IReadOnlyList<int> Dependencies,
    string? ScriptHint = null);
