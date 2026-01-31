namespace RR.Agent.Tools;

using Azure.AI.Agents.Persistent;

/// <summary>
/// Provides tool definitions for agent creation.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Gets all tool definitions for agent registration.
    /// </summary>
    /// <returns>List of tool definitions.</returns>
    IReadOnlyList<ToolDefinition> GetToolDefinitions();

    /// <summary>
    /// Gets tool resources for agent creation.
    /// </summary>
    /// <returns>Tool resources or null if none required.</returns>
    ToolResources? GetToolResources();
}
