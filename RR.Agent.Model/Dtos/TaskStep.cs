using System.Text.Json.Serialization;
using TaskStatusEnum = RR.Agent.Model.Enums.TaskStatus;

namespace RR.Agent.Model.Dtos;

/// <summary>
/// Represents a single executable step within a task plan.
/// </summary>
public sealed class TaskStep
{
    /// <summary>
    /// The sequential number of this step within the plan.
    /// </summary>
    [JsonPropertyName("stepNumber")]
    [JsonRequired]
    public int StepNumber { get; set; }

    /// <summary>
    /// Description of what this step accomplishes.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonRequired]
    public required string Description { get; set; }

    /// <summary>
    /// The Python code to execute for this step (populated by Executor agent).
    /// </summary>
    public string? PythonCode { get; set; }

    /// <summary>
    /// Description of what successful execution looks like.
    /// </summary>
    public string? ExpectedOutput { get; set; }

    /// <summary>
    /// Current status of this step.
    /// </summary>
    public TaskStatusEnum Status { get; set; } = TaskStatusEnum.Pending;

    /// <summary>
    /// The result of executing this step's Python code.
    /// </summary>
    public PythonExecutionResult? ExecutionResult { get; set; }

    /// <summary>
    /// The evaluation of this step's execution.
    /// </summary>
    public EvaluationResult? Evaluation { get; set; }

    /// <summary>
    /// Number of times this step has been attempted.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp when this step started executing.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when this step completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// List of Python packages required for this step.
    /// </summary>
    public List<string> RequiredPackages { get; set; } = [];

    /// <summary>
    /// Input files needed for this step (paths relative to workspace).
    /// </summary>
    public List<string> InputFiles { get; set; } = [];

    /// <summary>
    /// Output files expected from this step (paths relative to workspace).
    /// </summary>
    public List<string> OutputFiles { get; set; } = [];
}
