using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
[Route("api/samples/{sampleId:long}/analyses")]
public sealed class SampleAnalysisAssignmentsController(SampleAnalysisService analysisService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SampleAnalysisListItem>>> GetForSample(
        long sampleId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        var result = await analysisService.GetForSampleAsync(
            sampleId,
            currentUserId,
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<SampleAnalysisListItem>>> Assign(
        long sampleId,
        AssignSampleAnalysesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        var result = await analysisService.AssignAsync(
            sampleId,
            request,
            currentUserId,
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result);
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
