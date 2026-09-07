namespace SampleAnalysisTracking.Models;

public sealed class AnalysisResult
{
    public long Id { get; set; }
    public long SampleAnalysisId { get; set; }
    public long AnalysisParameterId { get; set; }
    public decimal NumericValue { get; set; }
    public string Unit { get; set; } = null!;
    public decimal? ReferenceMin { get; set; }
    public decimal? ReferenceMax { get; set; }
    public string? ResultNote { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
    public long? EnteredById { get; set; }

    public SampleAnalysis SampleAnalysis { get; set; } = null!;
    public AnalysisParameter AnalysisParameter { get; set; } = null!;
    public AppUser? EnteredBy { get; set; }
}
