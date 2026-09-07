using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class KnowledgeBaseController(KnowledgeBaseService knowledgeBaseService) : ControllerBase
{
    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeDocumentItem>>> GetDocuments(
        CancellationToken cancellationToken) =>
        Ok(await knowledgeBaseService.GetAllAsync(cancellationToken));

    [HttpGet("documents/{id:long}")]
    public async Task<ActionResult<KnowledgeDocumentDetail>> GetDocument(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseService.GetByIdAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPost("documents")]
    public async Task<ActionResult<KnowledgeDocumentItem>> CreateDocument(
        CreateKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await knowledgeBaseService.CreateAsync(
            request,
            currentUserId,
            cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ToError(result);
    }

    [HttpPut("documents/{id:long}")]
    public async Task<ActionResult<KnowledgeDocumentDetail>> UpdateDocument(
        long id,
        UpdateKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseService.UpdateDraftAsync(
            id,
            request,
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSearchItem>>> Search(
        SearchKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseService.SearchAsync(request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    private bool TryGetCurrentUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private ActionResult ToError<T>(ServiceResult<T> result) =>
        result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Conflict => Conflict(new { message = result.ErrorMessage }),
            ServiceError.ExternalService => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = result.ErrorMessage }),
            ServiceError.Forbidden => Forbid(),
            _ => ValidationProblem(result.ErrorMessage)
        };
}
