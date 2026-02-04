namespace RR.Agent.Service.Python;

/// <summary>
/// Service for managing Python virtual environments.
/// </summary>
public interface IPythonEnvironmentService
{
    /// <summary>
    /// Initializes the Python environment for the given workspace.
    /// Creates the virtual environment if it doesn't exist.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    Task<bool> InitializeEnvironmentAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the specified Python packages using pip.
    /// </summary>
    /// <param name="packages">List of package names to install.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all packages were installed successfully.</returns>
    Task<bool> InstallPackagesAsync(IEnumerable<string> packages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a single Python package using pip.
    /// </summary>
    /// <param name="packageName">Name of the package to install.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the package was installed successfully.</returns>
    Task<bool> InstallPackageAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the Python environment is ready for script execution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the environment is ready.</returns>
    Task<bool> IsEnvironmentReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the Python executable within the virtual environment.
    /// </summary>
    string GetVenvPythonPath();

    /// <summary>
    /// Gets the path to pip within the virtual environment.
    /// </summary>
    string GetVenvPipPath();

    /// <summary>
    /// Gets the workspace directory path.
    /// </summary>
    string GetWorkspacePath();

    /// <summary>
    /// Gets the virtual environment directory path.
    /// </summary>
    string GetVenvPath();

    /// <summary>
    /// Gets the scripts directory path within the workspace.
    /// </summary>
    string GetScriptsPath();

    /// <summary>
    /// Gets the output directory path within the workspace.
    /// </summary>
    string GetOutputPath();

    /// <summary>
    /// Cleans up the Python environment (optional).
    /// </summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
