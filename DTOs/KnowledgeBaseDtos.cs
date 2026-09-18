using System.ComponentModel.DataAnnotations;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

// RAG bilgi tabanına yeni bir doküman yükleme ve vektörleştirme isteği.
public sealed class CreateKnowledgeDocumentRequest
{
    // Doküman başlığı
    [Required, StringLength(200)]
    public string Title { get; init; } = null!;

    // Doküman kategorisi (Prosedür, Analiz Yöntemi, Kalite Kılavuzu)
    [EnumDataType(typeof(KnowledgeDocumentCategory))]
    public KnowledgeDocumentCategory Category { get; init; }

    // Kaynak durumu (Taslak veya Doğrulanmış)
    [EnumDataType(typeof(KnowledgeSourceStatus))]
    public KnowledgeSourceStatus SourceStatus { get; init; } = KnowledgeSourceStatus.Draft;

    // Resmî referans/doküman kodu (Doğrulanmış belgeler için zorunludur)
    [StringLength(500)]
    public string? SourceReference { get; init; }

    // Doküman revizyon/versiyon bilgisi
    [StringLength(80)]
    public string? SourceVersion { get; init; }

    // Parçalanacak ve embedding modeli ile vektörleştirilecek ham doküman metni
    [Required, StringLength(50000, MinimumLength = 30)]
    public string SourceText { get; init; } = null!;

    // İlişkilendirilecek analiz kodlarının kimlik listesi
    public IReadOnlyList<long> AnalysisCodeIds { get; init; } = [];
}

// Bilgi tabanındaki taslak dokümanın içeriğini ve vektörlerini güncelleme isteği.
public sealed class UpdateKnowledgeDocumentRequest
{
    // Güncellenen doküman başlığı
    [Required, StringLength(200)]
    public string Title { get; init; } = null!;

    // Güncellenen doküman kategorisi
    [EnumDataType(typeof(KnowledgeDocumentCategory))]
    public KnowledgeDocumentCategory Category { get; init; }

    // Güncellenen kaynak referansı
    [StringLength(500)]
    public string? SourceReference { get; init; }

    // Güncellenen doküman sürümü
    [StringLength(80)]
    public string? SourceVersion { get; init; }

    // Yeniden parçalanıp embedding vektörleri baştan üretilecek yeni metin içeriği
    [Required, StringLength(50000, MinimumLength = 30)]
    public string SourceText { get; init; } = null!;

    // Güncellenen analiz kodları listesi
    public IReadOnlyList<long> AnalysisCodeIds { get; init; } = [];
}

// RAG bilgi tabanında semantik vektör araması yapmak için gönderilen istek nesnesi.
public sealed class SearchKnowledgeRequest
{
    // Embedding modeline gönderilerek vektör uzayında aranacak serbest metin sorgusu
    [Required, StringLength(500, MinimumLength = 3)]
    public string Query { get; init; } = null!;
}

// Bilgi tabanı liste görünümünde dönen özet doküman kaydı.
public sealed record KnowledgeDocumentItem(
    long Id,
    string Title,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    string? SourceVersion,
    bool IsActive,
    // Dokümanın kaç adet parçaya (chunk) bölündüğü bilgisi
    int ChunkCount,
    DateTimeOffset CreatedAt,
    string UploadedByUsername);

// Bilgi dokümanına bağlı analiz kodu özeti.
public sealed record KnowledgeDocumentAnalysisCodeItem(
    long Id,
    string Code,
    string Name);

// Bilgi dokümanının tüm detaylarını ve bağlı analiz kodlarını içeren ayrıntılı kayıt.
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

// pgvector kosinüs benzerlik araması sonucu eşleşen metin parçacığı ve benzerlik skoru.
public sealed record KnowledgeSearchItem(
    long KnowledgeDocumentId,
    string DocumentTitle,
    KnowledgeDocumentCategory Category,
    KnowledgeSourceStatus SourceStatus,
    string? SourceReference,
    // Eşleşen parçanın sıra indeksi
    int ChunkIndex,
    // Eşleşen metin içeriği
    string Content,
    // 0 ile 1 arasındaki kosinüs benzerlik skoru (1'e yaklaştıkça anlamsal yakınlık artar)
    double? Similarity,
    // Arama yapılan analiz kodu ile dokümanın doğrudan eşleşip eşleşmediği
    bool IsAnalysisCodeMatch);

