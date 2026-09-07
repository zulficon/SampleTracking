using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/users")]
public sealed class UsersController(
    UserService userService) : ControllerBase
{
    [HttpGet("laboratory-workers")]
    public async Task<ActionResult<IReadOnlyList<UserItem>>> GetLaboratoryWorkers(
        CancellationToken cancellationToken)
    {
        var users = await userService.GetLaboratoryWorkersAsync(
            cancellationToken);

        return Ok(users);
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<UserListItem>>> GetPaged(
        [FromQuery] UserListQuery request,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetPagedAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserDetail>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetByIdAsync(
            id,
            cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserCreated>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.CreateAsync(
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return ToErrorResult(result.Error);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.User!.Id },
            result.User);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return ToErrorResult(result.Error);
        }

        return NoContent();
    }

    [HttpPatch("{id:long}/active")]
    public async Task<IActionResult> ChangeActiveState(
        long id,
        ChangeUserActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        var idClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!long.TryParse(idClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await userService.ChangeActiveStateAsync(
            id,
            currentUserId,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            return ToErrorResult(result.Error);
        }

        return NoContent();
    }

    private ActionResult ToErrorResult(
        UserOperationError error)
    {
        return error switch
        {
            UserOperationError.NotFound =>
                NotFound(new
                {
                    message = "Kullanıcı bulunamadı."
                }),

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

            UserOperationError.CannotDeactivateSelf =>
                BadRequest(new
                {
                    message = "Kendi hesabınızı pasifleştiremezsiniz."
                }),

            UserOperationError.CannotRemoveLastAdmin =>
                BadRequest(new
                {
                    message = "Sistemde en az bir aktif yönetici bulunmalıdır."
                }),

            _ => Problem(
                title: "Kullanıcı işlemi tamamlanamadı.",
                statusCode:
                    StatusCodes.Status500InternalServerError)
        };
    }
}
