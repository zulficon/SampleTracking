using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.IntegrationTests;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public const string DefaultPassword = "TestPassword123!";
    private readonly string databaseName =
        $"integration-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SampleDb"] =
                    "Host=localhost;Database=integration_tests;Username=test;Password=test",
                ["SeedData:Enabled"] = "false",
                ["DataProtection:KeysPath"] = null
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDataProtection()
                .UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<SampleAnalysisTrackingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SampleAnalysisTrackingDbContext>>();
            services.RemoveAll<IOllamaClient>();
            services.RemoveAll<IEmbeddingClient>();
            services.AddDbContext<SampleAnalysisTrackingDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<IOllamaClient>(FakeOllama);
            services.AddSingleton<IEmbeddingClient, FakeEmbeddingClient>();
        });
    }

    public FakeOllamaClient FakeOllama { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<SampleAnalysisTrackingDbContext>();
        db.Database.EnsureCreated();
        Seed(db);

        return host;
    }

    public async Task ExecuteDbAsync(
        Func<SampleAnalysisTrackingDbContext, Task> operation)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<SampleAnalysisTrackingDbContext>();
        await operation(db);
    }

    private static void Seed(SampleAnalysisTrackingDbContext db)
    {
        var now = new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero);
        var passwordHasher = new PasswordHasher<AppUser>();

        var admin = CreateUser(1, "admin.test", UserRole.Admin, now, passwordHasher);
        var assignedLaboratory = CreateUser(2, "lab.assigned", UserRole.Laboratory, now, passwordHasher);
        var otherLaboratory = CreateUser(3, "lab.other", UserRole.Laboratory, now, passwordHasher);
        var passwordUser = CreateUser(4, "password.user", UserRole.Field, now, passwordHasher);
        var sessionUser = CreateUser(5, "session.user", UserRole.Field, now, passwordHasher);

        var firstCode = new AnalysisCode
        {
            Id = 800,
            Code = "TEST_A",
            Name = "Test Analizi A",
            IsActive = true
        };
        var secondCode = new AnalysisCode
        {
            Id = 801,
            Code = "TEST_B",
            Name = "Test Analizi B",
            IsActive = true
        };

        var sample = new Sample
        {
            Id = 700,
            SampleCode = "SMP-TEST-0700",
            SampleType = "Kaya",
            Status = SampleStatus.Analyzing,
            CreatedById = admin.Id,
            AssignedToId = assignedLaboratory.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        var alreadyCancelled = new SampleAnalysis
        {
            Id = 901,
            SampleId = sample.Id,
            AnalysisCodeId = firstCode.Id,
            Status = AnalysisStatus.Cancelled,
            RequestedById = admin.Id,
            RequestedAt = now
        };
        var analysisToCancel = new SampleAnalysis
        {
            Id = 902,
            SampleId = sample.Id,
            AnalysisCodeId = secondCode.Id,
            Status = AnalysisStatus.InProgress,
            RequestedById = admin.Id,
            RequestedAt = now,
            StartedById = assignedLaboratory.Id,
            StartedAt = now.AddMinutes(10)
        };

        db.Users.AddRange(
            admin,
            assignedLaboratory,
            otherLaboratory,
            passwordUser,
            sessionUser);
        db.AnalysisCodes.AddRange(firstCode, secondCode);
        db.Samples.Add(sample);
        db.SampleAnalyses.AddRange(alreadyCancelled, analysisToCancel);
        db.SaveChanges();
    }

    private static AppUser CreateUser(
        long id,
        string username,
        UserRole role,
        DateTimeOffset now,
        IPasswordHasher<AppUser> passwordHasher)
    {
        var user = new AppUser
        {
            Id = id,
            Username = username,
            PasswordHash = string.Empty,
            FullName = username,
            Email = $"{username}@example.test",
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(
            user,
            DefaultPassword);
        return user;
    }

    public sealed class FakeOllamaClient : IOllamaClient
    {
        public string? LastReportPrompt { get; private set; }

        public Task<string> GenerateSampleReportAsync(
            string prompt,
            CancellationToken cancellationToken)
        {
            LastReportPrompt = prompt;
            return Task.FromResult("""
                {
                  "summarySentences": [
                    {
                      "text": "Numunenin bekleyen bir analiz kaydı bulunuyor.",
                      "usesRecordData": true,
                      "sourceNumbers": [99]
                    }
                  ]
                }
                """);
        }

        public Task<string> GenerateWorkloadInsightAsync(
            string prompt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult("""
                {
                  "summary": "Ekip genelinde iş yükü dengeli dağılmıştır.",
                  "keyObservations": [
                    "Tamamlanan analiz hacmi hedeflerle uyumludur.",
                    "Aktif iş kuyruğu yönetilebilir durumdadır."
                  ],
                  "recommendations": [
                    "Öncelikli numunelerin analiz sırasını düzenli takip edin."
                  ]
                }
                """);
        }
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        public Task<float[]> CreateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken)
        {
            var embedding = new float[1024];
            embedding[0] = text.Length;
            embedding[1] = text.Sum(character => character) % 997;
            return Task.FromResult(embedding);
        }
    }
}
