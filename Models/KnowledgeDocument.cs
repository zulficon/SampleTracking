using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.Models;

// RAG (Bilgi Tabanı) doküman varlığı.
// Laboratuvar prosedürlerini, analiz metotlarını veya kalite kılavuzlarını saklar.
public sealed class KnowledgeDocument
{
    // Dokümanın benzersiz kimliği
    public long Id { get; set; }

    // Doküman başlığı (örn. "İçme Suyu pH Ölçüm Talimatı")
    public string Title { get; set; } = null!;

    // Doküman kategorisi (Prosedür, Analiz Yöntemi, Kalite Kılavuzu)
    public KnowledgeDocumentCategory Category { get; set; }

    // Kaynağın güvenilirlik/onay durumu (Taslak / Doğrulanmış Kurumsal Kaynak)
    public KnowledgeSourceStatus SourceStatus { get; set; } = KnowledgeSourceStatus.Draft;

    // Resmî referans/doküman kodu (örn. "LAB-SOP-014", Doğrulanmış belgelerde zorunludur)
    public string? SourceReference { get; set; }

    // Dokümanın revizyon / sürüm numarası (örn. "v2.1")
    public string? SourceVersion { get; set; }

    // Dokümanın orijinal ham metin içeriği
    public string SourceText { get; set; } = null!;

    // Varsa yüklenen orijinal dosya adı
    public string? OriginalFileName { get; set; }

    // Dosya içerik türü (MIME type)
    public string? ContentType { get; set; }

    // Dokümanın aktiflik durumu; pasif belgeler RAG aramalarına dahil edilmez
    public bool IsActive { get; set; } = true;

    // Sisteme yüklenme zamanı
    public DateTimeOffset CreatedAt { get; set; }

    // Dokümanı sisteme yükleyen kullanıcının ID'si
    public long UploadedById { get; set; }

    // Yükleyen kullanıcı ilişkisi
    public AppUser UploadedBy { get; set; } = null!;

    // Dokümandan metin parçalama (chunking) ile üretilen ve vektörleştirilen parçalar (RAG arama hedefi)
    public ICollection<KnowledgeChunk> Chunks { get; } = new List<KnowledgeChunk>();

    // Dokümanın ilişkili olduğu analiz kodları bağlantıları (kategori/kod bazlı filtreleme için)
    public ICollection<KnowledgeDocumentAnalysisCode> AnalysisCodeLinks { get; }
        = new List<KnowledgeDocumentAnalysisCode>();
}

