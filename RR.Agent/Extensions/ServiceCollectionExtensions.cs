namespace RR.Agent.Extensions;

using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RR.Agent.Agents;
using RR.Agent.Configuration;
using RR.Agent.Evaluation;
using RR.Agent.Execution;
using RR.Agent.Infrastructure;
using RR.Agent.Planning;
using RR.Agent.Tools;

/// <summary>
/// Extension methods for registering agent services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all agent services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Configuration
        services.Configure<AzureAIFoundryOptions>(
            configuration.GetSection(AzureAIFoundryOptions.SectionName));
        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.SectionName));

        // Azure AI Client
        services.AddSingleton(sp =>
        {
            var aiOptions = configuration
                .GetSection(AzureAIFoundryOptions.SectionName)
                .Get<AzureAIFoundryOptions>();

            if (aiOptions is null || string.IsNullOrWhiteSpace(aiOptions.Url))
            {
                throw new InvalidOperationException(
                    $"Missing required configuration: {AzureAIFoundryOptions.SectionName}:Url");
            }

            return new PersistentAgentsClient(
                aiOptions.Url,
                new DefaultAzureCredential());
        });

        // Infrastructure
        services.AddSingleton<IRunPoller, RunPoller>();
        services.AddSingleton<IMessageProcessor, MessageProcessor>();

        // Tools
        services.AddSingleton<IToolProvider, CodeInterpreterToolProvider>();

        // Planning
        services.AddScoped<IPlanningModule, PlanningModule>();

        // Execution
        services.AddScoped<IExecutionEngine, ExecutionEngine>();
        services.AddScoped<IScriptGenerator, ScriptGenerator>();
        services.AddScoped<IFileManager, FileManager>();

        // Evaluation
        services.AddScoped<IEvaluationModule, EvaluationModule>();
        services.AddSingleton<IRetryStrategy, ExponentialBackoffRetryStrategy>();

        // Orchestration
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
