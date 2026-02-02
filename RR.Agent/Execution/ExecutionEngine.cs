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

        IMPORTANT - Before executing any script that uses input files:
        1. First, list all files in the current directory: import os; print(os.listdir('.'))
        2. Identify the correct filename for any input files mentioned
        3. If the expected filename is not found, search for files with matching extensions or partial names
        4. Update the script to use the correct filename if needed

        Execute the script and report:
        1. Whether execution was successful
        2. Any output produced
        3. Any errors encountered
        4. Any files generated (including their paths)

        If the script fails due to a file not found error:
        - List all available files
        - Identify the correct file
        - Modify the script to use the correct filename
        - Re-execute the corrected script
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
                return await ExecuteNonCodeStepAsync(step, context, inputFiles, stopwatch, cancellationToken);
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

        // Create attachments for input files only (not the script - we'll include code inline)
        var tools = _toolProvider.GetToolDefinitions().ToList();
        var attachments = new List<MessageAttachment>();

        // Add input files as attachments
        if (inputFiles is { Count: > 0 })
        {
            foreach (var inputFile in inputFiles.Where(f => f.IsUploaded))
            {
                attachments.Add(new MessageAttachment(inputFile.AgentFileId!, tools));
            }
        }

        // Build message with script content included directly (not as attachment)
        var messageContent = BuildExecutionMessageWithCode(script, inputFiles, retryContext);

        await _client.Messages.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: messageContent,
            attachments: attachments.Count > 0 ? attachments : null,
            cancellationToken: cancellationToken);

        // Run the execution agent with explicit instruction to execute the code
        var runResponse = await _client.Runs.CreateRunAsync(
            thread.Id,
            _executionAgentId!,
            additionalInstructions: "CRITICAL: You MUST execute the Python code provided in the message using the code_interpreter tool. Do not just describe what the code does - actually run it and report the results.");
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
        IReadOnlyList<InputFile>? inputFiles,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        return step.Type switch
        {
            StepType.Analysis => await ExecuteAnalysisStepAsync(step, context, stopwatch, cancellationToken),
            StepType.FileRead => await ExecuteFileReadStepAsync(step, inputFiles, stopwatch, cancellationToken),
            StepType.FileWrite => await ExecuteFileWriteStepAsync(step, context, inputFiles, stopwatch, cancellationToken),
            _ => CreateNotImplementedResult(step, stopwatch)
        };
    }

    private async Task<ExecutionResult> ExecuteAnalysisStepAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
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

    private async Task<ExecutionResult> ExecuteFileReadStepAsync(
        PlanStep step,
        IReadOnlyList<InputFile>? inputFiles,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing FileRead step: {Description}", step.Description);

        var filename = ExtractFilenameFromStep(step);

        // Generate Python script to read the file
        var scriptContent = BuildFileReadScript(filename);

        var script = new ScriptInfo(
            Language: "python",
            FileName: $"step_{step.Order}_fileread.py",
            Content: scriptContent);

        script = await _fileManager.SaveScriptLocallyAsync(script, cancellationToken);
        var fileId = await _fileManager.UploadFileAsync(script.LocalPath!, cancellationToken);
        script = script.WithAgentFileId(fileId);

        return await ExecuteScriptAsync(step, script, inputFiles, null, stopwatch, cancellationToken);
    }

    private async Task<ExecutionResult> ExecuteFileWriteStepAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing FileWrite step: {Description}", step.Description);

        var filename = ExtractFilenameFromStep(step);
        var content = ExtractContentForFileWrite(step, context);

        // Generate Python script to write the file
        var scriptContent = BuildFileWriteScript(filename, content);

        var script = new ScriptInfo(
            Language: "python",
            FileName: $"step_{step.Order}_filewrite.py",
            Content: scriptContent);

        script = await _fileManager.SaveScriptLocallyAsync(script, cancellationToken);
        var fileId = await _fileManager.UploadFileAsync(script.LocalPath!, cancellationToken);
        script = script.WithAgentFileId(fileId);

        return await ExecuteScriptAsync(step, script, inputFiles, null, stopwatch, cancellationToken);
    }

    private static string BuildFileReadScript(string filename)
    {
        return $@"import os
import sys

filename = '{filename}'

# List all available files for debugging
print('Available files in current directory:')
for f in os.listdir('.'):
    size = os.path.getsize(f) if os.path.isfile(f) else 0
    print(f'  - {{f}} ({{size}} bytes)')

# Try to find the file
filepath = None
if os.path.exists(filename):
    filepath = filename
else:
    # Search in current directory for partial match
    for f in os.listdir('.'):
        if f == filename or filename in f or f.endswith(filename):
            filepath = f
            break

if filepath is None:
    print(f'Error: File not found: {{filename}}')
    sys.exit(1)

# Check if file is binary (PDF, images, etc.)
binary_extensions = {{'.pdf', '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.zip', '.tar', '.gz', '.exe', '.dll', '.bin'}}
_, ext = os.path.splitext(filepath.lower())

if ext in binary_extensions:
    # Handle binary file
    size = os.path.getsize(filepath)
    print(f'File: {{filepath}}')
    print(f'Type: Binary file ({{ext}})')
    print(f'Size: {{size}} bytes')

    # For PDF, try to get page count if pypdf is available
    if ext == '.pdf':
        try:
            import subprocess
            subprocess.check_call([sys.executable, '-m', 'pip', 'install', '-q', 'pypdf'])
            from pypdf import PdfReader
            reader = PdfReader(filepath)
            print(f'Pages: {{len(reader.pages)}}')
        except Exception as e:
            print(f'(Could not read PDF metadata: {{e}})')

    print('--- Binary file loaded successfully ---')
else:
    # Read and output text file content
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        print(f'File: {{filepath}}')
        print(f'Size: {{len(content)}} characters')
        print('--- Content ---')
        print(content)
    except UnicodeDecodeError:
        # Fall back to binary handling
        size = os.path.getsize(filepath)
        print(f'File: {{filepath}}')
        print(f'Type: Binary file (encoding could not be determined)')
        print(f'Size: {{size}} bytes')
        print('--- Binary file loaded successfully ---')
";
    }

    private static string BuildFileWriteScript(string filename, string content)
    {
        // Escape content for Python string (use base64 to handle any content safely)
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var base64Content = Convert.ToBase64String(contentBytes);

        return $@"import os
import sys
import base64

filename = '{filename}'
content_b64 = '{base64Content}'

# Decode the content
content = base64.b64decode(content_b64).decode('utf-8')

# Write the file
with open(filename, 'w', encoding='utf-8') as f:
    f.write(content)

# Verify the write
if os.path.exists(filename):
    size = os.path.getsize(filename)
    print(f'Successfully wrote file: {{filename}}')
    print(f'File size: {{size}} bytes')

    # Show preview
    with open(filename, 'r', encoding='utf-8') as f:
        preview = f.read(500)
        if len(preview) == 500:
            preview += '... (truncated)'
    print('--- Preview ---')
    print(preview)
else:
    print(f'Error: Failed to write file: {{filename}}')
    sys.exit(1)
";
    }

    private static string ExtractFilenameFromStep(PlanStep step)
    {
        // Try to extract filename from description using common patterns
        var patterns = new[]
        {
            @"file[:\s]+['""]?([^\s'""]+)['""]?",
            @"filename[:\s]+['""]?([^\s'""]+)['""]?",
            @"read\s+['""]?([^\s'""]+)['""]?",
            @"write\s+(?:to\s+)?['""]?([^\s'""]+)['""]?",
            @"'([^']+\.[a-zA-Z0-9]+)'",
            @"""([^""]+\.[a-zA-Z0-9]+)"""
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                step.Description,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success && match.Groups.Count > 1)
            {
                var filename = match.Groups[1].Value;
                if (filename.Contains('.') || !filename.Contains(' '))
                {
                    return filename;
                }
            }
        }

        // Try expected output
        if (!string.IsNullOrWhiteSpace(step.ExpectedOutput))
        {
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    step.ExpectedOutput,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return $"output_step_{step.Order}.txt";
    }

    private static string ExtractContentForFileWrite(PlanStep step, IReadOnlyDictionary<int, ExecutionResult> context)
    {
        // Check for content in step description
        var contentMatch = System.Text.RegularExpressions.Regex.Match(
            step.Description,
            @"content[:\s]+[""'](.+?)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        if (contentMatch.Success)
        {
            return contentMatch.Groups[1].Value;
        }

        // Use output from dependent steps
        if (step.Dependencies.Count > 0)
        {
            var lastDep = step.Dependencies.Max();
            if (context.TryGetValue(lastDep, out var depResult) && depResult.IsSuccess && !string.IsNullOrWhiteSpace(depResult.Output))
            {
                return depResult.Output;
            }
        }

        // Use expected output
        if (!string.IsNullOrWhiteSpace(step.ExpectedOutput))
        {
            return step.ExpectedOutput;
        }

        return $"Generated by step {step.Order}: {step.Description}";
    }

    private ExecutionResult CreateNotImplementedResult(PlanStep step, Stopwatch stopwatch)
    {
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

    private static string BuildExecutionMessageWithCode(
        ScriptInfo script,
        IReadOnlyList<InputFile>? inputFiles,
        string? retryContext)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("EXECUTE THE FOLLOWING CODE IMMEDIATELY using the code_interpreter tool.");
        sb.AppendLine("Do NOT describe or explain the code - EXECUTE IT NOW and report the actual output.");
        sb.AppendLine();

        if (inputFiles is { Count: > 0 })
        {
            sb.AppendLine("INPUT FILES AVAILABLE (attached to this message):");
            foreach (var file in inputFiles.Where(f => f.IsUploaded))
            {
                sb.AppendLine($"- {file.FileName}");
            }
            sb.AppendLine();
            sb.AppendLine("NOTE: Files may appear with different names in the sandbox. The script below handles this.");
            sb.AppendLine();
        }

        sb.AppendLine($"```{script.Language}");
        sb.AppendLine(script.Content);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Copy this EXACT code into the code_interpreter");
        sb.AppendLine("2. Execute it immediately");
        sb.AppendLine("3. Report the actual output from execution");
        sb.AppendLine("4. If any files are generated, confirm their creation");

        if (!string.IsNullOrWhiteSpace(retryContext))
        {
            sb.AppendLine();
            sb.AppendLine($"RETRY CONTEXT: {retryContext}");
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
