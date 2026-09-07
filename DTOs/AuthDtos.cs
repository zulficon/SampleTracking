using System.ComponentModel.DataAnnotations;

namespace SampleAnalysisTracking.DTOs;

// API'ye gelen login verisi ve API'nin döndüğü güvenli kullanıcı bilgisi.
public sealed class LoginRequest
{
    [Required, StringLength(50)]
    public string Username { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}

public sealed record AuthUser(long Id, string Username, string Role);
