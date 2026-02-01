namespace RR.Agent.Execution.Models;

/// <summary>
/// Represents a user-provided input file that can be used by generated scripts.
/// </summary>
/// <param name="OriginalPath">The original file path provided by the user.</param>
/// <param name="FileName">The file name (without directory path).</param>
/// <param name="WorkspacePath">Path to the file in the workspace after copying.</param>
/// <param name="AgentFileId">The Azure AI Agent file ID after upload.</param>
public sealed record InputFile(
    string OriginalPath,
    string FileName,
    string? WorkspacePath = null,
    string? AgentFileId = null)
{
    /// <summary>
    /// Creates a new InputFile with the workspace path set.
    /// </summary>
    public InputFile WithWorkspacePath(string path) => this with { WorkspacePath = path };

    /// <summary>
    /// Creates a new InputFile with the agent file ID set.
    /// </summary>
    public InputFile WithAgentFileId(string id) => this with { AgentFileId = id };

    /// <summary>
    /// Gets a value indicating whether the file has been uploaded to Azure.
    /// </summary>
    public bool IsUploaded => !string.IsNullOrEmpty(AgentFileId);
}
