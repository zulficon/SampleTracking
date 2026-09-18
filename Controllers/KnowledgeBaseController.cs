using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

// RAG (Bilgi Tabanı) doküman yönetimi ve anlamsal (vektörel) arama API denetleyicisi.
// Bilgi belgelerinin eklenmesini, vektörleştirilmesini ve test aramalarını yönetir (Admin rolü).
[ApiController]
[Route("api/knowledge-base")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class KnowledgeBaseController(KnowledgeBaseService knowledgeBaseService) : ControllerBase
{
    // Bilgi tabanındaki tüm belgeleri ve parça (chunk) sayılarını listeler.
    // GET api/knowledge-base/documents
    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeDocumentItem>>> GetDocuments(
        CancellationToken cancellationToken) =>
        Ok(await knowledgeBaseService.GetAllAsync(cancellationToken));

    // Belirtilen kimlikteki dokümanın metnini, bağlı analiz kodlarını ve durumunu getirir.
    // GET api/knowledge-base/documents/{id}
    [HttpGet("documents/{id:long}")]
    public async Task<ActionResult<KnowledgeDocumentDetail>> GetDocument(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseService.GetByIdAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    // Yeni bir doküman ekler, metni parçalar ve embedding modeli ile vektörleştirerek kaydeder.
    // POST api/knowledge-base/documents
    [HttpPost("documents")]
    public async Task<ActionResult<KnowledgeDocumentItem>> CreateDocument(
        CreateKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        // Kullanıcı kimliğini doğrula
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Dokümanı oluştur ve vektörleştirme işlemini yürüt
        var result = await knowledgeBaseService.CreateAsync(
            request,
            currentUserId,
            cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ToError(result);
    }

    // Taslak bir dokümanın metnini günceller, eski parçalarını siler ve yeniden vektörleştirir.
    // PUT api/knowledge-base/documents/{id}
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

    // RAG Bilgi tabanında serbest metin ile kosinüs benzerliği (pgvector) araması yapar.
    // POST api/knowledge-base/search
    [HttpPost("search")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeSearchItem>>> Search(
        SearchKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await knowledgeBaseService.SearchAsync(request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : ToError(result);
    }

    // İstekte bulunan yöneticinin kullanıcı kimliğini Claims içerisinden okur.
    private bool TryGetCurrentUserId(out long userId) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    // Servis hata sonuçlarını uygun HTTP durum kodlarına dönüştürür.
    private ActionResult ToError<T>(ServiceResult<T> result) =>
        result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Conflict => Conflict(new { message = result.ErrorMessage }),
            // Embedding servisine (Ollama) ulaşılamadığında 503 Servis Kullanılamıyor döner
            ServiceError.ExternalService => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = result.ErrorMessage }),
            ServiceError.Forbidden => Forbid(),
            _ => ValidationProblem(result.ErrorMessage)
        };
}

