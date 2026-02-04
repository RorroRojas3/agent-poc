using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Options;

namespace RR.Agent.Service.Python;

/// <summary>
/// Service for managing Python virtual environments.
/// </summary>
public sealed class PythonEnvironmentService : IPythonEnvironmentService
{
    private readonly PythonEnvironmentOptions _options;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<PythonEnvironmentService> _logger;

    private string _workspacePath = string.Empty;
    private string _venvPath = string.Empty;
    private string _scriptsPath = string.Empty;
    private string _outputPath = string.Empty;
    private bool _isInitialized;

    public PythonEnvironmentService(
        IOptions<PythonEnvironmentOptions> options,
        IOptions<AgentOptions> agentOptions,
        ILogger<PythonEnvironmentService> logger)
    {
        _options = options.Value;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    public async Task<bool> InitializeEnvironmentAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _workspacePath = Path.GetFullPath(workspacePath);
            _venvPath = Path.Combine(_workspacePath, _options.VenvName);
            _scriptsPath = Path.Combine(_workspacePath, _options.ScriptsDirectory);
            _outputPath = Path.Combine(_workspacePath, _options.OutputDirectory);

            _logger.LogInformation("Initializing Python environment at {WorkspacePath}", _workspacePath);

            // Create workspace directories
            Directory.CreateDirectory(_workspacePath);
            Directory.CreateDirectory(_scriptsPath);
            Directory.CreateDirectory(_outputPath);

            // Check if venv already exists
            if (Directory.Exists(_venvPath) && File.Exists(GetVenvPythonPath()))
            {
                _logger.LogInformation("Virtual environment already exists at {VenvPath}", _venvPath);
                _isInitialized = true;
                return true;
            }

            // Create virtual environment
            _logger.LogInformation("Creating virtual environment at {VenvPath}", _venvPath);
            var createVenvResult = await RunProcessAsync(
                _options.PythonExecutable,
                $"-m venv \"{_venvPath}\"",
                _workspacePath,
                TimeSpan.FromSeconds(_options.PipTimeoutSeconds),
                cancellationToken);

            if (createVenvResult.ExitCode != 0)
            {
                _logger.LogError("Failed to create virtual environment: {Error}", createVenvResult.StandardError);
                return false;
            }

            _logger.LogInformation("Virtual environment created successfully");

            // Upgrade pip
            _logger.LogInformation("Upgrading pip...");
            var upgradePipResult = await RunProcessAsync(
                GetVenvPythonPath(),
                "-m pip install --upgrade pip",
                _workspacePath,
                TimeSpan.FromSeconds(_options.PipTimeoutSeconds),
                cancellationToken);

            if (upgradePipResult.ExitCode != 0)
            {
                _logger.LogWarning("Failed to upgrade pip: {Error}", upgradePipResult.StandardError);
                // Continue anyway, not critical
            }

            // Install default packages if specified
            if (_options.DefaultPackages.Count > 0)
            {
                _logger.LogInformation("Installing default packages: {Packages}", string.Join(", ", _options.DefaultPackages));
                await InstallPackagesAsync(_options.DefaultPackages, cancellationToken);
            }

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Python environment");
            return false;
        }
    }

    public async Task<bool> InstallPackagesAsync(IEnumerable<string> packages, CancellationToken cancellationToken = default)
    {
        var packageList = packages.ToList();
        if (packageList.Count == 0)
        {
            return true;
        }

        var packageString = string.Join(" ", packageList.Select(p => $"\"{p}\""));
        _logger.LogInformation("Installing packages: {Packages}", string.Join(", ", packageList));

        var result = await RunProcessAsync(
            GetVenvPipPath(),
            $"install {packageString}",
            _workspacePath,
            TimeSpan.FromSeconds(_options.PipTimeoutSeconds),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to install packages: {Error}", result.StandardError);
            return false;
        }

        _logger.LogInformation("Packages installed successfully");
        return true;
    }

    public async Task<bool> InstallPackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        return await InstallPackagesAsync([packageName], cancellationToken);
    }

    public async Task<bool> IsEnvironmentReadyAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            return false;
        }

        try
        {
            var pythonPath = GetVenvPythonPath();
            if (!File.Exists(pythonPath))
            {
                return false;
            }

            // Test that Python can run
            var result = await RunProcessAsync(
                pythonPath,
                "--version",
                _workspacePath,
                TimeSpan.FromSeconds(10),
                cancellationToken);

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetVenvPythonPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(_venvPath, "Scripts", "python.exe");
        }
        return Path.Combine(_venvPath, "bin", "python");
    }

    public string GetVenvPipPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(_venvPath, "Scripts", "pip.exe");
        }
        return Path.Combine(_venvPath, "bin", "pip");
    }

    public string GetWorkspacePath() => _workspacePath;

    public string GetVenvPath() => _venvPath;

    public string GetScriptsPath() => _scriptsPath;

    public string GetOutputPath() => _outputPath;

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        // Optionally clean up the virtual environment
        // For now, we keep it for reuse
        _logger.LogInformation("Cleanup requested for workspace at {WorkspacePath}", _workspacePath);
        return Task.CompletedTask;
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Running process: {FileName} {Arguments}", fileName, arguments);

        using var process = new Process { StartInfo = psi };
        var stdout = new List<string>();
        var stderr = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.Add(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.Add(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token);

            return new ProcessResult(
                process.ExitCode,
                string.Join(Environment.NewLine, stdout),
                string.Join(Environment.NewLine, stderr));
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }

            return new ProcessResult(-1, string.Join(Environment.NewLine, stdout), "Process timed out or was cancelled");
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
