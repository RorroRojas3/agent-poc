namespace RR.Agent.Execution;

using System.Text;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Agent.Configuration;
using RR.Agent.Execution.Models;
using RR.Agent.Infrastructure;
using RR.Agent.Planning.Models;
using RR.Agent.Tools;

/// <summary>
/// Generates executable scripts from plan steps using AI.
/// </summary>
public sealed class ScriptGenerator : IScriptGenerator
{
    private const string ScriptGenerationPrompt = """
        You are a code generation agent that creates executable scripts.

        Generate a script that accomplishes the given task. Follow these guidelines:
        - Prefer Python unless the task specifically requires C# or .NET
        - Write clean, well-commented code
        - Include proper error handling
        - Print results to stdout for capture
        - If writing files, use clear filenames and print the path

        IMPORTANT - Finding Input Files:
        Input files are available in the current working directory. To find them:
        ```python
        import os
        # List all files in current directory
        for f in os.listdir('.'):
            print(f)
        # Files may have their original name or a modified name
        # Search for the file by partial name match or extension
        ```

        IMPORTANT - Package Installation:
        If your script requires non-standard Python packages (anything beyond the standard library),
        you MUST include package installation at the beginning of your script using subprocess:

        ```python
        import subprocess
        import sys

        def install_packages(packages):
            for package in packages:
                subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", package])

        # Install required packages
        install_packages(["pandas", "numpy"])  # List all required packages

        # Now import and use the packages
        import pandas as pd
        import numpy as np
        ```

        Common packages that need installation: pandas, numpy, matplotlib, seaborn, requests,
        beautifulsoup4, openpyxl, xlrd, scikit-learn, pillow, PyPDF2, pypdf, pymupdf (import as fitz),
        pdf2image, pdfplumber, etc.

        For PDF processing (always use CodeExecution, not FileRead/FileWrite):
        - pypdf (recommended): For reading/writing PDF, extracting pages, splitting/merging
        - CRITICAL: Always save output files directly in the script - print the output path when done
          ```python
          import os
          import subprocess
          import sys
          subprocess.check_call([sys.executable, "-m", "pip", "install", "-q", "pypdf"])
          from pypdf import PdfReader, PdfWriter

          # Find the PDF file dynamically
          pdf_file = None
          for f in os.listdir('.'):
              if f.endswith('.pdf'):
                  pdf_file = f
                  break
          if not pdf_file:
              raise FileNotFoundError("No PDF file found")
          print(f"Found PDF: {pdf_file}")

          reader = PdfReader(pdf_file)
          writer = PdfWriter()
          pages_to_extract = min(5, len(reader.pages))
          for i in range(pages_to_extract):
              writer.add_page(reader.pages[i])

          output_path = "output.pdf"
          with open(output_path, "wb") as f:
              writer.write(f)
          print(f"Successfully saved {pages_to_extract} pages to: {output_path}")
          ```
        - pymupdf (fitz): For advanced PDF manipulation, text extraction, rendering
        - pdfplumber: For extracting tables and text from PDFs

        Standard library modules (no installation needed): os, sys, json, csv, re, datetime,
        pathlib, collections, itertools, functools, math, random, shutil, etc.

        Respond with ONLY the script code wrapped in a code block. For example:
        ```python
        # Your Python code here
        ```
        or
        ```csharp
        // Your C# code here
        ```

        Do not include any explanation outside the code block.
        """;

    private readonly PersistentAgentsClient _client;
    private readonly AzureAIFoundryOptions _aiOptions;
    private readonly IToolProvider _toolProvider;
    private readonly IRunPoller _runPoller;
    private readonly IMessageProcessor _messageProcessor;
    private readonly ILogger<ScriptGenerator> _logger;

    private string? _generatorAgentId;

