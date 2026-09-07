using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Clients;

public sealed class OllamaEmbeddingClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<RagOptions> ragOptions,
    ILogger<OllamaEmbeddingClient> logger) : IEmbeddingClient
{
    public async Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be blank.", nameof(text));
        }

        var request = new
        {
            model = ragOptions.Value.EmbeddingModel,
            input = text,
            keep_alive = ollamaOptions.Value.KeepAlive
        };

        using var response = await httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Ollama returned HTTP {StatusCode} while creating an embedding.",
                (int)response.StatusCode);
            throw new OllamaClientException(
                $"Ollama returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken);
        var embedding = payload?.Embeddings?.FirstOrDefault();

        if (embedding is null
            || embedding.Length != ragOptions.Value.EmbeddingDimensions)
        {
            logger.LogWarning("Ollama returned an invalid embedding response.");
            throw new OllamaClientException("Ollama returned an invalid embedding response.");
        }

        return embedding;
    }

    private sealed record OllamaEmbeddingResponse(float[][]? Embeddings);
}
