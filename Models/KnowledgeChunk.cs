using Pgvector;

namespace SampleAnalysisTracking.Models;

// RAG mimarisinde kullanılan parçalanmış metin bloğu (chunk) ve anlamsal vektör kaydı.
// PostgreSQL pgvector eklentisi ile doğrudan veritabanında vektör benzerlik araması yapılmasını sağlar.
public sealed class KnowledgeChunk
{
    // Parçacığın benzersiz kimliği
    public long Id { get; set; }

    // Bağlı olduğu ana dokümanın kimliği
    public long KnowledgeDocumentId { get; set; }

    // Doküman içindeki parça sırası (0'dan başlar)
    public int ChunkIndex { get; set; }

    // Parçanın ham metin içeriği (LLM'e bağlam olarak iletilen kısım)
    public string Content { get; set; } = null!;

    // Metnin embedding modeli tarafından üretilen çok boyutlu vektör temsili (pgvector vector tipi)
    public Vector Embedding { get; set; } = null!;

    // Oluşturulma zamanı
    public DateTimeOffset CreatedAt { get; set; }

    // Ana doküman ilişkisi
    public KnowledgeDocument KnowledgeDocument { get; set; } = null!;
}