    public ScriptGenerator(
        PersistentAgentsClient client,
        IOptions<AzureAIFoundryOptions> aiOptions,
        IToolProvider toolProvider,
        IRunPoller runPoller,
        IMessageProcessor messageProcessor,
        ILogger<ScriptGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(aiOptions);
        ArgumentNullException.ThrowIfNull(toolProvider);
        ArgumentNullException.ThrowIfNull(runPoller);
        ArgumentNullException.ThrowIfNull(messageProcessor);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _aiOptions = aiOptions.Value;
        _toolProvider = toolProvider;
        _runPoller = runPoller;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    public async Task<ScriptInfo> GenerateScriptAsync(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Generating script for step {Order}: {Description}",
            step.Order,
            step.Description);

        await EnsureGeneratorAgentExistsAsync(cancellationToken);

        // Build the prompt with context from previous steps and input files
        var prompt = BuildGenerationPrompt(step, context, inputFiles);

        // Create a new thread for script generation
        var threadResponse = await _client.Threads.CreateThreadAsync();
        var thread = threadResponse.Value;

        await _client.Messages.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: prompt);

        var runResponse = await _client.Runs.CreateRunAsync(
            thread.Id,
            _generatorAgentId!);
        var run = runResponse.Value;

        var completedRun = await _runPoller.WaitForCompletionAsync(
            thread.Id,
            run.Id,
            cancellationToken);

        if (completedRun.Status != RunStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Script generation failed: {completedRun.LastError?.Message}");
        }

        var response = await _messageProcessor.GetLatestAssistantMessageAsync(
            thread.Id,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("No script generated");
        }

        var script = ParseScriptResponse(step.Order, response);

        // Inject file discovery code for Python scripts with input files
        if (inputFiles is { Count: > 0 } && script.Language.ToLowerInvariant() is "python" or "py")
        {
            script = InjectFileDiscoveryCode(script, inputFiles);
        }

