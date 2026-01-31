namespace RR.Agent.Tools;

using Azure.AI.Agents.Persistent;

/// <summary>
/// Provides code interpreter tool definition for agent creation.
/// </summary>
public sealed class CodeInterpreterToolProvider : IToolProvider
{
    private static readonly IReadOnlyList<ToolDefinition> Tools =
    [
        new CodeInterpreterToolDefinition()
    ];

    public IReadOnlyList<ToolDefinition> GetToolDefinitions() => Tools;

    public ToolResources? GetToolResources() => null;
}
