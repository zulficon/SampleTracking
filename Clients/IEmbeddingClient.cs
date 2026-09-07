namespace SampleAnalysisTracking.Clients;

public interface IEmbeddingClient
{
    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken);
}
