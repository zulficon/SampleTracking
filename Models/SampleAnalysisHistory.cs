using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class SampleAnalysisHistory
{
    public long Id { get; set; }
    public long SampleAnalysisId { get; set; }
    public AnalysisStatus? OldStatus { get; set; }
    public AnalysisStatus NewStatus { get; set; }
    public long? ChangedById { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }

    public SampleAnalysis SampleAnalysis { get; set; } = null!;
    public AppUser? ChangedBy { get; set; }
}
