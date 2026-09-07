using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class KnowledgeDocumentAnalysisCodeConfiguration
    : IEntityTypeConfiguration<KnowledgeDocumentAnalysisCode>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocumentAnalysisCode> builder)
    {
        builder.ToTable("knowledge_document_analysis_codes");

        builder.HasKey(link => new { link.KnowledgeDocumentId, link.AnalysisCodeId });

        builder.Property(link => link.KnowledgeDocumentId)
            .HasColumnName("knowledge_document_id");

        builder.Property(link => link.AnalysisCodeId)
            .HasColumnName("analysis_code_id");

        builder.HasOne(link => link.KnowledgeDocument)
            .WithMany(document => document.AnalysisCodeLinks)
            .HasForeignKey(link => link.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.AnalysisCode)
            .WithMany(code => code.KnowledgeDocumentLinks)
            .HasForeignKey(link => link.AnalysisCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
