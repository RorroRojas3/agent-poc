# Configuration Guide

## Prerequisites

Before configuring the Agent POC system, ensure you have:

1. **.NET 10 SDK** installed
2. **Python 3.8+** installed and accessible via PATH
3. **Azure AI Foundry** project with deployed models

## Configuration Files

### appsettings.json

Located in `RR.Agent/appsettings.json`, this file contains all configurable options.

#### Azure AI Foundry Configuration

```json
{
  "AzureAIFoundry": {
    "Url": "",
    "DefaultModel": "gpt-4o",
    "PlannerModel": "gpt-4o",
    "ExecutorModel": "gpt-4o",
    "EvaluatorModel": "gpt-4o-mini"
  }
}
```

**Properties**:
- `Url`: Azure AI Foundry project endpoint (set via user-secrets, see below)
- `DefaultModel`: Fallback model when role-specific model not specified
- `PlannerModel`: Model used for task planning
- `ExecutorModel`: Model used for code execution
- `EvaluatorModel`: Model used for result evaluation (can use lighter model)

#### Agent Options

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

**Properties**:
- `MaxRetryAttempts`: Maximum times to retry a failed step (default: 3)
- `MaxIterations`: Safety limit for total workflow iterations (default: 10)
- `MaxStepsPerPlan`: Maximum steps allowed in a plan (default: 20)
- `WorkspaceDirectory`: Directory for Python environment and files (default: ./workspace)
- `RunTimeoutSeconds`: Maximum execution time for a single script (default: 300)
- `UseStructuredOutput`: Enable JSON schema validation (experimental, default: false)

#### Python Environment Options

```json
{
  "PythonEnvironment": {
    "PythonExecutable": "python",
    "VenvName": ".venv",
    "ScriptsDirectory": "scripts",
    "OutputDirectory": "output",
    "PipTimeoutSeconds": 120,
    "DefaultPackages": []
  }
}
```

**Properties**:
- `PythonExecutable`: Python command (default: "python", use "python3" on some Linux systems)
- `VenvName`: Virtual environment directory name (default: ".venv")
- `ScriptsDirectory`: Directory for generated Python scripts (default: "scripts")
- `OutputDirectory`: Directory for script outputs (default: "output")
- `PipTimeoutSeconds`: Timeout for pip operations (default: 120)
- `DefaultPackages`: Packages to install in every new environment (default: [])

## Setting Up Azure AI Foundry

### Step 1: Get Your Project URL

