using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Clients;

// Ollama yerel yapay zeka sunucusunun metin gömme (embedding) API'sini kullanan somut istemci.
// Doküman parçacıklarını ve arama sorgularını vektör uzayına taşır.
public sealed class OllamaEmbeddingClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<RagOptions> ragOptions,
    ILogger<OllamaEmbeddingClient> logger) : IEmbeddingClient
{
    // Verilen metin için Ollama'nın embedding modelini çalıştırarak sayısal vektör dizisi üretir.
    public async Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        // Boş veya sadece boşluktan oluşan metinler için embedding üretilemez.
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be blank.", nameof(text));
        }

        // Ollama /api/embed uç noktasının beklediği JSON istek gövdesi.
        // Yapılandırmada tanımlı embedding modeli (örn. qwen3-embedding:0.6b) ve hafızada tutma süresi (keep_alive) kullanılır.
        var request = new
        {
            // Yapay zeka embedding modelinin adı
            model = ragOptions.Value.EmbeddingModel,
            // Vektöre dönüştürülecek girdi metni
            input = text,
            // Modelin her istekten sonra RAM/VRAM'den atılmayıp sıcak tutulacağı süre (örn. 10m)
            keep_alive = ollamaOptions.Value.KeepAlive
        };

        // Ollama REST API'sinin gömme (embed) uç noktasına POST isteği gönderilir.
        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            cancellationToken);

        // İstek başarısız olursa (Ollama kapalıysa veya model bulunamazsa) hata loglanır ve fırlatılır.
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Ollama returned HTTP {StatusCode} while creating an embedding.",
                (int)response.StatusCode);
            throw new OllamaClientException(
                $"Ollama returned HTTP {(int)response.StatusCode}.");
        }

        // Ollama yanıtındaki embeddings matrisi çözümlenir (ilk eleman girdi metnimizin vektörüdür).
        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken);
        var embedding = payload?.Embeddings?.FirstOrDefault();

        // Model çıktısının doğrulanması: Vektör boş olamaz ve konfigürasyondaki boyutla (örn. 1024) tam eşleşmelidir.
        // Boyut uyuşmazlığı pgvector sütununa yazılırken veritabanı seviyesinde hataya neden olur.
        if (embedding is null
            || embedding.Length != ragOptions.Value.EmbeddingDimensions)
        {
            logger.LogWarning("Ollama returned an invalid embedding response.");
            throw new OllamaClientException("Ollama returned an invalid embedding response.");
        }

        // Üretilen vektör başarıyla döndürülür.
        return embedding;
    }

    // Ollama API'sinin /api/embed yanıt biçimini karşılayan iç veri modeli.
    private sealed record OllamaEmbeddingResponse(float[][]? Embeddings);
}
