namespace SampleAnalysisTracking.Clients;

public interface IOllamaClient
{
    Task<string> GenerateSampleReportAsync(
        string prompt,
        CancellationToken cancellationToken);

    Task<string> GenerateWorkloadInsightAsync(
        string prompt,
        CancellationToken cancellationToken);
}
