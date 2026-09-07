namespace SampleAnalysisTracking.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434/";
    public string Model { get; init; } = "qwen3:4b-instruct";
    public int TimeoutSeconds { get; init; } = 180;
    public int ContextWindow { get; init; } = 12288;
    public int MaxOutputTokens { get; init; } = 1200;
    public string KeepAlive { get; init; } = "10m";
    public double Temperature { get; init; } = 0.35;
    public double TopP { get; init; } = 0.9;
}
