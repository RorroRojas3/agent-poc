using Microsoft.Extensions.Logging;

namespace RR.Agent.Service.Tools
{
    public class PythonToolService(ILogger<PythonToolService> logger)
    {
        private readonly ILogger<PythonToolService> _logger = logger;
    }
}