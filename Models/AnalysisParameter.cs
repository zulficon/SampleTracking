namespace SampleAnalysisTracking.Models;

public sealed class AnalysisParameter
{
    public long Id { get; set; }
    public long AnalysisCodeId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string DefaultUnit { get; set; } = null!;
    public decimal? ReferenceMin { get; set; }
    public decimal? ReferenceMax { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public AnalysisCode AnalysisCode { get; set; } = null!;
    public ICollection<AnalysisResult> Results { get; } = new List<AnalysisResult>();
}
