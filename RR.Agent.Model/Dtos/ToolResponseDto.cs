using System.Text.Json.Serialization;
using RR.Agent.Model.Enums;

namespace RR.Agent.Model.Dtos
{
    public class ToolResponseDto
    {
        [JsonPropertyName("result")]
        public required ExecutionResult Result { get; set; }

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("hasWrittenFile")]
        public bool HasWrittenFile {get; set;} = false;

        [JsonPropertyName("filePath")]
        public string? FilePath {get; set;} 

        [JsonPropertyName("filename")]
        public string? Filename {get; set;}

        [JsonPropertyName("hasExecutedScript")]
        public bool HasExecutedScript {get; set;} = false;

        [JsonPropertyName("scriptStandardInput")]
        public string? ScriptStandardInput { get; set; }

        [JsonPropertyName("scriptStandardOutput")]
        public string? ScriptStandardOutput { get; set; }

        [JsonPropertyName("scriptExitCode")]
        public int ScriptExitCode { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors {get; set; } = [];
    }
}