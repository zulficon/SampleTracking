using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data;

public sealed class SampleAnalysisTrackingDbContext(
    DbContextOptions<SampleAnalysisTrackingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<SampleHistory> SampleHistories => Set<SampleHistory>();
    public DbSet<AnalysisCode> AnalysisCodes => Set<AnalysisCode>();
    public DbSet<AnalysisParameter> AnalysisParameters => Set<AnalysisParameter>();
    public DbSet<SampleAnalysis> SampleAnalyses => Set<SampleAnalysis>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<SampleAnalysisHistory> SampleAnalysisHistories => Set<SampleAnalysisHistory>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeDocumentAnalysisCode> KnowledgeDocumentAnalysisCodes => Set<KnowledgeDocumentAnalysisCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SampleAnalysisTrackingDbContext).Assembly);

        if (string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            modelBuilder.Entity<KnowledgeChunk>()
                .Ignore(chunk => chunk.Embedding);
        }
    }
}
