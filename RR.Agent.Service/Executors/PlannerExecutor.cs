using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Model.Dtos;
using RR.Agent.Model.Enums;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;
using RR.Agent.Service.Python;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RR.Agent.Service.Executors;

/// <summary>
/// Executor that uses an AI agent to create task plans.
/// </summary>
public sealed class PlannerExecutor(
    AgentService agentService,
    IPythonEnvironmentService envService,
    IOptions<AgentOptions> agentOptions,
    ILogger<PlannerExecutor> logger)
{
    private readonly AgentService _agentService = agentService;
    private readonly IPythonEnvironmentService _envService = envService;
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly ILogger<PlannerExecutor> _logger = logger;
    private const string _agentName = "Planner";

    public async Task<PlannerOutput> ExecuteAsync(PlannerInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use JsonStringEnumConverter so enums serialize/deserialize as strings (AI returns string values)
            var schemaOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            schemaOptions.Converters.Add(new JsonStringEnumConverter());
            JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(TaskPlan), serializerOptions: schemaOptions);

            var chatAgentClientOptions = new ChatClientAgentOptions
            {
                Id = $"{_agentName}-{Guid.NewGuid()}",
                Name = _agentName,
                ChatOptions = new ChatOptions
                {
                    Instructions = AgentPrompts.PlannerSystemPrompt,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                    schema: schema),
                    ModelId = _agentOptions.Planner.ModelId
                }
            };
            var agent = await _agentService.GetOrCreateChatClientAgentAsync(_agentOptions.Planner.Type, _agentName, chatAgentClientOptions, cancellationToken);

            var sessionId = Guid.NewGuid().ToString();
            var session = await _agentService.CreateSessionAsync(_agentName, sessionId, cancellationToken);

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
                prompt = $"Create a plan for the following task:\n\n{input.Task}\n\n";
            }

            var response = await _agentService.RunAsAgentResponseAsync(_agentName, sessionId, prompt, cancellationToken);

            var plan = response.Deserialize<TaskPlan>(schemaOptions);
            if (plan == null)
            {
                return CreateErrorOutput(input, "Failed to parse plan from agent response");
            }

            if (plan.Steps.Count == 0)
            {
                return CreateErrorOutput(input, "Plan contains no steps");
            }
            
            plan.OriginalTask = input.Task;
            if (plan.Steps.Count > _agentOptions.MaxStepsPerPlan)
            {
                _logger.LogWarning("Plan has {Count} steps, truncating to {Max}",
                    plan.Steps.Count, _agentOptions.MaxStepsPerPlan);
                plan.Steps = [.. plan.Steps.Take(_agentOptions.MaxStepsPerPlan)];
            }

            // Create or update execution context
            var context = input.Context ?? new WorkflowContext
            {
                Plan = plan
            };

            context.Plan = plan;
            context.CurrentStep = plan.CurrentStep;
            context.AddMessage(AgentRole.Planner, response.Text);

            plan.Status = TaskStatuses.Planning;

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

    #region Private methods
    private PlannerOutput CreateErrorOutput(PlannerInput input, string error)
    {
        var plan = new TaskPlan
        {
            OriginalTask = input.Task,
            Status = TaskStatuses.Failed
        };

        var context = input.Context ?? new WorkflowContext
        {
            Plan = plan
        };

        return new PlannerOutput
        {
            Plan = plan,
            Context = context,
            Success = false,
            Error = error
        };
    }
    #endregion
}
