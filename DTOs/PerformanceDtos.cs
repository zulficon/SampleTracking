using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class PerformanceQuery
{
    public string? Period { get; init; } = "all"; // all, today, last7days, last30days, thisMonth, thisYear, custom
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public UserRole? Role { get; init; }
}

public sealed record WorkerPerformanceItem(
    long UserId,
    string Username,
    string FullName,
    string Email,
    UserRole Role,
    int TotalAssignedSamples,
    int TotalCreatedSamples,
    int TotalAnalyses,
    int CompletedAnalyses,
    int InProgressAnalyses,
    int PendingAnalyses,
    int CancelledAnalyses,
    int TotalMeasuredParameters,
    decimal CompletionRatePercentage,
    double? AverageAnalysisDurationHours,
    string AverageAnalysisDurationFormatted);

public sealed record PerformanceSummaryKpis(
    int TotalCompletedAnalyses,
    int TotalActiveAnalyses,
    int TotalMeasuredParameters,
    int TotalSamplesTracked,
    double? OverallAverageCompletionHours,
    string OverallAverageCompletionFormatted,
    string? TopPerformerName,
    int TopPerformerCompletedCount,
    int ActiveWorkersCount);

public sealed record AnalysisTypePerformanceItem(
    string AnalysisCode,
    string AnalysisName,
    int TotalRequested,
    int CompletedCount,
    int InProgressCount,
    int CancelledCount,
    double? AverageDurationHours,
    string AverageDurationFormatted);

public sealed record PerformanceOverviewResponse(
    PerformanceSummaryKpis Kpis,
    IReadOnlyList<WorkerPerformanceItem> Workers,
    IReadOnlyList<AnalysisTypePerformanceItem> AnalysisTypes,
    string Period,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

public sealed record AiWorkloadInsightResponse(
    string Summary,
    IReadOnlyList<string> KeyObservations,
    IReadOnlyList<string> Recommendations,
    string Disclaimer,
    string Model);

internal sealed class AiWorkloadInsightModelResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("keyObservations")]
    public IReadOnlyList<string?>? KeyObservations { get; init; }

    [JsonPropertyName("recommendations")]
    public IReadOnlyList<string?>? Recommendations { get; init; }
}
