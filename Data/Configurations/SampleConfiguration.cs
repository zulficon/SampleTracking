using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class SampleConfiguration
    : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("samples", table =>
        {
            table.HasCheckConstraint(
                "ck_samples_status",
                "\"status\" IN (1, 2, 3, 4, 5, 6)");
        });

        builder.HasKey(sample => sample.Id);

        builder.HasIndex(sample => sample.SampleCode)
            .IsUnique();

        builder.HasIndex(sample => sample.Status);

        builder.HasIndex(sample => sample.LocationId);

        builder.HasIndex(sample => sample.AssignedToId);

        builder.Property(sample => sample.Id)
            .HasColumnName("id");

        builder.Property(sample => sample.SampleCode)
            .HasColumnName("sample_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sample => sample.SampleType)
            .HasColumnName("sample_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sample => sample.LocationId)
            .HasColumnName("location_id");

        builder.Property(sample => sample.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .HasDefaultValue(SampleStatus.Created)
            .HasSentinel((SampleStatus)0)
            .IsRequired();

        builder.Property(sample => sample.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(sample => sample.CreatedById)
            .HasColumnName("created_by");

        builder.Property(sample => sample.AssignedToId)
            .HasColumnName("assigned_to");

        builder.Property(sample => sample.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(sample => sample.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(sample => sample.Location)
            .WithMany(location => location.Samples)
            .HasForeignKey(sample => sample.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sample => sample.CreatedBy)
            .WithMany(user => user.CreatedSamples)
            .HasForeignKey(sample => sample.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sample => sample.AssignedTo)
            .WithMany(user => user.AssignedSamples)
            .HasForeignKey(sample => sample.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
