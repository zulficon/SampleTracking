namespace SampleAnalysisTracking.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public string EmbeddingModel { get; init; } = "qwen3-embedding:0.6b";
    public int EmbeddingDimensions { get; init; } = 1024;
    public int ChunkSizeCharacters { get; init; } = 1200;
    public int ChunkOverlapCharacters { get; init; } = 200;
    public int SearchResultLimit { get; init; } = 4;
}
