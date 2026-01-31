namespace RR.Agent.Infrastructure;

using Azure.AI.Agents.Persistent;

/// <summary>
/// Polls Azure AI Agent runs until completion.
/// </summary>
public interface IRunPoller
{
    /// <summary>
    /// Waits for a run to complete, handling intermediate states.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="runId">The run ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed run.</returns>
    Task<ThreadRun> WaitForCompletionAsync(
        string threadId,
        string runId,
        CancellationToken cancellationToken = default);
}
