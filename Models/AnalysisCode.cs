namespace SampleAnalysisTracking.Models;

public sealed class AnalysisCode
{
    public long Id {get; set;}
    public string Code {get;set;} = null!;
    public string Name {get;set;} = null!;
    public string? Description {get;set;}
    public bool IsActive{get;set;} = true;

    public ICollection<AnalysisParameter> Parameters { get; } = new List<AnalysisParameter>();
    public ICollection<SampleAnalysis> SampleAnalyses { get; } = new List<SampleAnalysis>();
    public ICollection<KnowledgeDocumentAnalysisCode> KnowledgeDocumentLinks { get; }
        = new List<KnowledgeDocumentAnalysisCode>();
}
