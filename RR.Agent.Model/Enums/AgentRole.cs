namespace RR.Agent.Model.Enums;

/// <summary>
/// Represents the different agent roles in the multi-agent workflow.
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// Agent responsible for breaking down tasks into executable steps.
    /// </summary>
    Planner,

    /// <summary>
    /// Agent responsible for writing and executing Python code.
    /// </summary>
    Executor,

    /// <summary>
    /// Agent responsible for evaluating execution results and determining next steps.
    /// </summary>
    Evaluator
}
