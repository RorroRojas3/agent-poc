using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using RR.Agent.Model.Dtos;
using RR.Agent.Service.Python;

namespace RR.Agent.Service.Tools;

/// <summary>
/// Handles tool calls from the Executor agent and returns results.
/// </summary>
public sealed class ToolHandler
{
    private readonly IPythonScriptExecutor _scriptExecutor;
    private readonly IPythonEnvironmentService _envService;
    private readonly ILogger<ToolHandler> _logger;

    public ToolHandler(
        IPythonScriptExecutor scriptExecutor,
        IPythonEnvironmentService envService,
        ILogger<ToolHandler> logger)
    {
        _scriptExecutor = scriptExecutor;
        _envService = envService;
        _logger = logger;
    }

    /// <summary>
    /// Handles a required tool call and returns the appropriate output.
    /// </summary>
    public async Task<ToolOutput> HandleToolCallAsync(
        RequiredToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        if (toolCall is not RequiredFunctionToolCall functionCall)
        {
            return new ToolOutput(toolCall, "Error: Unknown tool call type");
        }

        _logger.LogInformation("Handling tool call: {ToolName}", functionCall.Name);
        _logger.LogDebug("Tool arguments: {Arguments}", functionCall.Arguments);

        try
        {
            var result = functionCall.Name switch
            {
                "write_file" => await HandleWriteFileAsync(functionCall, cancellationToken),
                "read_file" => await HandleReadFileAsync(functionCall, cancellationToken),
                "execute_python" => await HandleExecutePythonAsync(functionCall, cancellationToken),
                "install_package" => await HandleInstallPackageAsync(functionCall, cancellationToken),
                "list_files" => await HandleListFilesAsync(functionCall, cancellationToken),
                "execute_script_file" => await HandleExecuteScriptFileAsync(functionCall, cancellationToken),
                "find_files" => await HandleFindFilesAsync(functionCall, cancellationToken),
                "read_external_file" => await HandleReadExternalFileAsync(functionCall, cancellationToken),
                "copy_to_workspace" => await HandleCopyToWorkspaceAsync(functionCall, cancellationToken),
                _ => $"Error: Unknown tool '{functionCall.Name}'"
            };

            return new ToolOutput(toolCall, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call {ToolName}", functionCall.Name);
            return new ToolOutput(toolCall, $"Error: {ex.Message}");
        }
    }

    private async Task<string> HandleWriteFileAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var filename = root.GetProperty("filename").GetString()
            ?? throw new ArgumentException("filename is required");
        var content = root.GetProperty("content").GetString()
            ?? throw new ArgumentException("content is required");

        var fullPath = await _scriptExecutor.WriteFileAsync(filename, content, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"File written successfully",
            path = fullPath
        });
    }

    private async Task<string> HandleReadFileAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var filename = root.GetProperty("filename").GetString()
            ?? throw new ArgumentException("filename is required");

        var content = await _scriptExecutor.ReadFileAsync(filename, cancellationToken);

        if (content == null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"File not found: {filename}"
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            content
        });
    }

    private async Task<string> HandleExecutePythonAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var scriptContent = root.GetProperty("script_content").GetString()
            ?? throw new ArgumentException("script_content is required");

        string? scriptName = null;
        if (root.TryGetProperty("script_name", out var scriptNameElement) &&
            scriptNameElement.ValueKind == JsonValueKind.String)
        {
            scriptName = scriptNameElement.GetString();
        }

        var result = await _scriptExecutor.ExecuteScriptAsync(scriptContent, scriptName, cancellationToken);

        return FormatExecutionResult(result);
    }

    private async Task<string> HandleInstallPackageAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var packageName = root.GetProperty("package_name").GetString()
            ?? throw new ArgumentException("package_name is required");

        var success = await _envService.InstallPackageAsync(packageName, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success,
            message = success
                ? $"Package '{packageName}' installed successfully"
                : $"Failed to install package '{packageName}'"
        });
    }

    private async Task<string> HandleListFilesAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        string? subdirectory = null;
        if (root.TryGetProperty("subdirectory", out var subDirElement) &&
            subDirElement.ValueKind == JsonValueKind.String)
        {
            subdirectory = subDirElement.GetString();
        }

        var files = await _scriptExecutor.ListFilesAsync(subdirectory, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            files,
            count = files.Count
        });
    }

    private async Task<string> HandleExecuteScriptFileAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var scriptPath = root.GetProperty("script_path").GetString()
            ?? throw new ArgumentException("script_path is required");

        string? arguments = null;
        if (root.TryGetProperty("arguments", out var argsElement) &&
            argsElement.ValueKind == JsonValueKind.String)
        {
            arguments = argsElement.GetString();
        }

        var result = await _scriptExecutor.ExecuteFileAsync(scriptPath, arguments, cancellationToken);

        return FormatExecutionResult(result);
    }

    private async Task<string> HandleFindFilesAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var filenamePattern = root.GetProperty("filename_pattern").GetString()
            ?? throw new ArgumentException("filename_pattern is required");

        string? searchPath = null;
        if (root.TryGetProperty("search_path", out var searchPathElement) &&
            searchPathElement.ValueKind == JsonValueKind.String)
        {
            searchPath = searchPathElement.GetString();
        }

        bool recursive = true;
        if (root.TryGetProperty("recursive", out var recursiveElement) &&
            recursiveElement.ValueKind == JsonValueKind.True || recursiveElement.ValueKind == JsonValueKind.False)
        {
            recursive = recursiveElement.GetBoolean();
        }

        int maxResults = 10;
        if (root.TryGetProperty("max_results", out var maxResultsElement) &&
            maxResultsElement.ValueKind == JsonValueKind.Number)
        {
            maxResults = maxResultsElement.GetInt32();
        }

        var foundFiles = new List<object>();
        var searchDirs = new List<string>();

        // If search path provided, use it; otherwise search common directories
        if (!string.IsNullOrEmpty(searchPath) && Directory.Exists(searchPath))
        {
            searchDirs.Add(searchPath);
        }
        else
        {
            // Search common user directories
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile, "Downloads");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (Directory.Exists(downloads)) searchDirs.Add(downloads);
            if (Directory.Exists(documents)) searchDirs.Add(documents);
            if (Directory.Exists(desktop)) searchDirs.Add(desktop);
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var dir in searchDirs)
        {
            if (foundFiles.Count >= maxResults) break;

            try
            {
                var files = Directory.EnumerateFiles(dir, filenamePattern, searchOption)
                    .Take(maxResults - foundFiles.Count);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        foundFiles.Add(new
                        {
                            path = file,
                            name = fileInfo.Name,
                            size_bytes = fileInfo.Length,
                            last_modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            extension = fileInfo.Extension
                        });
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
                _logger.LogDebug("Cannot access directory: {Directory}", dir);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error searching directory: {Directory}", dir);
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            files = foundFiles,
            count = foundFiles.Count,
            searched_directories = searchDirs
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> HandleReadExternalFileAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var filePath = root.GetProperty("file_path").GetString()
            ?? throw new ArgumentException("file_path is required");

        int maxSizeKb = 1024;
        if (root.TryGetProperty("max_size_kb", out var maxSizeElement) &&
            maxSizeElement.ValueKind == JsonValueKind.Number)
        {
            maxSizeKb = maxSizeElement.GetInt32();
        }

        if (!File.Exists(filePath))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"File not found: {filePath}"
            });
        }

        var fileInfo = new FileInfo(filePath);
        var isBinary = IsBinaryFile(filePath);

        if (isBinary)
        {
            // For binary files, return metadata only
            return JsonSerializer.Serialize(new
            {
                success = true,
                is_binary = true,
                file_info = new
                {
                    path = filePath,
                    name = fileInfo.Name,
                    size_bytes = fileInfo.Length,
                    extension = fileInfo.Extension,
                    last_modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                },
                message = "This is a binary file. Use copy_to_workspace to copy it to the workspace and process it with Python scripts."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        // Read text file
        var maxBytes = maxSizeKb * 1024;
        string content;
        bool truncated = false;

        if (fileInfo.Length > maxBytes)
        {
            using var reader = new StreamReader(filePath);
            var buffer = new char[maxBytes];
            var read = await reader.ReadAsync(buffer, 0, maxBytes);
            content = new string(buffer, 0, read);
            truncated = true;
        }
        else
        {
            content = await File.ReadAllTextAsync(filePath, cancellationToken);
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            is_binary = false,
            content,
            truncated,
            file_info = new
            {
                path = filePath,
                name = fileInfo.Name,
                size_bytes = fileInfo.Length,
                extension = fileInfo.Extension
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> HandleCopyToWorkspaceAsync(
        RequiredFunctionToolCall call,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(call.Arguments);
        var root = doc.RootElement;

        var sourcePath = root.GetProperty("source_path").GetString()
            ?? throw new ArgumentException("source_path is required");

        string? destinationName = null;
        if (root.TryGetProperty("destination_name", out var destElement) &&
            destElement.ValueKind == JsonValueKind.String)
        {
            destinationName = destElement.GetString();
        }

        if (!File.Exists(sourcePath))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Source file not found: {sourcePath}"
            });
        }

        var sourceFileName = Path.GetFileName(sourcePath);
        var targetName = destinationName ?? sourceFileName;
        var workspacePath = _envService.GetWorkspacePath();
        var targetPath = Path.Combine(workspacePath, targetName);

        // Ensure target directory exists
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true), cancellationToken);

        var fileInfo = new FileInfo(targetPath);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "File copied to workspace successfully",
            source_path = sourcePath,
            destination_path = targetPath,
            workspace_relative_path = targetName,
            size_bytes = fileInfo.Length
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool IsBinaryFile(string filePath)
    {
        var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".exe", ".dll", ".so", ".dylib",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp",
            ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac",
            ".bin", ".dat", ".db", ".sqlite"
        };

        var extension = Path.GetExtension(filePath);
        return binaryExtensions.Contains(extension);
    }

    private static string FormatExecutionResult(PythonExecutionResult result)
    {
        return JsonSerializer.Serialize(new
        {
            success = result.Result == Model.Enums.ExecutionResult.Success,
            exit_code = result.ExitCode,
            stdout = result.StandardOutput,
            stderr = result.StandardError,
            execution_time_ms = (int)result.ExecutionTime.TotalMilliseconds,
            script_path = result.ScriptPath,
            generated_files = result.GeneratedFiles,
            error = result.ErrorMessage
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
