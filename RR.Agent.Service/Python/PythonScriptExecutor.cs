using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;

namespace RR.Agent.Service.Python;

/// <summary>
/// Service for executing Python scripts within the virtual environment.
/// </summary>
public sealed class PythonScriptExecutor : IPythonScriptExecutor
{
    private readonly IPythonEnvironmentService _envService;
    private readonly PythonEnvironmentOptions _options;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<PythonScriptExecutor> _logger;

    public PythonScriptExecutor(
        IPythonEnvironmentService envService,
        IOptions<PythonEnvironmentOptions> options,
        IOptions<AgentOptions> agentOptions,
        ILogger<PythonScriptExecutor> logger)
    {
        _envService = envService;
        _options = options.Value;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    public async Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptContent,
        string? scriptName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate script name if not provided
            var fileName = scriptName ?? $"script_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}.py";
            if (!fileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".py";
            }

            // Write script to file
            var scriptPath = await WriteScriptAsync(scriptContent, fileName, cancellationToken);

            // Execute the script
            return await ExecuteFileAsync(scriptPath, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script");
            return PythonExecutionResult.Error($"Error executing script: {ex.Message}");
        }
    }

    public async Task<PythonExecutionResult> ExecuteFileAsync(
        string scriptPath,
        string? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate script path
            var fullScriptPath = GetSafeFilePath(scriptPath);
            if (!File.Exists(fullScriptPath))
            {
                return PythonExecutionResult.Error($"Script file not found: {scriptPath}");
            }

            var pythonPath = _envService.GetVenvPythonPath();
            if (!File.Exists(pythonPath))
            {
                return PythonExecutionResult.Error("Python virtual environment not initialized");
            }

            // Build arguments
            var args = $"\"{fullScriptPath}\"";
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                args += $" {arguments}";
            }

            _logger.LogInformation("Executing Python script: {ScriptPath}", fullScriptPath);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = args,
                WorkingDirectory = _envService.GetWorkspacePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set environment variables
            psi.Environment["PYTHONUNBUFFERED"] = "1";

            using var process = new Process { StartInfo = psi };

            var stdoutBuilder = new List<string>();
            var stderrBuilder = new List<string>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdoutBuilder.Add(e.Data);
                    _logger.LogDebug("[STDOUT] {Line}", e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stderrBuilder.Add(e.Data);
                    _logger.LogDebug("[STDERR] {Line}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { }

                stopwatch.Stop();
                return PythonExecutionResult.Timeout(stopwatch.Elapsed, fullScriptPath);
            }

            stopwatch.Stop();

            var stdout = string.Join(Environment.NewLine, stdoutBuilder);
            var stderr = string.Join(Environment.NewLine, stderrBuilder);

            // Truncate output if too large
            stdout = TruncateOutput(stdout);
            stderr = TruncateOutput(stderr);

            _logger.LogInformation(
                "Script execution completed with exit code {ExitCode} in {Duration}ms",
                process.ExitCode,
                stopwatch.ElapsedMilliseconds);

            if (process.ExitCode == 0)
            {
                var result = PythonExecutionResult.Success(stdout, stderr, stopwatch.Elapsed, fullScriptPath);
                result.GeneratedFiles = await DetectGeneratedFilesAsync(cancellationToken);
                return result;
            }
            else
            {
                return PythonExecutionResult.Failure(process.ExitCode, stdout, stderr, stopwatch.Elapsed, fullScriptPath);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing script file {ScriptPath}", scriptPath);
            return PythonExecutionResult.Error($"Error executing script: {ex.Message}");
        }
    }

    public async Task<string> WriteScriptAsync(
        string scriptContent,
        string scriptName,
        CancellationToken cancellationToken = default)
    {
        var scriptsPath = _envService.GetScriptsPath();
        var scriptPath = Path.Combine(scriptsPath, scriptName);

        // Ensure scripts directory exists
        Directory.CreateDirectory(scriptsPath);

        _logger.LogInformation("Writing script to {ScriptPath}", scriptPath);
        await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken);

        return scriptPath;
    }

    public async Task<string?> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFilePath(filePath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {FilePath}", filePath);
            return null;
        }
    }

    public async Task<string> WriteFileAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFilePath(filePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _logger.LogInformation("Writing file to {FilePath}", fullPath);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);

        return fullPath;
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(
        string? subdirectory = null,
        CancellationToken cancellationToken = default)
    {
        var basePath = _envService.GetWorkspacePath();
        if (!string.IsNullOrEmpty(subdirectory))
        {
            basePath = GetSafeFilePath(subdirectory);
        }

        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_envService.GetWorkspacePath(), f))
            .Where(f => !f.StartsWith(_options.VenvName)) // Exclude venv files
            .OrderBy(f => f)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    /// <summary>
    /// Gets a safe file path that is within the workspace directory.
    /// Prevents directory traversal attacks.
    /// </summary>
    private string GetSafeFilePath(string filePath)
    {
        var workspacePath = _envService.GetWorkspacePath();

        // If it's already an absolute path, verify it's within workspace
        if (Path.IsPathRooted(filePath))
        {
            var normalizedPath = Path.GetFullPath(filePath);
            if (!normalizedPath.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path '{filePath}' is outside the workspace directory");
            }
            return normalizedPath;
        }

        // Combine with workspace path and normalize
        var combined = Path.Combine(workspacePath, filePath);
        var fullPath = Path.GetFullPath(combined);

        // Verify the result is still within workspace
        if (!fullPath.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{filePath}' would escape the workspace directory");
        }

        return fullPath;
    }

    /// <summary>
    /// Truncates output to the maximum configured size.
    /// </summary>
    private string TruncateOutput(string output)
    {
        if (output.Length <= _agentOptions.MaxOutputSizeBytes)
        {
            return output;
        }

        var truncated = output[.._agentOptions.MaxOutputSizeBytes];
        return truncated + $"\n... [Output truncated, {output.Length - _agentOptions.MaxOutputSizeBytes} bytes omitted]";
    }

    /// <summary>
    /// Detects files that were generated by script execution.
    /// </summary>
    private async Task<List<string>> DetectGeneratedFilesAsync(CancellationToken cancellationToken)
    {
        // For now, return files in the output directory
        var outputPath = _envService.GetOutputPath();
        if (!Directory.Exists(outputPath))
        {
            return [];
        }

        await Task.CompletedTask; // Placeholder for async operations if needed

        return Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_envService.GetWorkspacePath(), f))
            .ToList();
    }
}
