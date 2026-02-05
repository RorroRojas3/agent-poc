using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace RR.Agent.Service.Tools
{
    public class FileToolService(ILogger<FileToolService> logger)
    {
        private readonly ILogger<FileToolService> _logger = logger;

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
        /// Writes content to a file at the specified path.
        /// </summary>
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
}