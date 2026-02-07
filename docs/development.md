# Development Guide

## Getting Started

### Prerequisites

1. **Development Environment**:
   - .NET 10 SDK
   - Visual Studio 2022, VS Code, or Rider
   - Git

2. **Runtime Requirements**:
   - Python 3.8 or higher
   - pip (Python package installer)

3. **Azure Account**:
   - Azure subscription
   - Azure AI Foundry project with deployed models

### Clone the Repository

```bash
git clone https://github.com/RorroRojas3/agent-poc.git
cd agent-poc
```

### Install Dependencies

```bash
dotnet restore
```

### Configure the Application

See [Configuration Guide](configuration.md) for detailed setup instructions.

Quick setup:
```bash
cd RR.Agent
dotnet user-secrets set "AzureAIFoundry:Url" "https://your-resource.services.ai.azure.com/api/projects/your-project"
```

### Build the Solution

```bash
dotnet build
```

### Run the Application

```bash
dotnet run --project RR.Agent
```

Or with a task:
```bash
dotnet run --project RR.Agent -- "Create a Python script that prints Hello World"
```

## Project Structure

```
agent-poc/
├── RR.Agent/                    # Console application
│   ├── Program.cs               # Entry point
│   ├── appsettings.json         # Configuration
│   └── RR.Agent.csproj          # Project file
│
├── RR.Agent.Service/            # Core business logic
│   ├── Agents/                  # Agent management
│   │   ├── AgentService.cs      # Azure AI client
│   │   ├── AgentPrompts.cs      # System prompts
│   │   └── ResponseSchemas.cs   # JSON schemas
│   ├── Executors/               # Workflow executors
│   │   ├── PlannerExecutor.cs
│   │   ├── CodeExecutor.cs
│   │   └── EvaluatorExecutor.cs
│   ├── Python/                  # Python environment
│   │   ├── PythonEnvironmentService.cs
│   │   └── PythonScriptExecutor.cs
│   ├── Tools/                   # Agent tools
│   │   ├── ToolDefinitions.cs
│   │   ├── ToolHandler.cs
│   │   ├── FileToolService.cs
│   │   └── PythonToolService.cs
│   ├── Workflows/               # Orchestration
│   │   └── AgentWorkflow.cs
│   └── Extensions/              # DI extensions
│       └── ServiceCollectionExtensions.cs
│
├── RR.Agent.Model/              # Shared models
│   ├── Dtos/                    # Data transfer objects
│   │   ├── TaskPlan.cs
│   │   ├── TaskStep.cs
│   │   ├── EvaluationResult.cs
│   │   └── ...
│   ├── Options/                 # Configuration classes
│   │   ├── AgentOptions.cs
│   │   ├── AzureAIFoundryOptions.cs
│   │   └── PythonEnvironmentOptions.cs
│   └── Enums/                   # Enumerations
│       ├── TaskStatuses.cs
│       └── ...
│
├── RR.Agent.Dto/                # Legacy DTOs (to be consolidated)
│
├── docs/                        # Documentation
│   ├── architecture.md
│   ├── configuration.md
│   ├── development.md
│   └── ...
│
└── RR.Agent.slnx                # Solution file
```

## Development Workflow

### 1. Create a Feature Branch

```bash
git checkout -b feature/your-feature-name
```

### 2. Make Changes

Follow the coding standards and patterns established in the codebase.

### 3. Build and Test

```bash
dotnet build
dotnet run --project RR.Agent -- "Test task"
```

### 4. Commit Changes

```bash
git add .
git commit -m "feat: add your feature description"
```

### 5. Push and Create PR

```bash
git push origin feature/your-feature-name
```

## Coding Standards

### C# Conventions

1. **Naming**:
   - PascalCase for classes, methods, properties
   - camelCase for local variables, parameters
   - Prefix interfaces with `I` (e.g., `IPythonScriptExecutor`)

2. **File Organization**:
   - One class per file
   - Filename matches class name
   - Use file-scoped namespaces

3. **XML Documentation**:
   - All public APIs must have XML doc comments
   - Include `<summary>`, `<param>`, `<returns>` tags
   - Add examples for complex methods

