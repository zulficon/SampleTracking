# AI Numune Raporu Modülü

## Amaç

Bu modül, bir numunenin analiz kayıtlarını tek bir yapıda toplayıp yerel Ollama modeliyle kısa bir inceleme raporu üretir. Model teknik karar vermez; numune veya analiz durumunu değiştirmez ve veritabanına yazmaz.

## Akış

```text
Tarayıcı
  -> POST /api/samples/{sampleId}/ai-report
  -> AiSampleReportsController
  -> AiSampleReportService
  -> SampleAnalysisService + SampleService
  -> KnowledgeBaseService -> yerel embedding modeli + pgvector
  -> IOllamaClient
  -> Yerel Ollama / Qwen3
```

`AiSampleReportService` veriyi doğrudan HTTP katmanından almaz. Önce mevcut servisleri kullanarak numunenin ve analizlerin erişim kontrolünü uygular. Böylece aynı servis başka bir controller, arka plan işi veya farklı bir kullanıcı arayüzünden çağrılsa da yetki kuralı korunur.

## Endpoint

```http
POST /api/samples/{sampleId}/ai-report
```

Yetkili roller:

- `Admin`: Her numune için rapor oluşturabilir.
- `Laboratory`: Yalnız kendisine atanmış numune için rapor oluşturabilir.

Başarılı cevap örneği:

```json
{
  "summary": "Numunenin süreç ve ölçüm özeti.",
  "summarySentences": [
    {
      "text": "Numunenin süreç ve ölçüm özeti.",
      "usesRecordData": true,
      "sourceNumbers": []
    }
  ],
  "knowledgeSources": [],
  "completedAnalyses": ["CHEM-01 · Kimyasal analiz (Completed)"],
  "pendingAnalyses": ["MIN-02 · Mineral analiz (InProgress)"],
  "attentionPoints": ["Referans sınırları dışındaki ölçümleri inceleyin."],
  "missingRequiredResults": ["CHEM-01 · Fe"],
  "disclaimer": "Bu AI raporu yalnızca inceleme desteğidir; teknik karar, onay veya durum değişikliği üretmez.",
  "model": "qwen3:4b-instruct"
}
```

Ollama erişilemezse endpoint `503 Service Unavailable` döner. Numune yoksa `404`, kullanıcının erişim yetkisi yoksa `403` döner.

## Modele Gönderilen Veri

Her çağrıda en fazla 20 analiz ve her analizde en fazla 50 parametre gönderilir. Gönderilen alanlar:

- Numune kodu, türü, konumu, durumu ve zaman bilgileri
- Analiz kodu, adı, durumu ve zaman bilgileri
- Parametre adı, birimi, referans sınırları ve zorunluluk bilgisi
- Kayıtlı sayısal ölçüm, ölçüm zamanı ve referans dışı uyarısı
- Numune açıklaması, analiz sonuç notu ve parametre sonuç notları
- Bilgi tabanından seçilen en fazla iki kaynak parçası

Parola ve kullanıcı iletişim alanları rapor verisine eklenmez. Açıklama ve sonuç notları modele gönderildiğinden, bu serbest metin alanlarına yazılmış kişisel bilgiler otomatik olarak ayıklanmaz.

## Güvenlik Kuralları

- AI çağrısı yalnız sunucu tarafından yapılır; tarayıcı doğrudan Ollama'ya bağlanmaz.
- Cookie, rol ve numune sahipliği kontrolü servis çağrısından önce uygulanır.
- Modelden JSON cevap istenir ve alanlar uygulama tarafından doğrulanır.
- Bazı resmî onay ve piyasaya sürme ifadeleri response katmanında filtrelenir; bu filtre tüm teknik karar ifadelerini kapsayan bir güvence değildir.
- Kaynak numaraları seçilen kaynaklarla sınırlandırılır; geçersiz numaralar çıkarılır.
- Model geçerli JSON üretemezse uygulama kayıtlı verilerden karar vermeyen temel bir özet üretir.

## Başka Bir Modüle Entegrasyon

Başka bir modül bu işlevi kullanmak isterse `AiSampleReportService.GenerateAsync(...)` metodunu çağırır. Yeni HTTP endpoint yazmak zorunlu değildir.

```csharp
var result = await aiSampleReportService.GenerateAsync(
    sampleId,
    currentUserId,
    isAdmin,
    cancellationToken);
```

Bu yaklaşımda erişim kontrolü, veri sınırı, Ollama hatası ve güvenli fallback davranışı ortak kalır. Yeni modül yalnız başarılı `AiSampleReportResponse` sonucunu kendi ekranında veya kendi iş akışında kullanır.

## Test

Integration test, bilgi tabanı boşken atanmış Laboratory kullanıcısının yapılandırılmış rapor alabildiğini ve geçersiz kaynak numaralarının elendiğini doğrular. Her test ayrı uygulama ve EF Core InMemory veritabanı kullanır; AI istemcileri sahtedir. Gerçek PostgreSQL/pgvector benzerlik araması bu testte doğrulanmaz.

```powershell
dotnet test SampleAnalysisTracking.IntegrationTests/SampleAnalysisTracking.IntegrationTests.csproj -c Release
```
