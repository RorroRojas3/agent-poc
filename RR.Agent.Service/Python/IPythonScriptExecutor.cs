using RR.Agent.Model.Dtos;

namespace RR.Agent.Service.Python;

/// <summary>
/// Service for executing Python scripts within the virtual environment.
/// </summary>
public interface IPythonScriptExecutor
{
    /// <summary>
    /// Executes a Python script from its content.
    /// Writes the script to a file and executes it.
    /// </summary>
    /// <param name="scriptContent">The Python code to execute.</param>
    /// <param name="scriptName">Optional name for the script file (defaults to generated name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result including stdout, stderr, and exit code.</returns>
    Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptContent,
        string? scriptName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an existing Python script file.
    /// </summary>
    /// <param name="scriptPath">Path to the Python script file.</param>
    /// <param name="arguments">Optional command-line arguments for the script.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result including stdout, stderr, and exit code.</returns>
    Task<PythonExecutionResult> ExecuteFileAsync(
        string scriptPath,
        string? arguments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a Python script to the scripts directory.
    /// </summary>
    /// <param name="scriptContent">The Python code to write.</param>
    /// <param name="scriptName">Name for the script file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the written script file.</returns>
    Task<string> WriteScriptAsync(
        string scriptContent,
        string scriptName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the content of a file from the workspace.
    /// </summary>
    /// <param name="filePath">Path relative to workspace or absolute path within workspace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content, or null if file doesn't exist.</returns>
    Task<string?> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes content to a file in the workspace.
    /// </summary>
    /// <param name="filePath">Path relative to workspace or absolute path within workspace.</param>
    /// <param name="content">Content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path to the written file.</returns>
    Task<string> WriteFileAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all files in the workspace directory.
    /// </summary>
    /// <param name="subdirectory">Optional subdirectory within workspace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file paths relative to workspace.</returns>
    Task<IReadOnlyList<string>> ListFilesAsync(
        string? subdirectory = null,
        CancellationToken cancellationToken = default);
}
