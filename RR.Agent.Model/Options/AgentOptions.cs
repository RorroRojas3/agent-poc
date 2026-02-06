using RR.Agent.Model.Enums;

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

    public PlannerAgent Planner { get; set; } = new PlannerAgent();

    public ExecutorAgent Executor { get; set; } = new ExecutorAgent();

    public EvaluatorAgent Evaluator { get; set; } = new EvaluatorAgent();
}

public class BaseAgentOptions
{
    public AgentsTypes Type {get; set;} = AgentsTypes.Azure_AI_Foundry;

    public string ModelId {get; set;} = "gpt-5-chat";
}

public class PlannerAgent : BaseAgentOptions
{
    
}

public class ExecutorAgent : BaseAgentOptions
{
    
}

public class EvaluatorAgent : BaseAgentOptions
{
    
}
