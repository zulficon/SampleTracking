using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class SampleListQuery
{
    [StringLength(100)]
    public string? Search { get; init; }

    public SampleStatus? Status { get; init; }

    [Range(1, long.MaxValue)]
    public long? LocationId { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 1000)]
    public int PageSize { get; init; } = 10;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages =>
        (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record SampleDashboardSummary(
    int Total,
    int Active,
    int Analyzing,
    int Completed,
    IReadOnlyDictionary<SampleStatus, int> StatusCounts);