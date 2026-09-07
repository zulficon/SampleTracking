using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SampleAnalysisTracking.Options;
using Microsoft.Extensions.Options;

namespace SampleAnalysisTracking.Clients;

public sealed class OllamaClient(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaClient> logger) : IOllamaClient
{
    public async Task<string> GenerateSampleReportAsync(
        string prompt,
        CancellationToken cancellationToken)
        => await GenerateAsync(
            prompt,
            CreateSampleReportResponseSchema(),
            "Return exactly one JSON object with summarySentences. Every item must have text, usesRecordData, and sourceNumbers. Do not return any other fields.",
            cancellationToken);

    public async Task<string> GenerateWorkloadInsightAsync(
        string prompt,
        CancellationToken cancellationToken)
        => await GenerateAsync(
            prompt,
            CreateResponseSchema("keyObservations", "recommendations"),
            "Return exactly one JSON object with these fields: summary, keyObservations, recommendations. keyObservations and recommendations must be JSON string arrays.",
            cancellationToken);

    private async Task<string> GenerateAsync(
        string prompt,
        object responseFormat,
        string responseSchema,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = options.Value.Model,
            messages = new[]
            {
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
                new { role = "user", content = prompt }
            },
            stream = false,
            think = false,
            format = responseFormat,
            keep_alive = options.Value.KeepAlive,
            options = new
            {
                temperature = options.Value.Temperature,
                top_p = options.Value.TopP,
                num_ctx = options.Value.ContextWindow,
                num_predict = options.Value.MaxOutputTokens
            }
        };

        using var response = await httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

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
            var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            if (string.IsNullOrWhiteSpace(payload?.Message?.Content))
            {
                logger.LogWarning("Ollama returned an empty analysis insight response.");
                throw new OllamaClientException("Ollama returned an empty response.");
            }

            if (!string.IsNullOrWhiteSpace(payload.DoneReason)
                && !string.Equals(payload.DoneReason, "stop", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Ollama generation finished with reason {DoneReason}.",
                    payload.DoneReason);
            }

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

    private sealed record OllamaChatResponse(
        OllamaChatMessage? Message,
        [property: JsonPropertyName("done_reason")] string? DoneReason);
    private sealed record OllamaChatMessage(string? Content);

    private static object CreateResponseSchema(params string[] arrayProperties) =>
        CreateResponseSchema(550, arrayProperties);

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

    private static string ExtractJsonObject(string content)
    {
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');

        return firstBrace >= 0 && lastBrace > firstBrace
            ? content[firstBrace..(lastBrace + 1)]
            : content.Trim();
    }
}

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
