using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
[Route("api/samples/{sampleId:long}/ai-report")]
public sealed class AiSampleReportsController(
    AiSampleReportService aiSampleReportService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AiSampleReportResponse>> Generate(
        long sampleId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await aiSampleReportService.GenerateAsync(
            sampleId,
            currentUserId,
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Value);
        }

        return result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Forbidden => Forbid(),
            ServiceError.ExternalService => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = result.ErrorMessage }),
            _ => ValidationProblem(result.ErrorMessage)
        };
    }
}
