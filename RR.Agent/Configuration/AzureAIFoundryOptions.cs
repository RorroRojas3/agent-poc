namespace RR.Agent.Configuration;

/// <summary>
/// Configuration options for Azure AI Foundry connection.
/// </summary>
public sealed class AzureAIFoundryOptions
{
    public const string SectionName = "AzureAIFoundry";

    /// <summary>
    /// The Azure AI Foundry project endpoint URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The default model deployment name to use.
    /// </summary>
    public required string DefaultModel { get; init; }
}
