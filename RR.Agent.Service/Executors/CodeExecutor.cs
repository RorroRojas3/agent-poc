using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Azure.AI.Agents.Persistent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;
using RR.Agent.Service.Tools;

namespace RR.Agent.Service.Executors;

/// <summary>
/// Executor that uses an AI agent with tools to execute task steps.
/// </summary>
public sealed class CodeExecutor(
    AgentService agentService,
    ToolHandler toolHandler,
    IOptions<AgentOptions> agentOptions,
    PythonToolService pythonToolService,
    FileToolService fileToolService,
    ILogger<CodeExecutor> logger)
{
    private readonly AgentService _agentService = agentService;
    private readonly ToolHandler _toolHandler = toolHandler;
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly ILogger<CodeExecutor> _logger = logger;
    private readonly PythonToolService _pythonToolService = pythonToolService;
    private readonly FileToolService _fileToolService = fileToolService;

    private const string AgentName = "Executor";

    private const string _agentName = "Executor";

    /// <summary>
    /// Executes a task step using the AI agent with tools.
    /// </summary>
    public async Task<CodeExecutorOutput> ExecuteAsync(CodeExecutorInput input, CancellationToken cancellationToken = default)
    {
        var step = input.Step;
        step.Status = TaskStatuses.Executing;
        step.StartedAt = DateTime.UtcNow;
        step.AttemptCount++;

        try
        {
            _logger.LogInformation("Executing step {StepNumber}: {Description}",
                step.StepNumber, TruncateForLog(step.Description));

            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                RespectNullableAnnotations = true,
            };
            var schemaNode = options.GetJsonSchemaAsNode(typeof(ToolResponseDto));
            JsonElement schemaElement = JsonSerializer.Deserialize<JsonElement>(schemaNode.ToJsonString());

            List<AITool> tools = [];
            tools.AddRange(_pythonToolService.GetTools());
            tools.AddRange(_fileToolService.GetTools());
            var chatAgentClientOptions = new ChatClientAgentOptions
            {
                Id = $"{_agentName}-{Guid.NewGuid()}",
                Name = _agentName,
                ChatOptions = new ChatOptions
                {
                    Instructions = AgentPrompts.ExecutorSystemPrompt,
                    ResponseFormat = new ChatResponseFormatJson(schemaElement),
                    ModelId = _agentOptions.Executor.ModelId,
                    Tools = tools
                }
            };
            var agent = await _agentService.GetOrCreateChatClientAgentAsync(_agentOptions.Executor.Type, _agentName, chatAgentClientOptions, cancellationToken);
            
            var sessionId = Guid.NewGuid().ToString();
            var session = await _agentService.CreateSessionAsync(_agentName, sessionId, cancellationToken);

            // Build the prompt
            var prompt = AgentPrompts.GetExecutorPrompt(
                step.Description,
                step.ExpectedOutput,
                step.RequiredPackages.Count > 0 ? step.RequiredPackages : input.Context.Plan.RequiredPackages);

            var response = await _agentService.RunAsync(_agentName, sessionId, prompt, cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                return CreateErrorOutput(input, "No response from executor agent");
            }

            var json = CleanResponse(response);
            var toolResponse = JsonSerializer.Deserialize<ToolResponseDto>(json, options);
            if (toolResponse == null)
            {
                return CreateErrorOutput(input, "Failed to parse executor response from agent response");
            }
            if (toolResponse.Result != ExecutionResult.Success)
            {
                return CreateErrorOutput(input, $"Executor reported failure: {toolResponse.Errors}");
            }


            // Update step with results
            var executionResult = PythonExecutionResult.Success();
            step.ExecutionResult = executionResult;
            step.CompletedAt = DateTime.UtcNow;

            // Update context
            input.Context.LastExecutionResult = executionResult;
            input.Context.AddMessage(AgentRole.Executor, response);


            _logger.LogInformation("Step {StepNumber} execution completed with result: {Result}",
                step.StepNumber, executionResult.Result);

            return new CodeExecutorOutput
            {
                Context = input.Context,
                ExecutionResult = executionResult,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step {StepNumber}", step.StepNumber);

            var errorResult = PythonExecutionResult.Error($"Execution error: {ex.Message}");
            step.ExecutionResult = errorResult;
            step.CompletedAt = DateTime.UtcNow;
            input.Context.LastExecutionResult = errorResult;

            return new CodeExecutorOutput
            {
                Context = input.Context,
                ExecutionResult = errorResult,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static CodeExecutorOutput CreateErrorOutput(CodeExecutorInput input, string error)
    {
        var errorResult = PythonExecutionResult.Error(error);
        input.Step.ExecutionResult = errorResult;
        input.Step.CompletedAt = DateTime.UtcNow;
        input.Context.LastExecutionResult = errorResult;

        return new CodeExecutorOutput
        {
            Context = input.Context,
            ExecutionResult = errorResult,
            Success = false,
            Error = error
        };
    }

    private static string CleanResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return response;
        }

        var json = response;
        if (json.StartsWith("```json"))
        {
            json = json[7..];
        }
        else if (json.StartsWith("```"))
        {
            json = json[3..];
        }
        if (json.EndsWith("```"))
        {
            json = json[..^3];
        }

        return json.Trim();
    }

    private PythonExecutionResult? ParsePythonResultFromOutput(ToolOutput output)
    {
        try
        {
            // The output content should be JSON from the tool handler
            var json = output.Output;
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
            var exitCode = root.TryGetProperty("exit_code", out var exitProp) ? exitProp.GetInt32() : -1;
            var stdout = root.TryGetProperty("stdout", out var stdoutProp) ? stdoutProp.GetString() ?? "" : "";
            var stderr = root.TryGetProperty("stderr", out var stderrProp) ? stderrProp.GetString() ?? "" : "";
            var scriptPath = root.TryGetProperty("script_path", out var pathProp) ? pathProp.GetString() : null;
            var execTimeMs = root.TryGetProperty("execution_time_ms", out var timeProp) ? timeProp.GetInt32() : 0;

            var result = success
                ? PythonExecutionResult.Success(stdout, stderr, TimeSpan.FromMilliseconds(execTimeMs), scriptPath)
                : PythonExecutionResult.Failure(exitCode, stdout, stderr, TimeSpan.FromMilliseconds(execTimeMs), scriptPath);

            // Parse generated files if present
            if (root.TryGetProperty("generated_files", out var filesProp) &&
                filesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                result.GeneratedFiles = filesProp.EnumerateArray()
                    .Select(f => f.GetString())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Cast<string>()
                    .ToList();
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string TruncateForLog(string message, int maxLength = 200)
    {
        if (message.Length <= maxLength)
        {
            return message;
        }
        return message[..maxLength] + "...";
    }
}
