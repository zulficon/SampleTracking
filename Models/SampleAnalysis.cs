using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class SampleAnalysis
{
    public long Id {get;set;}
    public long SampleId{get;set;}
    public long AnalysisCodeId{get;set;}
    public AnalysisStatus Status{get;set;} = AnalysisStatus.Requested;
    public string? ResultNote {get; set;}
    public long? RequestedById{get;set;}
    public DateTimeOffset RequestedAt {get;set;}
    public long? StartedById { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public long? CompletedById {get;set;}
    public DateTimeOffset? CompletedAt {get;set;}
    public Sample Sample {get;set;} = null!;
    public AnalysisCode AnalysisCode{get;set;} = null!;
    public AppUser? RequestedBy {get;set;}
    public AppUser? StartedBy { get; set; }
    public AppUser? CompletedBy{get;set;}
    public ICollection<AnalysisResult> Results { get; } = new List<AnalysisResult>();
    public ICollection<SampleAnalysisHistory> History { get; } = new List<SampleAnalysisHistory>();
}
