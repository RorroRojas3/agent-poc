namespace RR.Agent.Model.Options
{
    public class AzureOpenAIOptions
    {
        public Uri Uri { get; init; } = null!;

        public string Key { get; init; } = null!;

        public const string SectionName = "AzureOpenAI";
    }
}