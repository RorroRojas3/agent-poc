using Anthropic;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;
using OpenAI.Responses;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using MessageRole = Azure.AI.Agents.Persistent.MessageRole;

namespace RR.Agent.Service.Agents;

public interface IAgentService
{
}

/// <summary>
/// Service for managing Azure AI Foundry persistent agents.
/// </summary>
public sealed class AgentService : IDisposable
{
    private readonly PersistentAgentsClient _client;
    private readonly AzureAIFoundryOptions _options;
    private readonly AgentOptions _agentOptions;
    private readonly AnthropicClient _anthropicClient;
    private readonly ClaudeOptions _claudeOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly OllamaApiClient _ollamaClient;
    private readonly OpenAIOptions _openAIOptions;
    private readonly OpenAIClient _openAIClient;
    private readonly ILogger<AgentService> _logger;

   
    private readonly Dictionary<string, PersistentAgent> _agents = [];
    private readonly Dictionary<string, PersistentAgentThread> _threads = [];

    private readonly Dictionary<string, ChatClientAgent> _chatClientAgents = [];
    private readonly Dictionary<string, AgentSession> _agentSessions = [];

    public AgentService(
        IOptions<AzureAIFoundryOptions> options,
        IOptions<AgentOptions> agentOptions,
        IOptions<ClaudeOptions> claudeOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<OpenAIOptions> openAIOptions,
        ILogger<AgentService> logger)
    {
        _options = options.Value;
        _agentOptions = agentOptions.Value;
        _claudeOptions = claudeOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _openAIOptions = openAIOptions.Value;

        _logger = logger;

        _client = new PersistentAgentsClient(
            _options.Url,
            new DefaultAzureCredential());
        _anthropicClient = new AnthropicClient() { APIKey = _claudeOptions.ApiKey};
        _ollamaClient = new OllamaApiClient(_ollamaOptions.Uri, _ollamaOptions.Model);
        _openAIClient = new OpenAIClient(_openAIOptions.ApiKey);
    }

    public async Task<ChatClientAgent> GetOrCreateChatClientAgentAsync(
        AgentsTypes agentsTypes,
        string agentName,
        ChatClientAgentOptions chatClientAgentOptions,
        CancellationToken cancellationToken = default)
    {
        if (_chatClientAgents.TryGetValue(agentName, out var existingAgent))
        {
            return existingAgent;
        }

        _logger.LogInformation("Creating chat client agent: {AgentName}", agentName);

        var agent = await GetChatClientAgent(agentsTypes, chatClientAgentOptions, cancellationToken);

        _chatClientAgents[agentName] = agent;

        _logger.LogInformation("Chat client agent created: {AgentName}", agentName);

        return agent;
    }

    public async Task<AgentSession> CreateSessionAsync(string agentName,
        string sessionName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating session: {SessionName}", sessionName);

        var agent = _chatClientAgents[agentName];
        var agentSession = await agent.GetNewSessionAsync(cancellationToken: cancellationToken);

        _agentSessions[sessionName] = agentSession;

        return agentSession;
    }

    public async Task<string> RunAsync(
        string agentName,
        string sessionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        var agent = _chatClientAgents[agentName];
        var agentSession = _agentSessions[sessionName];

        var response = await agent.RunAsync(message, agentSession, cancellationToken: cancellationToken);

        var serializedSession = agentSession.Serialize();
        var resumedSession = await agent.DeserializeSessionAsync(serializedSession, cancellationToken: cancellationToken);
        _agentSessions[sessionName] = resumedSession;

        return response.Text;
    }

    public async Task<AgentResponse> RunAsAgentResponseAsync(
        string agentName,
        string sessionName,
        string message,
        CancellationToken cancellationToken = default)
    {
        var agent = _chatClientAgents[agentName];
        var agentSession = _agentSessions[sessionName];

        var response = await agent.RunAsync(message, agentSession, cancellationToken: cancellationToken);

        var serializedSession = agentSession.Serialize();
        var resumedSession = await agent.DeserializeSessionAsync(serializedSession, cancellationToken: cancellationToken);
        _agentSessions[sessionName] = resumedSession;

        return response;
    }

    /// <summary>
    /// Gets or creates an agent with the specified configuration.
    /// </summary>
    public async Task<PersistentAgent> GetOrCreateAgentAsync(
        string agentName,
        string systemPrompt,
        IEnumerable<ToolDefinition>? tools = null,
        BinaryData? responseFormat = null,
        CancellationToken cancellationToken = default)
    {
        if (_agents.TryGetValue(agentName, out var existingAgent))
        {
            return existingAgent;
        }

        _logger.LogInformation("Creating agent: {AgentName}", agentName);

        var model = _options.GetModelForRole(agentName);

        var agentResponse = await _client.Administration.CreateAgentAsync(
            model: model,
            name: agentName,
            instructions: systemPrompt,
            tools: tools?.ToList(),
            responseFormat: responseFormat,
            cancellationToken: cancellationToken);

        var agent = agentResponse.Value;
        _agents[agentName] = agent;
        _logger.LogInformation("Agent created: {AgentId}", agent.Id);

        return agent;
    }

    /// <summary>
    /// Creates a new thread for agent conversation.
    /// </summary>
    public async Task<PersistentAgentThread> CreateThreadAsync(
        string threadName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating thread: {ThreadName}", threadName);

        var threadResponse = await _client.Threads.CreateThreadAsync(cancellationToken: cancellationToken);

        var thread = threadResponse.Value;
        _threads[threadName] = thread;
        _logger.LogDebug("Thread created: {ThreadId}", thread.Id);

        return thread;
    }

