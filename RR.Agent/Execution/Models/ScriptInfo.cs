namespace RR.Agent.Execution.Models;

/// <summary>
/// Metadata about a generated script.
/// </summary>
/// <param name="Language">Programming language (Python or CSharp).</param>
/// <param name="FileName">The script file name.</param>
/// <param name="Content">The script content.</param>
/// <param name="LocalPath">Local file path where script is saved.</param>
/// <param name="AgentFileId">The file ID after upload to agent storage.</param>
public sealed record ScriptInfo(
    string Language,
    string FileName,
    string Content,
    string? LocalPath = null,
    string? AgentFileId = null)
{
    /// <summary>
    /// Creates a copy with the local path set.
    /// </summary>
    public ScriptInfo WithLocalPath(string path) => this with { LocalPath = path };

    /// <summary>
    /// Creates a copy with the agent file ID set.
    /// </summary>
    public ScriptInfo WithAgentFileId(string fileId) => this with { AgentFileId = fileId };
}
