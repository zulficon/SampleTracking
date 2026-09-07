using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using Xunit;

namespace SampleAnalysisTracking.IntegrationTests;

public sealed class CriticalFlowsTests : IDisposable
{
    // xUnit creates a new test class instance for each test. Keep both seeded
    // data and the application's authentication rate limit isolated with it.
    private readonly TestApplicationFactory factory = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Profile_WithoutSession_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidAndInvalidPassword_ReturnsExpectedStatuses()
    {
        using var client = CreateClient();

        var invalidResponse = await LoginAsync(
            client,
            "admin.test",
            "wrong-password");
        var validResponse = await LoginAsync(
            client,
            "admin.test",
            TestApplicationFactory.DefaultPassword);
        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WhenRateLimitExceeded_ReturnsTooManyRequests()
    {
        using var client = CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await LoginAsync(client, "unknown.user", "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var rejectedResponse = await LoginAsync(client, "unknown.user", "wrong-password");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ValidatesCurrentPassword_AndReplacesHash()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "password.user",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var wrongCurrentResponse = await client.PutAsJsonAsync(
            "/api/profile/change-password",
            new
            {
                currentPassword = "wrong-password",
                newPassword = "ChangedPassword123!",
                confirmNewPassword = "ChangedPassword123!"
            });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrentResponse.StatusCode);

        var changeResponse = await client.PutAsJsonAsync(
            "/api/profile/change-password",
            new
            {
                currentPassword = TestApplicationFactory.DefaultPassword,
                newPassword = "ChangedPassword123!",
                confirmNewPassword = "ChangedPassword123!"
            });
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        using var freshClient = CreateClient();
        var oldPasswordResponse = await LoginAsync(
            freshClient,
            "password.user",
            TestApplicationFactory.DefaultPassword);
        var newPasswordResponse = await LoginAsync(
            freshClient,
            "password.user",
            "ChangedPassword123!");

        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task ExistingSession_IsRejected_WhenUserBecomesInactive()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "session.user",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        await factory.ExecuteDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(
                item => item.Username == "session.user");
            user.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Laboratory_CannotAccess_UnassignedSampleAnalyses()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "lab.other",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/samples/700/analyses");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancellingLastActiveAnalysis_MovesSampleBackToReceived()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "lab.assigned",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cancelResponse = await client.PostAsJsonAsync(
            "/api/sample-analyses/902/cancel",
            new { note = "Test kapsamında analiz iptal edildi." });

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        await factory.ExecuteDbAsync(async db =>
        {
            var sample = await db.Samples.SingleAsync(item => item.Id == 700);
            var history = await db.SampleHistories
                .SingleAsync(item => item.SampleId == 700);

            Assert.Equal(SampleStatus.Received, sample.Status);
            Assert.Equal(SampleStatus.Analyzing, history.OldStatus);
            Assert.Equal(SampleStatus.Received, history.NewStatus);
            Assert.Contains("Tüm analizler iptal edildiği için", history.Note);
        });
    }

    [Fact]
    public async Task RemovedSingleAnalysisAiEndpoint_ReturnsNotFound()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(
            "/api/sample-analyses/902/ai-insight",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Home_DoesNotRenderSingleAnalysisAiPanel()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("generate-ai-insight", html);
        Assert.DoesNotContain("ai-insight-section", html);
        Assert.Contains("generate-ai-sample-report", html);
    }

    [Fact]
    public async Task AiSampleReport_AssignedLaboratoryUser_CanGenerateSafeStructuredReport()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "lab.assigned",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.PostAsync(
            "/api/samples/700/ai-report",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.FakeOllama.LastReportPrompt);
        Assert.Contains("resultNote", factory.FakeOllama.LastReportPrompt);

        var report = await response.Content.ReadFromJsonAsync<AiSampleReportResponse>();
        Assert.NotNull(report);
        Assert.Equal("Numunenin bekleyen bir analiz kaydı bulunuyor.", report.Summary);
        Assert.Single(report.SummarySentences);
        Assert.Empty(report.SummarySentences[0].SourceNumbers);
        Assert.Empty(report.KnowledgeSources);
        Assert.Empty(report.CompletedAnalyses);
        Assert.NotEmpty(report.PendingAnalyses);
        Assert.Single(report.AttentionPoints);
        Assert.Contains("AI raporu", report.Disclaimer);
    }

    [Fact]
    public async Task Performance_Overview_ReturnsKpisAndWorkerMetrics()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "admin.test",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/performance/overview?period=all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var overview = await response.Content.ReadFromJsonAsync<PerformanceOverviewResponse>(JsonOptions);
        Assert.NotNull(overview);
        Assert.NotNull(overview.Kpis);
        Assert.NotNull(overview.Workers);
        Assert.NotNull(overview.AnalysisTypes);
    }

    [Fact]
    public async Task Performance_AiWorkloadInsight_AuthorizedUser_ReturnsInsight()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "admin.test",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            "/api/performance/ai-workload-insight",
            new PerformanceQuery { Period = "all" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var insight = await response.Content.ReadFromJsonAsync<AiWorkloadInsightResponse>();
        Assert.NotNull(insight);
        Assert.NotEmpty(insight.Summary);
        Assert.NotEmpty(insight.KeyObservations);
        Assert.NotEmpty(insight.Recommendations);
        Assert.Contains("karar destek", insight.Disclaimer);
    }

    [Fact]
    public async Task KnowledgeDocument_DraftCanBeReadAndUpdated_VerifiedCannotBeUpdated()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "admin.test",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var draftCreateResponse = await client.PostAsJsonAsync(
            "/api/knowledge-base/documents",
            new
            {
                title = "Taslak RAG test belgesi",
                category = "Procedure",
                sourceStatus = "Draft",
                sourceReference = "DEMO-001",
                sourceVersion = "1.0",
                sourceText = "Bu taslak metin, ilk vektör parçasını oluşturmak için yeterli uzunluktadır.",
                analysisCodeIds = new[] { 800L }
            });
        Assert.Equal(HttpStatusCode.Created, draftCreateResponse.StatusCode);

        var createdDraft = await draftCreateResponse.Content
            .ReadFromJsonAsync<KnowledgeDocumentItem>(JsonOptions);
        Assert.NotNull(createdDraft);

        var detailResponse = await client.GetAsync(
            $"/api/knowledge-base/documents/{createdDraft.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var detail = await detailResponse.Content
            .ReadFromJsonAsync<KnowledgeDocumentDetail>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(KnowledgeSourceStatus.Draft, detail.SourceStatus);
        Assert.Contains(detail.AnalysisCodes, code => code.Id == 800);

        const string updatedText =
            "Güncellenmiş taslak metin, eski chunk yerine yeni embedding ile kaydedilmelidir.";
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/knowledge-base/documents/{createdDraft.Id}",
            new
            {
                title = "Güncellenmiş taslak RAG belgesi",
                category = "QualityGuideline",
                sourceReference = "DEMO-002",
                sourceVersion = "2.0",
                sourceText = updatedText,
                analysisCodeIds = new[] { 801L }
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content
            .ReadFromJsonAsync<KnowledgeDocumentDetail>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Güncellenmiş taslak RAG belgesi", updated.Title);
        Assert.Equal(updatedText, updated.SourceText);
        Assert.DoesNotContain(updated.AnalysisCodes, code => code.Id == 800);
        Assert.Contains(updated.AnalysisCodes, code => code.Id == 801);

        await factory.ExecuteDbAsync(async db =>
        {
            var chunks = await db.KnowledgeChunks
                .Where(chunk => chunk.KnowledgeDocumentId == createdDraft.Id)
                .ToListAsync();
            var linkedCodeIds = await db.KnowledgeDocumentAnalysisCodes
                .Where(link => link.KnowledgeDocumentId == createdDraft.Id)
                .Select(link => link.AnalysisCodeId)
                .ToListAsync();

            Assert.NotEmpty(chunks);
            Assert.All(chunks, chunk => Assert.Contains("Güncellenmiş", chunk.Content));
            Assert.Equal(new[] { 801L }, linkedCodeIds);
        });

        var verifiedCreateResponse = await client.PostAsJsonAsync(
            "/api/knowledge-base/documents",
            new
            {
                title = "Doğrulanmış RAG test belgesi",
                category = "AnalysisMethod",
                sourceStatus = "Verified",
                sourceReference = "LAB-SOP-001",
                sourceVersion = "Rev. 01",
                sourceText = "Bu doğrulanmış test belgesi düzenleme engelini kontrol edecek uzunluktadır.",
                analysisCodeIds = Array.Empty<long>()
            });
        Assert.Equal(HttpStatusCode.Created, verifiedCreateResponse.StatusCode);

        var createdVerified = await verifiedCreateResponse.Content
            .ReadFromJsonAsync<KnowledgeDocumentItem>(JsonOptions);
        Assert.NotNull(createdVerified);

        var forbiddenUpdateResponse = await client.PutAsJsonAsync(
            $"/api/knowledge-base/documents/{createdVerified.Id}",
            new
            {
                title = "Değiştirilmemeli",
                category = "AnalysisMethod",
                sourceReference = "LAB-SOP-001",
                sourceVersion = "Rev. 02",
                sourceText = "Verified belge içeriği bu istek sonucunda değiştirilmemelidir.",
                analysisCodeIds = Array.Empty<long>()
            });

        Assert.Equal(HttpStatusCode.Conflict, forbiddenUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task Performance_Overview_LaboratoryUser_ReturnsForbidden()
    {
        using var client = CreateClient();
        var loginResponse = await LoginAsync(
            client,
            "lab.assigned",
            TestApplicationFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/performance/overview?period=all");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient()
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        return client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });
    }
}