1. Navigate to [Azure AI Foundry](https://ai.azure.com/)
2. Select or create a project
3. Go to **Settings** or **Overview**
4. Copy the **Project API endpoint**, which looks like:
   ```
   https://<your-resource>.services.ai.azure.com/api/projects/<your-project>
   ```

### Step 2: Configure User Secrets (Development)

User secrets keep sensitive data out of source control.

```bash
cd RR.Agent
dotnet user-secrets set "AzureAIFoundry:Url" "https://your-resource.services.ai.azure.com/api/projects/your-project"
```

**Verify secrets**:
```bash
dotnet user-secrets list
```

### Step 3: Authentication

The system uses Azure Default Credential for authentication. Ensure you're logged in:

```bash
# Using Azure CLI
az login

# Or set environment variables
export AZURE_TENANT_ID="your-tenant-id"
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
```

## Environment Variables

For production or CI/CD, use environment variables instead of user-secrets:

```bash
# Azure AI Foundry
export AzureAIFoundry__Url="https://your-resource.services.ai.azure.com/api/projects/your-project"

# Agent Options
export Agent__MaxRetryAttempts=5
export Agent__MaxIterations=15
export Agent__WorkspaceDirectory="/app/workspace"

# Python Environment
export PythonEnvironment__PythonExecutable="python3"
export PythonEnvironment__PipTimeoutSeconds=180
```

**Note**: Double underscores (`__`) represent nested JSON properties.

## Model Selection

### Choosing Models

Different agent roles have different requirements:

| Agent Role | Recommended Models | Rationale |
|------------|-------------------|-----------|
| Planner | `gpt-4o`, `gpt-4` | Requires strong reasoning for task decomposition |
| Executor | `gpt-4o`, `gpt-4o-mini` | Needs good coding ability but can use lighter model |
| Evaluator | `gpt-4o-mini`, `gpt-3.5-turbo` | Simple evaluation logic, lighter model sufficient |

### Cost Optimization

To reduce costs while maintaining quality:

```json
{
  "AzureAIFoundry": {
    "PlannerModel": "gpt-4o",
    "ExecutorModel": "gpt-4o-mini",
    "EvaluatorModel": "gpt-4o-mini"
  }
}
```

## Structured Output (Experimental)

Structured output uses JSON schemas to enforce response format.

### Enabling Structured Output

```json
{
  "Agent": {
    "UseStructuredOutput": true
  }
}
```

### Requirements

- Model must support `response_format` with `json_schema` type
- Currently supported on: `gpt-4o`, `gpt-4o-mini`, `gpt-4-turbo` (2024-08-06+)

### Benefits

- Guaranteed JSON format compliance
- Reduced parsing errors
- Faster response processing

### Limitations

- Not all models support this feature
- May increase latency slightly
- Schemas are tightly coupled to response format

## Workspace Configuration

### Default Structure

```
workspace/
├── .venv/              # Virtual environment (auto-created)
├── scripts/            # Generated Python scripts
│   └── *.py
└── output/             # Script outputs
    └── *.txt, *.json, *.csv, etc.
```

### Custom Workspace Location

```json
{
  "Agent": {
    "WorkspaceDirectory": "/custom/path/to/workspace"
  }
}
```

**Best Practices**:
- Use absolute paths for production
- Ensure the directory is writable
- Consider disk space for large outputs
- Clean up workspace periodically

## Timeout Configuration

### Script Execution Timeout

Controls maximum time for a single Python script:

```json
{
  "Agent": {
    "RunTimeoutSeconds": 300
  }
}
```

**Recommendations**:
- Short tasks: 60-120 seconds
- Data processing: 300-600 seconds
- Long-running jobs: 900+ seconds

### Package Installation Timeout

Controls maximum time for pip operations:

```json
{
  "PythonEnvironment": {
    "PipTimeoutSeconds": 120
  }
}
```

**Recommendations**:
- Fast network: 60-120 seconds
- Slow network or large packages: 180-300 seconds

## Iteration Limits

### Max Iterations

Prevents infinite loops in the workflow:

```json
{
  "Agent": {
    "MaxIterations": 10
  }
}
```

**Calculation**: 
```
MaxIterations = (MaxStepsPerPlan * MaxRetryAttempts) + Buffer
```

**Example**: For 5 steps with 3 retries each, use at least 15 iterations.

### Max Steps Per Plan

Limits plan complexity:

```json
{
  "Agent": {
    "MaxStepsPerPlan": 20
  }
}
```

**Recommendations**:
- Simple tasks: 5-10 steps
- Complex tasks: 15-20 steps
- Very complex tasks: Consider breaking into subtasks

## Default Packages

Install common packages in every virtual environment:

```json
{
  "PythonEnvironment": {
    "DefaultPackages": [
      "requests",
      "pandas",
      "beautifulsoup4"
    ]
  }
}
```

**Benefits**:
- Faster execution (packages pre-installed)
- Consistent environment

**Drawbacks**:
- Longer initial setup
- Increased disk space
- May install unused packages

## Configuration Validation

The system validates configuration on startup:

```csharp
try
{
    host.Services.ValidateAgentConfiguration();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Configuration Error: {ex.Message}");
}
```

**Validated Items**:
- Azure AI Foundry URL is set
- Workspace directory is accessible
- Python executable is available
- Models are specified

## Troubleshooting Configuration

### Error: "AzureAIFoundry:Url is not configured"

**Solution**: Set the URL using user-secrets or environment variables
```bash
dotnet user-secrets set "AzureAIFoundry:Url" "your-url"
```

### Error: "Python executable not found"

**Solution**: Ensure Python is in PATH or specify full path
```json
{
  "PythonEnvironment": {
    "PythonExecutable": "/usr/bin/python3"
  }
}
```

### Error: "Failed to create virtual environment"

**Solution**: Verify Python venv module is installed
```bash
python -m venv --help
# If error, install venv
sudo apt-get install python3-venv  # Linux
```

### Error: "Authentication failed"

**Solution**: Log in to Azure
```bash
az login
# Or set service principal credentials
export AZURE_TENANT_ID="..."
export AZURE_CLIENT_ID="..."
export AZURE_CLIENT_SECRET="..."
```

## Production Configuration

### Recommended Settings

```json
{
  "AzureAIFoundry": {
    "Url": "from-environment-variable",
    "PlannerModel": "gpt-4o",
    "ExecutorModel": "gpt-4o-mini",
    "EvaluatorModel": "gpt-4o-mini"
  },
  "Agent": {
    "MaxRetryAttempts": 3,
    "MaxIterations": 15,
    "MaxStepsPerPlan": 20,
    "WorkspaceDirectory": "/app/workspace",
    "RunTimeoutSeconds": 600,
    "UseStructuredOutput": false
  },
  "PythonEnvironment": {
    "PythonExecutable": "python3",
    "VenvName": ".venv",
    "PipTimeoutSeconds": 180,
    "DefaultPackages": []
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

### Environment-Specific Configuration

Use different appsettings files:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides

The system automatically loads the appropriate file based on `DOTNET_ENVIRONMENT`.
