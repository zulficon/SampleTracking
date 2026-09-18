namespace SampleAnalysisTracking.Options;

// Yerel LLM (Büyük Dil Modeli) sunucusunun bağlantı ve çıkarım (inference) ayarları.
// appsettings.json dosyasındaki "Ollama" bölümü ile eşleşir.
public sealed class OllamaOptions
{
    // Konfigürasyon bölüm adı
    public const string SectionName = "Ollama";

    // Ollama REST API servis adresi
    public string BaseUrl { get; init; } = "http://localhost:11434/";

    // Metin üretimi ve analiz yorumlama için kullanılan ana LLM modeli (örn. Qwen 3 4B Instruct)
    public string Model { get; init; } = "qwen3:4b-instruct";

    // LLM üretim isteğinin maksimum bekleme süresi (saniye)
    public int TimeoutSeconds { get; init; } = 180;

    // Modelin hafızasında tutabileceği girdi ve çıktı toplam token sınırı (num_ctx)
    public int ContextWindow { get; init; } = 12288;

    // Modelin tek bir yanıtta üretebileceği maksimum token sayısı (num_predict)
    public int MaxOutputTokens { get; init; } = 1200;

    // Modelin bellekte (VRAM/RAM) boşta bekletilme süresi; sık çağrılarda yeniden yükleme maliyetini önler
    public string KeepAlive { get; init; } = "10m";

    // Yanıt yaratıcılık / rastlantısallık katsayısı; 0.35 değeri analitik ve verilere sadık raporlar için idealdir
    public double Temperature { get; init; } = 0.35;

    // Çekirdek örnekleme (nucleus sampling) eşiği; kümülatif olasılığı %90 olan token kümesini filtreler
    public double TopP { get; init; } = 0.9;
}

