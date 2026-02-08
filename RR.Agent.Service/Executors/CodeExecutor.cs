using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
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
    IOptions<AgentOptions> agentOptions,
    PythonToolService pythonToolService,
    FileToolService fileToolService,
    ILogger<CodeExecutor> logger)
{
    private readonly AgentService _agentService = agentService;
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly ILogger<CodeExecutor> _logger = logger;
    private readonly PythonToolService _pythonToolService = pythonToolService;
    private readonly FileToolService _fileToolService = fileToolService;

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

            var schemaOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            schemaOptions.Converters.Add(new JsonStringEnumConverter());
            JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(ToolResponseDto), serializerOptions: schemaOptions);
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
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                    schema: schema),
                    ModelId = _agentOptions.Executor.ModelId,
                    AllowMultipleToolCalls = true,
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

            var response = await _agentService.RunAsAgentResponseAsync(_agentName, sessionId, prompt, cancellationToken);
            if (response == null)
            {
                return CreateErrorOutput(input, "No response from executor agent");
            }

            var toolResponse = response.Deserialize<ToolResponseDto>(schemaOptions);
            if (toolResponse == null)
            {
                return CreateErrorOutput(input, "Failed to parse executor response as JSON");
            }
            if (toolResponse.Result != ExecutionResult.Success)
            {
                return CreateErrorOutput(input, $"Executor reported failure: {string.Join("; ", toolResponse.Errors)}");
            }

            // Update step with results
            var executionResult = PythonExecutionResult.Success();
            step.ExecutionResult = executionResult;
            step.CompletedAt = DateTime.UtcNow;

            // Update context
            input.Context.LastExecutionResult = executionResult;
            input.Context.AddMessage(AgentRole.Executor, toolResponse.Output);


            _logger.LogInformation("Step {StepNumber} execution completed with result: {Result}",
                step.StepNumber, executionResult.Result);

            return new CodeExecutorOutput
            {
                Context = input.Context,
                ExecutionResult = executionResult,
                ToolResponse = toolResponse,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step {StepNumber}", step.StepNumber);

            var errorResult = PythonExecutionResult.Error($"Execution error: {ex.Message}");
            var toolResponse = new ToolResponseDto
            {
                Result = ExecutionResult.Failure,
                Errors = [ex.Message],
                Output = string.Empty
            };
            step.ExecutionResult = errorResult;
            step.CompletedAt = DateTime.UtcNow;
            input.Context.LastExecutionResult = errorResult;

            return new CodeExecutorOutput
            {
                Context = input.Context,
                ExecutionResult = errorResult,
                Success = false,
                ToolResponse = toolResponse,
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
        var toolResponse = new ToolResponseDto
        {
            Result = ExecutionResult.Failure,
            Errors = [error],
            Output = string.Empty
        };

        return new CodeExecutorOutput
        {
            Context = input.Context,
            ExecutionResult = errorResult,
            ToolResponse = toolResponse,
            Success = false,
            Error = error
        };
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
