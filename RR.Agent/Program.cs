using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RR.Agent.Model.Enums;
using RR.Agent.Service.Extensions;
using RR.Agent.Service.Workflows;

// Build the host with configuration and services
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register agent services
builder.Services.AddAgentServices(builder.Configuration);

var host = builder.Build();

// Validate configuration
try
{
    host.Services.ValidateAgentConfiguration();
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Configuration Error: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("To configure Azure AI Foundry URL, run:");
    Console.WriteLine("  dotnet user-secrets set \"AzureAIFoundry:Url\" \"https://your-resource.services.ai.azure.com/api/projects/your-project\"");
    return 1;
}

// Get services
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var workflow = host.Services.GetRequiredService<AgentWorkflow>();

// Subscribe to workflow state changes
workflow.StateChanged += (sender, e) =>
{
    var color = e.State switch
    {
        "Completed" => ConsoleColor.Green,
        "Failed" or "Impossible" => ConsoleColor.Red,
        "Replanning" => ConsoleColor.Yellow,
        _ => ConsoleColor.Cyan
    };

    Console.ForegroundColor = color;
    Console.WriteLine($"[{e.State}] {e.Message}");
    Console.ResetColor();
};

// Get task from arguments or prompt user
string task;
if (args.Length > 0)
{
    task = string.Join(" ", args);
}
else
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           Agent POC - Multi-Agent Task Execution System          ║");
    Console.WriteLine("║══════════════════════════════════════════════════════════════════║");
    Console.WriteLine("║  This system uses AI agents to:                                  ║");
    Console.WriteLine("║  • Plan - Break down tasks into executable steps                 ║");
    Console.WriteLine("║  • Execute - Write and run Python scripts                        ║");
    Console.WriteLine("║  • Evaluate - Assess results and iterate if needed               ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine("Enter your task (or 'quit' to exit):");
    Console.Write("> ");
    task = Console.ReadLine() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(task) || task.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        return 0;
    }
}

logger.LogInformation("Starting task: {Task}", task);
Console.WriteLine();
Console.WriteLine(new string('─', 70));
Console.WriteLine();

try
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\nCancellation requested...");
    };

    // Execute the workflow
    var result = await workflow.ExecuteAsync(task, cts.Token);

    // Display results
    Console.WriteLine();
    Console.WriteLine(new string('═', 70));
    Console.WriteLine("EXECUTION SUMMARY");
    Console.WriteLine(new string('═', 70));

    var plan = result.Plan;
    Console.WriteLine($"Task: {plan.OriginalTask}");
    Console.WriteLine($"Status: {plan.Status}");
    Console.WriteLine($"Steps Completed: {plan.CompletedStepsCount}/{plan.Steps.Count}");
    Console.WriteLine($"Total Iterations: {plan.TotalIterations}");

    if (result.CreatedFiles.Count > 0)
    {
        Console.WriteLine($"Generated Files: {string.Join(", ", result.CreatedFiles)}");
    }

    Console.WriteLine();
    Console.WriteLine("Step Details:");
    foreach (var step in plan.Steps)
    {
        var statusSymbol = step.Status switch
        {
            TaskStatuses.Completed => "✓",
            TaskStatuses.Failed => "✗",
            TaskStatuses.Impossible => "⊘",
            _ => "○"
        };

        var statusColor = step.Status switch
        {
            TaskStatuses.Completed => ConsoleColor.Green,
            TaskStatuses.Failed or TaskStatuses.Impossible => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        Console.ForegroundColor = statusColor;
        Console.Write($"  {statusSymbol} ");
        Console.ResetColor();
        Console.WriteLine($"Step {step.StepNumber}: {step.Description}");

        if (step.Evaluation != null && !step.Evaluation.IsSuccessful && step.Evaluation.Issues.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var issue in step.Evaluation.Issues.Take(2))
            {
                Console.WriteLine($"      Issue: {issue}");
            }
            Console.ResetColor();
        }
    }

    Console.WriteLine();

    // Show final output if available
    if (result.LastExecutionResult != null &&
        !string.IsNullOrEmpty(result.LastExecutionResult.StandardOutput) &&
        plan.Status == TaskStatuses.Completed)
    {
        Console.WriteLine("Final Output:");
        Console.WriteLine(new string('-', 70));
        Console.ForegroundColor = ConsoleColor.White;
        var output = result.LastExecutionResult.StandardOutput;
        if (output.Length > 2000)
        {
            output = output[..2000] + "\n... (output truncated)";
        }
        Console.WriteLine(output);
        Console.ResetColor();
    }

    Console.WriteLine(new string('═', 70));

    return plan.Status == TaskStatuses.Completed ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nOperation cancelled.");
    return 130;
}
catch (Exception ex)
{
    logger.LogError(ex, "Error executing task");
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.Message}");
    Console.ResetColor();
    return 1;
}
