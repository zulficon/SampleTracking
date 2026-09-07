using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class KnowledgeDocument
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public KnowledgeDocumentCategory Category { get; set; }
    public KnowledgeSourceStatus SourceStatus { get; set; } = KnowledgeSourceStatus.Draft;
    public string? SourceReference { get; set; }
    public string? SourceVersion { get; set; }
    public string SourceText { get; set; } = null!;
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public long UploadedById { get; set; }

    public AppUser UploadedBy { get; set; } = null!;
    public ICollection<KnowledgeChunk> Chunks { get; } = new List<KnowledgeChunk>();
    public ICollection<KnowledgeDocumentAnalysisCode> AnalysisCodeLinks { get; }
        = new List<KnowledgeDocumentAnalysisCode>();
}
