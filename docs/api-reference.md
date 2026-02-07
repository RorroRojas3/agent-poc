# API Reference

## Table of Contents

- [Core Services](#core-services)
- [Executors](#executors)
- [Python Services](#python-services)
- [Tool Services](#tool-services)
- [Data Models](#data-models)
- [Configuration Options](#configuration-options)

## Core Services

### AgentWorkflow

Orchestrates the multi-agent workflow for task execution.

**Namespace**: `RR.Agent.Service.Workflows`

#### Methods

##### ExecuteAsync

Executes the complete workflow for a given task.

```csharp
public async Task<WorkflowContext> ExecuteAsync(
    string task, 
    CancellationToken cancellationToken = default)
```

**Parameters**:
- `task` (string): The user task description
- `cancellationToken` (CancellationToken): Optional cancellation token

**Returns**: `WorkflowContext` containing the final execution state

**Events**:
- `StateChanged`: Raised when workflow state changes

**Example**:
```csharp
var workflow = serviceProvider.GetRequiredService<AgentWorkflow>();

workflow.StateChanged += (sender, e) => 
{
    Console.WriteLine($"[{e.State}] {e.Message}");
};

var result = await workflow.ExecuteAsync("Create a Python script", cancellationToken);
```

### AgentService

Manages communication with Azure AI Foundry agents.

**Namespace**: `RR.Agent.Service.Agents`

#### Methods

##### CreateThreadAsync

Creates a new agent thread for conversation.

```csharp
public async Task<string> CreateThreadAsync(
    AgentRole role,
    CancellationToken cancellationToken = default)
```

**Parameters**:
- `role` (AgentRole): The agent role (Planner, Executor, Evaluator)
- `cancellationToken` (CancellationToken): Optional cancellation token

**Returns**: Thread ID as string

##### SendMessageAsync

Sends a message to an agent and retrieves the response.

```csharp
public async Task<string> SendMessageAsync(
    string threadId,
    string message,
    AgentRole role,
    IEnumerable<ToolDefinition>? tools = null,
    bool useStructuredOutput = false,
    string? responseSchema = null,
    CancellationToken cancellationToken = default)
```

**Parameters**:
- `threadId` (string): The thread ID
- `message` (string): Message content
- `role` (AgentRole): Agent role
- `tools` (IEnumerable<ToolDefinition>?): Available tools
- `useStructuredOutput` (bool): Enable JSON schema validation
- `responseSchema` (string?): JSON schema for structured output
- `cancellationToken` (CancellationToken): Optional cancellation token

**Returns**: Agent response as string

##### CleanupAsync

Cleans up agent resources.

```csharp
public async Task CleanupAsync(CancellationToken cancellationToken = default)
```

## Executors

### PlannerExecutor

Breaks down tasks into executable steps.

**Namespace**: `RR.Agent.Service.Executors`

#### Methods

##### ExecuteAsync

Creates a task plan from user input.

```csharp
public async Task<PlannerOutput> ExecuteAsync(
    PlannerInput input,
    CancellationToken cancellationToken = default)
```

**Input Properties**:
- `Task` (string): User task description
- `Context` (ExecutionContext?): Optional previous execution context
- `PreviousEvaluation` (EvaluationResult?): Previous evaluation for retries
- `IsRetry` (bool): Whether this is a retry attempt

**Output Properties**:
- `Success` (bool): Whether planning succeeded
- `Plan` (TaskPlan): The generated plan
- `Context` (ExecutionContext): Updated execution context
- `Error` (string?): Error message if failed

### CodeExecutor

Executes task steps using available tools.

**Namespace**: `RR.Agent.Service.Executors`

#### Methods

##### ExecuteAsync

Executes a single task step.

```csharp
public async Task<CodeExecutorOutput> ExecuteAsync(
    CodeExecutorInput input,
    CancellationToken cancellationToken = default)
```

**Input Properties**:
- `Context` (ExecutionContext): Current execution context
- `Step` (TaskStep): Step to execute

**Output Properties**:
- `Context` (ExecutionContext): Updated execution context
- `ToolResponse` (ToolResponseDto): Tool execution response
- `ExecutionResult` (PythonExecutionResult?): Python execution result (if applicable)

### EvaluatorExecutor

Evaluates execution results.

**Namespace**: `RR.Agent.Service.Executors`

#### Methods

##### ExecuteAsync

Evaluates the results of a step execution.

```csharp
public async Task<EvaluatorOutput> ExecuteAsync(
    EvaluatorInput input,
    CancellationToken cancellationToken = default)
```

**Input Properties**:
- `Context` (ExecutionContext): Current execution context
- `Step` (TaskStep): Executed step
- `ToolResponse` (ToolResponseDto): Tool response
- `ExecutionResult` (PythonExecutionResult?): Python execution result

**Output Properties**:
- `Evaluation` (EvaluationResult): Evaluation result
- `Context` (ExecutionContext): Updated execution context
- `IsTaskComplete` (bool): Whether task is complete
- `ShouldContinue` (bool): Whether to continue execution
- `NeedsReplan` (bool): Whether to create new plan

## Python Services

### IPythonEnvironmentService

Manages Python virtual environments.

**Namespace**: `RR.Agent.Service.Python`

#### Methods

##### InitializeEnvironmentAsync

Initializes Python virtual environment.

```csharp
Task<bool> InitializeEnvironmentAsync(
    string workspacePath,
    CancellationToken cancellationToken = default)
```

##### InstallPackageAsync

Installs a single Python package.

```csharp
Task<bool> InstallPackageAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

##### InstallPackagesAsync

Installs multiple Python packages.

```csharp
Task<bool> InstallPackagesAsync(
    IEnumerable<string> packages,
    CancellationToken cancellationToken = default)
```

##### IsEnvironmentReadyAsync

Checks if environment is ready.

```csharp
Task<bool> IsEnvironmentReadyAsync(CancellationToken cancellationToken = default)
```

##### GetVenvPythonPath

Gets path to Python executable in venv.

```csharp
string GetVenvPythonPath()
```

##### GetWorkspacePath / GetVenvPath / GetScriptsPath / GetOutputPath

Get various workspace directory paths.

```csharp
string GetWorkspacePath()
string GetVenvPath()
string GetScriptsPath()
string GetOutputPath()
```

### IPythonScriptExecutor

Executes Python scripts.

**Namespace**: `RR.Agent.Service.Python`

#### Methods

##### ExecuteScriptAsync

Executes a Python script file.

```csharp
Task<PythonExecutionResult> ExecuteScriptAsync(
    string scriptPath,
    int timeoutSeconds,
    CancellationToken cancellationToken = default)
```

##### ExecuteCodeAsync

Executes Python code directly.

```csharp
Task<PythonExecutionResult> ExecuteCodeAsync(
    string code,
    int timeoutSeconds,
    CancellationToken cancellationToken = default)
```

## Tool Services

### FileToolService

Handles workspace file operations.

**Namespace**: `RR.Agent.Service.Tools`

#### Methods

##### WriteFileAsync

Writes content to a file.

```csharp
Task<ToolResponseDto> WriteFileAsync(
    string content,
    string? filename = null,
    CancellationToken cancellationToken = default)
```

##### ReadFileAsync

Reads file content.

```csharp
Task<ToolResponseDto> ReadFileAsync(
    string filename,
    CancellationToken cancellationToken = default)
```

##### ListFilesAsync

Lists files in workspace.

```csharp
Task<ToolResponseDto> ListFilesAsync(CancellationToken cancellationToken = default)
```

##### FindFilesAsync

Searches for files by pattern.

```csharp
Task<ToolResponseDto> FindFilesAsync(
    string pattern,
    CancellationToken cancellationToken = default)
```

##### ReadExternalFileAsync

Reads a file from outside workspace.

```csharp
Task<ToolResponseDto> ReadExternalFileAsync(
    string filePath,
    CancellationToken cancellationToken = default)
```

##### CopyToWorkspaceAsync

Copies external file to workspace.

```csharp
Task<ToolResponseDto> CopyToWorkspaceAsync(
    string sourcePath,
    string? destinationFilename = null,
    CancellationToken cancellationToken = default)
```

### PythonToolService

Handles Python execution operations.

**Namespace**: `RR.Agent.Service.Tools`

#### Methods

##### ExecutePythonAsync

Executes Python code.

```csharp
Task<ToolResponseDto> ExecutePythonAsync(
    string code,
    int? timeoutSeconds = null,
    CancellationToken cancellationToken = default)
```

##### ExecuteScriptFileAsync

Executes a Python script file.

```csharp
Task<ToolResponseDto> ExecuteScriptFileAsync(
    string filename,
    int? timeoutSeconds = null,
    CancellationToken cancellationToken = default)
```

##### InstallPackageAsync

Installs a Python package.

```csharp
Task<ToolResponseDto> InstallPackageAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

## Data Models

### TaskPlan

Represents a complete task execution plan.

**Namespace**: `RR.Agent.Model.Dtos`

**Properties**:
- `OriginalTask` (string): Original task description
- `Steps` (List<TaskStep>): Ordered list of steps
- `RequiredPackages` (List<string>): Packages needed for plan
- `Status` (TaskStatuses): Overall plan status
- `CurrentStepNumber` (int): Current step number (1-indexed)
- `CompletedStepsCount` (int): Number of completed steps
- `TotalIterations` (int): Total workflow iterations

**Methods**:
- `CurrentStep`: Gets the current step
- `NextStep`: Gets the next step
- `MarkStepComplete`: Marks a step as complete
- `MarkStepFailed`: Marks a step as failed

### TaskStep

Represents a single execution step.

**Namespace**: `RR.Agent.Model.Dtos`

**Properties**:
- `StepNumber` (int): Step number (1-indexed)
- `Description` (string): Step description
- `ExpectedOutput` (string?): Expected output description
- `RequiredPackages` (List<string>): Packages needed for step
- `Status` (TaskStatuses): Step status
- `AttemptCount` (int): Number of execution attempts
- `Evaluation` (EvaluationResult?): Latest evaluation result

### EvaluationResult

Represents evaluation of a step execution.

**Namespace**: `RR.Agent.Model.Dtos`

**Properties**:
- `IsSuccessful` (bool): Whether execution succeeded
- `ConfidenceScore` (double): Confidence in evaluation (0-1)
- `Reasoning` (string): Explanation of evaluation
- `Issues` (List<string>): List of identified issues
- `SuggestedFix` (string?): Suggested fix for issues
- `RevisedApproach` (string?): Alternative approach suggestion
- `IsImpossible` (bool): Whether task is impossible

### PythonExecutionResult

Represents Python script execution result.

**Namespace**: `RR.Agent.Model.Dtos`

**Properties**:
- `StandardOutput` (string): stdout content
- `StandardError` (string): stderr content
- `ExitCode` (int): Process exit code
- `TimedOut` (bool): Whether execution timed out
- `ExecutionTimeMs` (long): Execution time in milliseconds

### ToolResponseDto

Represents tool execution response.

**Namespace**: `RR.Agent.Model.Dtos`

**Properties**:
- `Result` (string): Result description
- `Output` (string): Tool output
- `HasWrittenFile` (bool): Whether file was written
- `FilePath` (string?): Written file path
- `Filename` (string?): Written filename
- `HasExecutedScript` (bool): Whether script was executed
- `ScriptExitCode` (int): Script exit code
- `ScriptStandardOutput` (string?): Script stdout
- `ScriptStandardInput` (string?): Script code/input
- `Errors` (List<string>): List of errors

## Configuration Options

### AgentOptions

Core agent workflow configuration.

**Namespace**: `RR.Agent.Model.Options`

**Properties**:
- `MaxRetryAttempts` (int): Maximum retry attempts per step (default: 3)
- `MaxIterations` (int): Maximum workflow iterations (default: 10)
- `MaxStepsPerPlan` (int): Maximum steps in a plan (default: 20)
- `WorkspaceDirectory` (string): Workspace path (default: "./workspace")
- `RunTimeoutSeconds` (int): Script timeout (default: 300)
- `UseStructuredOutput` (bool): Enable structured output (default: false)

### AzureAIFoundryOptions

Azure AI Foundry configuration.

**Namespace**: `RR.Agent.Model.Options`

**Properties**:
- `Url` (string): Azure AI Foundry project URL
- `DefaultModel` (string): Default model name
- `PlannerModel` (string?): Planner-specific model
- `ExecutorModel` (string?): Executor-specific model
- `EvaluatorModel` (string?): Evaluator-specific model

### PythonEnvironmentOptions

Python environment configuration.

**Namespace**: `RR.Agent.Model.Options`

**Properties**:
- `PythonExecutable` (string): Python command (default: "python")
- `VenvName` (string): Venv directory name (default: ".venv")
- `ScriptsDirectory` (string): Scripts directory (default: "scripts")
- `OutputDirectory` (string): Output directory (default: "output")
- `PipTimeoutSeconds` (int): Pip timeout (default: 120)
- `DefaultPackages` (List<string>): Packages to pre-install (default: [])

## Enumerations

### TaskStatuses

Task/step status enumeration.

**Namespace**: `RR.Agent.Model.Enums`

**Values**:
- `Pending`: Not yet started
- `InProgress`: Currently executing
- `Completed`: Successfully completed
- `Failed`: Failed execution
- `Impossible`: Cannot be completed

### AgentRole

Agent role enumeration.

**Namespace**: `RR.Agent.Model.Enums`

**Values**:
- `Planner`: Task planning agent
- `Executor`: Code execution agent
- `Evaluator`: Result evaluation agent
