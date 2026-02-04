using Azure.AI.Agents.Persistent;
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
public sealed class CodeExecutor
{
    private readonly AgentService _agentService;
    private readonly ToolHandler _toolHandler;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<CodeExecutor> _logger;

    private const string AgentName = "Executor";

    public CodeExecutor(
        AgentService agentService,
        ToolHandler toolHandler,
        IOptions<AgentOptions> agentOptions,
        ILogger<CodeExecutor> logger)
    {
        _agentService = agentService;
        _toolHandler = toolHandler;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes a task step using the AI agent with tools.
    /// </summary>
    public async Task<CodeExecutorOutput> ExecuteAsync(CodeExecutorInput input, CancellationToken cancellationToken = default)
    {
        var step = input.Step;
        step.Status = Model.Enums.TaskStatus.Executing;
        step.StartedAt = DateTime.UtcNow;
        step.AttemptCount++;

        try
        {
            _logger.LogInformation("Executing step {StepNumber}: {Description}",
                step.StepNumber, TruncateForLog(step.Description));

            // Create the executor agent with tools
            var agent = await _agentService.GetOrCreateAgentAsync(
                AgentName,
                AgentPrompts.ExecutorSystemPrompt,
                tools: ToolDefinitions.GetAllTools().Cast<ToolDefinition>(),
                cancellationToken: cancellationToken);

            // Create a thread for this execution
            var thread = await _agentService.CreateThreadAsync(
                $"executor-step{step.StepNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                cancellationToken);

            // Build the prompt
            var prompt = AgentPrompts.GetExecutorPrompt(
                step.Description,
                step.ExpectedOutput,
                step.RequiredPackages.Count > 0 ? step.RequiredPackages : input.Context.Plan.RequiredPackages);

            // Send message and run agent
            var run = await _agentService.SendMessageAndRunAsync(
                thread.Id,
                agent.Id,
                prompt,
                cancellationToken);

            // Wait for completion, handling tool calls
            PythonExecutionResult? lastPythonResult = null;

            var completedRun = await _agentService.WaitForRunCompletionAsync(
                thread.Id,
                run.Id,
                async toolCalls =>
                {
                    var outputs = new List<ToolOutput>();
                    foreach (var toolCall in toolCalls)
                    {
                        var output = await _toolHandler.HandleToolCallAsync(toolCall, cancellationToken);
                        outputs.Add(output);

                        // Track Python execution results
                        if (toolCall is RequiredFunctionToolCall funcCall &&
                            (funcCall.Name == "execute_python" || funcCall.Name == "execute_script_file"))
                        {
                            lastPythonResult = ParsePythonResultFromOutput(output);
                        }
                    }
                    return outputs;
                },
                cancellationToken);

            // Get the response
            var response = await _agentService.GetLatestAssistantMessageAsync(
                thread.Id,
                cancellationToken);

            // Determine the execution result
            PythonExecutionResult executionResult;
            if (lastPythonResult != null)
            {
                executionResult = lastPythonResult;
            }
            else if (completedRun.Status == RunStatus.Completed)
            {
                // No Python execution was done, but agent completed successfully
                executionResult = PythonExecutionResult.Success(
                    response ?? "Agent completed without Python execution",
                    string.Empty,
                    TimeSpan.Zero);
            }
            else
            {
                executionResult = PythonExecutionResult.Error(
                    $"Agent run ended with status: {completedRun.Status}");
            }

            // Update step with results
            step.ExecutionResult = executionResult;
            step.CompletedAt = DateTime.UtcNow;

            // Update context
            input.Context.LastExecutionResult = executionResult;
            if (!string.IsNullOrEmpty(response))
            {
                input.Context.AddMessage(AgentRole.Executor, response);
            }

            _logger.LogInformation("Step {StepNumber} execution completed with result: {Result}",
                step.StepNumber, executionResult.Result);

            return new CodeExecutorOutput
            {
                Context = input.Context,
                ExecutionResult = executionResult,
                Success = executionResult.Result == ExecutionResult.Success
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
