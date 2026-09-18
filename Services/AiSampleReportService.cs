using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Services;

// Numune analiz sonuçlarını yerel yapay zeka (LLM) ve RAG (bilgi tabanı) ile birleştirerek
// analitik değerlendirme raporları üreten temel iş mantığı servisi.
public sealed class AiSampleReportService(
    SampleService sampleService,
    SampleAnalysisService sampleAnalysisService,
    KnowledgeBaseService knowledgeBaseService,
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> options)
{
    // Yapay zeka tarafından üretilen raporun yalnızca karar destek amaçlı olduğunu belirten yasal uyarı metni.
    private const string Disclaimer =
        "Bu AI raporu yalnızca inceleme desteğidir; teknik karar, onay veya durum değişikliği üretmez.";

    // Modelin uygunsuz veya yetkisiz karar ifadeleri üretmesi durumunda gösterilen güvenli filtreleme mesajı.
    private const string FilteredSummary =
        "Model özeti teknik karar içerdiği için gösterilmedi. Kayıtlı analiz ve ölçüm bilgilerini inceleyin.";

    // JSON çözümleme ve serileştirme seçenekleri.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Modelin resmî veya nihai onay niteliğinde ("kesinlikle onaylanmıştır", "piyasaya sürülebilir" vb.)
    // yetkisiz yasal hüküm bildirmesini engelleyen güvenlik regex filtresi.
    private static readonly Regex UnsupportedDecisionPattern = new(
        @"\b(kesinlikle\s+onaylanm\p{L}*|yasal\s+onay\p{L}*|piyasaya\s+sürülebilir\p{L}*|tüketime\s+sunulabilir\p{L}*|resmi\s+olarak\s+onayl\p{L}*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Belirtilen numune için RAG destekli yapay zeka analiz raporu üretir.
    public async Task<ServiceResult<AiSampleReportResponse>> GenerateAsync(
        long sampleId,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        // 1. Numuneye ait analiz kayıtlarını veritabanından çek.
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

        // 2. Numunenin temel kimlik ve açıklama bilgilerini sorgula.
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

        // 3. Her analizin parametre ölçüm sonuçlarını ve referans limitlerini detaylı olarak topla (maksimum 20 analiz).
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

        // 4. Numune ve analiz verilerini yapılandırılmış veri modeline dönüştür.
        var reportData = new SampleReportData(sample, details);

        // 5. RAG (Retrieval-Augmented Generation): Bilgi tabanında numune türü ve analiz kodlarıyla eşleşen prosedür ve kılavuzları ara.
        var knowledgeSources = await FindKnowledgeSourcesAsync(reportData, cancellationToken);

        // Modele aktarılacak en yüksek benzerliğe sahip en fazla 2 bilgi kaynağını seç.
        var retrievedSources = knowledgeSources.Take(2).ToArray();

        try
        {
            // 6. Numune verilerini ve getirilen RAG dokümanlarını içeren prompt'u oluşturup Ollama LLM servisine gönder.
            var rawResponse = await ollamaClient.GenerateSampleReportAsync(
                BuildPrompt(reportData, retrievedSources),
                cancellationToken);

            // 7. Modelin döndürdüğü yapılandırılmış JSON çıktısını çözümle.
            var modelResponse = JsonSerializer.Deserialize<AiSampleReportModelResponse>(
                rawResponse,
                JsonOptions);

            // 8. Modelin ürettiği cümleleri doğrula (uzunluk, boşluk, kaynak numarası aralığı ve yetkisiz onay kontrolü).
            var summarySentences = NormalizeSummarySentences(
                modelResponse?.SummarySentences,
                retrievedSources.Length);

            // Model geçerli hiçbir cümle üretemediyse uygulamanın güvenli kural tabanlı yedeğine (fallback) dön.
            if (summarySentences.Count == 0)
            {
                return ServiceResult<AiSampleReportResponse>.Success(
                    CreateApplicationFallback(reportData, knowledgeSources));
            }

            // 9. Cümlelerde gerçekten atıfta bulunulan kaynak numaralarını tespit et.
            var usedSourceNumbers = summarySentences
                .SelectMany(sentence => sentence.SourceNumbers)
                .Distinct()
                .ToHashSet();

            // 10. Sonuç yanıtını oluştur; doğrulanmış cümleleri, kullanılan kaynakları ve model adını ekle.
            var response = CreateApplicationFallback(reportData, knowledgeSources) with
            {
                // Cümleleri birleştirerek genel özet metnini oluştur
                Summary = string.Join(' ', summarySentences.Select(sentence => sentence.Text)),
                SummarySentences = summarySentences,
                KnowledgeSources = ToResponseSources(retrievedSources, usedSourceNumbers),
                Model = options.Value.Model
            };

            return ServiceResult<AiSampleReportResponse>.Success(response);
        }
        // Ollama modeli beklenmeyen biçimde yanıt verdiğinde veya çöktüğünde
        catch (OllamaClientException exception)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                $"Yerel AI modeli geçerli bir numune raporu üretemedi ({exception.Message}).");
        }
        // Ollama sunucusu kapalıysa veya ağ erişimi yoksa
        catch (HttpRequestException)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                "Yerel Ollama servisine ulaşılamadı. Ollama'nın açık olduğunu kontrol edin.");
        }
        // Model yanıt verirken zaman aşımına uğradıysa
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<AiSampleReportResponse>.Failure(
                ServiceError.ExternalService,
                "Yerel AI modeli rapor üretirken zaman aşımına uğradı. Tekrar deneyin.");
        }
        // Model JSON dışında bir metin ürettiyse güvenli uygulama yedeği sunulur
        catch (JsonException)
        {
            return ServiceResult<AiSampleReportResponse>.Success(
                CreateApplicationFallback(reportData, knowledgeSources));
        }
    }

    // RAG araması: Numunenin türü, açıklaması ve analiz kodlarını birleştirerek bilgi tabanında anlamsal eşleşme arar.
    private async Task<IReadOnlyList<KnowledgeSearchItem>> FindKnowledgeSourcesAsync(
        SampleReportData reportData,
        CancellationToken cancellationToken)
    {
        // Arama sorgusu metni: Numune türü + numune açıklaması + analiz kod ve adları
        var searchText = string.Join(
            ' ',
            new[]
            {
                reportData.Sample.SampleType,
                reportData.Sample.Description,
                string.Join(' ', reportData.Analyses.Select(analysis =>
                    $"{analysis.AnalysisCode} {analysis.AnalysisName}"))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        // Tercih edilen analiz kodları ID listesi
        var analysisCodeIds = reportData.Analyses
            .Select(analysis => analysis.AnalysisCodeId)
            .Distinct()
            .ToArray();

        // Bilgi tabanında embedding vektörü üzerinden kosinüs benzerliği araması yap
        var searchResult = await knowledgeBaseService.SearchAsync(
            searchText,
            analysisCodeIds,
            cancellationToken);

        return searchResult.Succeeded ? searchResult.Value! : [];
    }

    // Yerel LLM'e iletilecek olan prompt'u (istem metnini) inşa eden metod.
    // Prompt Mühendisliği: Laboratuvar verilerini JSON olarak gömer ve RAG kaynaklarını numaralandırılmış olarak ekler.
    private static string BuildPrompt(
        SampleReportData reportData,
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources)
    {
        // Model için hafifletilmiş, temiz numune ve ölçüm verisi nesnesi
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

        // LLM Talimatları:
        // - En fazla 5 öz Türkçe cümle üret.
        // - Her cümle kayıtlara veya RAG kaynaklarına dayanmalıdır.
        // - Referans sınır aşımı varsa belirt, teknisyen notlarını özetle.
        // - Yasal onay veya nihai piyasa onayı verme.
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

    // Model çalışmadığında veya geçersiz yanıt verdiğinde kullanılan deterministik uygulama yedeği (Fallback).
    // Veritabanındaki gerçek durumları (tamamlanan, bekleyen, limit dışı parametreler) kod mantığıyla özetler.
    private AiSampleReportResponse CreateApplicationFallback(
        SampleReportData reportData,
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources)
    {
        // Tamamlanan analizler listesi
        var completed = reportData.Analyses
            .Where(analysis => analysis.Status.ToString() == "Completed")
            .Select(FormatAnalysis)
            .Take(8)
            .ToArray();

        // Devam eden veya beklemede olan analizler listesi
        var pending = reportData.Analyses
            .Where(analysis => analysis.Status.ToString() is "Requested" or "InProgress")
            .Select(FormatAnalysis)
            .Take(8)
            .ToArray();

        // İptal edilen analiz sayısı
        var cancelledCount = reportData.Analyses.Count(
            analysis => analysis.Status.ToString() == "Cancelled");

        // Zorunlu olduğu halde sonucu girilmemiş parametreler
        var missingRequired = reportData.Analyses
            .SelectMany(analysis => analysis.Parameters
                .Where(parameter => parameter.IsRequired && parameter.Result is null)
                .Select(parameter => $"{analysis.AnalysisCode} · {parameter.Name}"))
            .Take(8)
            .ToArray();

        // Referans limitleri dışına çıkan ölçümlerin sayısı
        var outsideReferenceCount = reportData.Analyses
            .SelectMany(analysis => analysis.Parameters)
            .Count(parameter => parameter.Result?.IsOutsideReference == true);

        // Dikkat edilmesi gereken operasyonel uyarı noktaları
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

        // Güvenli uygulama özeti yanıtını döndür
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

    // RAG bilgi kaynaklarını arayüzde gösterilmek üzere biçimlendirir.
    private static IReadOnlyList<AiReportKnowledgeSource> ToResponseSources(
        IReadOnlyList<KnowledgeSearchItem> knowledgeSources,
        IReadOnlySet<int> usedSourceNumbers) =>
        knowledgeSources
            .Take(2)
            .Select((source, index) => new { Source = source, SourceNumber = index + 1 })
            // Sadece modelin gerçekten atıf yaptığı kaynakları dahil et
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

    // Modelin ürettiği cümleleri temizler, güvenlik kurallarına göre filtreler ve kaynak aralıklarını doğrular.
    private static IReadOnlyList<AiReportSentence> NormalizeSummarySentences(
        IReadOnlyList<AiSampleReportModelSentence>? values,
        int sourceCount) =>
        values?
            // Boş cümleleri ve yetkisiz onay ifadesi içeren cümleleri filtrele
            .Where(value => !string.IsNullOrWhiteSpace(value.Text)
                            && !ContainsUnsupportedDecision(value.Text!))
            .Select(value => new AiReportSentence(
                TrimTo(value.Text!, 230),
                value.UsesRecordData,
                value.SourceNumbers?
                    .Distinct()
                    // Sadece modele iletilen geçerli kaynak numaralarını kabul et
                    .Where(number => number >= 1 && number <= sourceCount)
                    .Take(2)
                    .ToArray()
                ?? []))
            // Yalnızca kayıt verisi kullanan veya geçerli bir kaynağa atıf yapan cümleleri koru
            .Where(sentence => sentence.UsesRecordData || sentence.SourceNumbers.Count > 0)
            .Take(5)
            .ToArray()
        ?? [];

    // Analiz kod ve adını biçimlendirir
    private static string FormatAnalysis(SampleAnalysisDetail analysis) =>
        $"{analysis.AnalysisCode} · {analysis.AnalysisName} ({analysis.Status})";

    // Metin listesini normalize eder
    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string?>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value)
                            && !ContainsUnsupportedDecision(value!))
            .Take(8)
            .Select(value => TrimTo(value!, 400))
            .ToArray()
        ?? [];

    // Özet metnini uzunluk ve güvenlik açısından filtreler
    private static string NormalizeSummary(string value)
    {
        var summary = TrimTo(value, 1500);
        return ContainsUnsupportedDecision(summary)
            ? FilteredSummary
            : summary;
    }

    // Metnin yasaklı/onaylayıcı karar ifadeleri içerip içermediğini denetler
    private static bool ContainsUnsupportedDecision(string value) =>
        UnsupportedDecisionPattern.IsMatch(value);

    // Metni belirtilen maksimum karakter uzunluğuna göre keser
    private static string TrimTo(string value, int maximumLength) =>
        value.Trim().Length <= maximumLength
            ? value.Trim()
            : value.Trim()[..maximumLength];

    // RAG doküman içeriğinden arayüz için kısa bir alıntı üretir
    private static string CreateExcerpt(string value) =>
        TrimAtWordBoundary(value, 240);

    // Metni kelime bütünlüğünü bozmadan belirli bir uzunlukta keser
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

    // Rapor üretimi için numune ve analiz detaylarını tutan iç veri yapısı.
    private sealed record SampleReportData(
        SampleDetail Sample,
        IReadOnlyList<SampleAnalysisDetail> Analyses);
}

