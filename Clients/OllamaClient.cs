using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SampleAnalysisTracking.Options;
using Microsoft.Extensions.Options;

namespace SampleAnalysisTracking.Clients;

// Ollama yerel yapay zeka sunucusu ile REST API üzerinden iletişim kuran ana LLM istemcisi.
// Yapılandırılmış JSON şemaları (Structured Outputs), sistem yönergeleri (System Prompts)
// ve hiperparametre denetimi (temperature, context window vb.) uygular.
public sealed class OllamaClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaClient> logger) : IOllamaClient
{
    // Numune analiz raporu için yerel LLM modeline çağrı yapar.
    // summarySentences, text, usesRecordData ve sourceNumbers alanlarını zorunlu kılan katı JSON şeması ile çalışır.
    public async Task<string> GenerateSampleReportAsync(
        string prompt,
        CancellationToken cancellationToken)
        => await GenerateAsync(
            prompt,
            CreateSampleReportResponseSchema(),
            "Return exactly one JSON object with summarySentences. Every item must have text, usesRecordData, and sourceNumbers. Do not return any other fields.",
            cancellationToken);

    // Laboratuvar iş yükü ve performans içgörüsü için yerel LLM modeline çağrı yapar.
    // summary, keyObservations ve recommendations alanlarını içeren JSON şeması ile çalışır.
    public async Task<string> GenerateWorkloadInsightAsync(
        string prompt,
        CancellationToken cancellationToken)
        => await GenerateAsync(
            prompt,
            CreateResponseSchema("keyObservations", "recommendations"),
            "Return exactly one JSON object with these fields: summary, keyObservations, recommendations. keyObservations and recommendations must be JSON string arrays.",
            cancellationToken);

    // Belirtilen JSON şeması ve sistem prompt'u ile Ollama /api/chat uç noktasına istek gönderen merkezi metot.
    private async Task<string> GenerateAsync(
        string prompt,
        object responseFormat,
        string responseSchema,
        CancellationToken cancellationToken)
    {
        // Ollama API'sine gönderilecek sohbet (chat) oturumu ve hiperparametre nesnesi.
        var request = new
        {
            // Kullanılacak LLM modeli (örn. qwen3:4b-instruct)
            model = options.Value.Model,
            // Modele aktarılan mesaj listesi (Sistem Rolü + Kullanıcı Rolü)
            messages = new[]
            {
                // Sistem prompt'u: Modelin rolünü, dilini (Türkçe), sınırlarını ve halüsinasyon koruma kurallarını belirler.
                new
                {
                    role = "system",
                    content = """
                        You are a specialized laboratory data analysis and decision-support assistant communicating in clear, professional Turkish.
                        Your objective is to provide analytical, context-aware, and varied synthesis of laboratory data without being repetitive or rigid.
                        
                        Guidelines:
                        1. Ground your observations strictly in the provided data, numeric readings, reference limits, and technician notes.
                        2. Clearly describe whether values stay within expected reference bounds or exceed minimum/maximum thresholds.
                        3. Vary your wording and sentence structures naturally to reflect the specific context and characteristics of each sample, analysis, or workload.
                        4. Do not issue final administrative/legal product certifications (e.g. do not state "resmi onay verilmiştir" or "piyasaya sürülebilir").
                        5. Output pure JSON without Markdown code fences.
                        """
                    + Environment.NewLine
                    + responseSchema
                },
                // Kullanıcı girdisi: Numune analiz verileri ve RAG ile getirilmiş bilgi kaynakları
                new { role = "user", content = prompt }
            },
            // Yanıtın parça parça (streaming) değil, tek seferde tamamlanmasını sağlar.
            stream = false,
            // Düşünce (reasoning tokens) zincirinin çıktıya dahil edilmesini kapatır.
            think = false,
            // Modelin yalnızca belirtilen JSON şemasına uygun çıktı üretmesini zorunlu kılar (Grammar / Constrained Decoding).
            format = responseFormat,
            // Modelin RAM/VRAM'de sıcak tutulma süresi
            keep_alive = options.Value.KeepAlive,
            // Model çalıştırma hiperparametreleri
            options = new
            {
                // Düşük sıcaklık (örn. 0.35): Halüsinasyonu azaltır, deterministik ve verilere sadık yanıtlar üretir.
                temperature = options.Value.Temperature,
                // Top-p (nucleus sampling): Olasılık dağılımının en olası yüzdesini sınırlandırır.
                top_p = options.Value.TopP,
                // Modelin hafızasında tutabileceği maksimum girdi+çıktı token sayısı (bağlam penceresi).
                num_ctx = options.Value.ContextWindow,
                // Modelin üretebileceği maksimum yanıt token sayısı.
                num_predict = options.Value.MaxOutputTokens
            }
        };

        // Ollama /api/chat uç noktasına HTTP POST isteği gönderilir.
        using var response = await httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

        // Ollama HTTP hatası verirse (servis hatası veya model yüklenememe), hata içeriği okunarak özel exception fırlatılır.
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorDetail = string.IsNullOrWhiteSpace(errorBody)
                ? "No error details were returned."
                : errorBody.Trim()[..Math.Min(errorBody.Trim().Length, 500)];

            logger.LogWarning(
                "Ollama returned HTTP {StatusCode} while generating an analysis insight: {ErrorDetail}",
                (int)response.StatusCode,
                errorDetail);
            throw new OllamaClientException(
                $"Ollama returned HTTP {(int)response.StatusCode}: {errorDetail}");
        }

