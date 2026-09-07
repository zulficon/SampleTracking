using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

public sealed class CreateKnowledgeDocumentRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = null!;

    [EnumDataType(typeof(KnowledgeDocumentCategory))]
    public KnowledgeDocumentCategory Category { get; init; }

    [EnumDataType(typeof(KnowledgeSourceStatus))]
    public KnowledgeSourceStatus SourceStatus { get; init; } = KnowledgeSourceStatus.Draft;

    [StringLength(500)]
    public string? SourceReference { get; init; }

    [StringLength(80)]
    public string? SourceVersion { get; init; }

    [Required, StringLength(50000, MinimumLength = 30)]
    public string SourceText { get; init; } = null!;

    public IReadOnlyList<long> AnalysisCodeIds { get; init; } = [];
}

public sealed class UpdateKnowledgeDocumentRequest
{
    [Required, StringLength(200)]
    public string Title { get; init; } = null!;

    [EnumDataType(typeof(KnowledgeDocumentCategory))]
    public KnowledgeDocumentCategory Category { get; init; }

    [StringLength(500)]
    public string? SourceReference { get; init; }

    [StringLength(80)]
    public string? SourceVersion { get; init; }

    [Required, StringLength(50000, MinimumLength = 30)]
    public string SourceText { get; init; } = null!;

    public IReadOnlyList<long> AnalysisCodeIds { get; init; } = [];
}

public sealed class SearchKnowledgeRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Query { get; init; } = null!;
}

public sealed record KnowledgeDocumentItem(
    long Id,
    string Title,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    string? SourceVersion,
    bool IsActive,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    string UploadedByUsername);

public sealed record KnowledgeDocumentAnalysisCodeItem(
    long Id,
    string Code,
    string Name);

public sealed record KnowledgeDocumentDetail(
    long Id,
    string Title,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    string? SourceVersion,
    string SourceText,
    bool IsActive,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    string UploadedByUsername,
    IReadOnlyList<KnowledgeDocumentAnalysisCodeItem> AnalysisCodes);

public sealed record KnowledgeSearchItem(
    long KnowledgeDocumentId,
    string DocumentTitle,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    int ChunkIndex,
    string Content,
    double? Similarity,
    bool IsAnalysisCodeMatch);