    /// <summary>
    /// Sends a message to a thread and runs the agent.
    /// </summary>
    public async Task<ThreadRun> SendMessageAndRunAsync(
        string threadId,
        string agentId,
        string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending message to thread {ThreadId}: {Message}", threadId, TruncateForLog(message));

        // Add message to thread
        await _client.Messages.CreateMessageAsync(
            threadId,
            MessageRole.User,
            message,
            cancellationToken: cancellationToken);

        // Create run
        var runResponse = await _client.Runs.CreateRunAsync(
            threadId,
            agentId,
            cancellationToken: cancellationToken);

        return runResponse.Value;
    }

    /// <summary>
    /// Waits for a run to complete, handling tool calls if needed.
    /// </summary>
    public async Task<ThreadRun> WaitForRunCompletionAsync(
        string threadId,
        string runId,
        Func<IEnumerable<RequiredToolCall>, Task<IEnumerable<ToolOutput>>>? toolCallHandler = null,
        CancellationToken cancellationToken = default)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_agentOptions.RunTimeoutSeconds));

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            var runResponse = await _client.Runs.GetRunAsync(threadId, runId, timeoutCts.Token);
            var run = runResponse.Value;

            _logger.LogDebug("Run status: {Status}", run.Status);

            // Check terminal states
            if (run.Status == RunStatus.Completed)
            {
                return run;
            }

            if (run.Status == RunStatus.Failed ||
                run.Status == RunStatus.Cancelled ||
                run.Status == RunStatus.Expired)
            {
                _logger.LogWarning(
                    "Run ended with status: {Status}, ErrorCode: {ErrorCode}, Error: {Error}",
                    run.Status,
                    run.LastError?.Code,
                    run.LastError?.Message);
                return run;
            }

            // Check if action required
            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitAction)
            {
                if (toolCallHandler == null)
                {
                    throw new InvalidOperationException("Run requires tool outputs but no handler provided");
                }

                var toolOutputs = await toolCallHandler(submitAction.ToolCalls);
                await _client.Runs.SubmitToolOutputsToRunAsync(
                    threadId,
                    runId,
                    toolOutputs.ToList(),
                    cancellationToken: timeoutCts.Token);
                continue;
            }

            // Still in progress
            if (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
            {
                await Task.Delay(_agentOptions.PollingIntervalMs, timeoutCts.Token);
                continue;
            }

            // Unknown status
            _logger.LogWarning("Unknown run status: {Status}", run.Status);
            await Task.Delay(_agentOptions.PollingIntervalMs, timeoutCts.Token);
        }

        throw new OperationCanceledException("Run timed out");
    }

    /// <summary>
    /// Gets the latest assistant message from a thread.
    /// </summary>
    public async Task<string?> GetLatestAssistantMessageAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var messages = _client.Messages.GetMessagesAsync(
            threadId,
            order: ListSortOrder.Descending,
            cancellationToken: cancellationToken);

        await foreach (var message in messages)
        {
            if (message.Role == MessageRole.Agent)
            {
                foreach (var content in message.ContentItems)
                {
                    if (content is MessageTextContent textContent)
                    {
                        return textContent.Text;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the PersistentAgentsClient for direct access.
    /// </summary>
    public PersistentAgentsClient GetClient() => _client;

    /// <summary>
    /// Cleans up all created agents and threads.
    /// </summary>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up agents and threads...");

        foreach (var (name, thread) in _threads)
        {
            try
            {
                await _client.Threads.DeleteThreadAsync(thread.Id, cancellationToken);
                _logger.LogDebug("Deleted thread: {ThreadName}", name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete thread: {ThreadName}", name);
            }
        }
        _threads.Clear();

        foreach (var (name, agent) in _agents)
        {
            try
            {
                await _client.Administration.DeleteAgentAsync(agent.Id, cancellationToken);
                _logger.LogDebug("Deleted agent: {AgentName}", name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete agent: {AgentName}", name);
            }
        }
        _agents.Clear();

        _logger.LogInformation("Cleanup completed");
    }

    public void Dispose()
    {
        // Note: PersistentAgentsClient doesn't implement IDisposable,
        // but we should clean up resources if needed
    }

    #region Private Methods
    private async Task<ChatClientAgent> GetChatClientAgent(AgentsTypes agentsTypes, ChatClientAgentOptions options, CancellationToken cancellationToken = default)
    {
        ChatClientAgent chatClientAgent;
        switch (agentsTypes)
        {
            case AgentsTypes.Azure_AI_Foundry:
                chatClientAgent = await _client.CreateAIAgentAsync(options.ChatOptions!.ModelId!, options, cancellationToken: cancellationToken);
                break;
            case AgentsTypes.Anthropic:
                chatClientAgent = _anthropicClient.AsAIAgent(options);
                break;
            case AgentsTypes.Ollama:
                chatClientAgent = new ChatClientAgent(_ollamaClient, instructions: options.ChatOptions!.Instructions!, name: options.Name, tools: options.ChatOptions.Tools);
                break;
            case AgentsTypes.OpenAI:
                #pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
                var responseClient = _openAIClient.GetResponsesClient(options.ChatOptions!.ModelId!);
                #pragma warning restore OPENAI001
                chatClientAgent = responseClient.AsAIAgent(instructions: options.ChatOptions!.Instructions!, name: options.Name, tools: options.ChatOptions.Tools);
                break;
            default:
                throw new InvalidCastException("Unsupported agent type");
        }
        
        return chatClientAgent;
    }

    private static string TruncateForLog(string message, int maxLength = 200)
    {
        if (message.Length <= maxLength)
        {
            return message;
        }
        return message[..maxLength] + "...";
    }

    #endregion
}
