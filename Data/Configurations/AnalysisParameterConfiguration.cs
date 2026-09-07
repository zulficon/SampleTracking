using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class AnalysisParameterConfiguration : IEntityTypeConfiguration<AnalysisParameter>
{
    public void Configure(EntityTypeBuilder<AnalysisParameter> builder)
    {
        builder.ToTable("analysis_parameters", table =>
        {
            table.HasCheckConstraint(
                "ck_analysis_parameters_reference_range",
                "\"reference_min\" IS NULL OR \"reference_max\" IS NULL OR \"reference_min\" <= \"reference_max\"");
        });

        builder.HasKey(parameter => parameter.Id);
        builder.HasIndex(parameter => new { parameter.AnalysisCodeId, parameter.Code }).IsUnique();

        builder.Property(parameter => parameter.Id).HasColumnName("id");
        builder.Property(parameter => parameter.AnalysisCodeId).HasColumnName("analysis_code_id");
        builder.Property(parameter => parameter.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.Property(parameter => parameter.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(parameter => parameter.DefaultUnit).HasColumnName("default_unit").HasMaxLength(30).IsRequired();
        builder.Property(parameter => parameter.ReferenceMin).HasColumnName("reference_min").HasPrecision(18, 6);
        builder.Property(parameter => parameter.ReferenceMax).HasColumnName("reference_max").HasPrecision(18, 6);
        builder.Property(parameter => parameter.IsRequired).HasColumnName("is_required").HasDefaultValue(true);
        builder.Property(parameter => parameter.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(parameter => parameter.DisplayOrder).HasColumnName("display_order");

        builder.HasOne(parameter => parameter.AnalysisCode)
            .WithMany(analysisCode => analysisCode.Parameters)
            .HasForeignKey(parameter => parameter.AnalysisCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
