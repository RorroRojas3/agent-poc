namespace RR.Agent.Execution;

using System.Diagnostics;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Execution.Models;
using RR.Agent.Infrastructure;
using RR.Agent.Planning.Models;
using RR.Agent.Tools;

/// <summary>
/// Executes plan steps using Azure AI Agent's code interpreter.
/// </summary>
public sealed class ExecutionEngine : IExecutionEngine
{
    private const string ExecutionPrompt = """
        You are a code execution agent. Your job is to execute the provided script using the code interpreter.

        Execute the script and report:
        1. Whether execution was successful
        2. Any output produced
        3. Any errors encountered
        4. Any files generated

        If the script fails, analyze the error and suggest what might have gone wrong.
        """;

    private readonly PersistentAgentsClient _client;
    private readonly AzureAIFoundryOptions _aiOptions;
    private readonly IScriptGenerator _scriptGenerator;
    private readonly IFileManager _fileManager;
    private readonly IToolProvider _toolProvider;
    private readonly IRunPoller _runPoller;
    private readonly IMessageProcessor _messageProcessor;
    private readonly ILogger<ExecutionEngine> _logger;

    private string? _executionAgentId;

    public ExecutionEngine(
        PersistentAgentsClient client,
        IOptions<AzureAIFoundryOptions> aiOptions,
        IScriptGenerator scriptGenerator,
        IFileManager fileManager,
        IToolProvider toolProvider,
        IRunPoller runPoller,
        IMessageProcessor messageProcessor,
        ILogger<ExecutionEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(aiOptions);
        ArgumentNullException.ThrowIfNull(scriptGenerator);
        ArgumentNullException.ThrowIfNull(fileManager);
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(runPoller);
        ArgumentNullException.ThrowIfNull(messageProcessor);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _aiOptions = aiOptions.Value;
        _scriptGenerator = scriptGenerator;
        _fileManager = fileManager;
        _toolProvider = toolProvider;
        _runPoller = runPoller;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteStepAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles = null,
        string? retryContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Executing step {Order}: {Description}",
            step.Order,
            step.Description);

