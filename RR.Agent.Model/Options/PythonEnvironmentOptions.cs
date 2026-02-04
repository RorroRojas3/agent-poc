namespace RR.Agent.Model.Options;

/// <summary>
/// Configuration options for Python virtual environment and script execution.
/// </summary>
public sealed class PythonEnvironmentOptions
{
    public const string SectionName = "PythonEnvironment";

    /// <summary>
    /// Path or command to the system Python executable used to create virtual environments.
    /// </summary>
    public string PythonExecutable { get; set; } = "python";

    /// <summary>
    /// Name of the virtual environment directory within the workspace.
    /// </summary>
    public string VenvName { get; set; } = "venv";

    /// <summary>
    /// Maximum time in seconds to wait for a Python script to complete execution.
    /// </summary>
    public int ExecutionTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum time in seconds to wait for pip install operations.
    /// </summary>
    public int PipTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Packages to pre-install when creating the virtual environment.
    /// </summary>
    public List<string> DefaultPackages { get; set; } = [];

    /// <summary>
    /// Whether to create an isolated virtual environment for each run.
    /// </summary>
    public bool IsolateEnvironment { get; set; } = true;

    /// <summary>
    /// Directory name for storing Python scripts within the workspace.
    /// </summary>
    public string ScriptsDirectory { get; set; } = "scripts";

    /// <summary>
    /// Directory name for storing script outputs within the workspace.
    /// </summary>
    public string OutputDirectory { get; set; } = "output";
}
