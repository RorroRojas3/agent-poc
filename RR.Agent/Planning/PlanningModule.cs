namespace RR.Agent.Planning;

using System.Text;
using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Exceptions;
using RR.Agent.Execution.Models;
using RR.Agent.Infrastructure;
using RR.Agent.Planning.Models;
using RR.Agent.Tools;

/// <summary>
/// Parses user requests and decomposes them into actionable execution plans using AI.
/// </summary>
public sealed class PlanningModule : IPlanningModule
{
    private const string PlanningPrompt = """
        You are a planning agent that decomposes user requests into executable steps.

        Analyze the user's request and create a structured execution plan with clear, actionable steps.
        Each step should be concrete and executable via code (Python preferred, C# if specifically requested).

        Respond with a JSON object in the following format:
        {
            "summary": "Brief description of what the plan accomplishes",
            "complexity": 1-10 (estimated complexity),
            "steps": [
                {
                    "order": 1,
                    "description": "Clear description of what this step does",
                    "type": "CodeExecution|FileRead|FileWrite|Analysis",
                    "expectedOutput": "What output or result is expected",
                    "dependencies": [],
                    "scriptHint": "Optional hint for the script (language, approach, required packages)"
                }
            ]
        }

        Step Type Guidelines:
        - CodeExecution: Use for ALL file processing that requires libraries (PDF, Excel, images, data analysis).
          This includes: PDF operations (extract pages, merge, split), Excel/CSV processing, image manipulation,
          data analysis, any file format that requires specialized packages.
        - FileRead: ONLY for reading plain text files (.txt, .json, .xml, .csv for simple viewing).
        - FileWrite: ONLY for writing plain text output to a file (NOT binary files like PDF, images, etc.).
        - Analysis: For reasoning or decision-making steps that don't require code.

        CRITICAL for binary/complex files (PDF, Excel, images, archives):
        - ALWAYS use CodeExecution type for BOTH reading AND writing - never use FileRead/FileWrite
        - A single CodeExecution step should handle the complete operation (read input, process, write output)
        - Do NOT split binary file operations into separate read/write steps
        - Include required packages in scriptHint (e.g., "Use pypdf for PDF operations", "Use openpyxl for Excel")

        Example for PDF extraction:
        - CORRECT: One CodeExecution step: "Extract first 5 pages from input PDF and save as output.pdf"
        - WRONG: Separate steps for "Extract pages" and "Save to file" - this will fail for binary files

        General Guidelines:
        - Break complex tasks into small, testable steps
        - Each step should have a clear, verifiable output
        - Order steps by dependencies (later steps can depend on earlier ones)
        - Estimate complexity based on number of steps and difficulty
        - Keep the plan focused and minimal - don't add unnecessary steps
        """;

    private readonly PersistentAgentsClient _client;
    private readonly AzureAIFoundryOptions _aiOptions;
    private readonly IToolProvider _toolProvider;
    private readonly IRunPoller _runPoller;
    private readonly IMessageProcessor _messageProcessor;
    private readonly ILogger<PlanningModule> _logger;

    private string? _planningAgentId;

    public PlanningModule(
        PersistentAgentsClient client,
        IOptions<AzureAIFoundryOptions> aiOptions,
        IToolProvider toolProvider,
        IRunPoller runPoller,
        IMessageProcessor messageProcessor,
        ILogger<PlanningModule> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(aiOptions);
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(runPoller);
        ArgumentNullException.ThrowIfNull(messageProcessor);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _aiOptions = aiOptions.Value;
        _toolProvider = toolProvider;
        _runPoller = runPoller;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    public async Task<ExecutionPlan> CreatePlanAsync(
        string userRequest,
        IReadOnlyList<InputFile>? inputFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);

        _logger.LogInformation("Creating execution plan for request: {Request}", userRequest);

        try
        {
            // Ensure planning agent exists
            await EnsurePlanningAgentExistsAsync(cancellationToken);

            // Create a new thread for this planning session
            var threadResponse = await _client.Threads.CreateThreadAsync();
            var thread = threadResponse.Value;

            // Build the planning request with file context
            var messageContent = BuildPlanningMessage(userRequest, inputFiles);

            // Send the user request
            await _client.Messages.CreateMessageAsync(
                threadId: thread.Id,
                role: MessageRole.User,
                content: messageContent);

            // Run the planning agent
            var runResponse = await _client.Runs.CreateRunAsync(
                thread.Id,
                _planningAgentId!);
            var run = runResponse.Value;

            // Wait for completion
            var completedRun = await _runPoller.WaitForCompletionAsync(
                thread.Id,
                run.Id,
                cancellationToken);

            if (completedRun.Status != RunStatus.Completed)
            {
                throw new PlanningException(
                    userRequest,
                    $"Planning failed with status: {completedRun.Status}. Error: {completedRun.LastError?.Message}");
            }

            // Extract the plan from the response
            var response = await _messageProcessor.GetLatestAssistantMessageAsync(
                thread.Id,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                throw new PlanningException(userRequest, "No response received from planning agent");
            }

            var plan = ParsePlanResponse(userRequest, response);

            _logger.LogInformation(
                "Created plan with {StepCount} steps, complexity: {Complexity}",
                plan.Steps.Count,
                plan.EstimatedComplexity);

            return plan;
        }
        catch (PlanningException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plan for request: {Request}", userRequest);
            throw new PlanningException(userRequest, "Failed to create execution plan", ex);
        }
    }

