namespace SampleAnalysisTracking.Models;

public sealed class KnowledgeDocumentAnalysisCode
{
    public long KnowledgeDocumentId { get; set; }
    public long AnalysisCodeId { get; set; }

    public KnowledgeDocument KnowledgeDocument { get; set; } = null!;
    public AnalysisCode AnalysisCode { get; set; } = null!;
}
