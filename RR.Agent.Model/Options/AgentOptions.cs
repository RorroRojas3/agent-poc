namespace RR.Agent.Model.Options;

/// <summary>
/// Configuration options for agent behavior and workflow execution.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>
    /// Maximum number of retry attempts for failed operations.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Polling interval in milliseconds when waiting for agent run completion.
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;

    /// <summary>
    /// Maximum time in seconds to wait for a single agent run to complete.
    /// </summary>
    public int RunTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Directory path for the agent workspace where scripts and outputs are stored.
    /// </summary>
    public string WorkspaceDirectory { get; set; } = "./workspace";

    /// <summary>
    /// Maximum number of iterations in the plan-execute-evaluate loop.
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Maximum number of steps allowed in a single task plan.
    /// </summary>
    public int MaxStepsPerPlan { get; set; } = 20;

    /// <summary>
    /// Maximum size in bytes for captured stdout/stderr output.
    /// </summary>
    public int MaxOutputSizeBytes { get; set; } = 102400; // 100KB

    /// <summary>
    /// Whether to use structured output (JSON schema) for Planner and Evaluator agents.
    /// This requires model support for response_format with json_schema type.
    /// Default is false for compatibility.
    /// </summary>
    public bool UseStructuredOutput { get; set; } = false;
}
