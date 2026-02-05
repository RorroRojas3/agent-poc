namespace RR.Agent.Model.Options
{
    public class OpenAIOptions
    {
        public string ApiKey { get; init; } = null!;

        public string Model { get; init; } = "gpt-5-nano";

        public const string SectionName = "OpenAI";
    }
}