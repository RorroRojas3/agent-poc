using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;
using RR.Agent.Service.Python;

namespace RR.Agent.Service.Executors;

/// <summary>
/// Executor that uses an AI agent to create task plans.
/// </summary>
public sealed class PlannerExecutor
{
    private readonly AgentService _agentService;
    private readonly IPythonEnvironmentService _envService;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<PlannerExecutor> _logger;

    private const string AgentName = "Planner";

    public PlannerExecutor(
        AgentService agentService,
        IPythonEnvironmentService envService,
        IOptions<AgentOptions> agentOptions,
        ILogger<PlannerExecutor> logger)
    {
        _agentService = agentService;
        _envService = envService;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a task plan for the given input.
    /// </summary>
    public async Task<PlannerOutput> ExecuteAsync(PlannerInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Planning task: {Task}", TruncateForLog(input.Task));

            // Create or get the planner agent (optionally with structured output)
            var responseFormat = _agentOptions.UseStructuredOutput
                ? ResponseSchemas.PlannerResponseSchema
                : null;

            var agent = await _agentService.GetOrCreateAgentAsync(
                AgentName,
                AgentPrompts.PlannerSystemPrompt,
                responseFormat: responseFormat,
                cancellationToken: cancellationToken);

            // Create a thread for this planning session
            var thread = await _agentService.CreateThreadAsync(
                $"planner-{DateTime.UtcNow:yyyyMMddHHmmss}",
                cancellationToken);

            // Build the prompt
            string prompt;
            if (input.IsRetry && input.PreviousEvaluation != null)
            {
                prompt = AgentPrompts.GetRetryPlannerPrompt(
                    input.Task,
                    input.PreviousEvaluation.Reasoning,
                    input.PreviousEvaluation.RevisedApproach);
            }
            else
            {
                prompt = $"Create a plan for the following task:\n\n{input.Task}\n\nOutput as JSON only.";
            }

            // Send message and run agent
            var run = await _agentService.SendMessageAndRunAsync(
                thread.Id,
                agent.Id,
                prompt,
                cancellationToken);

            // Wait for completion
            var completedRun = await _agentService.WaitForRunCompletionAsync(
                thread.Id,
                run.Id,
                cancellationToken: cancellationToken);

            if (completedRun.Status != Azure.AI.Agents.Persistent.RunStatus.Completed)
            {
                return CreateErrorOutput(input, $"Agent run failed with status: {completedRun.Status}");
            }

            // Get the response
            var response = await _agentService.GetLatestAssistantMessageAsync(
                thread.Id,
                cancellationToken);

            if (string.IsNullOrEmpty(response))
            {
                return CreateErrorOutput(input, "No response from planner agent");
            }

            // Parse the JSON response
            var plan = ParsePlanResponse(response, input.Task);
            if (plan == null)
            {
                return CreateErrorOutput(input, "Failed to parse plan from agent response");
            }

            // Validate the plan
            if (plan.Steps.Count == 0)
            {
                return CreateErrorOutput(input, "Plan contains no steps");
            }

            if (plan.Steps.Count > _agentOptions.MaxStepsPerPlan)
            {
                _logger.LogWarning("Plan has {Count} steps, truncating to {Max}",
                    plan.Steps.Count, _agentOptions.MaxStepsPerPlan);
                plan.Steps = plan.Steps.Take(_agentOptions.MaxStepsPerPlan).ToList();
            }

            // Create or update execution context
            var context = input.Context ?? new WorkflowContext
            {
                Plan = plan,
                WorkspacePath = _envService.GetWorkspacePath(),
                VenvPath = _envService.GetVenvPath()
            };

            context.Plan = plan;
            context.CurrentStep = plan.CurrentStep;
            context.AddMessage(AgentRole.Planner, response);

            plan.Status = Model.Enums.TaskStatus.Planning;

            _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);

            return new PlannerOutput
            {
                Plan = plan,
                Context = context,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during planning");
            return CreateErrorOutput(input, $"Planning error: {ex.Message}");
        }
    }

    private TaskPlan? ParsePlanResponse(string response, string originalTask)
    {
        try
        {
            // Clean up the response (remove markdown code blocks if present)
            var json = response.Trim();
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
            json = json.Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var plan = new TaskPlan
            {
                OriginalTask = originalTask,
                TaskAnalysis = root.TryGetProperty("taskAnalysis", out var analysis)
                    ? analysis.GetString()
                    : null
            };

            // Parse steps
            if (root.TryGetProperty("steps", out var stepsElement) && stepsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    var step = new TaskStep
                    {
                        StepNumber = stepElement.TryGetProperty("stepNumber", out var num)
                            ? num.GetInt32()
                            : plan.Steps.Count + 1,
                        Description = stepElement.TryGetProperty("description", out var desc)
                            ? desc.GetString() ?? "No description"
                            : "No description",
                        ExpectedOutput = stepElement.TryGetProperty("expectedOutput", out var expected)
                            ? expected.GetString()
                            : null
                    };

                    // Parse required packages for this step
                    if (stepElement.TryGetProperty("requiredPackages", out var stepPkgs) &&
                        stepPkgs.ValueKind == JsonValueKind.Array)
                    {
                        step.RequiredPackages = stepPkgs.EnumerateArray()
                            .Select(p => p.GetString())
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Cast<string>()
                            .ToList();
                    }

                    plan.Steps.Add(step);
                }
            }

            // Parse overall required packages
            if (root.TryGetProperty("requiredPackages", out var pkgsElement) &&
                pkgsElement.ValueKind == JsonValueKind.Array)
            {
                plan.RequiredPackages = pkgsElement.EnumerateArray()
                    .Select(p => p.GetString())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Cast<string>()
                    .Distinct()
                    .ToList();
            }

            // If no overall packages but steps have packages, aggregate them
            if (plan.RequiredPackages.Count == 0)
            {
                plan.RequiredPackages = plan.Steps
                    .SelectMany(s => s.RequiredPackages)
                    .Distinct()
                    .ToList();
            }

            return plan;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response: {Response}", TruncateForLog(response));
            return null;
        }
    }

    private PlannerOutput CreateErrorOutput(PlannerInput input, string error)
    {
        var plan = new TaskPlan
        {
            OriginalTask = input.Task,
            Status = Model.Enums.TaskStatus.Failed
        };

        var context = input.Context ?? new WorkflowContext
        {
            Plan = plan,
            WorkspacePath = _envService.GetWorkspacePath(),
            VenvPath = _envService.GetVenvPath()
        };

        return new PlannerOutput
        {
            Plan = plan,
            Context = context,
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