        return script;
    }

    private static ScriptInfo InjectFileDiscoveryCode(ScriptInfo script, IReadOnlyList<InputFile> inputFiles)
    {
        // Build file patterns from input files
        var extensions = inputFiles
            .Select(f => Path.GetExtension(f.FileName).ToLowerInvariant())
            .Where(ext => !string.IsNullOrEmpty(ext))
            .Distinct()
            .ToList();

        var filenames = inputFiles
            .Select(f => Path.GetFileNameWithoutExtension(f.FileName).ToLowerInvariant())
            .Distinct()
            .ToList();

        var extensionCheck = extensions.Count > 0
            ? $"f.lower().endswith(({string.Join(", ", extensions.Select(e => $"'{e}'"))}))"
            : "False";

        var nameCheck = filenames.Count > 0
            ? $"any(name in f.lower() for name in [{string.Join(", ", filenames.Select(n => $"'{n}'"))}])"
            : "False";

        var preamble = $@"# === AUTO-INJECTED FILE DISCOVERY CODE ===
import os

# List all available files
print('Available files in current directory:')
_available_files = os.listdir('.')
for _f in _available_files:
    print(f'  - {{_f}}')

# Find input files by extension or name
_file_mapping = {{}}
for _input_file in {System.Text.Json.JsonSerializer.Serialize(inputFiles.Select(f => f.FileName).ToList())}:
    _ext = os.path.splitext(_input_file.lower())[1]
    _name = os.path.splitext(_input_file.lower())[0]

    # Search for matching file
    for _f in _available_files:
        if _f.lower().endswith(_ext) or _name in _f.lower():
            _file_mapping[_input_file] = _f
            print(f'Mapped {{_input_file}} -> {{_f}}')
            break

    if _input_file not in _file_mapping:
        print(f'Warning: Could not find file matching {{_input_file}}')

# Helper function to get actual filename
def get_actual_file(original_name):
    return _file_mapping.get(original_name, original_name)

# === END AUTO-INJECTED CODE ===

";
        // Now replace common hardcoded filename patterns in the script
        var modifiedContent = script.Content;

        foreach (var inputFile in inputFiles)
        {
            var filename = inputFile.FileName;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            // Replace hardcoded string literals with get_actual_file calls
            modifiedContent = modifiedContent
                .Replace($"\"{filename}\"", $"get_actual_file(\"{filename}\")")
                .Replace($"'{filename}'", $"get_actual_file('{filename}')")
                .Replace($"\"{nameWithoutExt}.pdf\"", $"get_actual_file(\"{filename}\")")
                .Replace($"'{nameWithoutExt}.pdf'", $"get_actual_file('{filename}')");
        }

        return script with { Content = preamble + modifiedContent };
    }

    private string BuildGenerationPrompt(
        PlanStep step,
        IReadOnlyDictionary<int, ExecutionResult> context,
        IReadOnlyList<InputFile>? inputFiles)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("Generate a script for the following task:");
        prompt.AppendLine();
        prompt.AppendLine($"Task: {step.Description}");
        prompt.AppendLine($"Expected output: {step.ExpectedOutput}");

        if (!string.IsNullOrWhiteSpace(step.ScriptHint))
        {
            prompt.AppendLine($"Hint: {step.ScriptHint}");
        }

        // Add input files context
        if (inputFiles is { Count: > 0 })
        {
            prompt.AppendLine();
            prompt.AppendLine("IMPORTANT - Input files available:");
            foreach (var file in inputFiles)
            {
                prompt.AppendLine($"- {file.FileName}");
            }
            prompt.AppendLine();
            prompt.AppendLine("""
                CRITICAL: Files are attached and available in the current directory, but may have different names.
                Your script MUST include file discovery at the start:

                import os
                print("Available files:", os.listdir('.'))
                # Find the target file by extension or partial name match
                target_file = None
                for f in os.listdir('.'):
                    if f.endswith('.pdf') or 'untitled' in f.lower():  # Adjust pattern as needed
                        target_file = f
                        print(f"Found target file: {f}")
                        break
                if not target_file:
                    raise FileNotFoundError("Could not find the input file")

                Always search for files dynamically - never hardcode filenames directly.
                """);
        }

        // Add context from dependent steps
        if (step.Dependencies.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Context from previous steps:");

            foreach (var depOrder in step.Dependencies)
            {
                if (context.TryGetValue(depOrder, out var depResult) && depResult.IsSuccess)
                {
                    prompt.AppendLine($"- Step {depOrder} output: {depResult.Output}");

                    if (depResult.GeneratedFiles.Count > 0)
                    {
                        prompt.AppendLine($"  Generated files: {string.Join(", ", depResult.GeneratedFiles)}");
                    }
                }
            }
        }

        return prompt.ToString();
    }

    private ScriptInfo ParseScriptResponse(int stepOrder, string response)
    {
        // Extract code from markdown code block
        var (language, content) = ExtractCodeBlock(response);

        if (string.IsNullOrWhiteSpace(content))
        {
            // If no code block found, assume the entire response is the script
            content = response.Trim();
            language = "python"; // Default to Python
        }

        var extension = language.ToLowerInvariant() switch
        {
            "python" or "py" => ".py",
            "csharp" or "c#" or "cs" => ".cs",
            _ => ".py"
        };

        var fileName = $"step_{stepOrder}_script{extension}";

        _logger.LogDebug(
            "Generated {Language} script for step {Order}: {FileName}",
            language,
            stepOrder,
            fileName);

        return new ScriptInfo(
            Language: language,
            FileName: fileName,
            Content: content);
    }

    private static (string language, string content) ExtractCodeBlock(string response)
    {
        // Find the start of the code block
        var blockStart = response.IndexOf("```", StringComparison.Ordinal);
        if (blockStart < 0)
        {
            return ("", "");
        }

        // Find the language identifier (if any)
        var lineEnd = response.IndexOf('\n', blockStart);
        if (lineEnd < 0)
        {
            return ("", "");
        }

        var language = response[(blockStart + 3)..lineEnd].Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(language))
        {
            language = "python";
        }

        // Find the end of the code block
        var contentStart = lineEnd + 1;
        var blockEnd = response.IndexOf("```", contentStart, StringComparison.Ordinal);
        if (blockEnd < 0)
        {
            blockEnd = response.Length;
        }

        var content = response[contentStart..blockEnd].Trim();

        return (language, content);
    }

    private async Task EnsureGeneratorAgentExistsAsync(CancellationToken cancellationToken)
    {
        if (_generatorAgentId is not null)
        {
            return;
        }

        _logger.LogDebug("Creating script generator agent");

        var tools = _toolProvider.GetToolDefinitions().ToList();
        var agentResponse = await _client.Administration.CreateAgentAsync(
            model: _aiOptions.DefaultModel,
            name: "ScriptGenerator",
            instructions: ScriptGenerationPrompt,
            tools: tools,
            toolResources: _toolProvider.GetToolResources());

        _generatorAgentId = agentResponse.Value.Id;
        _logger.LogInformation("Created script generator agent with ID: {AgentId}", _generatorAgentId);
    }
}
