using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class CreateSampleRequest
{
    [Required, StringLength(50)]
    public string SampleCode { get; init; } = null!;

    [Required, StringLength(50)]
    public string SampleType { get; init; } = null!;

    public long? LocationId { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [StringLength(1000)]
    public string? InitialNote { get; init; }
}

public sealed class UpdateSampleRequest
{
    [Required, StringLength(50)]
    public string SampleType { get; init; } = null!;

    public long? LocationId { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }
}

public sealed class ChangeSampleStatusRequest
{
    [EnumDataType(typeof(SampleStatus))]
    public SampleStatus NewStatus { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}

public sealed record SampleListItem(
    long Id,
    string SampleCode,
    string SampleType,
    SampleStatus Status,
    string? LocationName,
    string? CreatedByUsername,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SampleHistoryItem(
    long Id,
    SampleStatus? OldStatus,
    SampleStatus NewStatus,
    string? ChangedByUsername,
    DateTimeOffset ChangedAt,
    string? Note);

public sealed record SampleDetail(
    long Id,
    string SampleCode,
    string SampleType,
    SampleStatus Status,
    long? LocationId,
    string? LocationName,
    string? Description,
    long? CreatedById,
    string? CreatedByUsername,
    long? AssignedToId,
    string? AssignedToUsername,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SampleHistoryItem> History);

public sealed class AssignLaboratoryWorkerRequest
{
    [Range(1, long.MaxValue)]
    public long? LaboratoryWorkerId { get; init; }
}
