using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Controllers;

// Numune için yapay zeka destekli değerlendirme raporunu tetikleyen ve sunan API denetleyicisi.
// Yalnızca Admin ve Laboratuvar personeli rollerine açıktır.
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Laboratory))]
[Route("api/samples/{sampleId:long}/ai-report")]
public sealed class AiSampleReportsController(
    AiSampleReportService aiSampleReportService) : ControllerBase
{
    // Belirtilen numunenin analiz kayıtlarını ve RAG bilgi tabanını harmanlayarak yapay zeka raporu üretir.
    // POST api/samples/{sampleId}/ai-report
    [HttpPost]
    public async Task<ActionResult<AiSampleReportResponse>> Generate(
        long sampleId,
        CancellationToken cancellationToken)
    {
        // İstekte bulunan kullanıcının kimlik bilgisini doğrula
        if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var currentUserId))
        {
            return Unauthorized();
        }

        // Yapay zeka servis motorunu çalıştırarak numune için raporu üret
        var result = await aiSampleReportService.GenerateAsync(
            sampleId,
            currentUserId,
            User.IsInRole(nameof(UserRole.Admin)),
            cancellationToken);

        // Başarılı ise üretilen AI raporunu ve atıfları döndür
        if (result.Succeeded)
        {
            return Ok(result.Value);
        }

        // Hata durumlarını HTTP durum kodları ile eşleştir (örn. Ollama kapalıysa 503 Service Unavailable)
        return result.Error switch
        {
            ServiceError.NotFound => NotFound(),
            ServiceError.Forbidden => Forbid(),
            // Yerel Ollama LLM servisine erişilemediğinde 503 döndürülür
            ServiceError.ExternalService => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = result.ErrorMessage }),
            _ => ValidationProblem(result.ErrorMessage)
        };
    }
}

