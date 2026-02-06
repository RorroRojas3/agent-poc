namespace RR.Agent.Model.Options
{
    public class OllamaOptions
    {
        public Uri Uri { get; set; } = new Uri("http://localhost:11434");

        public string Model { get; set; } = "phi-3";

        public const string SectionName = "Ollama";
    }
}