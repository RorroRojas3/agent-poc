# Troubleshooting Guide

## Common Issues and Solutions

### Configuration Issues

#### Error: "AzureAIFoundry:Url is not configured"

**Symptoms**: Application fails to start with configuration error.

**Cause**: Azure AI Foundry URL not set in user secrets or environment variables.

**Solutions**:

1. **Set user secrets** (Development):
   ```bash
   cd RR.Agent
   dotnet user-secrets set "AzureAIFoundry:Url" "https://your-resource.services.ai.azure.com/api/projects/your-project"
   ```

2. **Set environment variable** (Production):
   ```bash
   export AzureAIFoundry__Url="https://your-resource.services.ai.azure.com/api/projects/your-project"
   ```

3. **Verify configuration**:
   ```bash
   dotnet user-secrets list
   ```

#### Error: "Python executable not found"

**Symptoms**: Failed to initialize Python environment.

**Cause**: Python not in PATH or wrong executable name.

**Solutions**:

1. **Check Python installation**:
   ```bash
   python --version
   python3 --version
   ```

2. **Update configuration** in `appsettings.json`:
   ```json
   {
     "PythonEnvironment": {
       "PythonExecutable": "python3"
     }
   }
   ```

3. **Use full path** if Python not in PATH:
   ```json
   {
     "PythonEnvironment": {
       "PythonExecutable": "/usr/bin/python3"
     }
   }
   ```

#### Error: "Failed to create virtual environment"

**Symptoms**: Initialization fails when creating venv.

**Cause**: Python venv module not installed.

**Solutions**:

**Linux/Ubuntu**:
```bash
sudo apt-get update
sudo apt-get install python3-venv
```

**macOS**:
```bash
# venv included with Python 3.3+
# If missing, reinstall Python
brew reinstall python
```

**Windows**:
```bash
# venv included with Python installer
# Ensure "pip" and "tcl/tk" were selected during installation
# Reinstall Python if needed
```

### Authentication Issues

#### Error: "Authentication failed" or "401 Unauthorized"

**Symptoms**: Cannot connect to Azure AI Foundry.

**Cause**: Not authenticated with Azure or missing permissions.

**Solutions**:

1. **Log in to Azure**:
   ```bash
   az login
   ```

2. **Verify account**:
   ```bash
   az account show
   ```

3. **Set service principal** (for CI/CD):
   ```bash
   export AZURE_TENANT_ID="your-tenant-id"
   export AZURE_CLIENT_ID="your-client-id"
   export AZURE_CLIENT_SECRET="your-client-secret"
   ```

4. **Check permissions**: Ensure your account has access to the Azure AI Foundry project.

#### Error: "Resource not found" or "404 Not Found"

**Symptoms**: Cannot find Azure AI Foundry project.

**Cause**: Incorrect project URL or missing deployment.

**Solutions**:

1. **Verify URL format**:
   ```
   https://<resource-name>.services.ai.azure.com/api/projects/<project-name>
   ```

2. **Check project exists** in Azure portal

3. **Verify model deployments** are active

### Execution Issues

#### Error: "Max iterations reached"

**Symptoms**: Workflow stops with "Max iterations reached" message.

**Cause**: Task is too complex or stuck in retry loop.

**Solutions**:

1. **Increase max iterations** in `appsettings.json`:
   ```json
   {
     "Agent": {
       "MaxIterations": 20
     }
   }
   ```

2. **Simplify the task**: Break into smaller subtasks

3. **Review execution logs** to identify stuck steps

4. **Check for replanning loops**: Agent may be stuck trying different approaches

#### Error: "Script execution timed out"

**Symptoms**: Python script doesn't complete within timeout.

**Cause**: Script takes longer than `RunTimeoutSeconds`.

**Solutions**:

1. **Increase timeout** in `appsettings.json`:
   ```json
   {
     "Agent": {
       "RunTimeoutSeconds": 600
     }
   }
   ```

2. **Optimize the script**: Review generated script for inefficiencies

3. **Check for infinite loops** in generated code

