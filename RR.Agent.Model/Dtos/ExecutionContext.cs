using RR.Agent.Model.Enums;

namespace RR.Agent.Model.Dtos;

/// <summary>
/// Represents the shared execution context passed between workflow executors.
/// Contains the current plan state and conversation history.
/// </summary>
public sealed class WorkflowContext
{
    /// <summary>
    /// The current task plan being executed.
    /// </summary>
    public required TaskPlan Plan { get; set; }

    /// <summary>
    /// The current step being worked on (convenience reference).
    /// </summary>
    public TaskStep? CurrentStep { get; set; }

    /// <summary>
    /// Absolute path to the workspace directory.
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the Python virtual environment.
    /// </summary>
    public string VenvPath { get; set; } = string.Empty;

    /// <summary>
    /// Shared state dictionary for passing data between executors.
    /// </summary>
    public Dictionary<string, object> SharedState { get; set; } = [];

    /// <summary>
    /// History of messages exchanged in the agent conversation.
    /// </summary>
    public List<ConversationMessage> ConversationHistory { get; set; } = [];

    /// <summary>
    /// Total number of iterations in the current workflow run.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// List of packages that have been installed during this execution.
    /// </summary>
    public List<string> InstalledPackages { get; set; } = [];

    /// <summary>
    /// List of files created during execution.
    /// </summary>
    public List<string> CreatedFiles { get; set; } = [];

    /// <summary>
    /// Most recent evaluation result from the Evaluator.
    /// </summary>
    public EvaluationResult? LastEvaluation { get; set; }

    /// <summary>
    /// Most recent Python execution result.
    /// </summary>
    public PythonExecutionResult? LastExecutionResult { get; set; }

    /// <summary>
    /// Whether the workflow should continue to the next iteration.
    /// </summary>
    public bool ShouldContinue { get; set; } = true;

    /// <summary>
    /// Whether the task is complete (successfully or otherwise).
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    public void AddMessage(AgentRole role, string content)
    {
        ConversationHistory.Add(new ConversationMessage
        {
            Role = role,
            Content = content
        });
    }

    /// <summary>
    /// Gets a value from shared state with type conversion.
    /// </summary>
    public T? GetState<T>(string key) where T : class
    {
        return SharedState.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// Sets a value in shared state.
    /// </summary>
    public void SetState(string key, object value)
    {
        SharedState[key] = value;
    }
}

/// <summary>
/// Represents a message in the agent conversation history.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// The role of the agent that sent this message.
    /// </summary>
    public required AgentRole Role { get; set; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional metadata associated with the message.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
