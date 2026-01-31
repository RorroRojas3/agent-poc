namespace RR.Agent.Infrastructure;

/// <summary>
/// Processes messages from agent threads.
/// </summary>
public interface IMessageProcessor
{
    /// <summary>
    /// Extracts text content from thread messages.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consolidated text output.</returns>
    Task<string> GetTextOutputAsync(
        string threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts file references from thread messages.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file IDs.</returns>
    Task<IReadOnlyList<string>> GetFileReferencesAsync(
        string threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest assistant message from a thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest assistant message text.</returns>
    Task<string?> GetLatestAssistantMessageAsync(
        string threadId,
        CancellationToken cancellationToken = default);
}
