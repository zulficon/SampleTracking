using System.ComponentModel.DataAnnotations;

namespace SampleAnalysisTracking.DTOs;

public sealed class CreateAnalysisCodeRequest
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; init; } = null!;

    [Required, StringLength(100)]
    public string Name { get; init; } = null!;

    [StringLength(1000)]
    public string? Description { get; init; }
}

public sealed class UpdateAnalysisCodeRequest
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; init; } = null!;

    [Required, StringLength(100)]
    public string Name { get; init; } = null!;

    [StringLength(1000)]
    public string? Description { get; init; }
}

public sealed class SetAnalysisCodeActiveRequest
{
    public bool IsActive { get; init; }
}

public sealed class CreateAnalysisParameterRequest
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; init; } = null!;

    [Required, StringLength(100)]
    public string Name { get; init; } = null!;

    [Required, StringLength(30)]
    public string DefaultUnit { get; init; } = null!;

    public decimal? ReferenceMin { get; init; }
    public decimal? ReferenceMax { get; init; }
    public bool IsRequired { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public sealed class UpdateAnalysisParameterRequest
{
    [Required, StringLength(30), RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; init; } = null!;

    [Required, StringLength(100)]
    public string Name { get; init; } = null!;

    [Required, StringLength(30)]
    public string DefaultUnit { get; init; } = null!;

    public decimal? ReferenceMin { get; init; }
    public decimal? ReferenceMax { get; init; }
    public bool IsRequired { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public sealed class SetAnalysisParameterActiveRequest
{
    public bool IsActive { get; init; }
}

public sealed record AnalysisParameterItem(
    long Id,
    string Code,
    string Name,
    string DefaultUnit,
    decimal? ReferenceMin,
    decimal? ReferenceMax,
    bool IsRequired,
    bool IsActive,
    int DisplayOrder);

public sealed record AnalysisCodeItem(
    long Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record AnalysisCodeDetail(
    long Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<AnalysisParameterItem> Parameters);
