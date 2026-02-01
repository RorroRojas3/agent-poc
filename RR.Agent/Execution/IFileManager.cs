namespace RR.Agent.Execution;

using RR.Agent.Execution.Models;

/// <summary>
/// Manages file operations for the agent workspace.
/// </summary>
public interface IFileManager
{
    /// <summary>
    /// Prepares an input file by copying it to the workspace and uploading to Azure.
    /// </summary>
    /// <param name="filePath">Path to the user's input file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prepared input file with workspace path and agent file ID.</returns>
    Task<InputFile> PrepareInputFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares multiple input files by copying them to the workspace and uploading to Azure.
    /// </summary>
    /// <param name="filePaths">Paths to the user's input files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prepared input files with workspace paths and agent file IDs.</returns>
    Task<IReadOnlyList<InputFile>> PrepareInputFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a script to the local workspace.
    /// </summary>
    /// <param name="script">Script information to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated script info with local path.</returns>
    Task<ScriptInfo> SaveScriptLocallyAsync(
        ScriptInfo script,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to Azure AI Agent storage.
    /// </summary>
    /// <param name="localPath">Path to the local file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent file ID.</returns>
    Task<string> UploadFileAsync(
        string localPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from agent storage.
    /// </summary>
    /// <param name="fileId">The file ID to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File content as bytes.</returns>
    Task<byte[]> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up temporary files in the workspace.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the workspace directory exists.
    /// </summary>
    Task EnsureWorkspaceExistsAsync(CancellationToken cancellationToken = default);
}
