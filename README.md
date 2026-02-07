# Agent POC - Multi-Agent Task Execution System

A prototype agentic system using .NET 10 and Microsoft Agent Framework that demonstrates autonomous task execution with planning, Python script execution, and iterative problem-solving.

## 📚 Documentation

- **[Complete Documentation](docs/)** - Comprehensive guides and references
- **[Architecture Guide](docs/architecture.md)** - System design and components
- **[Configuration Guide](docs/configuration.md)** - Setup and configuration options
- **[API Reference](docs/api-reference.md)** - Detailed API documentation
- **[Examples & Use Cases](docs/examples.md)** - Sample tasks and patterns
- **[Development Guide](docs/development.md)** - Contributing and development
- **[Troubleshooting Guide](docs/troubleshooting.md)** - Common issues and solutions

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

## ✨ Features

- **🎯 Plan**: AI agent breaks down complex tasks into discrete, executable steps
- **⚡ Execute**: Generates and runs Python scripts with tool calling support
- **✅ Evaluate**: Assesses results, determines success/retry/replan/impossible
- **📁 File System Access**: Find, read, and copy files from anywhere on the system
- **🐍 Python Environment**: Automatic venv creation and package management
- **🔄 Retry Logic**: Automatic retries with configurable limits and replanning

## 📁 Project Structure

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
├── RR.Agent.Model/              # Shared models and configuration
│   ├── Options/                 # Configuration classes
│   ├── Enums/                   # Status enumerations
│   └── Dtos/                    # Data transfer objects
└── docs/                        # Documentation
    ├── architecture.md          # System architecture
    ├── configuration.md         # Configuration guide
    ├── api-reference.md         # API documentation
    ├── examples.md              # Examples and use cases
    ├── development.md           # Development guide
    └── troubleshooting.md       # Troubleshooting guide
```

## 🚀 Quick Start

### Prerequisites

- .NET 10 SDK
- Python 3.8+ (for script execution)
- Azure AI Foundry project with deployed models

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/RorroRojas3/agent-poc.git
   cd agent-poc
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Configure Azure AI Foundry**:
   ```bash
   cd RR.Agent
   dotnet user-secrets set "AzureAIFoundry:Url" "https://your-resource.services.ai.azure.com/api/projects/your-project"
   ```

4. **Build the application**:
   ```bash
   dotnet build
   ```

5. **Run the application**:
   ```bash
   dotnet run --project RR.Agent
   ```

For detailed setup instructions, see the [Configuration Guide](docs/configuration.md).

## ⚙️ Configuration

The application is configured through `appsettings.json` and user secrets. Key configuration options include:

- **Azure AI Foundry**: Connection URL and model selection
- **Agent Options**: Retry limits, timeouts, workspace location
- **Python Environment**: Python executable, package management

For detailed configuration options, see the [Configuration Guide](docs/configuration.md).

## 💡 Usage

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

```bash
# Simple task
dotnet run --project RR.Agent -- "Create a Python script that prints 'Hello World'"

# Data processing
dotnet run --project RR.Agent -- "Read data.csv and calculate the average of the price column"

# File conversion
dotnet run --project RR.Agent -- "Convert data.csv to JSON format"

# Web scraping
dotnet run --project RR.Agent -- "Fetch news headlines from https://news.ycombinator.com and save to JSON"
```

For more examples and use cases, see the [Examples Guide](docs/examples.md).

## 🛠️ Available Tools

The Executor agent has access to these tools:

### Workspace Operations
- `write_file` - Write content to a file in the workspace
- `read_file` - Read a file from the workspace
- `list_files` - List files in the workspace

### Python Execution
- `execute_python` - Execute Python code directly
- `execute_script_file` - Execute an existing script file
- `install_package` - Install a pip package

### File System Access
- `find_files` - Search for files by pattern (supports wildcards)
- `read_external_file` - Read content from any file path
- `copy_to_workspace` - Copy external files to the workspace

For detailed tool documentation, see the [API Reference](docs/api-reference.md).

## 🔄 Workflow States

The workflow progresses through these states:

1. **Initializing** - Setting up Python environment
2. **Planning** - Creating execution plan
3. **Installing** - Installing required packages
4. **Executing** - Running Python scripts
5. **Evaluating** - Assessing results
6. **Replanning** - Creating revised plan (if needed)
7. **Completed** / **Failed** / **Impossible** - Final states

For detailed workflow information, see the [Architecture Guide](docs/architecture.md).

## 🔧 Error Handling

- **Retry**: Failed steps are retried up to `MaxRetryAttempts` times
- **Replan**: If the Evaluator suggests a different approach, the Planner creates a new plan
- **Impossible**: After multiple failures, tasks are marked as impossible
- **Max Iterations**: Safety limit prevents infinite loops

For troubleshooting common issues, see the [Troubleshooting Guide](docs/troubleshooting.md).

## 🧪 Advanced Features

### Structured Output (Experimental)

Enable JSON schema validation for consistent response formats:

```json
{
  "Agent": {
    "UseStructuredOutput": true
  }
}
```

> **Note**: Requires model support for `response_format` with `json_schema` type (e.g., `gpt-4o`, `gpt-4o-mini`).

For more advanced configuration, see the [Configuration Guide](docs/configuration.md).

## 📦 Dependencies

- `Microsoft.Agents.AI` - Microsoft Agent Framework
- `Microsoft.Agents.AI.AzureAI.Persistent` - Azure AI Foundry Persistent Agents
- `Azure.Identity` - Azure authentication

## 🤝 Contributing

Contributions are welcome! Please see the [Development Guide](docs/development.md) for:

- Coding standards and conventions
- Project structure and architecture
- Adding new features and tools
- Testing and debugging
- Pull request process

## 📖 Documentation

Full documentation is available in the [docs](docs/) directory:

- **[Architecture](docs/architecture.md)** - System design and components
- **[Configuration](docs/configuration.md)** - Setup and configuration
- **[API Reference](docs/api-reference.md)** - Detailed API documentation
- **[Examples](docs/examples.md)** - Sample tasks and use cases
- **[Development](docs/development.md)** - Development guidelines
- **[Troubleshooting](docs/troubleshooting.md)** - Common issues and solutions

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- Built with [.NET 10](https://dotnet.microsoft.com/)
- Powered by [Microsoft Agent Framework](https://github.com/microsoft/agents)
- Uses [Azure AI Foundry](https://azure.microsoft.com/en-us/products/ai-services/)
