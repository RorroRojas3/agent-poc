namespace RR.Agent.Model.Options;

/// <summary>
/// Configuration options for Azure AI Foundry connection and model deployment.
/// </summary>
public sealed class AzureAIFoundryOptions
{
    public const string SectionName = "AzureAIFoundry";

    /// <summary>
    /// The Azure AI Foundry project endpoint URL.
    /// Format: https://{resource}.services.ai.azure.com/api/projects/{project}
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// The default model deployment name to use for agents.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Optional model override for the Planner agent.
    /// </summary>
    public string? PlannerModel { get; set; }

    /// <summary>
    /// Optional model override for the Executor agent.
    /// </summary>
    public string? ExecutorModel { get; set; }

    /// <summary>
    /// Optional model override for the Evaluator agent.
    /// </summary>
    public string? EvaluatorModel { get; set; }

    /// <summary>
    /// Gets the model to use for a specific agent role, falling back to DefaultModel.
    /// </summary>
    public string GetModelForRole(string role) => role.ToLowerInvariant() switch
    {
        "planner" => PlannerModel ?? DefaultModel,
        "executor" => ExecutorModel ?? DefaultModel,
        "evaluator" => EvaluatorModel ?? DefaultModel,
        _ => DefaultModel
    };
}