        try
        {
            // Ollama sohbet yanıtı JSON olarak çözümlenir.
            var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            // Model yanıtı boş döndüyse hata üretilir.
            if (string.IsNullOrWhiteSpace(payload?.Message?.Content))
            {
                logger.LogWarning("Ollama returned an empty analysis insight response.");
                throw new OllamaClientException("Ollama returned an empty response.");
            }

            // Modelin bitiş nedeni kontrol edilir (beklenen "stop" tur; token limiti veya bağlam dolması uyarılabilir).
            if (!string.IsNullOrWhiteSpace(payload.DoneReason)
                && !string.Equals(payload.DoneReason, "stop", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Ollama generation finished with reason {DoneReason}.",
                    payload.DoneReason);
            }

            // Dönen metin içerisindeki JSON nesnesi ayıklanır (markdown veya ek boşluk temizliği).
            return ExtractJsonObject(payload.Message.Content);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Ollama returned an unreadable chat response.");
            throw new OllamaClientException(
                "Ollama returned an unreadable response.",
                exception);
        }
    }

    // Ollama API'sinin sohbet yanıt şeması.
    private sealed record OllamaChatResponse(
        OllamaChatMessage? Message,
        [property: JsonPropertyName("done_reason")] string? DoneReason);
    // Sohbet mesajının içeriği.
    private sealed record OllamaChatMessage(string? Content);

    // Genel içgörü şablonu oluşturan yardımcı metot.
    private static object CreateResponseSchema(params string[] arrayProperties) =>
        CreateResponseSchema(550, arrayProperties);

    // Numune raporu çıktısının JSON Schema kurallarını belirler.
    // Her cümlenin text, usesRecordData (veritabanı verisi kullanıldı mı) ve sourceNumbers (RAG kaynakları) içermesini zorunlu kılar.
    private static object CreateSampleReportResponseSchema() => new
    {
        type = "object",
        properties = new
        {
            summarySentences = new
            {
                type = "array",
                minItems = 1,
                maxItems = 5,
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", maxLength = 230 },
                        usesRecordData = new { type = "boolean" },
                        sourceNumbers = new
                        {
                            type = "array",
                            maxItems = 2,
                            items = new { type = "integer", minimum = 1 }
                        }
                    },
                    required = new[] { "text", "usesRecordData", "sourceNumbers" },
                    additionalProperties = false
                }
            }
        },
        required = new[] { "summarySentences" },
        additionalProperties = false
    };

    // Dinamik dizi özellikleri alan JSON şeması oluşturur (iş yükü içgörüleri vb. için).
    private static object CreateResponseSchema(
        int summaryMaximumLength,
        params string[] arrayProperties)
    {
        var properties = new Dictionary<string, object>
        {
            ["summary"] = new { type = "string", maxLength = summaryMaximumLength }
        };

        foreach (var property in arrayProperties)
        {
            properties[property] = new
            {
                type = "array",
                maxItems = 3,
                items = new { type = "string" }
            };
        }

        return new
        {
            type = "object",
            properties,
            required = properties.Keys.ToArray(),
            additionalProperties = false
        };
    }

    // Model yanıtındaki ilk '{' ve son '}' karakterlerini bularak saf JSON bloğunu izole eder.
    private static string ExtractJsonObject(string content)
    {
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');

        return firstBrace >= 0 && lastBrace > firstBrace
            ? content[firstBrace..(lastBrace + 1)]
            : content.Trim();
    }
}

// Ollama API hataları veya geçersiz model çıktıları için özel istisna sınıfı.
public sealed class OllamaClientException : Exception
{
    public OllamaClientException(string message) : base(message)
    {
    }

    public OllamaClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
