namespace RR.Agent.Infrastructure;

using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;

/// <summary>
/// Polls Azure AI Agent runs until completion with timeout support.
/// </summary>
public sealed class RunPoller : IRunPoller
{
    private readonly PersistentAgentsClient _client;
    private readonly AgentOptions _options;
    private readonly ILogger<RunPoller> _logger;

    public RunPoller(
        PersistentAgentsClient client,
        IOptions<AgentOptions> options,
        ILogger<RunPoller> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ThreadRun> WaitForCompletionAsync(
        string threadId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(_options.RunTimeoutSeconds);

        _logger.LogDebug("Starting to poll run {RunId} on thread {ThreadId}", runId, threadId);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (stopwatch.Elapsed > timeout)
            {
                _logger.LogWarning(
                    "Run {RunId} timed out after {Seconds} seconds",
                    runId,
                    timeout.TotalSeconds);

                throw new TimeoutException(
                    $"Run {runId} did not complete within {timeout.TotalSeconds} seconds.");
            }

            var response = await _client.Runs.GetRunAsync(threadId, runId);
            var run = response.Value;

            _logger.LogDebug("Run {RunId} status: {Status}", runId, run.Status);

            if (run.Status == RunStatus.Completed)
            {
                _logger.LogInformation(
                    "Run {RunId} completed successfully in {ElapsedMs}ms",
                    runId,
                    stopwatch.ElapsedMilliseconds);
                return run;
            }

            if (run.Status == RunStatus.Failed)
            {
                _logger.LogWarning(
                    "Run {RunId} failed: {Error}",
                    runId,
                    run.LastError?.Message ?? "Unknown error");
                return run;
            }

            if (run.Status == RunStatus.Cancelled)
            {
                _logger.LogInformation("Run {RunId} was cancelled", runId);
                return run;
            }

            if (run.Status == RunStatus.Expired)
            {
                _logger.LogWarning("Run {RunId} expired", runId);
                return run;
            }

            if (run.Status == RunStatus.RequiresAction)
            {
                _logger.LogDebug("Run {RunId} requires action", runId);
            }

            // For Queued, InProgress, RequiresAction, or other states - wait and poll again
            await Task.Delay(_options.PollingIntervalMs, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException(cancellationToken);
    }
}
