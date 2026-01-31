# RR.Agent - Azure AI Agent Framework

An autonomous task executor built on Azure AI Foundry's persistent agents. This framework decomposes natural language requests into executable plans, runs them using code interpretation, and evaluates results with intelligent retry mechanisms.

## Features

- **AI-Powered Planning** - Automatically breaks down complex requests into structured, executable steps
- **Code Execution** - Generates and runs Python/C# scripts via Azure AI code interpreter
- **Intelligent Retry** - Evaluates failures and retries with exponential backoff
- **Real-Time Streaming** - Progress updates with visual indicators during execution
- **Modular Architecture** - Clean separation between planning, execution, and evaluation phases

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Agent Orchestrator                         │
│    Coordinates the complete workflow: Plan → Execute → Eval     │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│    Planning   │     │   Execution   │     │  Evaluation   │
│    Module     │     │    Engine     │     │    Module     │
├───────────────┤     ├───────────────┤     ├───────────────┤
│ • Parse       │     │ • Generate    │     │ • Analyze     │
│   request     │     │   scripts     │     │   results     │
│ • Create      │     │ • Run code    │     │ • Determine   │
│   steps       │     │   interpreter │     │   verdict     │
│ • Validate    │     │ • Capture     │     │ • Suggest     │
│   plan        │     │   output      │     │   retries     │
└───────────────┘     └───────────────┘     └───────────────┘
```

## Project Structure

```
RR.Agent/
├── Agents/                 # Orchestration layer
│   ├── IAgentOrchestrator.cs
│   └── AgentOrchestrator.cs
├── Planning/               # Request decomposition
│   ├── PlanningModule.cs
│   └── Models/
│       ├── ExecutionPlan.cs
│       ├── PlanStep.cs
│       └── StepType.cs
├── Execution/              # Step execution engine
│   ├── ExecutionEngine.cs
│   ├── ScriptGenerator.cs
│   ├── FileManager.cs
│   └── Models/
│       ├── ExecutionResult.cs
│       └── ExecutionStatus.cs
├── Evaluation/             # Result analysis & retry logic
│   ├── EvaluationModule.cs
│   ├── ExponentialBackoffRetryStrategy.cs
│   └── Models/
│       ├── EvaluationResult.cs
│       └── EvaluationVerdict.cs
├── Infrastructure/         # Azure AI integration
│   ├── RunPoller.cs
│   └── MessageProcessor.cs
├── Configuration/          # Settings models
│   ├── AzureAIFoundryOptions.cs
│   └── AgentOptions.cs
├── Exceptions/             # Custom exceptions
├── Extensions/             # DI setup
├── Tools/                  # Tool providers
├── Program.cs              # Entry point
└── appsettings.json        # Configuration
```

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Azure subscription with Azure AI Foundry project
- Azure credentials configured (supports DefaultAzureCredential)
- Model deployment (default: `gpt-4o`)

## Configuration

### appsettings.json

```json
{
  "AzureAIFoundry": {
    "Url": "https://<your-project>.api.azureml.ms",
    "DefaultModel": "gpt-4o"
  },
  "Agent": {
    "MaxRetryAttempts": 3,
    "PollingIntervalMs": 500,
    "RunTimeoutSeconds": 300,
    "WorkspaceDirectory": "./workspace"
  }
}
```

### User Secrets (Recommended for local development)

```bash
dotnet user-secrets set "AzureAIFoundry:Url" "https://<your-project>.api.azureml.ms"
```

## Getting Started

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project RR.Agent
```

### Usage

The application runs as an interactive REPL:

```
╔═══════════════════════════════════════════════════════════════╗
║                    RR.Agent - Task Executor                   ║
║        Powered by Azure AI Foundry Persistent Agents          ║
╚═══════════════════════════════════════════════════════════════╝

Type a task and press Enter. Type 'exit' or 'quit' to close.
Press Ctrl+C to cancel the current operation.

Task> Analyze the last 30 days of sales data and create a summary report
```

## Workflow

1. **Planning Phase** - AI analyzes your request and creates a structured plan with ordered steps
2. **Plan Presentation** - Shows the execution plan for visibility
3. **Execution Phase** - Runs each step sequentially using code interpreter
4. **Evaluation Phase** - Assesses results and determines if steps succeeded
5. **Retry Phase** - Automatically retries failed steps with adjustments
6. **Completion** - Reports final outcome with generated files

### Execution Plan Format

The planning module generates structured plans:

```json
{
  "summary": "Analyze sales data and generate summary report",
  "complexity": 5,
  "steps": [
    {
      "order": 1,
      "description": "Load and parse sales data from CSV",
      "type": "CodeExecution",
      "expectedOutput": "DataFrame with sales records",
      "dependencies": [],
      "scriptHint": "Python pandas"
    },
    {
      "order": 2,
      "description": "Calculate key metrics",
      "type": "CodeExecution",
      "expectedOutput": "Revenue totals, averages, trends",
      "dependencies": [1]
    }
  ]
}
```

### Step Types

| Type | Description |
|------|-------------|
| `CodeExecution` | Generate and run Python/C# code |
| `FileRead` | Read content from files |
| `FileWrite` | Write content to files |
| `Analysis` | Analyze data or results |
| `UserInput` | Request user input |

## Evaluation Verdicts

The evaluation module determines next actions:

| Verdict | Description | Action |
|---------|-------------|--------|
| `Success` | Step achieved its goal | Proceed to next step |
| `Retry` | Temporary failure, retryable | Retry with adjustments |
| `Impossible` | Cannot be completed | Abort with explanation |

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Azure.Identity | 1.17.1 | Azure authentication |
| Azure.AI.Agents.Persistent | - | Persistent agents client |
| Microsoft.Agents.AI | 1.0.0-preview | Agent framework |
| Microsoft.Extensions.Hosting | - | Application hosting |

## Error Handling

Custom exceptions provide context for failures:

- `AgentException` - Base exception for agent errors
- `PlanningException` - Planning phase failures
- `ExecutionException` - Step execution failures
- `ImpossibleTaskException` - Task cannot be completed

## Retry Strategy

Failed steps use exponential backoff:

- Delay: 1s → 2s → 4s → ...
- Configurable max attempts (default: 3)
- AI-powered failure analysis distinguishes retryable vs. impossible failures

## Development

### Adding New Step Types

1. Add the type to `StepType.cs`
2. Update `ExecutionEngine.ExecuteStepAsync()` to handle the new type
3. Update `ScriptGenerator` if code generation is needed

### Adding New Tool Providers

1. Implement `IToolProvider`
2. Register in `ServiceCollectionExtensions`

## License

See [LICENSE.txt](LICENSE.txt) for details.
