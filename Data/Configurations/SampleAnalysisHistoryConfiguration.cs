using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class SampleAnalysisHistoryConfiguration : IEntityTypeConfiguration<SampleAnalysisHistory>
{
    public void Configure(EntityTypeBuilder<SampleAnalysisHistory> builder)
    {
        builder.ToTable("sample_analysis_history", table =>
        {
            table.HasCheckConstraint(
                "ck_sample_analysis_history_old_status",
                "\"old_status\" IS NULL OR \"old_status\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "ck_sample_analysis_history_new_status",
                "\"new_status\" IN (1, 2, 3, 4)");
        });

        builder.HasKey(history => history.Id);
        builder.HasIndex(history => new { history.SampleAnalysisId, history.ChangedAt });

        builder.Property(history => history.Id).HasColumnName("id");
        builder.Property(history => history.SampleAnalysisId).HasColumnName("sample_analysis_id");
        builder.Property(history => history.OldStatus).HasColumnName("old_status").HasColumnType("integer");
        builder.Property(history => history.NewStatus).HasColumnName("new_status").HasColumnType("integer").IsRequired();
        builder.Property(history => history.ChangedById).HasColumnName("changed_by");
        builder.Property(history => history.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()");
        builder.Property(history => history.Note).HasColumnName("note").HasMaxLength(1000);

        builder.HasOne(history => history.SampleAnalysis)
            .WithMany(analysis => analysis.History)
            .HasForeignKey(history => history.SampleAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(history => history.ChangedBy)
            .WithMany()
            .HasForeignKey(history => history.ChangedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