        try
        {
            // For non-code steps, we might handle them differently
            if (step.Type != StepType.CodeExecution)
            {
                return await ExecuteNonCodeStepAsync(step, context, stopwatch, cancellationToken);
            }

            // Generate the script with input file context
            var script = await _scriptGenerator.GenerateScriptAsync(step, context, inputFiles, cancellationToken);

            // Save script locally for debugging
            script = await _fileManager.SaveScriptLocallyAsync(script, cancellationToken);

            // Upload script to agent storage
            var fileId = await _fileManager.UploadFileAsync(script.LocalPath!, cancellationToken);
            script = script.WithAgentFileId(fileId);

            // Execute the script via code interpreter
            var result = await ExecuteScriptAsync(step, script, inputFiles, retryContext, stopwatch, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult(
                StepOrder: step.Order,
                Status: ExecutionStatus.Cancelled,
                Output: null,
                ErrorMessage: "Operation was cancelled",
                GeneratedFiles: [],
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Step {Order} timed out", step.Order);

            return new ExecutionResult(
                StepOrder: step.Order,
                Status: ExecutionStatus.TimedOut,
                Output: null,
                ErrorMessage: ex.Message,
                GeneratedFiles: [],
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {Order} failed with error", step.Order);

            return new ExecutionResult(
                StepOrder: step.Order,
                Status: ExecutionStatus.Failed,
                Output: null,
                ErrorMessage: ex.Message,
                GeneratedFiles: [],
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<byte[]> GetFileContentAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        return await _fileManager.DownloadFileAsync(fileId, cancellationToken);
    }

    private async Task<ExecutionResult> ExecuteScriptAsync(
        PlanStep step,
        ScriptInfo script,
        IReadOnlyList<InputFile>? inputFiles,
        string? retryContext,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        await EnsureExecutionAgentExistsAsync(cancellationToken);

        // Create thread
        var threadResponse = await _client.Threads.CreateThreadAsync();
        var thread = threadResponse.Value;

        // Create message with script attachment and input file attachments
        var tools = _toolProvider.GetToolDefinitions().ToList();
        var attachments = new List<MessageAttachment>
        {
            new(script.AgentFileId!, tools)
        };

        // Add input files as attachments
        if (inputFiles is { Count: > 0 })
        {
            foreach (var inputFile in inputFiles.Where(f => f.IsUploaded))
            {
                attachments.Add(new MessageAttachment(inputFile.AgentFileId!, tools));
            }
        }

        var messageContent = BuildExecutionMessage(script, inputFiles, retryContext);

        await _client.Messages.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: messageContent,
            attachments: attachments);

        // Run the execution agent
        var runResponse = await _client.Runs.CreateRunAsync(
            thread.Id,
            _executionAgentId!,
            additionalInstructions: "Execute the provided script using the code interpreter tool.");
        var run = runResponse.Value;

        // Wait for completion
        var completedRun = await _runPoller.WaitForCompletionAsync(
            thread.Id,
            run.Id,
            cancellationToken);

        // Get results
        var output = await _messageProcessor.GetLatestAssistantMessageAsync(thread.Id, cancellationToken);
        var files = await _messageProcessor.GetFileReferencesAsync(thread.Id, cancellationToken);

        ExecutionStatus status;
        if (completedRun.Status == RunStatus.Completed)
        {
            status = DetermineSuccessFromOutput(output);
        }
        else if (completedRun.Status == RunStatus.Failed)
        {
            status = ExecutionStatus.Failed;
        }
        else if (completedRun.Status == RunStatus.Cancelled)
        {
            status = ExecutionStatus.Cancelled;
        }
        else if (completedRun.Status == RunStatus.Expired)
        {
            status = ExecutionStatus.TimedOut;
        }
        else
        {
            status = ExecutionStatus.Failed;
        }

        var errorMessage = status != ExecutionStatus.Success
            ? completedRun.LastError?.Message ?? ExtractErrorFromOutput(output)
            : null;

        _logger.LogInformation(
            "Step {Order} completed with status {Status} in {ElapsedMs}ms",
            step.Order,
            status,
            stopwatch.ElapsedMilliseconds);

        return new ExecutionResult(
            StepOrder: step.Order,
            Status: status,
            Output: output,
            ErrorMessage: errorMessage,
            GeneratedFiles: files,
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
            Script: script);
    }

    private async Task<ExecutionResult> ExecuteNonCodeStepAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        // For Analysis steps, we can use the AI to reason about previous results
        if (step.Type == StepType.Analysis)
        {
            await EnsureExecutionAgentExistsAsync(cancellationToken);

            var threadResponse = await _client.Threads.CreateThreadAsync();
            var thread = threadResponse.Value;

            var analysisPrompt = $"""
                Analyze the following based on this task: {step.Description}

                Previous step results:
                {FormatContext(context)}

                Expected output: {step.ExpectedOutput}
                """;

            await _client.Messages.CreateMessageAsync(
                threadId: thread.Id,
                role: MessageRole.User,
                content: analysisPrompt);

            var runResponse = await _client.Runs.CreateRunAsync(
                thread.Id,
                _executionAgentId!);
            var run = runResponse.Value;

            var completedRun = await _runPoller.WaitForCompletionAsync(
                thread.Id,
                run.Id,
                cancellationToken);

            var output = await _messageProcessor.GetLatestAssistantMessageAsync(thread.Id, cancellationToken);

            return new ExecutionResult(
                StepOrder: step.Order,
                Status: completedRun.Status == RunStatus.Completed
                    ? ExecutionStatus.Success
                    : ExecutionStatus.Failed,
                Output: output,
                ErrorMessage: completedRun.LastError?.Message,
                GeneratedFiles: [],
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }

        // For other non-code steps, mark as needing implementation
        _logger.LogWarning("Step type {Type} not fully implemented", step.Type);

        return new ExecutionResult(
            StepOrder: step.Order,
            Status: ExecutionStatus.Success,
            Output: $"Step type {step.Type} acknowledged",
            ErrorMessage: null,
            GeneratedFiles: [],
            ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
    }

    private static string FormatContext(IReadOnlyDictionary<int, ExecutionResult> context)
    {
        if (context.Count == 0)
        {
            return "No previous steps.";
        }

        return string.Join("\n", context.Select(kvp =>
            $"Step {kvp.Key}: {(kvp.Value.IsSuccess ? "Success" : "Failed")} - {kvp.Value.Output ?? "No output"}"));
    }

    private static string BuildExecutionMessage(
        ScriptInfo script,
        IReadOnlyList<InputFile>? inputFiles,
        string? retryContext)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Execute the attached {script.Language} script and report the results.");

        if (inputFiles is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("The following input files are also attached and available for the script to use:");
            foreach (var file in inputFiles.Where(f => f.IsUploaded))
            {
                sb.AppendLine($"- {file.FileName}");
            }
        }

        if (!string.IsNullOrWhiteSpace(retryContext))
        {
            sb.AppendLine();
            sb.AppendLine($"Previous attempt context: {retryContext}");
        }

        return sb.ToString();
    }

    private static ExecutionStatus DetermineSuccessFromOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return ExecutionStatus.Failed;
        }

        // Check for common error indicators
        var lowerOutput = output.ToLowerInvariant();
        if (lowerOutput.Contains("error:") ||
            lowerOutput.Contains("exception:") ||
            lowerOutput.Contains("traceback") ||
            lowerOutput.Contains("failed to execute"))
        {
            return ExecutionStatus.Failed;
        }

        return ExecutionStatus.Success;
    }

    private static string? ExtractErrorFromOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return "No output received";
        }

        // Look for error patterns
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            var lowerLine = line.ToLowerInvariant();
            if (lowerLine.Contains("error:") ||
                lowerLine.Contains("exception:"))
            {
                return line.Trim();
            }
        }

        return null;
    }

    private async Task EnsureExecutionAgentExistsAsync(CancellationToken cancellationToken)
    {
        if (_executionAgentId is not null)
        {
            return;
        }

        _logger.LogDebug("Creating execution agent");

        var tools = _toolProvider.GetToolDefinitions().ToList();
        var agentResponse = await _client.Administration.CreateAgentAsync(
            model: _aiOptions.DefaultModel,
            name: "ExecutionAgent",
            instructions: ExecutionPrompt,
            tools: tools,
            toolResources: _toolProvider.GetToolResources());

        _executionAgentId = agentResponse.Value.Id;
        _logger.LogInformation("Created execution agent with ID: {AgentId}", _executionAgentId);
    }
}
