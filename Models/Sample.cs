using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class Sample
{
    public long Id { get; set; }
    public string SampleCode { get; set; } = null!;
    public string SampleType { get; set; } = null!;
    public long? LocationId { get; set; }

    public SampleStatus Status { get; set; } = SampleStatus.Created;

    public string? Description { get; set; }
    public long? CreatedById { get; set; }
    public long? AssignedToId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Location? Location { get; set; }
    public AppUser? CreatedBy { get; set; }
    public AppUser? AssignedTo { get; set; }

    public ICollection<SampleHistory> History { get; }
        = new List<SampleHistory>();

    public ICollection<SampleAnalysis> Analyses { get; }
        = new List<SampleAnalysis>();
}