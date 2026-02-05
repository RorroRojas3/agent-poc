using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RR.Agent.Model.Options;
using RR.Agent.Service.Agents;
using RR.Agent.Service.Executors;
using RR.Agent.Service.Python;
using RR.Agent.Service.Tools;
using RR.Agent.Service.Workflows;

namespace RR.Agent.Service.Extensions;

/// <summary>
/// Extension methods for registering agent services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all agent services to the service collection.
    /// </summary>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register options
        services.Configure<AzureAIFoundryOptions>(
            configuration.GetSection(AzureAIFoundryOptions.SectionName));
        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.SectionName));
        services.Configure<PythonEnvironmentOptions>(
            configuration.GetSection(PythonEnvironmentOptions.SectionName));
        services.Configure<ClaudeOptions>(
            configuration.GetSection(ClaudeOptions.SectionName));

        // Register Python services
        services.AddSingleton<IPythonEnvironmentService, PythonEnvironmentService>();
        services.AddSingleton<IPythonScriptExecutor, PythonScriptExecutor>();

        // Register tools
        services.AddSingleton<ToolHandler>();

        // Register agent service
        services.AddSingleton<AgentService>();

        // Register executors
        services.AddTransient<PlannerExecutor>();
        services.AddTransient<CodeExecutor>();
        services.AddTransient<EvaluatorExecutor>();

        // Register workflow
        services.AddSingleton<AgentWorkflow>();

        return services;
    }

    /// <summary>
    /// Validates that required configuration is present.
    /// </summary>
    public static void ValidateAgentConfiguration(this IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();

        var azureUrl = configuration[$"{AzureAIFoundryOptions.SectionName}:Url"];
        if (string.IsNullOrEmpty(azureUrl))
        {
            throw new InvalidOperationException(
                $"Azure AI Foundry URL is not configured. " +
                $"Set '{AzureAIFoundryOptions.SectionName}:Url' in appsettings.json or user secrets.");
        }

        var workspaceDir = configuration[$"{AgentOptions.SectionName}:WorkspaceDirectory"];
        if (!string.IsNullOrEmpty(workspaceDir))
        {
            // Ensure workspace directory can be created
            try
            {
                var fullPath = Path.GetFullPath(workspaceDir);
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create workspace directory '{workspaceDir}': {ex.Message}", ex);
            }
        }
    }
}
