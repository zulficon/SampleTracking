using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class AnalysisCodeConfiguration
    : IEntityTypeConfiguration<AnalysisCode>
{
    public void Configure(EntityTypeBuilder<AnalysisCode> builder)
    {
        builder.ToTable("analysis_codes");

        builder.HasKey(analysisCode => analysisCode.Id);

        builder.HasIndex(analysisCode => analysisCode.Code)
            .IsUnique();

        builder.Property(analysisCode => analysisCode.Id)
            .HasColumnName("id");

        builder.Property(analysisCode => analysisCode.Code)
            .HasColumnName("code")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(analysisCode => analysisCode.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(analysisCode => analysisCode.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(analysisCode => analysisCode.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);
    }
}