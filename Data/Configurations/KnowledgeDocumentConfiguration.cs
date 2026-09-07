using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class KnowledgeDocumentConfiguration
    : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents", table =>
        {
            table.HasCheckConstraint(
                "ck_knowledge_documents_category",
                "\"category\" IN (1, 2, 3)");

            table.HasCheckConstraint(
                "ck_knowledge_documents_source_status",
                "\"source_status\" IN (1, 2)");

            table.HasCheckConstraint(
                "ck_knowledge_documents_verified_source_reference",
                "\"source_status\" <> 2 OR NULLIF(BTRIM(\"source_reference\"), '') IS NOT NULL");
        });

        builder.HasKey(document => document.Id);
        builder.HasIndex(document => new { document.IsActive, document.Category });

        builder.Property(document => document.Id)
            .HasColumnName("id");

        builder.Property(document => document.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(document => document.Category)
            .HasColumnName("category")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(document => document.SourceStatus)
            .HasColumnName("source_status")
            .HasColumnType("integer")
            .HasDefaultValue(KnowledgeSourceStatus.Draft)
            .HasSentinel(KnowledgeSourceStatus.Draft)
            .IsRequired();

        builder.Property(document => document.SourceReference)
            .HasColumnName("source_reference")
            .HasMaxLength(500);

        builder.Property(document => document.SourceVersion)
            .HasColumnName("source_version")
            .HasMaxLength(80);

        builder.Property(document => document.SourceText)
            .HasColumnName("source_text")
            .IsRequired();

        builder.Property(document => document.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(260);

        builder.Property(document => document.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(document => document.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(document => document.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(document => document.UploadedById)
            .HasColumnName("uploaded_by");

        builder.HasOne(document => document.UploadedBy)
            .WithMany(user => user.UploadedKnowledgeDocuments)
            .HasForeignKey(document => document.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