Example:
```csharp
/// <summary>
/// Executes a Python script in the virtual environment.
/// </summary>
/// <param name="scriptPath">Path to the Python script file.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Execution result containing stdout, stderr, and exit code.</returns>
public async Task<PythonExecutionResult> ExecuteScriptAsync(
    string scriptPath,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

4. **Async/Await**:
   - Append `Async` to async method names
   - Always use `ConfigureAwait(false)` in library code (not needed in app code)
   - Provide `CancellationToken` parameters

5. **Error Handling**:
   - Use try-catch for expected errors
   - Log exceptions before rethrowing
   - Return result objects instead of throwing for business logic failures

### Dependency Injection

Register services in `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddAgentServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register options
    services.Configure<AgentOptions>(
        configuration.GetSection("Agent"));
    
    // Register services
    services.AddSingleton<IMyService, MyService>();
    
    return services;
}
```

### Logging

Use structured logging with proper log levels:

```csharp
_logger.LogInformation(
    "Processing step {StepNumber} of {TotalSteps}",
    currentStep,
    totalSteps);

_logger.LogWarning(
    "Retry attempt {Attempt} of {MaxAttempts} for step {StepNumber}",
    attemptCount,
    maxAttempts,
    stepNumber);

_logger.LogError(ex,
    "Failed to execute step {StepNumber}: {Error}",
    stepNumber,
    ex.Message);
