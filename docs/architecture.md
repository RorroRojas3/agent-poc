# Architecture

## Overview

Agent POC is a multi-agent task execution system built on .NET 10 and Microsoft Agent Framework. The system employs a Plan-Execute-Evaluate loop to autonomously break down complex tasks, generate Python code, and iteratively solve problems.

## System Architecture

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

## Core Components

### 1. AgentWorkflow

**Location**: `RR.Agent.Service/Workflows/AgentWorkflow.cs`

The orchestrator that coordinates the entire multi-agent workflow. It manages the lifecycle of task execution through distinct phases:

- **Initialization**: Sets up Python virtual environment
- **Planning**: Breaks down tasks into executable steps
- **Execution**: Runs Python scripts and file operations
- **Evaluation**: Assesses results and determines next actions
- **Iteration**: Retries or replans based on evaluation

**Key Responsibilities**:
- Manages workflow state transitions
- Coordinates between executors
- Handles package installation
- Enforces safety limits (max iterations, timeouts)
- Emits state change events for UI updates

### 2. Planner Executor

**Location**: `RR.Agent.Service/Executors/PlannerExecutor.cs`

Analyzes user requests and decomposes them into discrete, executable steps.

**Core Principles**:
- **Atomic steps**: Each step accomplishes one logical unit of work
- **Self-contained**: Steps produce verifiable output
- **Fail-fast**: Steps ordered to surface failures early
- **Simplicity first**: Uses simplest approach that works

**Input**: User task description, optional context from previous attempts
**Output**: `TaskPlan` with ordered steps, required packages, and expected outputs

### 3. Code Executor

**Location**: `RR.Agent.Service/Executors/CodeExecutor.cs`

Executes individual task steps using available tools.

**Capabilities**:
- Write files to workspace
- Execute Python scripts
- Install Python packages
- Read external files
- Search for files by pattern

**Decision Framework**:
- Analyzes tasks to determine if tools are needed
- Selects appropriate tool(s) for the task
- Handles tool failures and retries
- Provides structured responses

### 4. Evaluator Executor

**Location**: `RR.Agent.Service/Executors/EvaluatorExecutor.cs`

Assesses execution results and determines success or failure.

**Evaluation Types**:
- **Direct responses**: Validates text-only outputs
- **File operations**: Confirms file creation/modification
- **Script execution**: Analyzes stdout, stderr, exit codes

**Outcomes**:
- **Success**: Step completed, continue to next
- **Retry**: Step failed, retry with same approach
- **Replan**: Multiple failures, suggest alternative approach
- **Impossible**: Task cannot be completed

**Confidence Scoring**:
- 0.9-1.0: Very confident, clear success/failure
- 0.7-0.9: Reasonably confident, minor uncertainties
- 0.5-0.7: Moderate confidence, some ambiguity
- Below 0.5: Low confidence, suggest human review

### 5. Agent Service

**Location**: `RR.Agent.Service/Agents/AgentService.cs`

Manages communication with Azure AI Foundry persistent agents.

**Responsibilities**:
- Creates and maintains agent threads
- Sends messages and tool calls
- Handles structured output (JSON schemas)
- Manages agent cleanup

### 6. Python Environment Service

**Location**: `RR.Agent.Service/Python/PythonEnvironmentService.cs`

Manages Python virtual environments and package installation.

**Features**:
- Creates isolated virtual environments
- Installs packages via pip
- Upgrades pip automatically
- Handles cross-platform paths (Windows/Linux/macOS)
- Maintains workspace directories

**Directory Structure**:
```
workspace/
├── .venv/              # Python virtual environment
├── scripts/            # Generated Python scripts
└── output/             # Script outputs and artifacts
```

### 7. Python Script Executor

**Location**: `RR.Agent.Service/Python/PythonScriptExecutor.cs`

Executes Python code in the virtual environment.

**Execution Modes**:
- **Direct code**: Execute Python code strings
- **Script files**: Execute .py files from workspace
- **Timeout handling**: Enforces execution time limits
- **Output capture**: Captures stdout/stderr

## Data Flow

### 1. Initial Planning Phase

```
User Task
    ↓
PlannerExecutor
    ↓
TaskPlan (with Steps)
    ↓
Package Installation
```

### 2. Execution Loop

```
Current Step
    ↓
CodeExecutor (executes using tools)
    ↓
Tool Response + Execution Results
    ↓
EvaluatorExecutor
    ↓
Evaluation Decision
    ├─ Success → Next Step
    ├─ Retry → Same Step (increment attempt)
    ├─ Replan → PlannerExecutor (create new plan)
    └─ Impossible → Mark Failed
```

### 3. Completion

