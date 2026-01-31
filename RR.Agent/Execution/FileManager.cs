namespace RR.Agent.Execution;

using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Execution.Models;

/// <summary>
/// Manages file operations for the agent workspace using local filesystem and Azure AI Agent storage.
/// </summary>
public sealed class FileManager : IFileManager
{
    private readonly PersistentAgentsClient _client;
    private readonly AgentOptions _options;
    private readonly ILogger<FileManager> _logger;
    private readonly string _workspacePath;

    public FileManager(
        PersistentAgentsClient client,
        IOptions<AgentOptions> options,
        ILogger<FileManager> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _options = options.Value;
        _logger = logger;
        _workspacePath = Path.GetFullPath(_options.WorkspaceDirectory);
    }

    public async Task<ScriptInfo> SaveScriptLocallyAsync(
        ScriptInfo script,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        await EnsureWorkspaceExistsAsync(cancellationToken);

        var fileName = script.FileName;
        var filePath = Path.Combine(_workspacePath, fileName);

        // Ensure unique filename if file exists
        var counter = 1;
        while (File.Exists(filePath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            filePath = Path.Combine(_workspacePath, $"{nameWithoutExt}_{counter}{extension}");
            counter++;
        }

        await File.WriteAllTextAsync(filePath, script.Content, cancellationToken);

        _logger.LogDebug("Saved script to {FilePath}", filePath);

        return script.WithLocalPath(filePath);
    }

    public async Task<string> UploadFileAsync(
        string localPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException($"File not found: {localPath}", localPath);
        }

        _logger.LogDebug("Uploading file {FilePath} to Azure AI Agent storage", localPath);

        var response = await _client.Files.UploadFileAsync(
            filePath: localPath,
            purpose: PersistentAgentFilePurpose.Agents,
            cancellationToken: cancellationToken);

        var fileInfo = response.Value;
        _logger.LogInformation(
            "Uploaded file {FilePath} with ID {FileId}",
            localPath,
            fileInfo.Id);

        return fileInfo.Id;
    }

    public async Task<byte[]> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        _logger.LogDebug("Downloading file {FileId} from Azure AI Agent storage", fileId);

        var response = await _client.Files.GetFileContentAsync(fileId);
        var content = response.Value.ToArray();

        _logger.LogDebug(
            "Downloaded file {FileId}, size: {Size} bytes",
            fileId,
            content.Length);

        return content;
    }

    public Task CleanupWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_workspacePath))
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("Cleaning up workspace at {WorkspacePath}", _workspacePath);

        var files = Directory.GetFiles(_workspacePath);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Delete(file);
                _logger.LogDebug("Deleted file {FilePath}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file {FilePath}", file);
            }
        }

        return Task.CompletedTask;
    }

    public Task EnsureWorkspaceExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_workspacePath))
        {
            Directory.CreateDirectory(_workspacePath);
            _logger.LogDebug("Created workspace directory at {WorkspacePath}", _workspacePath);
        }

        return Task.CompletedTask;
    }
}
