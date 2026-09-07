using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class SampleAnalysisConfiguration
    : IEntityTypeConfiguration<SampleAnalysis>
{
    public void Configure(EntityTypeBuilder<SampleAnalysis> builder)
    {
        builder.ToTable("sample_analyses", table =>
        {
            table.HasCheckConstraint(
                "ck_sample_analyses_status",
                "\"status\" IN (1, 2, 3, 4)");
        });

        builder.HasKey(sampleAnalysis => sampleAnalysis.Id);

        builder.HasIndex(sampleAnalysis => new
        {
            sampleAnalysis.SampleId,
            sampleAnalysis.AnalysisCodeId
        }).IsUnique();

        builder.HasIndex(sampleAnalysis => sampleAnalysis.Status);

        builder.Property(sampleAnalysis => sampleAnalysis.Id)
            .HasColumnName("id");

        builder.Property(sampleAnalysis => sampleAnalysis.SampleId)
            .HasColumnName("sample_id");

        builder.Property(sampleAnalysis => sampleAnalysis.AnalysisCodeId)
            .HasColumnName("analysis_code_id");

        builder.Property(sampleAnalysis => sampleAnalysis.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .HasDefaultValue(AnalysisStatus.Requested)
            .HasSentinel((AnalysisStatus)0)
            .IsRequired();

        builder.Property(sampleAnalysis => sampleAnalysis.ResultNote)
            .HasColumnName("result_note")
            .HasMaxLength(2000);

        builder.Property(sampleAnalysis => sampleAnalysis.RequestedById)
            .HasColumnName("requested_by");

        builder.Property(sampleAnalysis => sampleAnalysis.RequestedAt)
            .HasColumnName("requested_at")
            .HasDefaultValueSql("now()");

        builder.Property(sampleAnalysis => sampleAnalysis.StartedById)
            .HasColumnName("started_by");

        builder.Property(sampleAnalysis => sampleAnalysis.StartedAt)
            .HasColumnName("started_at");

        builder.Property(sampleAnalysis => sampleAnalysis.CompletedById)
            .HasColumnName("completed_by");

        builder.Property(sampleAnalysis => sampleAnalysis.CompletedAt)
            .HasColumnName("completed_at");

        builder.HasOne(sampleAnalysis => sampleAnalysis.Sample)
            .WithMany(sample => sample.Analyses)
            .HasForeignKey(sampleAnalysis => sampleAnalysis.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sampleAnalysis => sampleAnalysis.AnalysisCode)
            .WithMany(analysisCode => analysisCode.SampleAnalyses)
            .HasForeignKey(sampleAnalysis => sampleAnalysis.AnalysisCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sampleAnalysis => sampleAnalysis.RequestedBy)
            .WithMany(user => user.RequestedAnalyses)
            .HasForeignKey(sampleAnalysis => sampleAnalysis.RequestedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(sampleAnalysis => sampleAnalysis.CompletedBy)
            .WithMany(user => user.CompletedAnalyses)
            .HasForeignKey(sampleAnalysis => sampleAnalysis.CompletedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(sampleAnalysis => sampleAnalysis.StartedBy)
            .WithMany()
            .HasForeignKey(sampleAnalysis => sampleAnalysis.StartedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