```
All Steps Completed OR Max Iterations Reached
    ↓
Agent Cleanup
    ↓
WorkflowContext (final results)
```

## State Management

### Workflow States

| State | Description |
|-------|-------------|
| `Initializing` | Setting up Python environment |
| `Planning` | Creating execution plan |
| `Installing` | Installing required packages |
| `Executing` | Running task step |
| `Evaluating` | Assessing execution results |
| `Replanning` | Creating revised plan |
| `Completed` | Task completed successfully |
| `Failed` | Task failed |
| `Impossible` | Task determined impossible |
| `Stopped` | Workflow manually stopped |

### Task Statuses

| Status | Description |
|--------|-------------|
| `Pending` | Step not yet started |
| `InProgress` | Step currently executing |
| `Completed` | Step completed successfully |
| `Failed` | Step failed, may retry |
| `Impossible` | Step cannot be completed |

## Tool Architecture

### Available Tools

Tools are defined in `RR.Agent.Service/Tools/ToolDefinitions.cs` and implemented in tool services.

#### Workspace Operations
- `write_file`: Write content to workspace files
- `read_file`: Read files from workspace
- `list_files`: List workspace files

#### Python Execution
- `execute_python`: Execute Python code directly
- `execute_script_file`: Execute existing .py files
- `install_package`: Install pip packages

#### File System Access
- `find_files`: Search for files by pattern (supports wildcards)
- `read_external_file`: Read files from any path
- `copy_to_workspace`: Copy external files to workspace

### Tool Handler

**Location**: `RR.Agent.Service/Tools/ToolHandler.cs`

Processes tool calls from agents and delegates to appropriate services:
- **FileToolService**: Handles workspace file operations
- **PythonToolService**: Handles Python execution and package management

## Configuration

### Options Classes

Located in `RR.Agent.Model/Options/`:

- **AgentOptions**: Core workflow settings (max retries, iterations, timeouts)
- **AzureAIFoundryOptions**: Azure AI Foundry connection and model configuration
- **PythonEnvironmentOptions**: Python environment settings

### Dependency Injection

All services are registered in `RR.Agent.Service/Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<AgentService>();
services.AddSingleton<PlannerExecutor>();
services.AddSingleton<CodeExecutor>();
services.AddSingleton<EvaluatorExecutor>();
services.AddSingleton<AgentWorkflow>();
services.AddSingleton<IPythonEnvironmentService, PythonEnvironmentService>();
services.AddSingleton<IPythonScriptExecutor, PythonScriptExecutor>();
```

## Error Handling

### Retry Strategy

1. **Step-level retries**: Each step can retry up to `MaxRetryAttempts` (default: 3)
2. **Replanning**: After multiple failures, evaluator suggests replanning
3. **Impossible detection**: After exhausting retries and replans, mark as impossible
4. **Iteration limits**: Workflow stops after `MaxIterations` to prevent infinite loops

### Failure Recovery

- **Transient failures**: Automatic retry with same approach
- **Persistent failures**: Replan with alternative approach
- **Systemic failures**: Mark task as impossible after exhausting options

## Extensibility

### Adding New Tools

1. Define tool schema in `ToolDefinitions.cs`
2. Implement tool logic in appropriate service
3. Update `ToolHandler.cs` to route tool calls
4. Update executor system prompts if needed

### Adding New Agent Roles

1. Create new executor class inheriting from base pattern
2. Define system prompt in `AgentPrompts.cs`
3. Register in dependency injection
4. Integrate into `AgentWorkflow.cs` orchestration

### Supporting New AI Providers

The architecture is designed to work with Azure AI Foundry, but can be extended:

1. Create new options class (e.g., `OpenAIOptions`)
2. Implement provider-specific client in `AgentService`
3. Handle provider-specific features (structured output, etc.)

## Performance Considerations

### Parallel Execution

Currently sequential; steps execute one at a time. Future enhancement: parallel execution of independent steps.

### Caching

- **Virtual environment**: Reused across runs
- **Installed packages**: Persist in venv
- **Agent threads**: Maintained during workflow

### Resource Limits

- **RunTimeoutSeconds**: Maximum time for single script execution (default: 300s)
- **MaxIterations**: Safety limit for workflow loops (default: 10)
- **PipTimeoutSeconds**: Timeout for package installation (default: 120s)

## Security Considerations

### Sandboxing

- Python scripts execute in isolated virtual environment
- File access controlled through tool permissions
- External file access requires explicit tool calls

### Input Validation

- File paths validated before operations
- Package names sanitized before installation
- Code execution limited to workspace directory

### Secret Management

- Azure credentials via user-secrets (development)
- Environment variables (production)
- No secrets in code or configuration files
