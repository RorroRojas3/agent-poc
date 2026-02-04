using System.Text.Json;
using Azure.AI.Agents.Persistent;

namespace RR.Agent.Service.Tools;

/// <summary>
/// Defines the function tools available to the Executor agent.
/// </summary>
public static class ToolDefinitions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Tool for writing content to a file in the workspace.
    /// </summary>
    public static FunctionToolDefinition WriteFileTool => new(
        name: "write_file",
        description: "Write content to a file in the workspace. Use this to create Python scripts, data files, or any other text files. The file will be created in the workspace directory.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                filename = new
                {
                    type = "string",
                    description = "Name of the file to create (e.g., 'parse_pdf.py' or 'data/input.txt'). Paths are relative to the workspace."
                },
                content = new
                {
                    type = "string",
                    description = "The content to write to the file."
                }
            },
            required = new[] { "filename", "content" }
        }, JsonOptions));

    /// <summary>
    /// Tool for reading content from a file in the workspace.
    /// </summary>
    public static FunctionToolDefinition ReadFileTool => new(
        name: "read_file",
        description: "Read the content of a file from the workspace. Use this to check script outputs, read data files, or verify file contents.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                filename = new
                {
                    type = "string",
                    description = "Name of the file to read. Paths are relative to the workspace."
                }
            },
            required = new[] { "filename" }
        }, JsonOptions));

    /// <summary>
    /// Tool for executing a Python script.
    /// </summary>
    public static FunctionToolDefinition ExecutePythonTool => new(
        name: "execute_python",
        description: "Execute Python code in the workspace's virtual environment. The code will be written to a script file and executed. Use this after writing a Python script to run it and get results.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                script_content = new
                {
                    type = "string",
                    description = "The Python code to execute. Should be complete, runnable Python code."
                },
                script_name = new
                {
                    type = "string",
                    description = "Optional name for the script file (e.g., 'process_data.py'). If not provided, a name will be generated."
                }
            },
            required = new[] { "script_content" }
        }, JsonOptions));

    /// <summary>
    /// Tool for installing a Python package via pip.
    /// </summary>
    public static FunctionToolDefinition InstallPackageTool => new(
        name: "install_package",
        description: "Install a Python package using pip in the workspace's virtual environment. Use this before executing scripts that require external packages.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                package_name = new
                {
                    type = "string",
                    description = "Name of the Python package to install (e.g., 'pandas', 'pdfplumber', 'requests')."
                }
            },
            required = new[] { "package_name" }
        }, JsonOptions));

    /// <summary>
    /// Tool for listing files in the workspace.
    /// </summary>
    public static FunctionToolDefinition ListFilesTool => new(
        name: "list_files",
        description: "List all files in the workspace directory. Use this to see what files exist, including scripts, data files, and outputs.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                subdirectory = new
                {
                    type = "string",
                    description = "Optional subdirectory to list files from (e.g., 'scripts', 'output'). If not provided, lists all files in workspace."
                }
            }
        }, JsonOptions));

    /// <summary>
    /// Tool for executing an existing Python script file.
    /// </summary>
    public static FunctionToolDefinition ExecuteScriptFileTool => new(
        name: "execute_script_file",
        description: "Execute an existing Python script file from the workspace. Use this to run a script that was previously written to a file.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                script_path = new
                {
                    type = "string",
                    description = "Path to the Python script file to execute (e.g., 'scripts/parse_pdf.py')."
                },
                arguments = new
                {
                    type = "string",
                    description = "Optional command-line arguments to pass to the script."
                }
            },
            required = new[] { "script_path" }
        }, JsonOptions));

    /// <summary>
    /// Tool for finding files on the local file system.
    /// </summary>
    public static FunctionToolDefinition FindFilesTool => new(
        name: "find_files",
        description: "Search for files on the local file system by name pattern. Use this to locate files like PDFs, documents, or data files that exist outside the workspace. Searches common locations like Downloads, Documents, Desktop, and can search from a specific starting directory.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                filename_pattern = new
                {
                    type = "string",
                    description = "The filename or pattern to search for (e.g., 'untitled.pdf', '*.csv', 'report*'). Supports wildcards * and ?."
                },
                search_path = new
                {
                    type = "string",
                    description = "Optional starting directory to search from (e.g., 'C:\\Users\\Rorro\\Downloads'). If not provided, searches common user directories."
                },
                recursive = new
                {
                    type = "boolean",
                    description = "Whether to search subdirectories recursively. Default is true."
                },
                max_results = new
                {
                    type = "integer",
                    description = "Maximum number of results to return. Default is 10."
                }
            },
            required = new[] { "filename_pattern" }
        }, JsonOptions));

    /// <summary>
    /// Tool for reading files from any path on the file system.
    /// </summary>
    public static FunctionToolDefinition ReadExternalFileTool => new(
        name: "read_external_file",
        description: "Read the content of a file from any absolute path on the file system. Use this to read files located outside the workspace, such as user documents, downloads, or other data files. For binary files like PDFs, this will return information about the file rather than raw content.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                file_path = new
                {
                    type = "string",
                    description = "The absolute path to the file to read (e.g., 'C:\\Users\\Rorro\\Downloads\\document.pdf' or 'C:\\Data\\input.csv')."
                },
                max_size_kb = new
                {
                    type = "integer",
                    description = "Maximum file size to read in KB. Default is 1024 (1MB). Larger files will be truncated."
                }
            },
            required = new[] { "file_path" }
        }, JsonOptions));

    /// <summary>
    /// Tool for copying an external file to the workspace.
    /// </summary>
    public static FunctionToolDefinition CopyToWorkspaceTool => new(
        name: "copy_to_workspace",
        description: "Copy a file from any location on the file system to the workspace. Use this to bring external files (like PDFs, CSVs, etc.) into the workspace where they can be processed by Python scripts.",
        parameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                source_path = new
                {
                    type = "string",
                    description = "The absolute path to the source file to copy."
                },
                destination_name = new
                {
                    type = "string",
                    description = "Optional name for the file in the workspace. If not provided, uses the original filename."
                }
            },
            required = new[] { "source_path" }
        }, JsonOptions));

    /// <summary>
    /// Gets all tools available to the Executor agent.
    /// </summary>
    public static IReadOnlyList<FunctionToolDefinition> GetAllTools() =>
    [
        WriteFileTool,
        ReadFileTool,
        ExecutePythonTool,
        InstallPackageTool,
        ListFilesTool,
        ExecuteScriptFileTool,
        FindFilesTool,
        ReadExternalFileTool,
        CopyToWorkspaceTool
    ];

    /// <summary>
    /// Gets the names of all available tools.
    /// </summary>
    public static IReadOnlyList<string> GetToolNames() =>
    [
        "write_file",
        "read_file",
        "execute_python",
        "install_package",
        "list_files",
        "execute_script_file",
        "find_files",
        "read_external_file",
        "copy_to_workspace"
    ];
}
