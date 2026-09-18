namespace SampleAnalysisTracking.Options;

// RAG (Retrieval-Augmented Generation / Geri Getirme Destekli Üretim) mimarisi ayarları.
// Bilgi belgelerinin parçalanması (chunking), embedding modeli ve arama limitlerini tanımlar.
public sealed class RagOptions
{
    // Konfigürasyon bölüm adı
    public const string SectionName = "Rag";

    // Metinleri vektörlere dönüştürmek için kullanılan embedding modeli (örn. Qwen 3 Embedding 0.6B)
    public string EmbeddingModel { get; init; } = "qwen3-embedding:0.6b";

    // Embedding modelinin ürettiği vektör boyutu (pgvector tablosundaki vector(1024) boyutu ile aynı olmalıdır)
    public int EmbeddingDimensions { get; init; } = 1024;

    // Uzun dokümanların bölüneceği parça boyutu (karakter sayısı)
    public int ChunkSizeCharacters { get; init; } = 1200;

    // Parçalar arasındaki anlamsal bağlamın kopmaması için kullanılan örtüşme (overlap) karakter miktarı
    public int ChunkOverlapCharacters { get; init; } = 200;

    // Vektörel semantik aramada modele bağlam (context) olarak sunulacak maksimum doküman/parça sayısı
    public int SearchResultLimit { get; init; } = 4;
}

