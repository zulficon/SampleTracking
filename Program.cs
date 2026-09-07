using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.Data.Seed;
using SampleAnalysisTracking.Models;
using SampleAnalysisTracking.Options;
using SampleAnalysisTracking.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                allowIntegerValues: false));
    });
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var dataProtection = builder.Services.AddDataProtection();
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var connectionString = builder.Configuration.GetConnectionString("SampleDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException(
            "SampleDb connection string is missing. Add it with dotnet user-secrets before starting the API.");
    }

    connectionString =
        "Host=localhost;Database=integration_tests;Username=test;Password=test";
}

builder.Services.AddDbContext<SampleAnalysisTrackingDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));

builder.Services.AddScoped<SampleService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<AnalysisCatalogService>();
builder.Services.AddScoped<SampleAnalysisService>();
builder.Services.AddScoped<KnowledgeBaseService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

builder.Services
    .AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _)
                   && !string.IsNullOrWhiteSpace(options.Model)
                   && options.TimeoutSeconds is >= 10 and <= 300
                   && options.ContextWindow is >= 1024 and <= 16384
                   && options.MaxOutputTokens is >= 50 and <= 1600
                   && !string.IsNullOrWhiteSpace(options.KeepAlive),
        "Ollama configuration is invalid.");

builder.Services
    .AddOptions<RagOptions>()
    .BindConfiguration(RagOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.EmbeddingModel)
                   && options.EmbeddingDimensions is >= 128 and <= 4096
                   && options.ChunkSizeCharacters is >= 300 and <= 5000
                   && options.ChunkOverlapCharacters >= 0
                   && options.ChunkOverlapCharacters < options.ChunkSizeCharacters
                   && options.SearchResultLimit is >= 1 and <= 10,
        "RAG configuration is invalid.");

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<OllamaOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<OllamaOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<AiSampleReportService>();
builder.Services.AddScoped<PerformanceAnalyticsService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SampleAnalysisTracking.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/";
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleClaim = context.Principal?.FindFirstValue(ClaimTypes.Role);

            if (!long.TryParse(userIdClaim, out var userId)
                || string.IsNullOrWhiteSpace(roleClaim))
            {
                await RejectPrincipalAsync(context);
                return;
            }

            var db = context.HttpContext.RequestServices
                .GetRequiredService<SampleAnalysisTrackingDbContext>();
            var currentUser = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.IsActive, user.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (currentUser is null
                || !currentUser.IsActive
                || !string.Equals(
                    currentUser.Role.ToString(),
                    roleClaim,
                    StringComparison.Ordinal))
            {
                await RejectPrincipalAsync(context);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    if (app.Configuration.GetValue<bool>("SeedData:Enabled"))
    {
        await DevelopmentDataSeeder.SeedAsync(
            app.Services,
            app.Configuration);
    }

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
{
    context.RejectPrincipal();
    await context.HttpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);
}

public partial class Program;
