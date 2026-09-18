using System.Text.Json.Serialization;
using SampleAnalysisTracking.Common.Enums;

namespace SampleAnalysisTracking.DTOs;

// İstemciye (frontend) döndürülen zenginleştirilmiş AI numune rapor yanıtı.
// Modelin ürettiği cümleleri, veritabanından derlenen özet listeleri ve RAG kaynaklarını bir arada sunar.
public sealed record AiSampleReportResponse(
    // Raporun birleştirilmiş metin özeti
    string Summary,
    // Veritabanında tamamlanmış durumdaki analizlerin listesi
    IReadOnlyList<string> CompletedAnalyses,
    // Devam eden veya henüz başlamamış analizlerin listesi
    IReadOnlyList<string> PendingAnalyses,
    // Limit dışı ölçümler, gecikmeler veya dikkat gerektiren kritik noktalar
    IReadOnlyList<string> AttentionPoints,
    // Zorunlu olduğu halde henüz sonucu girilmemiş parametreler
    IReadOnlyList<string> MissingRequiredResults,
    // Yasal ve teknik sorumluluk reddi beyanı (AI raporunun onay niteliği taşımadığı bilgisi)
    string Disclaimer,
    // Raporu üreten yerel LLM model adı ve mod bilgisi
    string Model)
{
    // Modelin ürettiği her bir cümlenin kaynak bağıntılarını (atıflarını) taşıyan cümle listesi
    public IReadOnlyList<AiReportSentence> SummarySentences { get; init; } = [];

    // Raporda atıfta bulunulan veya bağlam olarak modele sunulan RAG bilgi belgeleri
    public IReadOnlyList<AiReportKnowledgeSource> KnowledgeSources { get; init; } = [];
}

// Yapay zekanın ürettiği tekil bir cümle ve bu cümlenin dayanakları (atıfları).
public sealed record AiReportSentence(
    // Cümlenin Türkçe metni
    string Text,
    // Cümlenin laboratuvar numune/analiz veritabanı kayıtlarına dayanıp dayanmadığı
    bool UsesRecordData,
    // Cümlenin atıfta bulunduğu RAG bilgi kaynaklarının numaraları (örn. [1], [2])
    IReadOnlyList<int> SourceNumbers);

// RAG mekanizması ile bilgi tabanından çekilen ve modele bağlam olarak verilen doküman detayı.
public sealed record AiReportKnowledgeSource(
    // Modele sunulan bağlamdaki sıra numarası (1, 2...)
    int SourceNumber,
    // Doküman başlığı
    string Title,
    // Doküman kategorisi (Prosedür, Analiz Metodu vb.)
    KnowledgeDocumentCategory Category,
    // Doğrulanmış veya taslak kaynak durumu
    KnowledgeSourceStatus SourceStatus,
    // Resmî kaynak referans kodu
    string? SourceReference,
    // Arama sorgusu ile doküman parçacığı arasındaki kosinüs benzerlik skoru (0.0 - 1.0)
    double? Similarity,
    // Eşleşme türü ("Analiz kodu eşleşmesi" veya "Anlamsal eşleşme")
    string MatchType,
    // Dokümandan modele aktarılan metin alıntısı
    string Excerpt);

// Ollama'nın yapılandırılmış JSON çıktısını karşılamak için kullanılan iç model.
internal sealed class AiSampleReportModelResponse
{
    // Model tarafından üretilen cümleler dizisi
    [JsonPropertyName("summarySentences")]
    public IReadOnlyList<AiSampleReportModelSentence>? SummarySentences { get; init; }
}

// Modelin JSON çıktısındaki her bir cümlenin ham şeması.
internal sealed class AiSampleReportModelSentence
{
    // Modelin yazdığı cümle
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    // Kayıt verisi kullanıldı mı bayrağı
    [JsonPropertyName("usesRecordData")]
    public bool UsesRecordData { get; init; }

    // Kullanılan kaynak numaraları dizisi
    [JsonPropertyName("sourceNumbers")]
    public IReadOnlyList<int>? SourceNumbers { get; init; }
}

