using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class UserListQuery
{
    [StringLength(100)]
    public string? Search { get; init; }

    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 1000)]
    public int PageSize { get; init; } = 10;
}