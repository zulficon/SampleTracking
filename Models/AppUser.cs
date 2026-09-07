using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

public sealed class AppUser
{
    public long Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Sample> CreatedSamples { get; } = new List<Sample>();
    public ICollection<Sample> AssignedSamples { get; } = new List<Sample>();
    public ICollection<SampleHistory> StatusChanges { get; } = new List<SampleHistory>();
    public ICollection<SampleAnalysis> RequestedAnalyses { get; } = new List<SampleAnalysis>();
    public ICollection<SampleAnalysis> CompletedAnalyses { get; } = new List<SampleAnalysis>();
    public ICollection<KnowledgeDocument> UploadedKnowledgeDocuments { get; } = new List<KnowledgeDocument>();
}
