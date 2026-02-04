using TaskStatusEnum = RR.Agent.Model.Enums.TaskStatus;

namespace RR.Agent.Model.Dtos;

/// <summary>
/// Represents a complete execution plan for a user task, containing multiple steps.
/// </summary>
public sealed class TaskPlan
{
    /// <summary>
    /// Unique identifier for this plan.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The original task description provided by the user.
    /// </summary>
    public required string OriginalTask { get; set; }

    /// <summary>
    /// The planner's analysis of the task and approach.
    /// </summary>
    public string? TaskAnalysis { get; set; }

    /// <summary>
    /// The ordered list of steps to execute.
    /// </summary>
    public List<TaskStep> Steps { get; set; } = [];

    /// <summary>
    /// Index of the current step being executed (0-based).
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Current status of the overall plan.
    /// </summary>
    public TaskStatusEnum Status { get; set; } = TaskStatusEnum.Pending;

    /// <summary>
    /// Timestamp when the plan was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the plan completed execution.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// List of all Python packages required across all steps.
    /// </summary>
    public List<string> RequiredPackages { get; set; } = [];

    /// <summary>
    /// Total number of iterations (retries) performed across all steps.
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Final output or result summary from the plan execution.
    /// </summary>
    public string? FinalResult { get; set; }

    /// <summary>
    /// Gets the current step being executed, or null if complete.
    /// </summary>
    public TaskStep? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count
            ? Steps[CurrentStepIndex]
            : null;

    /// <summary>
    /// Gets whether all steps have been completed successfully.
    /// </summary>
    public bool IsComplete =>
        Steps.Count > 0 && Steps.All(s => s.Status == TaskStatusEnum.Completed);

    /// <summary>
    /// Gets the number of completed steps.
    /// </summary>
    public int CompletedStepsCount =>
        Steps.Count(s => s.Status == TaskStatusEnum.Completed);

    /// <summary>
    /// Advances to the next step if available.
    /// </summary>
    /// <returns>True if there is a next step, false if plan is complete.</returns>
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
