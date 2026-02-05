using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace RR.Agent.Service.Tools;

public class FileToolService(ILogger<FileToolService> logger)
{
    private readonly ILogger<FileToolService> _logger = logger;

    /// <summary>
    /// Checks whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The absolute or relative path to the file.</param>
    /// <returns>
    /// <see langword="true"/> if the file exists; otherwise, <see langword="false"/>.
    /// Returns <see langword="false"/> if an error occurs during the check.
    /// </returns>
    /// <example>
    /// <code>
    /// bool exists = await fileService.FileExistsAsync("C:/data/config.json");
    /// if (exists)
    /// {
    ///     // Process the file
    /// }
    /// </code>
    /// </example>
    [Description("Checks if a file exists at the specified path.")]
    public async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            await Task.CompletedTask;
            return File.Exists(path);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking file existence {Path}: {ErrorMessage}", path, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Reads the entire content of a file as a string.
    /// </summary>
    /// <param name="path">The absolute or relative path to the file to read.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The content of the file as a string, or <see langword="null"/> if the file cannot be read.
    /// </returns>
    /// <example>
    /// <code>
    /// string? content = await fileService.ReadFileAsync("C:/data/input.txt");
    /// if (content is not null)
    /// {
    ///     Console.WriteLine(content);
    /// }
    /// </code>
    /// </example>
    [Description("Reads the content of a file at the specified path.")]
    public async Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error reading file {Path}: {ErrorMessage}", path, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Writes the specified content to a file, creating the file if it does not exist or overwriting it if it does.
    /// </summary>
    /// <param name="path">The absolute or relative path to the file to write.</param>
    /// <param name="content">The string content to write to the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the file was written successfully; otherwise, <see langword="false"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// bool success = await fileService.WriteFileAsync("C:/data/output.txt", "Generated content");
    /// if (success)
    /// {
    ///     Console.WriteLine("File saved successfully.");
    /// }
    /// </code>
    /// </example>
    [Description("Writes content to a file at the specified path.")]
    public async Task<bool> WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            await File.WriteAllTextAsync(path, content, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error writing file {Path}: {ErrorMessage}", path, ex.Message);
            return false;
        }
    }
}