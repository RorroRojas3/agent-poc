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

    public async Task<InputFile> PrepareInputFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Input file not found: {filePath}", filePath);
        }

        await EnsureWorkspaceExistsAsync(cancellationToken);

        var fileName = Path.GetFileName(filePath);
        var inputFile = new InputFile(filePath, fileName);

        // Copy file to workspace
        var workspacePath = Path.Combine(_workspacePath, fileName);

        // Ensure unique filename if file exists
        var counter = 1;
        while (File.Exists(workspacePath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            workspacePath = Path.Combine(_workspacePath, $"{nameWithoutExt}_{counter}{extension}");
            counter++;
        }

        await Task.Run(() => File.Copy(filePath, workspacePath), cancellationToken);
        inputFile = inputFile.WithWorkspacePath(workspacePath);

        _logger.LogDebug("Copied input file {OriginalPath} to {WorkspacePath}", filePath, workspacePath);

        // Upload to Azure AI Agent storage
        var fileId = await UploadFileAsync(workspacePath, cancellationToken);
        inputFile = inputFile.WithAgentFileId(fileId);

        _logger.LogInformation(
            "Prepared input file {FileName} with ID {FileId}",
            fileName,
            fileId);

        return inputFile;
    }

    public async Task<IReadOnlyList<InputFile>> PrepareInputFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var inputFiles = new List<InputFile>();

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inputFile = await PrepareInputFileAsync(filePath, cancellationToken);
            inputFiles.Add(inputFile);
        }

        return inputFiles;
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

    public async Task<string?> DownloadAndSaveFileAsync(
        string fileId,
        string? suggestedFilename = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        try
        {
            await EnsureWorkspaceExistsAsync(cancellationToken);

            // Get filename from Azure if not provided
            var filename = suggestedFilename;
            if (string.IsNullOrWhiteSpace(filename))
            {
                try
                {
                    var fileInfo = await _client.Files.GetFileAsync(fileId, cancellationToken);
                    filename = fileInfo.Value.Filename;
                }
                catch
                {
                    // Fallback to a default name
                    filename = $"output_{fileId[..8]}.bin";
                }
            }

            // Download the file content
            var content = await DownloadFileAsync(fileId, cancellationToken);

            // Skip empty files
            if (content.Length == 0)
            {
                _logger.LogDebug("Skipping empty file {FileId}", fileId);
                return null;
            }

            // Determine the local path
            var filePath = Path.Combine(_workspacePath, filename);

            // Ensure unique filename if file exists
            var counter = 1;
            while (File.Exists(filePath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                var extension = Path.GetExtension(filename);
                filePath = Path.Combine(_workspacePath, $"{nameWithoutExt}_{counter}{extension}");
                counter++;
            }

            // Save to disk
            await File.WriteAllBytesAsync(filePath, content, cancellationToken);

            _logger.LogInformation(
                "Downloaded and saved file {FileId} to {FilePath} ({Size} bytes)",
                fileId,
                filePath,
                content.Length);

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download file {FileId}", fileId);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> DownloadGeneratedFilesAsync(
        IEnumerable<string> fileIds,
        IEnumerable<string>? excludeFileIds = null,
        CancellationToken cancellationToken = default)
    {
        var excludeSet = excludeFileIds?.ToHashSet() ?? [];
        var downloadedPaths = new List<string>();

        foreach (var fileId in fileIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip excluded files (e.g., input files, scripts)
            if (excludeSet.Contains(fileId))
            {
                _logger.LogDebug("Skipping excluded file {FileId}", fileId);
                continue;
            }

            var localPath = await DownloadAndSaveFileAsync(fileId, null, cancellationToken);
            if (localPath is not null)
            {
                downloadedPaths.Add(localPath);
            }
        }

        return downloadedPaths;
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
