namespace SampleAnalysisTracking.Clients;

// Yerel LLM (Büyük Dil Modeli) servisi ile iletişim kuran istemci arayüzü.
// Ollama üzerinden metin üretimi ve yapılandırılmış JSON çıktısı almayı sağlar.
public interface IOllamaClient
{
    // Belirli bir numunenin analiz sonuçlarını ve RAG bağlamını içeren prompt'u yerel LLM modeline iletir;
    // modelden JSON formatında doğrulanmış numune değerlendirme raporu metnini döndürür.
    Task<string> GenerateSampleReportAsync(
        string prompt,
        CancellationToken cancellationToken);

    // Laboratuvar iş yükü, gecikmeler ve performans metriklerini içeren prompt'u modele gönderir;
    // modelden özet gözlemler ve operasyonel öneriler içeren JSON metnini döndürür.
    Task<string> GenerateWorkloadInsightAsync(
        string prompt,
        CancellationToken cancellationToken);
}

