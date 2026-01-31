namespace RR.Agent.Configuration;

/// <summary>
/// Configuration options for agent behavior.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>
    /// Maximum retry attempts for failed steps.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Polling interval in milliseconds when waiting for run completion.
    /// </summary>
    public int PollingIntervalMs { get; init; } = 500;

    /// <summary>
    /// Timeout in seconds for a single run operation.
    /// </summary>
    public int RunTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Directory path for storing generated scripts locally.
    /// </summary>
    public string WorkspaceDirectory { get; init; } = "./workspace";
}
