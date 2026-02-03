using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RR.Agent.Agents;
using RR.Agent.Extensions;

// Build the host with configuration and services
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Register agent services
builder.Services.AddAgentServices(builder.Configuration);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();

// Run the interactive REPL
await RunInteractiveAgentAsync(host.Services);

async Task RunInteractiveAgentAsync(IServiceProvider services)
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║          Azure AI Agent - Autonomous Task Executor          ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  Enter your task request and the agent will:               ║");
    Console.WriteLine("║  1. Create an execution plan                               ║");
    Console.WriteLine("║  2. Execute each step via code interpreter                 ║");
    Console.WriteLine("║  3. Evaluate results and retry on failure (max 3 attempts) ║");
    Console.WriteLine("║                                                            ║");
    Console.WriteLine("║  Use --file <path> to provide input files                  ║");
    Console.WriteLine("║  Example: --file data.csv Analyze this file                ║");
    Console.WriteLine("║                                                            ║");
    Console.WriteLine("║  Type 'exit' or 'quit' to exit                             ║");
    Console.WriteLine("║  Press Ctrl+C to cancel current operation                  ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    using var scope = services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Task> ");
        Console.ResetColor();

        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Goodbye!");
            break;
        }

        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C to cancel current operation
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[Cancelling...]");
        };

        try
        {
            Console.WriteLine();

            // Parse input for --file flags
            var (request, filePaths) = ParseInputWithFiles(input);

            if (string.IsNullOrWhiteSpace(request))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Please provide a task request along with the files.");
                Console.ResetColor();
                continue;
            }

            await ProcessRequestWithUpdatesAsync(orchestrator, request, filePaths, cts.Token);
            Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nOperation cancelled by user.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            logger.LogError(ex, "Error processing request");
        }
    }
}

(string Request, List<string> FilePaths) ParseInputWithFiles(string input)
{
    var filePaths = new List<string>();
    var requestParts = new List<string>();

    // Tokenize input handling quoted strings
    var tokens = TokenizeInput(input);

    for (int i = 0; i < tokens.Count; i++)
    {
        if (tokens[i].Equals("--file", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Count)
        {
            var filePath = tokens[i + 1];

            // Strip surrounding quotes if present
            filePath = filePath.Trim('"', '\'');

            if (File.Exists(filePath))
            {
                filePaths.Add(filePath);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: File not found: {filePath}");
                Console.ResetColor();
            }
            i++; // Skip the file path token
        }
        else
        {
            requestParts.Add(tokens[i]);
        }
    }

    return (string.Join(" ", requestParts), filePaths);
}

List<string> TokenizeInput(string input)
{
    var tokens = new List<string>();
    var currentToken = new System.Text.StringBuilder();
    var inQuotes = false;
    var quoteChar = '\0';

    for (int i = 0; i < input.Length; i++)
    {
        var c = input[i];

        if (!inQuotes && (c == '"' || c == '\''))
        {
            // Start of quoted string
            inQuotes = true;
            quoteChar = c;
            currentToken.Append(c);
        }
        else if (inQuotes && c == quoteChar)
        {
            // End of quoted string
            inQuotes = false;
            currentToken.Append(c);
            quoteChar = '\0';
        }
        else if (!inQuotes && c == ' ')
        {
            // Space outside quotes - end of token
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }
        else
        {
            currentToken.Append(c);
        }
    }

    // Add final token if any
    if (currentToken.Length > 0)
    {
        tokens.Add(currentToken.ToString());
    }

    return tokens;
}

async Task ProcessRequestWithUpdatesAsync(
    IAgentOrchestrator orchestrator,
    string request,
    List<string> filePaths,
    CancellationToken cancellationToken)
{
    await foreach (var update in orchestrator.ProcessRequestStreamingAsync(request, filePaths, cancellationToken))
    {
        var (color, prefix) = update.Phase switch
        {
            OrchestratorPhase.PreparingFiles => (ConsoleColor.DarkCyan, "FILES"),
            OrchestratorPhase.Planning => (ConsoleColor.Blue, "PLANNING"),
            OrchestratorPhase.PlanPresentation => (ConsoleColor.Magenta, "PLAN"),
            OrchestratorPhase.Executing => (ConsoleColor.Yellow, "EXECUTING"),
            OrchestratorPhase.Evaluating => (ConsoleColor.Cyan, "EVALUATING"),
            OrchestratorPhase.Retrying => (ConsoleColor.DarkYellow, "RETRYING"),
            OrchestratorPhase.Completed => (ConsoleColor.Green, "COMPLETED"),
            OrchestratorPhase.Failed => (ConsoleColor.Red, "FAILED"),
            _ => (ConsoleColor.White, "INFO")
        };

        Console.ForegroundColor = color;

        if (update.CurrentStep.HasValue && update.TotalSteps.HasValue)
        {
            Console.Write($"[{prefix}] [{update.CurrentStep}/{update.TotalSteps}] ");
        }
        else
        {
            Console.Write($"[{prefix}] ");
        }

        Console.ResetColor();

        // For plan presentation, show on multiple lines
        if (update.Phase == OrchestratorPhase.PlanPresentation)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            foreach (var line in update.Message.Split('\n'))
            {
                Console.WriteLine($"  {line}");
            }
            Console.ResetColor();
        }
        else if (update.Phase == OrchestratorPhase.Completed)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var line in update.Message.Split('\n'))
            {
                Console.WriteLine($"  {line}");
            }
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(update.Message);
        }
    }
}
