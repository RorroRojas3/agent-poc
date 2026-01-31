namespace RR.Agent.Infrastructure;

using System.Text;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Processes messages from Azure AI Agent threads.
/// </summary>
public sealed class MessageProcessor : IMessageProcessor
{
    private readonly PersistentAgentsClient _client;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(
        PersistentAgentsClient client,
        ILogger<MessageProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }

    public async Task<string> GetTextOutputAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        var messages = _client.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Ascending,
            cancellationToken: cancellationToken);

        var output = new StringBuilder();

        await foreach (var message in messages)
        {
            foreach (var content in message.ContentItems)
            {
                if (content is MessageTextContent textContent)
                {
                    if (output.Length > 0)
                    {
                        output.AppendLine();
                    }
                    output.Append(textContent.Text);
                }
            }
        }

        _logger.LogDebug(
            "Extracted {Length} characters of text from thread {ThreadId}",
            output.Length,
            threadId);

        return output.ToString();
    }

    public async Task<IReadOnlyList<string>> GetFileReferencesAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        var messages = _client.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Ascending,
            cancellationToken: cancellationToken);

        var fileIds = new List<string>();

        await foreach (var message in messages)
        {
            foreach (var content in message.ContentItems)
            {
                if (content is MessageImageFileContent imageContent)
                {
                    fileIds.Add(imageContent.FileId);
                }
            }

            // Also check attachments
            if (message.Attachments is not null)
            {
                foreach (var attachment in message.Attachments)
                {
                    if (!string.IsNullOrEmpty(attachment.FileId))
                    {
                        fileIds.Add(attachment.FileId);
                    }
                }
            }
        }

        _logger.LogDebug(
            "Found {Count} file references in thread {ThreadId}",
            fileIds.Count,
            threadId);

        return fileIds;
    }

    public async Task<string?> GetLatestAssistantMessageAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);

        var messages = _client.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Descending,
            cancellationToken: cancellationToken);

        await foreach (var message in messages)
        {
            if (message.Role == MessageRole.Agent)
            {
                var text = new StringBuilder();
                foreach (var content in message.ContentItems)
                {
                    if (content is MessageTextContent textContent)
                    {
                        text.Append(textContent.Text);
                    }
                }

                if (text.Length > 0)
                {
                    return text.ToString();
                }
            }
        }

        return null;
    }
}
