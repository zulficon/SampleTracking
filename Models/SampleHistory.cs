using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class SampleHistory
{
    public long Id { get; set; }
    public long SampleId { get; set; }

    public SampleStatus? OldStatus { get; set; }
    public SampleStatus NewStatus { get; set; }

    public long? ChangedById { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }

    public Sample Sample { get; set; } = null!;
    public AppUser? ChangedBy { get; set; }
}