namespace SampleAnalysisTracking.Models;

// Bilgi tabanı dokümanları ile laboratuvar analiz kodları arasındaki çoka-çok ilişkiyi temsil eden ara tablo.
// RAG semantik aramasında analiz koduna göre önceliklendirme ve filtreleme yapılmasını sağlar.
public sealed class KnowledgeDocumentAnalysisCode
{
    // Bilgi dokümanı kimliği
    public long KnowledgeDocumentId { get; set; }

    // Eşleşen analiz kodu kimliği
    public long AnalysisCodeId { get; set; }

    // Bilgi dokümanı ilişkisi
    public KnowledgeDocument KnowledgeDocument { get; set; } = null!;

    // Analiz kodu ilişkisi
    public AnalysisCode AnalysisCode { get; set; } = null!;
}

