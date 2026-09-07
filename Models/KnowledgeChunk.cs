using Pgvector;

namespace SampleAnalysisTracking.Models;

public sealed class KnowledgeChunk
{
    public long Id { get; set; }
    public long KnowledgeDocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = null!;
    public Vector Embedding { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public KnowledgeDocument KnowledgeDocument { get; set; } = null!;
}
