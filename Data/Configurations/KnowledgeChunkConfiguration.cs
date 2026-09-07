using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Data.Configurations;

public sealed class KnowledgeChunkConfiguration
    : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");

        builder.HasKey(chunk => chunk.Id);
        builder.HasIndex(chunk => new { chunk.KnowledgeDocumentId, chunk.ChunkIndex })
            .IsUnique();

        builder.Property(chunk => chunk.Id)
            .HasColumnName("id");

        builder.Property(chunk => chunk.KnowledgeDocumentId)
            .HasColumnName("knowledge_document_id");

        builder.Property(chunk => chunk.ChunkIndex)
            .HasColumnName("chunk_index");

        builder.Property(chunk => chunk.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(chunk => chunk.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1024)")
            .IsRequired();

        builder.Property(chunk => chunk.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(chunk => chunk.KnowledgeDocument)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
