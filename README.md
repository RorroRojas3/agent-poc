# Agent POC - Multi-Agent Task Execution System

A prototype agentic system using .NET 10 and Microsoft Agent Framework that demonstrates autonomous task execution with planning, Python script execution, and iterative problem-solving.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    AgentWorkflow Orchestration               │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐    │
│  │   Planner    │──►│    Code      │──►│  Evaluator   │    │
│  │   Executor   │   │   Executor   │   │   Executor   │    │
│  └──────────────┘   └──────────────┘   └──────────────┘    │
│         ▲                                     │             │
│         └─────────── retry/replan ───────────┘             │
├─────────────────────────────────────────────────────────────┤
│  Python Services: venv creation, pip install, execution     │
│  Tools: write_file, read_file, execute_python, find_files   │
└─────────────────────────────────────────────────────────────┘
```

## Features

- **Plan**: AI agent breaks down complex tasks into discrete, executable steps
- **Execute**: Generates and runs Python scripts with tool calling support
- **Evaluate**: Assesses results, determines success/retry/replan/impossible
- **File System Access**: Find, read, and copy files from anywhere on the system
- **Python Environment**: Automatic venv creation and package management
- **Retry Logic**: Automatic retries with configurable limits and replanning

## Project Structure

```
agent-poc/
├── RR.Agent/                    # Console application entry point
│   ├── Program.cs               # Main entry point with workflow execution
│   └── appsettings.json         # Configuration
├── RR.Agent.Service/            # Core business logic
│   ├── Agents/                  # Agent management
│   │   ├── AgentService.cs      # Azure AI Foundry agent client
│   │   ├── AgentPrompts.cs      # System prompts for each agent role
│   │   └── ResponseSchemas.cs   # JSON schemas for structured output
│   ├── Executors/               # Workflow executors
│   │   ├── PlannerExecutor.cs   # Creates task plans
│   │   ├── CodeExecutor.cs      # Executes Python code with tools
│   │   └── EvaluatorExecutor.cs # Evaluates execution results
│   ├── Python/                  # Python environment management
│   │   ├── PythonEnvironmentService.cs  # venv creation, pip install
│   │   └── PythonScriptExecutor.cs      # Script execution
│   ├── Tools/                   # Agent tool definitions
│   │   ├── ToolDefinitions.cs   # Available tools for the Executor
│   │   └── ToolHandler.cs       # Tool call implementations
│   └── Workflows/               # Workflow orchestration
│       └── AgentWorkflow.cs     # Plan-Execute-Evaluate loop
└── RR.Agent.Model/              # Shared models and configuration
    ├── Options/                 # Configuration classes
    ├── Enums/                   # Status enumerations
    └── Dtos/                    # Data transfer objects
```

## Prerequisites

- .NET 10 SDK
- Python 3.8+ (for script execution)
- Azure AI Foundry project with deployed models

## Configuration

### 1. Set Azure AI Foundry URL

```bash
cd RR.Agent
dotnet user-secrets set "AzureAIFoundry:Url" "https://your-resource.services.ai.azure.com/api/projects/your-project"
```

### 2. Configure Models (Optional)

In `appsettings.json`:

```json
{
  "AzureAIFoundry": {
    "DefaultModel": "gpt-4o",
    "PlannerModel": "gpt-4o",
    "ExecutorModel": "gpt-4o",
    "EvaluatorModel": "gpt-4o-mini"
  }
}
```

### 3. Agent Options

```json
{
  "Agent": {
    "MaxRetryAttempts": 3,
    "MaxIterations": 10,
    "MaxStepsPerPlan": 20,
    "WorkspaceDirectory": "./workspace",
    "RunTimeoutSeconds": 300,
    "UseStructuredOutput": false
  }
}
```

## Usage

### Run with Command Line Argument

```bash
dotnet run --project RR.Agent -- "Create a Python script that calculates fibonacci numbers"
```

### Run Interactively

```bash
dotnet run --project RR.Agent
```

Then enter your task at the prompt.

### Example Tasks

```
# Simple task
Create a Python script that prints 'Hello World'

# Data processing
Read the CSV file at C:\Users\Rorro\Downloads\data.csv and calculate the average of the 'price' column

# PDF extraction
Extract text from C:\Users\Rorro\Downloads\document.pdf and save it to output.txt

# Web scraping
Fetch the latest news headlines from https://news.ycombinator.com and save them to a JSON file
```

## Available Tools

The Executor agent has access to these tools:

### Workspace Operations
| Tool | Description |
|------|-------------|
| `write_file` | Write content to a file in the workspace |
| `read_file` | Read a file from the workspace |
| `list_files` | List files in the workspace |

### Python Execution
| Tool | Description |
|------|-------------|
| `execute_python` | Execute Python code directly |
| `execute_script_file` | Execute an existing script file |
| `install_package` | Install a pip package |

### File System Access
| Tool | Description |
|------|-------------|
| `find_files` | Search for files by pattern (supports wildcards) |
| `read_external_file` | Read content from any file path |
| `copy_to_workspace` | Copy external files to the workspace |

## Workflow States

The workflow progresses through these states:

1. **Initializing** - Setting up Python environment
2. **Planning** - Creating execution plan
3. **Installing** - Installing required packages
4. **Executing** - Running Python scripts
5. **Evaluating** - Assessing results
6. **Replanning** - Creating revised plan (if needed)
7. **Completed** / **Failed** / **Impossible** - Final states

## Error Handling

- **Retry**: Failed steps are retried up to `MaxRetryAttempts` times
- **Replan**: If the Evaluator suggests a different approach, the Planner creates a new plan
- **Impossible**: After multiple failures, tasks are marked as impossible
- **Max Iterations**: Safety limit prevents infinite loops

## Structured Output (Experimental)

Enable JSON schema validation for Planner and Evaluator responses:

```json
{
  "Agent": {
    "UseStructuredOutput": true
  }
}
```

> Note: Requires model support for `response_format` with `json_schema` type.

## Dependencies

- `Microsoft.Agents.AI` - Microsoft Agent Framework
- `Microsoft.Agents.AI.AzureAI.Persistent` - Azure AI Foundry Persistent Agents
- `Azure.Identity` - Azure authentication

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

## License

MIT