4. **For long-running tasks**: Consider breaking into steps

#### Error: "Package installation failed"

**Symptoms**: Pip fails to install required packages.

**Cause**: Network issues, package doesn't exist, or timeout.

**Solutions**:

1. **Increase pip timeout**:
   ```json
   {
     "PythonEnvironment": {
       "PipTimeoutSeconds": 300
     }
   }
   ```

2. **Check package name**: Ensure spelling is correct

3. **Test manual installation**:
   ```bash
   workspace/.venv/bin/pip install <package-name>
   ```

4. **Check network connectivity**: Ensure PyPI is accessible

5. **Use package index mirror** if PyPI is blocked:
   ```bash
   pip install --index-url https://pypi.org/simple <package-name>
   ```

### Planning Issues

#### Issue: Plan is too simple/complex

**Symptoms**: Generated plan doesn't match task complexity.

**Cause**: Ambiguous task description or model limitations.

**Solutions**:

1. **Provide more context** in task description:
   - ✓ "Read sales.csv with columns date, product, amount and calculate monthly totals"
   - ✗ "Analyze sales data"

2. **Be specific about expected output**:
   - ✓ "Save results to a JSON file with monthly summaries"
   - ✗ "Show me the results"

3. **Use better model for planning**:
   ```json
   {
     "AzureAIFoundry": {
       "PlannerModel": "gpt-4o"
     }
   }
   ```

#### Issue: Steps fail repeatedly

**Symptoms**: Same step fails multiple times despite retries.

**Cause**: Incorrect approach, missing dependencies, or impossible task.

**Solutions**:

1. **Review failure messages** in execution summary

2. **Check generated scripts** in `workspace/scripts/`

3. **Verify input data**:
   - File exists and is accessible
   - File format matches expectations
   - Data is not corrupted

4. **Simplify the task**:
   - Break into smaller steps
   - Test each step independently

5. **Provide example output** in task description

### File Access Issues

#### Error: "File not found"

**Symptoms**: Agent cannot find external file.

**Cause**: Incorrect path, file doesn't exist, or permission issues.

**Solutions**:

1. **Use absolute paths**: `C:\Users\YourName\data.csv`

2. **Check file exists**:
   ```bash
   ls -la /path/to/file  # Linux/Mac
   dir C:\path\to\file   # Windows
   ```

3. **Check permissions**: Ensure file is readable

4. **Use forward slashes** for cross-platform compatibility:
   - ✓ `C:/Users/YourName/data.csv`
   - ✗ `C:\Users\YourName\data.csv` (may fail on non-Windows)

#### Error: "Permission denied"

**Symptoms**: Cannot read or write files.

**Cause**: Insufficient permissions.

**Solutions**:

1. **Check workspace permissions**:
   ```bash
   ls -la workspace/
   ```

2. **Verify file permissions**:
   ```bash
   chmod 644 file.txt  # Linux/Mac
   ```

3. **Run with appropriate user**: Don't run as root unless necessary

4. **Check antivirus**: May block script execution

### Python Script Issues

#### Error: "ModuleNotFoundError"

**Symptoms**: Python script fails with module not found.

**Cause**: Package not installed in virtual environment.

**Solutions**:

1. **Check required packages** are listed in plan

2. **Manual installation**:
   ```bash
   workspace/.venv/bin/pip install <package-name>
   ```

3. **Verify venv is being used**:
   ```bash
   workspace/.venv/bin/python --version
   ```

4. **Recreate virtual environment**:
   ```bash
   rm -rf workspace/.venv
   # Run application again to recreate
   ```

#### Error: "SyntaxError" in generated script

**Symptoms**: Python script has syntax errors.

**Cause**: Model generated invalid Python code.

**Solutions**:

1. **Review generated script** in `workspace/scripts/`

2. **Retry the task**: May generate different code

3. **Use better model** for executor:
   ```json
   {
     "AzureAIFoundry": {
       "ExecutorModel": "gpt-4o"
     }
   }
   ```

