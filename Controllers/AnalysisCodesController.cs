using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
[Route("api/analysis-codes")]
public sealed class AnalysisCodesController(AnalysisCatalogService catalogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnalysisCodeItem>>> GetAll(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var canSeeInactive = User.IsInRole(nameof(UserRole.Admin)) && includeInactive;
        return Ok(await catalogService.GetAllAsync(canSeeInactive, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AnalysisCodeDetail>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var detail = await catalogService.GetByIdAsync(
            id,
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> Create(
        CreateAnalysisCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToError(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> Update(
        long id,
        UpdateAnalysisCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.UpdateAsync(id, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPatch("{id:long}/active")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> SetActive(
        long id,
        SetAnalysisCodeActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.SetActiveAsync(id, request.IsActive, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPost("{id:long}/parameters")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> AddParameter(
        long id,
        CreateAnalysisParameterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.AddParameterAsync(id, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPut("{id:long}/parameters/{parameterId:long}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> UpdateParameter(
        long id,
        long parameterId,
        UpdateAnalysisParameterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.UpdateParameterAsync(id, parameterId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPatch("{id:long}/parameters/{parameterId:long}/active")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AnalysisCodeDetail>> SetParameterActive(
        long id,
        long parameterId,
        SetAnalysisParameterActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.SetParameterActiveAsync(
            id,
            parameterId,
            request.IsActive,
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    private ActionResult ToError<T>(ServiceResult<T> result) =>
        result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Conflict => Conflict(new { message = result.ErrorMessage }),
            _ => ValidationProblem(result.ErrorMessage)
        };
}
