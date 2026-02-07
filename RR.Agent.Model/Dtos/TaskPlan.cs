using System.Text.Json.Serialization;
using RR.Agent.Model.Enums;

namespace RR.Agent.Model.Dtos;

/// <summary>
/// Represents a complete execution plan for a user task, containing multiple steps.
/// </summary>
public sealed class TaskPlan
{
    /// <summary>
    /// Unique identifier for this plan.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The original task description provided by the user.
    /// </summary>
    [JsonPropertyName("originalTask")]
    public string OriginalTask { get; set; } = null!;

    /// <summary>
    /// The planner's analysis of the task and approach.
    /// </summary>
    [JsonPropertyName("taskAnalysis")]
    public string? TaskAnalysis { get; set; }

    /// <summary>
    /// The ordered list of steps to execute.
    /// </summary>
    [JsonPropertyName("steps")]
    [JsonRequired]
    public List<TaskStep> Steps { get; set; } = [];

    /// <summary>
    /// Index of the current step being executed (0-based).
    /// </summary>
    [JsonPropertyName("currentStepIndex")]
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Current status of the overall plan.
    /// </summary>
    [JsonPropertyName("status")]
    public TaskStatuses Status { get; set; } = TaskStatuses.Pending;

    /// <summary>
    /// Timestamp when the plan was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when the plan completed execution.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// List of all Python packages required across all steps.
    /// </summary>
    [JsonPropertyName("requiredPackages")]
    public List<string> RequiredPackages { get; set; } = [];

    /// <summary>
    /// Total number of iterations (retries) performed across all steps.
    /// </summary>
    [JsonPropertyName("totalIterations")]
    public int TotalIterations { get; set; }

    /// <summary>
    /// Gets the current step being executed, or null if complete.
    /// </summary>
    [JsonIgnore]
    public TaskStep? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count
            ? Steps[CurrentStepIndex]
            : null;

    /// <summary>
    /// Gets whether all steps have been completed successfully.
    /// </summary>
    [JsonIgnore]
    public bool IsComplete =>
        Steps.Count > 0 && Steps.All(s => s.Status == TaskStatuses.Completed);

    /// <summary>
    /// Gets the number of completed steps.
    /// </summary>
    [JsonIgnore]
    public int CompletedStepsCount =>
        Steps.Count(s => s.Status == TaskStatuses.Completed);

    /// <summary>
    /// Advances to the next step if available.
    /// </summary>
    /// <returns>True if there is a next step, false if plan is complete.</returns>.
    public bool MoveToNextStep()
    {
        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
            return true;
        }
        return false;
    }
}
