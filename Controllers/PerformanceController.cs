using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Manager))]
[Route("api/performance")]
public sealed class PerformanceController(
    PerformanceAnalyticsService performanceService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<PerformanceOverviewResponse>> GetOverview(
        [FromQuery] PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var isElevated = User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Manager));

        var overview = await performanceService.GetPerformanceOverviewAsync(
            query,
            currentUserId,
            isElevated,
            cancellationToken);

        return Ok(overview);
    }

    [HttpPost("ai-workload-insight")]
    [Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Manager))]
    public async Task<ActionResult<AiWorkloadInsightResponse>> GenerateAiWorkloadInsight(
        [FromBody] PerformanceQuery? query,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await performanceService.GenerateAiWorkloadInsightAsync(
            query ?? new PerformanceQuery(),
            currentUserId,
            isElevated: true,
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Value);
        }

        return result.Error switch
        {
            ServiceError.ExternalService => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = result.ErrorMessage }),
            _ => ValidationProblem(result.ErrorMessage)
        };
    }
}
