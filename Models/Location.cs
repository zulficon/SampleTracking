namespace SampleAnalysisTracking.Models;

public sealed class Location
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<Sample> Samples { get; } = new List<Sample>();
}