```

## Adding New Features

### Adding a New Tool

1. **Define the tool schema** in `ToolDefinitions.cs`:

```csharp
public static readonly ToolDefinition ReadCsvFileTool = new()
{
    Type = "function",
    Function = new FunctionDefinition
    {
        Name = "read_csv",
        Description = "Read a CSV file and return its contents",
        Parameters = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Path to the CSV file"
                },
                "has_header": {
                    "type": "boolean",
                    "description": "Whether the CSV has a header row"
                }
            },
            "required": ["file_path"]
        }
        """)
    }
};
```

2. **Implement the tool** in appropriate service:

```csharp
public async Task<ToolResponseDto> ReadCsvAsync(
    string filePath,
    bool hasHeader,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

3. **Register in ToolHandler**:

```csharp
case "read_csv":
    var filePath = toolCall.Arguments["file_path"].GetString();
    var hasHeader = toolCall.Arguments.TryGetProperty("has_header", out var h) 
        ? h.GetBoolean() 
        : true;
    return await _fileToolService.ReadCsvAsync(filePath, hasHeader, cancellationToken);
```

4. **Update executor tools list** in appropriate executor:

```csharp
Tools = 
[
    ToolDefinitions.WriteTool,
    ToolDefinitions.ReadCsvTool,
    // ... other tools
]
```

### Adding a New Executor

1. **Create executor class**:

```csharp
public sealed class MyExecutor
{
    private readonly AgentService _agentService;
    private readonly ILogger<MyExecutor> _logger;
    
    public MyExecutor(
        AgentService agentService,
        ILogger<MyExecutor> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }
    
    public async Task<MyOutput> ExecuteAsync(
        MyInput input,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

2. **Define system prompt** in `AgentPrompts.cs`:

```csharp
public const string MyExecutorSystemPrompt = """
    You are a specialized agent for...
    """;
```

3. **Register in DI**:

```csharp
services.AddSingleton<MyExecutor>();
```

4. **Integrate into workflow** in `AgentWorkflow.cs`.

### Modifying System Prompts

System prompts are located in `AgentPrompts.cs`. When modifying:

1. **Test thoroughly** - Prompt changes can significantly affect behavior
2. **Document intent** - Add comments explaining the reasoning
3. **Version control** - Consider versioning prompts for experimentation

## Debugging

### Local Debugging

1. **Set breakpoints** in Visual Studio or VS Code
2. **Configure launch settings** in `RR.Agent/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "RR.Agent": {
      "commandName": "Project",
      "commandLineArgs": "Create a test script",
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

3. **Start debugging** (F5)

### Logging Configuration

Adjust log levels in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning",
      "RR.Agent": "Debug"
    }
  }
}
```

### Inspecting Workflow State

Add state inspection in `AgentWorkflow.cs`:

```csharp
_logger.LogDebug(
    "Current state - Step: {Step}/{Total}, Status: {Status}, Iteration: {Iteration}",
    plan.CurrentStepNumber,
    plan.Steps.Count,
    plan.Status,
    context.IterationCount);
```

### Examining Python Output

Python scripts and outputs are saved in the workspace:

```bash
# View generated scripts
ls workspace/scripts/

# View script outputs
ls workspace/output/

# Check virtual environment
workspace/.venv/bin/pip list
```

## Testing

### Manual Testing

Create test scenarios in `RR.Agent/Program.cs` or via command line:

```bash
# Simple task
dotnet run --project RR.Agent -- "Print hello world"

# File operation
dotnet run --project RR.Agent -- "Create a JSON file with sample user data"

# Data processing
dotnet run --project RR.Agent -- "Calculate fibonacci numbers up to 100"

# External file access
dotnet run --project RR.Agent -- "Read data.csv and calculate average"
```

### Unit Testing (Future)

The project structure supports adding unit tests:

```
agent-poc/
└── tests/
    ├── RR.Agent.Service.Tests/
    ├── RR.Agent.Model.Tests/
    └── RR.Agent.Tests/
```

Example test structure:

```csharp
public class PlannerExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ValidTask_ReturnsSuccessfulPlan()
    {
        // Arrange
        var planner = CreatePlanner();
        var input = new PlannerInput { Task = "Test task" };
        
        // Act
        var result = await planner.ExecuteAsync(input);
        
        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Plan.Steps);
    }
}
```

## Performance Optimization

### Reducing Latency

1. **Model selection**: Use lighter models for simple tasks
2. **Structured output**: Enable for faster parsing (if supported)
3. **Tool optimization**: Minimize tool calls per step
4. **Caching**: Reuse virtual environments

### Reducing Costs

1. **Model tiers**: Use `gpt-4o-mini` for executor and evaluator
2. **Prompt optimization**: Shorter prompts reduce token usage
3. **Early termination**: Fail fast on impossible tasks
4. **Batch operations**: Consolidate multiple file operations

### Monitoring Performance

Add performance logging:

```csharp
var stopwatch = Stopwatch.StartNew();
var result = await executor.ExecuteAsync(input);
stopwatch.Stop();

_logger.LogInformation(
    "Execution completed in {ElapsedMs}ms",
    stopwatch.ElapsedMilliseconds);
```

## Common Development Tasks

### Clearing the Workspace

```bash
rm -rf workspace/
```

### Resetting Virtual Environment

```bash
rm -rf workspace/.venv/
dotnet run --project RR.Agent -- "test"  # Recreates venv
```

### Updating Dependencies

```bash
dotnet list package --outdated
dotnet add package <PackageName>
dotnet restore
```

### Formatting Code

```bash
dotnet format
```

## Troubleshooting Development Issues

### Issue: Changes Not Reflected

**Solution**: Rebuild the solution
```bash
dotnet clean
dotnet build
```

### Issue: User Secrets Not Loading

**Solution**: Verify user secrets are initialized
```bash
cd RR.Agent
dotnet user-secrets list
```

### Issue: Python Not Found

**Solution**: Check Python installation
```bash
python --version
which python  # Linux/Mac
where python  # Windows
```

### Issue: Agent Not Responding

**Solution**: Check Azure AI Foundry connectivity
```bash
# Test authentication
az account show

# Verify endpoint is accessible
curl https://your-resource.services.ai.azure.com/
```

## Contributing

### Pull Request Process

1. Create feature branch from `main`
2. Make changes following coding standards
3. Test locally
4. Update documentation if needed
5. Create PR with clear description
6. Address review feedback
7. Squash and merge when approved

### Commit Message Format

Follow conventional commits:

```
feat: add new CSV reading tool
fix: handle timeout in Python executor
docs: update configuration guide
refactor: simplify tool handler logic
test: add unit tests for planner
```

## Resources

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Azure AI Foundry Documentation](https://learn.microsoft.com/en-us/azure/ai-services/)
- [Microsoft Agents Framework](https://github.com/microsoft/agents)
- [Python Virtual Environments](https://docs.python.org/3/library/venv.html)
