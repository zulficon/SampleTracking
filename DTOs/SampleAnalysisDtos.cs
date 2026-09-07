using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class AssignSampleAnalysesRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<long> AnalysisCodeIds { get; init; } = [];
}

public sealed class SaveAnalysisResultItemRequest
{
    [Range(1, long.MaxValue)]
    public long AnalysisParameterId { get; init; }

    public decimal NumericValue { get; init; }

    [StringLength(1000)]
    public string? ResultNote { get; init; }
}

public sealed class SaveAnalysisResultsRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<SaveAnalysisResultItemRequest> Results { get; init; } = [];
}

public sealed class CompleteSampleAnalysisRequest
{
    [StringLength(2000)]
    public string? ResultNote { get; init; }
}

public sealed class CancelSampleAnalysisRequest
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string Note { get; init; } = null!;
}

public sealed record AnalysisResultItem(
    long Id,
    long AnalysisParameterId,
    string ParameterCode,
    string ParameterName,
    decimal NumericValue,
    string Unit,
    decimal? ReferenceMin,
    decimal? ReferenceMax,
    bool IsOutsideReference,
    string? ResultNote,
    DateTimeOffset MeasuredAt,
    string? EnteredByUsername);

public sealed record SampleAnalysisParameterItem(
    long Id,
    string Code,
    string Name,
    string DefaultUnit,
    decimal? ReferenceMin,
    decimal? ReferenceMax,
    bool IsRequired,
    int DisplayOrder,
    AnalysisResultItem? Result);

public sealed record SampleAnalysisHistoryItem(
    long Id,
    AnalysisStatus? OldStatus,
    AnalysisStatus NewStatus,
    string? ChangedByUsername,
    DateTimeOffset ChangedAt,
    string? Note);

public sealed record SampleAnalysisListItem(
    long Id,
    long SampleId,
    long AnalysisCodeId,
    string AnalysisCode,
    string AnalysisName,
    AnalysisStatus Status,
    string? RequestedByUsername,
    DateTimeOffset RequestedAt,
    string? StartedByUsername,
    DateTimeOffset? StartedAt,
    string? CompletedByUsername,
    DateTimeOffset? CompletedAt);

public sealed record SampleAnalysisDetail(
    long Id,
    long SampleId,
    string SampleCode,
    long AnalysisCodeId,
    string AnalysisCode,
    string AnalysisName,
    AnalysisStatus Status,
    string? ResultNote,
    string? RequestedByUsername,
    DateTimeOffset RequestedAt,
    string? StartedByUsername,
    DateTimeOffset? StartedAt,
    string? CompletedByUsername,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<SampleAnalysisParameterItem> Parameters,
    IReadOnlyList<SampleAnalysisHistoryItem> History);
