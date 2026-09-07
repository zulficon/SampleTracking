using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthService authService,
    UserService userService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult<AuthUser>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.AuthenticateAsync(
            request,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Kullanıcı adı veya parola hatalı." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Ok(user);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult<UserCreated>> Register(
    RegisterUserRequest request,
    CancellationToken cancellationToken)
    {
        var result = await userService.RegisterAsync(
            request,
            cancellationToken);

        if (result.Succeeded)
        {
            return StatusCode(
                StatusCodes.Status201Created,
                result.User);
        }

        return result.Error switch
        {
            UserOperationError.UsernameAlreadyExists =>
                Conflict(new
                {
                    message = "Bu kullanıcı adı zaten kullanılıyor."
                }),

            UserOperationError.EmailAlreadyExists =>
                Conflict(new
                {
                    message = "Bu e-posta adresi zaten kullanılıyor."
                }),

            _ => Problem(
                title: "Kullanıcı kaydı oluşturulamadı.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<AuthUser> Me()
    {
        if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var id))
        {
            return Unauthorized();
        }

        var username = User.Identity?.Name;
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized();
        }

        return Ok(new AuthUser(id, username, role));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

}
