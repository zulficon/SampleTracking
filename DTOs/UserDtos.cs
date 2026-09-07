using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class RegisterUserRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$")]
    public string Username { get; init; } = null!;

    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = null!;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = null!;
}

public sealed class CreateUserRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$")]
    public string Username { get; init; } = null!;

    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; }

    [Required, StringLength(100, MinimumLength = 8)]
    public string TemporaryPassword { get; init; } = null!;
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; }
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(100)]
    public string CurrentPassword { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; init; } = null!;

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; init; } = null!;
}

public sealed class ChangeUserActiveStateRequest
{
    public bool IsActive { get; init; }
}

public sealed record UserListItem(
    long Id,
    string Username,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record UserDetail(
    long Id,
    string Username,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CreatedSampleCount,
    int AssignedSampleCount);

public sealed record UserCreated(
    long Id,
    string Username,
    UserRole Role,
    bool IsActive);

public sealed record UserProfile(
    long Id,
    string Username,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive);
