using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RR.Agent.Model.Options;

namespace RR.Agent.Service.Tools
{
    public class PythonToolService(ILogger<PythonToolService> logger, 
        PythonEnvironmentOptions options, AgentOptions agentOptions)
    {
        private readonly ILogger<PythonToolService> _logger = logger;
        private readonly PythonEnvironmentOptions _options = options;
        private readonly AgentOptions _agentOptions = agentOptions;

        private readonly string _workspacePath = Path.GetFullPath(agentOptions.WorkspaceDirectory);
        private readonly string _virtualEnvPath = Path.Combine(agentOptions.WorkspaceDirectory, options.VenvName);
        private readonly string _scriptsPath = Path.Combine(agentOptions.WorkspaceDirectory, options.ScriptsDirectory);
        private readonly string _outputPath = Path.Combine(agentOptions.WorkspaceDirectory, options.OutputDirectory);

        [Description("Gets the path to the Python executable in the virtual environment.")]
        public string GetVirtualEnvironmentPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(_virtualEnvPath, "Scripts", "python.exe");
            }
            return Path.Combine(_virtualEnvPath, "bin", "python");
        }

        [Description("Gets the path to the pip executable in the virtual environment.")]
        public string GetVirtualEnvironmentPipPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(_virtualEnvPath, "Scripts", "pip.exe");
            }
            return Path.Combine(_virtualEnvPath, "bin", "pip");
        }

        public async Task<bool> CreateVirtualEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating Python virtual environment at {VenvPath}", _virtualEnvPath);

                // Create workspace directories if they don't exist, ignored if they do
                Directory.CreateDirectory(_workspacePath);
                Directory.CreateDirectory(_scriptsPath);
                Directory.CreateDirectory(_outputPath);

                // Check if venv already exists
                if (Directory.Exists(_virtualEnvPath) && File.Exists(GetVirtualEnvironmentPath()))
                {
                    _logger.LogInformation("Virtual environment already exists at {VenvPath}", _virtualEnvPath);
                    return true;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m venv \"{_virtualEnvPath}\"",
                    WorkingDirectory = _workspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi)!;
                
                string output = await process.StandardOutput.ReadToEndAsync();
                string errors = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                    throw new Exception($"Failed to create virtual environment (exit code {process.ExitCode}): {errors}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Python virtual environment.");
                return false;
            }
        }

        [Description("Installs the specified Python packages into the virtual environment.")]
        public async Task<bool> InstallPackagesAsync(string[] packages, CancellationToken cancellationToken = default)
        {
            try
            {
                if (packages == null || packages.Length == 0)
                {
                    _logger.LogInformation("No Python packages specified for installation.");
                    return true;
                }

                var packageString = string.Join(" ", packages.Select(p => $"\"{p}\""));

                var psi = new ProcessStartInfo
                {
                    FileName = GetVirtualEnvironmentPipPath(),       
                    WorkingDirectory = _workspacePath,       
                    Arguments = $"install {packageString}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi)!;
                
                string output = await process.StandardOutput.ReadToEndAsync();
                string errors = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    throw new Exception($"Python script failed (exit code {process.ExitCode}): {errors}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install Python packages.");
                return false;
            }
        }

        [Description("Executes a Python script from the scripts directory.")]
        public async Task<string> ExcetueScriptAsync(string fileName, string arguments = "")
        {
            try
            {
                var filePath = Path.Combine(_scriptsPath, fileName);
                var psi = new ProcessStartInfo
                {
                    FileName = GetVirtualEnvironmentPath(),              
                    Arguments = $"\"{filePath}\" {arguments}",
                    WorkingDirectory = _workspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi)!;
                
                string output = await process.StandardOutput.ReadToEndAsync();
                string errors = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    throw new Exception($"Python script failed (exit code {process.ExitCode}): {errors}");

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Python script: {Script}", fileName);
                throw;
            }
        }
    }
}