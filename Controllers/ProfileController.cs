using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(
    UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserProfile>> Get(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var profile = await userService.GetProfileAsync(
            currentUserId,
            cancellationToken);

        return profile is null
            ? Unauthorized()
            : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await userService.UpdateProfileAsync(
            currentUserId,
            request,
            cancellationToken);

        return result.Error switch
        {
            UserOperationError.None => NoContent(),
            UserOperationError.EmailAlreadyExists => Conflict(new
            {
                message = "Bu e-posta adresi zaten kullanılıyor."
            }),
            UserOperationError.NotFound => Unauthorized(),
            _ => Problem(
                title: "Profil güncellenemedi.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await userService.ChangePasswordAsync(
            currentUserId,
            request,
            cancellationToken);

        return result.Error switch
        {
            UserOperationError.None => NoContent(),
            UserOperationError.CurrentPasswordIncorrect => BadRequest(new
            {
                message = "Mevcut parola hatalı."
            }),
            UserOperationError.NewPasswordSameAsCurrent => BadRequest(new
            {
                message = "Yeni parola mevcut paroladan farklı olmalıdır."
            }),
            UserOperationError.NotFound => Unauthorized(),
            _ => Problem(
                title: "Parola değiştirilemedi.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private bool TryGetCurrentUserId(out long userId)
    {
        return long.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
