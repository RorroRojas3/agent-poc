using System.ComponentModel;

namespace RR.Agent.Model.Enums
{
    public enum AgentsTypes
    {
        [Description("Azure AI Foundry")]
        Azure_AI_Foundry = 1,

        [Description("Anthropic")]
        Anthropic = 2,

        [Description("Ollama")]
        Ollama = 3,

        [Description("OpenAI")]
        OpenAI = 4,

        [Description("Azure OpenAI")]
        Azure_OpenAI = 5
    }
}
