namespace SampleAnalysisTracking.Clients;

// Metinleri anlamsal vektör dizilerine (float array embedding) dönüştüren istemci arayüzü.
// RAG (Retrieval-Augmented Generation) mimarisinde semantik arama için temel oluşturur.
public interface IEmbeddingClient
{
    // Verilen metin parçacığını (chunk veya arama sorgusu) embedding modeline gönderir
    // ve metnin çok boyutlu uzaydaki vektör temsilini (örn. 1024 boyutlu float dizisi) döndürür.
    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}

