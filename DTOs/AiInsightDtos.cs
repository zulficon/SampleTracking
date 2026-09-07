using System.Text.Json.Serialization;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed record AiSampleReportResponse(
    string Summary,
    IReadOnlyList<string> CompletedAnalyses,
    IReadOnlyList<string> PendingAnalyses,
    IReadOnlyList<string> AttentionPoints,
    IReadOnlyList<string> MissingRequiredResults,
    string Disclaimer,
    string Model)
{
    public IReadOnlyList<AiReportSentence> SummarySentences { get; init; } = [];
    public IReadOnlyList<AiReportKnowledgeSource> KnowledgeSources { get; init; } = [];
}

public sealed record AiReportSentence(
    string Text,
    bool UsesRecordData,
    IReadOnlyList<int> SourceNumbers);

public sealed record AiReportKnowledgeSource(
    int SourceNumber,
    string Title,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    double? Similarity,
    string MatchType,
    string Excerpt);

internal sealed class AiSampleReportModelResponse
{
    [JsonPropertyName("summarySentences")]
    public IReadOnlyList<AiSampleReportModelSentence>? SummarySentences { get; init; }
}

internal sealed class AiSampleReportModelSentence
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("usesRecordData")]
    public bool UsesRecordData { get; init; }

    [JsonPropertyName("sourceNumbers")]
    public IReadOnlyList<int>? SourceNumbers { get; init; }
}
