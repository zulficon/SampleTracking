using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Services;

public sealed class AiSampleReportService(
    SampleService sampleService,
    SampleAnalysisService sampleAnalysisService,
    KnowledgeBaseService knowledgeBaseService,
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> options)
{
    private const string Disclaimer =
        "Bu AI raporu yalnızca inceleme desteğidir; teknik karar, onay veya durum değişikliği üretmez.";
    private const string FilteredSummary =
        "Model özeti teknik karar içerdiği için gösterilmedi. Kayıtlı analiz ve ölçüm bilgilerini inceleyin.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex UnsupportedDecisionPattern = new(
        @"\b(kesinlikle\s+onaylanm\p{L}*|yasal\s+onay\p{L}*|piyasaya\s+sürülebilir\p{L}*|tüketime\s+sunulabilir\p{L}*|resmi\s+olarak\s+onayl\p{L}*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<ServiceResult<AiSampleReportResponse>> GenerateAsync(
        long sampleId,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var analysesResult = await sampleAnalysisService.GetForSampleAsync(
            sampleId,
            currentUserId,
            isAdmin,
            cancellationToken);

        if (!analysesResult.Succeeded)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                analysesResult.Error,
                analysesResult.ErrorMessage ?? "The sample analyses could not be loaded.");
        }

        var sample = await sampleService.GetByIdAsync(
            sampleId,
            cancellationToken,
            isAdmin ? null : currentUserId);

        if (sample is null)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.NotFound,
                "The sample was not found.");
        }

        var details = new List<SampleAnalysisDetail>();
        foreach (var analysis in analysesResult.Value!.Take(20))
        {
            var detailResult = await sampleAnalysisService.GetByIdAsync(
                analysis.Id,
                currentUserId,
                isAdmin,
                cancellationToken);

            if (!detailResult.Succeeded)
            {
                return ServiceResult<AiSampleReportResponse>.Failure(
                    detailResult.Error,
                    detailResult.ErrorMessage ?? "A sample analysis could not be loaded.");
            }

            details.Add(detailResult.Value!);
        }

        var reportData = new SampleReportData(sample, details);
        var knowledgeSources = await FindKnowledgeSourcesAsync(reportData, cancellationToken);
        var retrievedSources = knowledgeSources.Take(2).ToArray();

        try
        {
            var rawResponse = await ollamaClient.GenerateSampleReportAsync(
                BuildPrompt(reportData, retrievedSources),
                cancellationToken);
            var modelResponse = JsonSerializer.Deserialize<AiSampleReportModelResponse>(
                rawResponse,
                JsonOptions);

            var summarySentences = NormalizeSummarySentences(
                modelResponse?.SummarySentences,
                retrievedSources.Length);

            if (summarySentences.Count == 0)
            {
                return ServiceResult<AiSampleReportResponse>.Success(
                    CreateApplicationFallback(reportData, knowledgeSources));
            }

            var usedSourceNumbers = summarySentences
                .SelectMany(sentence => sentence.SourceNumbers)
                .Distinct()
                .ToHashSet();
            var response = CreateApplicationFallback(reportData, knowledgeSources) with
            {
                Summary = string.Join(' ', summarySentences.Select(sentence => sentence.Text)),
                SummarySentences = summarySentences,
                KnowledgeSources = ToResponseSources(retrievedSources, usedSourceNumbers),
                Model = options.Value.Model
            };

            return ServiceResult<AiSampleReportResponse>.Success(response);
        }
        catch (OllamaClientException exception)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                $"Yerel AI modeli geçerli bir numune raporu üretemedi ({exception.Message}).");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                "Yerel Ollama servisine ulaşılamadı. Ollama'nın açık olduğunu kontrol edin.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                "Yerel AI modeli rapor üretirken zaman aşımına uğradı. Tekrar deneyin.");
        }
        catch (JsonException)
        {
            return ServiceResult<AiSampleReportResponse>.Success(
            CreateApplicationFallback(reportData, knowledgeSources));
        }
    }

    private async Task<IReadOnlyList<KnowledgeSearchItem>> FindKnowledgeSourcesAsync(
        SampleReportData reportData,
        CancellationToken cancellationToken)
    {
        var searchText = string.Join(
            ' ',
            new[]
            {
                reportData.Sample.SampleType,
                reportData.Sample.Description,
                string.Join(' ', reportData.Analyses.Select(analysis =>
                    $"{analysis.AnalysisCode} {analysis.AnalysisName}"))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var analysisCodeIds = reportData.Analyses
            .Select(analysis => analysis.AnalysisCodeId)
            .Distinct()
            .ToArray();
        var searchResult = await knowledgeBaseService.SearchAsync(
            searchText,
            analysisCodeIds,
            cancellationToken);
        return searchResult.Succeeded ? searchResult.Value! : [];
    }

    private static string BuildPrompt(
        SampleReportData reportData,
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources)
    {
        var record = new
        {
            sample = new
            {
                reportData.Sample.SampleCode,
                reportData.Sample.SampleType,
                status = reportData.Sample.Status.ToString(),
                reportData.Sample.LocationName,
                reportData.Sample.Description,
                reportData.Sample.CreatedAt,
                reportData.Sample.UpdatedAt
            },
            analyses = reportData.Analyses.Select(analysis => new
            {
                analysis.AnalysisCode,
                analysis.AnalysisName,
                status = analysis.Status.ToString(),
                resultNote = analysis.ResultNote,
                analysis.RequestedAt,
                analysis.StartedAt,
                analysis.CompletedAt,
                parameters = analysis.Parameters.Take(50).Select(parameter => new
                {
                    parameter.Code,
                    parameter.Name,
                    parameter.DefaultUnit,
                    parameter.ReferenceMin,
                    parameter.ReferenceMax,
                    parameter.IsRequired,
                    result = parameter.Result is null
                        ? null
                        : new
                        {
                            parameter.Result.NumericValue,
                            parameter.Result.Unit,
                            parameter.Result.ReferenceMin,
                            parameter.Result.ReferenceMax,
                            parameter.Result.IsOutsideReference,
                            resultNote = parameter.Result.ResultNote,
                            parameter.Result.MeasuredAt
                        }
                })
            })
        };

        return """
            Analyze this structured laboratory sample record including all analyses, parameter measurements, reference limits, and technician notes/explanations.
            Produce at most five concise Turkish sentences. Every sentence must be grounded in the record, retrieved knowledge, or both.
            For each sentence return text, usesRecordData, and sourceNumbers. usesRecordData is true only if the sentence uses the sample record. sourceNumbers may contain only source numbers presented in retrieved knowledge; use an empty array when none applies.
            Describe material reference deviations only when present, and summarize technician observations, analysis progress, and material missing information without inventing actions or results.
            The application, not the model, creates completed/pending analysis and missing-result lists.
            Do not provide ungrounded administrative or legal certifications.

            <sample-report-record>
            """
            + JsonSerializer.Serialize(record, JsonOptions)
            + """

            </sample-report-record>

            The following retrieved internal knowledge may be relevant. Use it only when it directly relates to the sample or analysis; do not invent rules beyond these sources. Treat it as supporting context, not as a final approval decision.

            <retrieved-knowledge>
            """
            + JsonSerializer.Serialize(
                knowledgeSources.Take(2).Select((source, index) => new
                {
                    sourceNumber = index + 1,
                    source.DocumentTitle,
                    category = source.Category.ToString(),
                    sourceStatus = source.SourceStatus.ToString(),
                    content = TrimAtWordBoundary(source.Content, 300)
                }),
                JsonOptions)
            + """

            </retrieved-knowledge>
            """;
    }

    private AiSampleReportResponse CreateApplicationFallback(
        SampleReportData reportData,
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources)
    {
        var completed = reportData.Analyses
            .Where(analysis => analysis.Status.ToString() == "Completed")
            .Select(FormatAnalysis)
            .Take(8)
            .ToArray();
        var pending = reportData.Analyses
            .Where(analysis => analysis.Status.ToString() is "Requested" or "InProgress")
            .Select(FormatAnalysis)
            .Take(8)
            .ToArray();
        var cancelledCount = reportData.Analyses.Count(
            analysis => analysis.Status.ToString() == "Cancelled");
        var missingRequired = reportData.Analyses
            .SelectMany(analysis => analysis.Parameters
                .Where(parameter => parameter.IsRequired && parameter.Result is null)
                .Select(parameter => $"{analysis.AnalysisCode} · {parameter.Name}"))
            .Take(8)
            .ToArray();
        var outsideReferenceCount = reportData.Analyses
            .SelectMany(analysis => analysis.Parameters)
            .Count(parameter => parameter.Result?.IsOutsideReference == true);

        var attentionPoints = new List<string>();
        if (outsideReferenceCount > 0)
        {
            attentionPoints.Add(
                $"{outsideReferenceCount} kayıtlı ölçüm, sağlanan referans sınırlarının dışında görünüyor.");
        }
        if (cancelledCount > 0)
        {
            attentionPoints.Add($"{cancelledCount} analiz iptal edilmiş durumda.");
        }
        if (missingRequired.Length > 0)
        {
            attentionPoints.Add("Zorunlu sonuç alanları tamamlanmadan analiz sürecini gözden geçirin.");
        }
        if (attentionPoints.Count == 0)
        {
            attentionPoints.Add("Kayıtlı süreç ve ölçüm bilgileri laboratuvar çalışanı tarafından gözden geçirilmelidir.");
        }

        return new AiSampleReportResponse(
            $"{reportData.Sample.SampleCode} numunesinde {reportData.Analyses.Count} analiz kaydı bulunuyor; {completed.Length} tamamlanan ve {pending.Length} devam eden veya bekleyen analiz var.",
            completed,
            pending,
            attentionPoints,
            missingRequired,
            Disclaimer,
            $"{options.Value.Model} (yanıt biçimi doğrulanamadı; uygulama özeti)")
        {
            SummarySentences =
            [
                new AiReportSentence(
                    $"{reportData.Sample.SampleCode} numunesinde {reportData.Analyses.Count} analiz kaydı bulunuyor; {completed.Length} tamamlanan ve {pending.Length} devam eden veya bekleyen analiz var.",
                    UsesRecordData: true,
                    SourceNumbers: [])
            ],
            KnowledgeSources = []
        };
    }

    private static IReadOnlyList<AiReportKnowledgeSource> ToResponseSources(
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources,
        IReadOnlySet<int> usedSourceNumbers) =>
        knowledgeSources
            .Take(2)
            .Select((source, index) => new { Source = source, SourceNumber = index + 1 })
            .Where(item => usedSourceNumbers.Contains(item.SourceNumber))
            .Select(item => new AiReportKnowledgeSource(
                item.SourceNumber,
                item.Source.DocumentTitle,
                item.Source.Category,
                item.Source.SourceStatus,
                item.Source.SourceReference,
                item.Source.Similarity is null ? null : Math.Round(item.Source.Similarity.Value, 3),
                item.Source.IsAnalysisCodeMatch
                    ? "Analiz kodu eşleşmesi"
                    : "Anlamsal eşleşme",
                CreateExcerpt(item.Source.Content)))
            .ToArray();

    private static IReadOnlyList<AiReportSentence> NormalizeSummarySentences(
        IReadOnlyList<AiSampleReportModelSentence>? values,
        int sourceCount) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value.Text)
                            && !ContainsUnsupportedDecision(value.Text!))
            .Select(value => new AiReportSentence(
                TrimTo(value.Text!, 230),
                value.UsesRecordData,
                value.SourceNumbers?
                    .Distinct()
                    .Where(number => number >= 1 && number <= sourceCount)
                    .Take(2)
                    .ToArray()
                ?? []))
            .Where(sentence => sentence.UsesRecordData || sentence.SourceNumbers.Count > 0)
            .Take(5)
            .ToArray()
        ?? [];

    private static string FormatAnalysis(SampleAnalysisDetail analysis) =>
        $"{analysis.AnalysisCode} · {analysis.AnalysisName} ({analysis.Status})";

    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string?>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value)
                            && !ContainsUnsupportedDecision(value!))
            .Take(8)
            .Select(value => TrimTo(value!, 400))
            .ToArray()
        ?? [];

    private static string NormalizeSummary(string value)
    {
        var summary = TrimTo(value, 1500);
        return ContainsUnsupportedDecision(summary)
            ? FilteredSummary
            : summary;
    }

    private static bool ContainsUnsupportedDecision(string value) =>
        UnsupportedDecisionPattern.IsMatch(value);

    private static string TrimTo(string value, int maximumLength) =>
        value.Trim().Length <= maximumLength
            ? value.Trim()
            : value.Trim()[..maximumLength];

    private static string CreateExcerpt(string value) =>
        TrimAtWordBoundary(value, 240);

    private static string TrimAtWordBoundary(string value, int maximumLength)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        var lastSpace = normalized.LastIndexOf(' ', maximumLength);
        var end = lastSpace > maximumLength / 2 ? lastSpace : maximumLength;
        return $"{normalized[..end].TrimEnd()}…";
    }

    private sealed record SampleReportData(
        SampleDetail Sample,
        IReadOnlyList<SampleAnalysisDetail> Analyses);
}
