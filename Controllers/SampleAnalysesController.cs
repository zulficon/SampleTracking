using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
[Route("api/sample-analyses")]
public sealed class SampleAnalysesController(SampleAnalysisService analysisService) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<ActionResult<SampleAnalysisDetail>> GetById(
        long id,
        CancellationToken cancellationToken) =>
        await Execute(id, (userId, isAdmin) =>
            analysisService.GetByIdAsync(id, userId, isAdmin, cancellationToken));

    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<SampleAnalysisDetail>> Start(
        long id,
        CancellationToken cancellationToken) =>
        await Execute(id, (userId, isAdmin) =>
            analysisService.StartAsync(id, userId, isAdmin, cancellationToken));

    [HttpPut("{id:long}/results")]
    public async Task<ActionResult<SampleAnalysisDetail>> SaveResults(
        long id,
        SaveAnalysisResultsRequest request,
        CancellationToken cancellationToken) =>
        await Execute(id, (userId, isAdmin) =>
            analysisService.SaveResultsAsync(id, request, userId, isAdmin, cancellationToken));

    [HttpPost("{id:long}/complete")]
    public async Task<ActionResult<SampleAnalysisDetail>> Complete(
        long id,
        CompleteSampleAnalysisRequest request,
        CancellationToken cancellationToken) =>
        await Execute(id, (userId, isAdmin) =>
            analysisService.CompleteAsync(id, request, userId, isAdmin, cancellationToken));

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<SampleAnalysisDetail>> Cancel(
        long id,
        CancelSampleAnalysisRequest request,
        CancellationToken cancellationToken) =>
        await Execute(id, (userId, isAdmin) =>
            analysisService.CancelAsync(id, request, userId, isAdmin, cancellationToken));

    private async Task<ActionResult<SampleAnalysisDetail>> Execute(
        long _,
        Func<long, bool, Task<ServiceResult<SampleAnalysisDetail>>> operation)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await operation(currentUserId, User.IsInRole(nameof(UserRole.Admin)));
        if (result.Succeeded) return Ok(result.Value);

        return result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Forbidden => Forbid(),
            ServiceError.Conflict => Conflict(new { message = result.ErrorMessage }),
            _ => ValidationProblem(result.ErrorMessage)
        };
    }
}