    public bool ValidatePlan(ExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Steps.Count == 0)
        {
            _logger.LogWarning("Plan has no steps");
            return false;
        }

        // Check for valid step ordering
        var stepOrders = new HashSet<int>();
        foreach (var step in plan.Steps)
        {
            if (!stepOrders.Add(step.Order))
            {
                _logger.LogWarning("Duplicate step order: {Order}", step.Order);
                return false;
            }

            // Check dependencies exist
            foreach (var dep in step.Dependencies)
            {
                if (dep >= step.Order)
                {
                    _logger.LogWarning(
                        "Step {Order} has invalid dependency on step {Dep}",
                        step.Order,
                        dep);
                    return false;
                }
            }
        }

        return true;
    }

    public async Task<ExecutionPlan> RefinePlanAsync(
        ExecutionPlan originalPlan,
        string feedback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(feedback);

        _logger.LogInformation("Refining plan based on feedback");

        var refinementRequest = $"""
            Original request: {originalPlan.OriginalRequest}

            Original plan:
            {JsonSerializer.Serialize(originalPlan, new JsonSerializerOptions { WriteIndented = true })}

            Feedback from execution:
            {feedback}

            Please create a refined plan that addresses the feedback while accomplishing the original goal.
            """;

        return await CreatePlanAsync(refinementRequest, null, cancellationToken);
    }

    private static string BuildPlanningMessage(string userRequest, IReadOnlyList<InputFile>? inputFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Create an execution plan for the following request:");
        sb.AppendLine();
        sb.AppendLine(userRequest);

        if (inputFiles is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("The following input files are available for use in the plan:");
            foreach (var file in inputFiles)
            {
                sb.AppendLine($"- {file.FileName} (original path: {file.OriginalPath})");
            }
            sb.AppendLine();
            sb.AppendLine("Scripts can read, modify, or process these files. Reference them by filename in your step descriptions.");
        }

        return sb.ToString();
    }

    private async Task EnsurePlanningAgentExistsAsync(CancellationToken cancellationToken)
    {
        if (_planningAgentId is not null)
        {
            return;
        }

        _logger.LogDebug("Creating planning agent");

        var tools = _toolProvider.GetToolDefinitions().ToList();
        var agentResponse = await _client.Administration.CreateAgentAsync(
            model: _aiOptions.DefaultModel,
            name: "PlanningAgent",
            instructions: PlanningPrompt,
            tools: tools,
            toolResources: _toolProvider.GetToolResources());

        _planningAgentId = agentResponse.Value.Id;
        _logger.LogInformation("Created planning agent with ID: {AgentId}", _planningAgentId);
    }

    private ExecutionPlan ParsePlanResponse(string originalRequest, string response)
    {
        try
        {
            // Extract JSON from the response (it might be wrapped in markdown code blocks)
            var json = ExtractJson(response);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "Execution plan";
            var complexity = root.TryGetProperty("complexity", out var complexityProp)
                ? complexityProp.GetInt32()
                : 5;

            var steps = new List<PlanStep>();
            foreach (var stepElement in root.GetProperty("steps").EnumerateArray())
            {
                var order = stepElement.GetProperty("order").GetInt32();
                var description = stepElement.GetProperty("description").GetString() ?? "";
                var typeStr = stepElement.GetProperty("type").GetString() ?? "CodeExecution";
                var expectedOutput = stepElement.GetProperty("expectedOutput").GetString() ?? "";

                var dependencies = new List<int>();
                if (stepElement.TryGetProperty("dependencies", out var depsElement))
                {
                    foreach (var dep in depsElement.EnumerateArray())
                    {
                        dependencies.Add(dep.GetInt32());
                    }
                }

                var scriptHint = stepElement.TryGetProperty("scriptHint", out var hintProp)
                    ? hintProp.GetString()
                    : null;

                var stepType = Enum.TryParse<StepType>(typeStr, ignoreCase: true, out var parsed)
                    ? parsed
                    : StepType.CodeExecution;

                steps.Add(new PlanStep(order, description, stepType, expectedOutput, dependencies, scriptHint));
            }

            // Sort steps by order
            steps.Sort((a, b) => a.Order.CompareTo(b.Order));

            return new ExecutionPlan(originalRequest, summary, steps, complexity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse plan response: {Response}", response);
            throw new PlanningException(originalRequest, "Failed to parse planning response", ex);
        }
    }

    private static string ExtractJson(string response)
    {
        // Try to find JSON in markdown code blocks
        var jsonStart = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = response.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = response.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return response[jsonStart..jsonEnd].Trim();
            }
        }

        // Try to find JSON block without language specifier
        jsonStart = response.IndexOf("```", StringComparison.Ordinal);
        if (jsonStart >= 0)
        {
            jsonStart = response.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = response.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return response[jsonStart..jsonEnd].Trim();
            }
        }

        // Try to find raw JSON object
        jsonStart = response.IndexOf('{');
        if (jsonStart >= 0)
        {
            var jsonEnd = response.LastIndexOf('}');
            if (jsonEnd > jsonStart)
            {
                return response[jsonStart..(jsonEnd + 1)];
            }
        }

        // Return as-is and let JSON parser fail with a clear error
        return response;
    }
}
