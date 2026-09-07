using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class AnalysisResultConfiguration : IEntityTypeConfiguration<AnalysisResult>
{
    public void Configure(EntityTypeBuilder<AnalysisResult> builder)
    {
        builder.ToTable("analysis_results", table =>
        {
            table.HasCheckConstraint(
                "ck_analysis_results_reference_range",
                "\"reference_min\" IS NULL OR \"reference_max\" IS NULL OR \"reference_min\" <= \"reference_max\"");
        });

        builder.HasKey(result => result.Id);
        builder.HasIndex(result => new { result.SampleAnalysisId, result.AnalysisParameterId }).IsUnique();

        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.SampleAnalysisId).HasColumnName("sample_analysis_id");
        builder.Property(result => result.AnalysisParameterId).HasColumnName("analysis_parameter_id");
        builder.Property(result => result.NumericValue).HasColumnName("numeric_value").HasPrecision(18, 6);
        builder.Property(result => result.Unit).HasColumnName("unit").HasMaxLength(30).IsRequired();
        builder.Property(result => result.ReferenceMin).HasColumnName("reference_min").HasPrecision(18, 6);
        builder.Property(result => result.ReferenceMax).HasColumnName("reference_max").HasPrecision(18, 6);
        builder.Property(result => result.ResultNote).HasColumnName("result_note").HasMaxLength(1000);
        builder.Property(result => result.MeasuredAt).HasColumnName("measured_at").HasDefaultValueSql("now()");
        builder.Property(result => result.EnteredById).HasColumnName("entered_by");

        builder.HasOne(result => result.SampleAnalysis)
            .WithMany(analysis => analysis.Results)
            .HasForeignKey(result => result.SampleAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(result => result.AnalysisParameter)
            .WithMany(parameter => parameter.Results)
            .HasForeignKey(result => result.AnalysisParameterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(result => result.EnteredBy)
            .WithMany()
            .HasForeignKey(result => result.EnteredById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
