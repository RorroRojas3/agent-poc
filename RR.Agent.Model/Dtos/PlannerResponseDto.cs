using System.Text.Json.Serialization;

namespace RR.Agent.Model.Dtos;

/// <summary>
/// Simplified DTO for planner agent JSON response.
/// Contains only the fields the planner should populate.
/// </summary>
public sealed class PlannerResponseDto
{
    /// <summary>
    /// Brief analysis of the task and approach.
    /// </summary>
    [JsonPropertyName("taskAnalysis")]
    public string? TaskAnalysis { get; set; }

    /// <summary>
    /// The ordered list of steps to execute.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<PlannerStepDto> Steps { get; set; } = [];

    /// <summary>
    /// List of all Python packages required across all steps.
    /// </summary>
    [JsonPropertyName("requiredPackages")]
    public List<string> RequiredPackages { get; set; } = [];
}

/// <summary>
/// Simplified step DTO for planner response.
/// </summary>
public sealed class PlannerStepDto
{
    /// <summary>
    /// The sequential number of this step.
    /// </summary>
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    /// <summary>
    /// Description of what this step accomplishes.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Description of what successful execution looks like.
    /// </summary>
    [JsonPropertyName("expectedOutput")]
    public string? ExpectedOutput { get; set; }

    /// <summary>
    /// Python packages required for this step.
    /// </summary>
    [JsonPropertyName("requiredPackages")]
    public List<string> RequiredPackages { get; set; } = [];
}
