using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class SampleHistoryConfiguration
    : IEntityTypeConfiguration<SampleHistory>
{
    public void Configure(EntityTypeBuilder<SampleHistory> builder)
    {
        builder.ToTable("sample_history", table =>
        {
            table.HasCheckConstraint(
                "ck_sample_history_old_status",
                "\"old_status\" IS NULL OR " +
                "\"old_status\" IN (1, 2, 3, 4, 5, 6)");

            table.HasCheckConstraint(
                "ck_sample_history_new_status",
                "\"new_status\" IN (1, 2, 3, 4, 5, 6)");
        });

        builder.HasKey(history => history.Id);

        builder.HasIndex(history => new
        {
            history.SampleId,
            history.ChangedAt
        });

        builder.Property(history => history.Id)
            .HasColumnName("id");

        builder.Property(history => history.SampleId)
            .HasColumnName("sample_id");

        builder.Property(history => history.OldStatus)
            .HasColumnName("old_status")
            .HasColumnType("integer");

        builder.Property(history => history.NewStatus)
            .HasColumnName("new_status")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(history => history.ChangedById)
            .HasColumnName("changed_by");

        builder.Property(history => history.ChangedAt)
            .HasColumnName("changed_at")
            .HasDefaultValueSql("now()");

        builder.Property(history => history.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);

        builder.HasOne(history => history.Sample)
            .WithMany(sample => sample.History)
            .HasForeignKey(history => history.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(history => history.ChangedBy)
            .WithMany(user => user.StatusChanges)
            .HasForeignKey(history => history.ChangedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}