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

        [JsonPropertyName("errors")]
        public List<string> Errors {get; set; } = [];
    }
}