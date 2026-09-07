using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Services;

public sealed class PerformanceAnalyticsService(
    SampleAnalysisTrackingDbContext db,
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> options,
    ILogger<PerformanceAnalyticsService> logger)
{
    private const string Disclaimer =
        "Bu AI iş yükü özeti karar destek ve inceleme amaçlıdır; personel değerlendirme veya idari yaptırım niteliği taşımaz.";
    private const string FilteredSummary =
        "Model özeti uygunsuz karar veya etiketleme içerdiği için gösterilmedi. İstatistik tablosunu inceleyin.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex UnsupportedDecisionPattern = new(
        @"\b(yetersiz\p{L}*|başarısız\p{L}*|tembel\p{L}*|cezalandır\p{L}*|incompetent\p{L}*|punish\p{L}*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<PerformanceOverviewResponse> GetPerformanceOverviewAsync(
        PerformanceQuery query,
        long currentUserId,
        bool isElevated,
        CancellationToken cancellationToken = default)
    {
        var (startDate, endDate) = ResolveDateRange(query);

        // Base queries with date filtering
        var samplesQuery = db.Samples.AsNoTracking();
        IQueryable<SampleAnalysis> analysesQuery = db.SampleAnalyses.AsNoTracking().Include(a => a.AnalysisCode);
        var resultsQuery = db.AnalysisResults.AsNoTracking();

        if (startDate.HasValue)
        {
            samplesQuery = samplesQuery.Where(s => s.CreatedAt >= startDate.Value);
            analysesQuery = analysesQuery.Where(a => a.RequestedAt >= startDate.Value);
            resultsQuery = resultsQuery.Where(r => r.MeasuredAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            samplesQuery = samplesQuery.Where(s => s.CreatedAt <= endDate.Value);
            analysesQuery = analysesQuery.Where(a => a.RequestedAt <= endDate.Value);
            resultsQuery = resultsQuery.Where(r => r.MeasuredAt <= endDate.Value);
        }

        var usersQuery = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (query.Role.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.Role == query.Role.Value);
        }
        else
        {
            // By default, focus on Laboratory and Field staff in performance overview
            usersQuery = usersQuery.Where(u => u.Role == UserRole.Laboratory || u.Role == UserRole.Field);
        }

        var users = await usersQuery
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var samples = await samplesQuery
            .Select(s => new { s.Id, s.CreatedById, s.AssignedToId, s.Status, s.CreatedAt })
            .ToListAsync(cancellationToken);

        var analyses = await analysesQuery
            .Select(a => new
            {
                a.Id,
                a.SampleId,
                a.AnalysisCodeId,
                AnalysisCode = a.AnalysisCode.Code,
                AnalysisName = a.AnalysisCode.Name,
                a.Status,
                a.RequestedById,
                a.StartedById,
                a.CompletedById,
                a.RequestedAt,
                a.StartedAt,
                a.CompletedAt
            })
            .ToListAsync(cancellationToken);

        var results = await resultsQuery
            .Select(r => new { r.Id, r.EnteredById, r.MeasuredAt })
            .ToListAsync(cancellationToken);

        var workerItems = new List<WorkerPerformanceItem>(users.Count);

        foreach (var user in users)
        {
            var assignedSamplesCount = samples.Count(s => s.AssignedToId == user.Id);
            var createdSamplesCount = samples.Count(s => s.CreatedById == user.Id);

            var userAnalyses = analyses.Where(a =>
                a.CompletedById == user.Id ||
                a.StartedById == user.Id ||
                (a.CompletedById == null && a.StartedById == null && samples.Any(s => s.Id == a.SampleId && s.AssignedToId == user.Id))
            ).ToList();

            var completedCount = analyses.Count(a => a.CompletedById == user.Id);
            var inProgressCount = analyses.Count(a => a.StartedById == user.Id && a.Status == AnalysisStatus.InProgress);
            var pendingCount = analyses.Count(a =>
                a.Status == AnalysisStatus.Requested &&
                samples.Any(s => s.Id == a.SampleId && s.AssignedToId == user.Id));
            var cancelledCount = analyses.Count(a =>
                a.Status == AnalysisStatus.Cancelled &&
                (a.StartedById == user.Id || samples.Any(s => s.Id == a.SampleId && s.AssignedToId == user.Id)));

            var totalAnalyses = completedCount + inProgressCount + pendingCount + cancelledCount;
            var totalMeasured = results.Count(r => r.EnteredById == user.Id);

            var completedWithDuration = analyses
                .Where(a => a.CompletedById == user.Id && a.StartedAt.HasValue && a.CompletedAt.HasValue && a.CompletedAt > a.StartedAt)
                .Select(a => (a.CompletedAt!.Value - a.StartedAt!.Value).TotalHours)
                .ToList();

            double? avgDuration = completedWithDuration.Count > 0
                ? Math.Round(completedWithDuration.Average(), 1)
                : null;

            var completionRate = totalAnalyses > 0
                ? Math.Round((decimal)completedCount / totalAnalyses * 100m, 1)
                : (user.Role == UserRole.Field && createdSamplesCount > 0 ? 100m : 0m);

            workerItems.Add(new WorkerPerformanceItem(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                assignedSamplesCount,
                createdSamplesCount,
                totalAnalyses,
                completedCount,
                inProgressCount,
                pendingCount,
                cancelledCount,
                totalMeasured,
                completionRate,
                avgDuration,
                FormatDuration(avgDuration)));
        }

        // Sort: Laboratory workers with most completions first, then field workers
        workerItems = workerItems
            .OrderByDescending(w => w.CompletedAnalyses)
            .ThenByDescending(w => w.TotalCreatedSamples)
            .ThenBy(w => w.FullName)
            .ToList();

        // Calculate KPIs
        var totalCompletedAnalyses = analyses.Count(a => a.Status == AnalysisStatus.Completed);
        var totalActiveAnalyses = analyses.Count(a => a.Status is AnalysisStatus.InProgress or AnalysisStatus.Requested);
        var totalMeasuredParameters = results.Count;
        var totalSamplesTracked = samples.Count;

        var allDurations = analyses
            .Where(a => a.Status == AnalysisStatus.Completed && a.StartedAt.HasValue && a.CompletedAt.HasValue && a.CompletedAt > a.StartedAt)
            .Select(a => (a.CompletedAt!.Value - a.StartedAt!.Value).TotalHours)
            .ToList();

        double? overallAvgHours = allDurations.Count > 0 ? Math.Round(allDurations.Average(), 1) : null;

        var topPerformer = workerItems.FirstOrDefault(w => w.Role == UserRole.Laboratory && w.CompletedAnalyses > 0);

        var kpis = new PerformanceSummaryKpis(
            totalCompletedAnalyses,
            totalActiveAnalyses,
            totalMeasuredParameters,
            totalSamplesTracked,
            overallAvgHours,
            FormatDuration(overallAvgHours),
            topPerformer?.FullName,
            topPerformer?.CompletedAnalyses ?? 0,
            users.Count);

        // Analysis Type breakdown
        var analysisTypeGroups = analyses
            .GroupBy(a => new { a.AnalysisCode, a.AnalysisName })
            .Select(g =>
            {
                var completed = g.Count(a => a.Status == AnalysisStatus.Completed);
                var inProgress = g.Count(a => a.Status == AnalysisStatus.InProgress);
                var cancelled = g.Count(a => a.Status == AnalysisStatus.Cancelled);
                var durations = g
                    .Where(a => a.Status == AnalysisStatus.Completed && a.StartedAt.HasValue && a.CompletedAt.HasValue && a.CompletedAt > a.StartedAt)
                    .Select(a => (a.CompletedAt!.Value - a.StartedAt!.Value).TotalHours)
                    .ToList();
                double? avgDur = durations.Count > 0 ? Math.Round(durations.Average(), 1) : null;

                return new AnalysisTypePerformanceItem(
                    g.Key.AnalysisCode,
                    g.Key.AnalysisName,
                    g.Count(),
                    completed,
                    inProgress,
                    cancelled,
                    avgDur,
                    FormatDuration(avgDur));
            })
            .OrderByDescending(t => t.TotalRequested)
            .ToList();

        return new PerformanceOverviewResponse(
            kpis,
            workerItems,
            analysisTypeGroups,
            query.Period ?? "all",
            startDate,
            endDate);
    }

    public async Task<ServiceResult<AiWorkloadInsightResponse>> GenerateAiWorkloadInsightAsync(
        PerformanceQuery query,
        long currentUserId,
        bool isElevated,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetPerformanceOverviewAsync(query, currentUserId, isElevated, cancellationToken);

        try
        {
            var prompt = BuildWorkloadPrompt(overview);
            var rawResponse = await ollamaClient.GenerateWorkloadInsightAsync(prompt, cancellationToken);

            var modelResponse = JsonSerializer.Deserialize<AiWorkloadInsightModelResponse>(
                rawResponse,
                JsonOptions);

            if (string.IsNullOrWhiteSpace(modelResponse?.Summary))
            {
                return ServiceResult<AiWorkloadInsightResponse>.Success(
                    CreateFallbackWorkloadResponse(overview));
            }

            var response = new AiWorkloadInsightResponse(
                NormalizeSummary(modelResponse.Summary),
                NormalizeItems(modelResponse.KeyObservations),
                NormalizeItems(modelResponse.Recommendations),
                Disclaimer,
                options.Value.Model);

            return ServiceResult<AiWorkloadInsightResponse>.Success(response);
        }
        catch (OllamaClientException ex)
        {
            logger.LogWarning(ex, "Ollama model failed to generate workload insight.");
            return ServiceResult<AiWorkloadInsightResponse>.Success(
                CreateFallbackWorkloadResponse(overview));
        }
        catch (HttpRequestException)
        {
            return ServiceResult<AiWorkloadInsightResponse>.Failure(
                ServiceError.ExternalService,
                "Yerel Ollama servisine ulaşılamadı. Ollama'nın açık olduğunu kontrol edin.");
        }
        catch (JsonException)
        {
            return ServiceResult<AiWorkloadInsightResponse>.Success(
                CreateFallbackWorkloadResponse(overview));
        }
    }

    private static string BuildWorkloadPrompt(PerformanceOverviewResponse overview)
    {
        var data = new
        {
            kpis = overview.Kpis,
            period = overview.Period,
            workers = overview.Workers.Select(w => new
            {
                w.FullName,
                role = w.Role.ToString(),
                w.CompletedAnalyses,
                w.InProgressAnalyses,
                w.PendingAnalyses,
                w.TotalMeasuredParameters,
                w.CompletionRatePercentage,
                w.AverageAnalysisDurationFormatted
            }),
            topAnalysisTypes = overview.AnalysisTypes.Take(6).Select(t => new
            {
                t.AnalysisCode,
                t.AnalysisName,
                t.TotalRequested,
                t.CompletedCount,
                t.InProgressCount,
                t.AverageDurationFormatted
            })
        };

        return """
            Review this laboratory workforce, throughput, and workload distribution record.
            Synthesize the overall team efficiency, workload balance among technicians, completed task volume,
            and any potential operational bottlenecks in Turkish.
            Return a JSON object with:
            - summary: concise high-level Turkish overview of team performance and overall throughput.
            - keyObservations: string array of 3-5 key factual observations regarding workload distribution, active queue, and completion pace.
            - recommendations: string array of 2-4 actionable operational recommendations for laboratory management.
            Do not judge individuals harshly or make punitive claims. Remain constructive and factual.

            <workload-record>
            """
            + JsonSerializer.Serialize(data, JsonOptions)
            + """

            </workload-record>
            """;
    }

    private AiWorkloadInsightResponse CreateFallbackWorkloadResponse(PerformanceOverviewResponse overview)
    {
        var observations = new List<string>
        {
            $"Laboratuvar genelinde toplam {overview.Kpis.TotalCompletedAnalyses} analiz tamamlanmış, {overview.Kpis.TotalActiveAnalyses} analiz aktif süreçtedir.",
            $"Toplam {overview.Kpis.TotalMeasuredParameters} parametre ölçümü {overview.Kpis.ActiveWorkersCount} aktif personel tarafından sisteme işlenmiştir."
        };

        if (!string.IsNullOrWhiteSpace(overview.Kpis.TopPerformerName))
        {
            observations.Add($"Dönem içinde en yüksek analiz tamamlama hacmine {overview.Kpis.TopPerformerName} ({overview.Kpis.TopPerformerCompletedCount} tamamlanan analiz) ulaşmıştır.");
        }

        if (overview.Kpis.OverallAverageCompletionHours.HasValue)
        {
            observations.Add($"Ortalama analiz sonuçlandırma süresi yaklaşık {overview.Kpis.OverallAverageCompletionFormatted} olarak kaydedilmiştir.");
        }

        var recommendations = new List<string>
        {
            "Aktif iş yükünün çalışanlar arasında dengeli dağıtılması için bekleyen analiz kuyruklarını periyodik gözden geçirin.",
            "Ortalama tamamlanma süresi uzun olan analiz paketlerinde ön hazırlık süreçlerini optimize edin."
        };

        var summary = $"İncelenen dönemde laboratuvar ekibi {overview.Kpis.TotalCompletedAnalyses} analizi başarıyla sonuçlandırmıştır. Ekip genelinde ortalama tamamlama süresi {overview.Kpis.OverallAverageCompletionFormatted} seviyesindedir.";

        return new AiWorkloadInsightResponse(
            summary,
            observations,
            recommendations,
            Disclaimer,
            $"{options.Value.Model} (Uygulama İstatistik Özeti)");
    }

    private static (DateTimeOffset? StartDate, DateTimeOffset? EndDate) ResolveDateRange(PerformanceQuery query)
    {
        var now = DateTimeOffset.UtcNow;

        return (query.Period?.ToLowerInvariant()) switch
        {
            "today" => (new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero), now),
            "last7days" => (now.AddDays(-7), now),
            "last30days" => (now.AddDays(-30), now),
            "thismonth" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
            "thisyear" => (new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero), now),
            "custom" => (query.StartDate, query.EndDate),
            _ => (null, null)
        };
    }

    private static string FormatDuration(double? hours)
    {
        if (!hours.HasValue || hours.Value <= 0) return "—";

        var totalHours = hours.Value;
        if (totalHours < 1.0)
        {
            var minutes = Math.Max(1, (int)Math.Round(totalHours * 60));
            return $"{minutes} dk";
        }

        if (totalHours < 24.0)
        {
            var h = (int)Math.Floor(totalHours);
            var m = (int)Math.Round((totalHours - h) * 60);
            return m > 0 ? $"{h} sa {m} dk" : $"{h} saat";
        }

        var days = Math.Round(totalHours / 24.0, 1);
        return $"{days} gün";
    }

    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string?>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value) && !ContainsUnsupportedDecision(value!))
            .Take(6)
            .Select(value => TrimTo(value!, 400))
            .ToArray()
        ?? [];

    private static string NormalizeSummary(string value)
    {
        var summary = TrimTo(value, 1500);
        return ContainsUnsupportedDecision(summary) ? FilteredSummary : summary;
    }

    private static bool ContainsUnsupportedDecision(string value) =>
        UnsupportedDecisionPattern.IsMatch(value);

    private static string TrimTo(string value, int maximumLength) =>
        value.Trim().Length <= maximumLength
            ? value.Trim()
            : value.Trim()[..maximumLength];
}