4. **Simplify the task**: Reduce complexity

5. **Report issue** if problem persists

### Performance Issues

#### Issue: Slow execution

**Symptoms**: Tasks take very long to complete.

**Cause**: Multiple factors - model latency, large files, complex processing.

**Solutions**:

1. **Use faster models** for non-critical steps:
   ```json
   {
     "AzureAIFoundry": {
       "ExecutorModel": "gpt-4o-mini",
       "EvaluatorModel": "gpt-4o-mini"
     }
   }
   ```

2. **Reduce file sizes**: Process smaller datasets first

3. **Optimize task description**: Be more specific to reduce planning time

4. **Enable structured output** (if supported):
   ```json
   {
     "Agent": {
       "UseStructuredOutput": true
     }
   }
   ```

5. **Monitor network latency** to Azure AI Foundry

#### Issue: High cost

**Symptoms**: Azure AI costs are higher than expected.

**Cause**: Using expensive models for all agents.

**Solutions**:

1. **Use model tiers** appropriately:
   ```json
   {
     "AzureAIFoundry": {
       "PlannerModel": "gpt-4o",
       "ExecutorModel": "gpt-4o-mini",
       "EvaluatorModel": "gpt-4o-mini"
     }
   }
   ```

2. **Optimize prompts**: Shorter prompts = fewer tokens

3. **Reduce retries** if acceptable:
   ```json
   {
     "Agent": {
       "MaxRetryAttempts": 2
     }
   }
   ```

4. **Cache results**: Reuse workspace/venv between runs

## Debugging Techniques

### Enable Debug Logging

In `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "RR.Agent": "Debug"
    }
  }
}
```

### Inspect Workflow State

Add custom logging in `AgentWorkflow.cs`:
```csharp
_logger.LogDebug("State: Step {Current}/{Total}, Status: {Status}",
    plan.CurrentStepNumber, plan.Steps.Count, plan.Status);
```

### Review Generated Files

Check workspace contents:
```bash
# Scripts
ls -la workspace/scripts/
cat workspace/scripts/*.py

# Outputs
ls -la workspace/output/
cat workspace/output/*.txt

# Virtual environment
workspace/.venv/bin/pip list
```

### Test Python Scripts Manually

```bash
# Activate venv
source workspace/.venv/bin/activate  # Linux/Mac
workspace\.venv\Scripts\activate     # Windows

# Run script
python workspace/scripts/script.py

# Deactivate
deactivate
```

### Use Breakpoints

In Visual Studio or VS Code:
1. Set breakpoints in executors
2. Start debugging (F5)
3. Inspect variables and state
4. Step through execution

## Getting Help

### Collect Diagnostic Information

When reporting issues, include:

1. **Configuration**:
   ```bash
   dotnet --version
   python --version
   cat RR.Agent/appsettings.json
   ```

2. **Error messages**: Full error text and stack trace

3. **Execution summary**: Copy output from application

4. **Generated scripts**: Contents of failing scripts

5. **Environment**:
   - OS and version
   - .NET SDK version
   - Python version
   - Azure AI Foundry region

### Resources

- **GitHub Issues**: https://github.com/RorroRojas3/agent-poc/issues
- **Azure AI Foundry Docs**: https://learn.microsoft.com/azure/ai-services/
- **.NET Documentation**: https://docs.microsoft.com/dotnet/
- **Python Documentation**: https://docs.python.org/

### Common Error Patterns

| Error Pattern | Likely Cause | Quick Fix |
|---------------|--------------|-----------|
| "not configured" | Missing configuration | Set user secrets or env vars |
| "not found" | Missing file/resource | Check paths and existence |
| "permission denied" | Access issues | Check file/directory permissions |
| "timeout" | Operation too slow | Increase timeout settings |
| "authentication" | Auth failure | Run `az login` |
| "max iterations" | Stuck in loop | Simplify task or increase limit |
| "ModuleNotFoundError" | Missing package | Check required packages |
| "SyntaxError" | Invalid Python | Retry or use better model |
