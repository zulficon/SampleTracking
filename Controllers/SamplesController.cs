using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;


namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize]
[Route("api/samples")]
public sealed class SamplesController(SampleService sampleService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles =nameof(UserRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<SampleListItem>>> GetAll(
        [FromQuery] SampleStatus? status,
        [FromQuery] long? locationId,
        CancellationToken cancellationToken)
    {
        var samples = await sampleService.GetAllAsync(
            status,
            locationId,
            cancellationToken);
        return Ok(samples);
    }

[HttpGet("paged")]
[Authorize(Roles =nameof(UserRole.Admin))]
public async Task<ActionResult<PagedResult<SampleListItem>>> GetPaged(
    [FromQuery] SampleListQuery request,
    CancellationToken cancellationToken)
    {
        var result = await sampleService.GetPagedAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

[HttpGet("mine/paged")]
[Authorize(Roles = nameof(UserRole.Laboratory))]
public async Task<ActionResult<PagedResult<SampleListItem>>> GetMyPaged(
    [FromQuery] SampleListQuery request,
    CancellationToken cancellationToken
)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result =  await sampleService.GetPagedAsync(request,cancellationToken,currentUserId);
        return Ok(result);
    }




[HttpGet("summary")]
[Authorize(Roles =nameof(UserRole.Admin))]
public async Task<ActionResult<SampleDashboardSummary>> GetSummary(
    CancellationToken cancellationToken
)
    {
        var summary = await sampleService.GetSampleDashboardSummaryAsync(cancellationToken);
        return Ok(summary);
    }

[HttpGet("{id:long}")]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
public async Task<ActionResult<SampleDetail>> GetById(
    long id,
    CancellationToken cancellationToken)
{
    long? assignedToId = null;

    if (User.IsInRole(nameof(UserRole.Laboratory)))
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        assignedToId = currentUserId;
    }

    var sample = await sampleService.GetByIdAsync(
        id,
        cancellationToken,
        assignedToId);

    return sample is null ? NotFound() : Ok(sample);
}

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> Create(CreateSampleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await sampleService.CreateAsync(request, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.Error == ServiceError.Conflict
                ? Conflict(new { message = result.ErrorMessage })
                : ValidationProblem(result.ErrorMessage);
        }

        var sample = result.Value!;

        return CreatedAtAction(nameof(GetById), new { id = sample.Id }, new { sample.Id, sample.SampleCode, sample.Status });
    }

[HttpPatch("{id:long}")]
[Authorize(Roles = nameof(UserRole.Admin))]
public async Task<ActionResult> Update(
    long id,
    UpdateSampleRequest request,
    CancellationToken cancellationToken
)
    {
        var result = await sampleService.UpdateAsync(
            id,
            request,
            cancellationToken
        );
        if (!result.Succeeded)
        {
            return ToError(result);
        }
        return NoContent();
    }

[HttpPatch("{id:long}/laboratory-worker")]
[Authorize(Roles = nameof(UserRole.Admin))]
public async Task<ActionResult> AssignLaboratoryWorker(
    long id,
    AssignLaboratoryWorkerRequest request,
    CancellationToken cancellationToken
)
    {
        var result = await sampleService.AssignLaboratoryWorkerAsync(
            id,
            request,
            cancellationToken
        );
        if (!result.Succeeded)
        {
            return ToError(result);

        }
        return NoContent();
    }




[HttpPatch("{id:long}/status")]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
public async Task<ActionResult> ChangeStatus(
    long id,
    ChangeSampleStatusRequest request,
    CancellationToken cancellationToken)
{
    if (!TryGetCurrentUserId(out var currentUserId))
    {
        return Unauthorized();
    }

    var requireAssignment = User.IsInRole(nameof(UserRole.Laboratory));

    var result = await sampleService.ChangeStatusAsync(
        id,
        request,
        currentUserId,
        requireAssignment,
        cancellationToken);

    if (!result.Succeeded)
    {
        return ToError(result);
    }

    return NoContent();
}

private bool TryGetCurrentUserId(out long userId) =>
    long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

private ActionResult ToError<T>(ServiceResult<T> result) =>
    result.Error switch
    {
        ServiceError.NotFound => NotFound(),
        ServiceError.Forbidden => Forbid(),
        ServiceError.Conflict => Conflict(new { message = result.ErrorMessage }),
        _ => ValidationProblem(result.ErrorMessage)
    };


}
