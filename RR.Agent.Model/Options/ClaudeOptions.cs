namespace RR.Agent.Model.Options
{
    public sealed class ClaudeOptions
    {
        public string ApiKey { get; init; } = null!;

        public string Model { get; init; } = null!;

        public const string SectionName = "Claude";
    }
}
