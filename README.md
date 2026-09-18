# Numune Analiz Takip

Numunelerin kabulden sonuçlandırmaya kadar yaşam döngüsünü, analiz sonuçlarını ve kullanıcı yetkilerini yöneten ASP.NET Core uygulamasıdır. Proje; PostgreSQL ve pgvector üzerinde veri saklar, yerel Ollama modelleriyle bilgi tabanı araması, numune raporu ve iş yükü özeti üretir.

AI çıktıları yalnızca inceleme ve karar desteği içindir. Uygulama otomatik analiz onayı, mevzuat uygunluğu veya akreditasyon kararı üretmez.

## Özellikler

- Numune kaydı, laboratuvar çalışanı atama, durum akışı ve işlem geçmişi
- Analiz kataloğu, parametre tanımları, ölçüm girişi ve zorunlu sonuç kontrolü
- Cookie tabanlı oturum ve rol/atama temelli erişim kontrolü
- Kullanıcı ve profil yönetimi
- Yönetici performans göstergeleri ve iş yükü özeti
- Ollama ile yapılandırılmış AI numune raporu
- pgvector tabanlı bilgi tabanı ve semantik arama
- Model yanıtları için kaynak doğrulama, karar ifadesi filtreleri ve kural tabanlı özetler

## Teknolojiler

| Katman | Teknoloji |
| --- | --- |
| Uygulama | .NET 10, ASP.NET Core MVC ve Web API |
| Veri | PostgreSQL, Entity Framework Core, Npgsql |
| Vektör arama | pgvector, Pgvector.EntityFrameworkCore |
| Yerel AI | Ollama, `qwen3:4b-instruct`, `qwen3-embedding:0.6b` |
| Arayüz | Razor Views, vanilla JavaScript ve CSS |
| Test | xUnit, WebApplicationFactory, EF Core InMemory |

## Gereksinimler

- .NET 10 SDK
- PostgreSQL ve `vector` uzantısını oluşturabilen bir veritabanı kullanıcısı
- AI ve bilgi tabanı özellikleri için Ollama

Gerekli Ollama modelleri:

```shell
ollama pull qwen3:4b-instruct
ollama pull qwen3-embedding:0.6b
```

Varsayılan Ollama adresi `http://localhost:11434/` şeklindedir. Model, zaman aşımı, parça boyutu ve arama limiti ayarları [appsettings.json](appsettings.json) içindeki `Ollama` ve `Rag` bölümlerindedir. Embedding boyutu mevcut şemada 1024'tür.

## Yerel kurulum

### 1. Veritabanını oluşturun

PostgreSQL'de boş bir geliştirme veritabanı oluşturun. İlk yerel kurulumda seed işlemi migration'ları uygular ve `vector` uzantısını hazırlar; bağlantı kullanıcısının gerekli yetkilere sahip olması gerekir.

```sql
CREATE DATABASE numune_analiz_db;
```

### 2. Yerel ayarları tanımlayın

Önerilen yöntem .NET user-secrets kullanmaktır:

```shell
dotnet user-secrets set "ConnectionStrings:SampleDb" "Host=localhost;Port=5432;Database=numune_analiz_db;Username=postgres;Password=PAROLANIZ"
dotnet user-secrets set "SeedData:Enabled" "true"
dotnet user-secrets set "SeedData:DefaultPassword" "GucluBirDemoParolasi123!"
```

Alternatif olarak [appsettings.example.json](appsettings.example.json) dosyasını `appsettings.Development.json` adıyla kopyalayıp değerleri düzenleyebilirsiniz. Bu dosya Git tarafından yok sayılır. Gerçek parolaları `appsettings.json` içine yazmayın.

`SeedData:Enabled=true` yalnızca Development ortamında çalışır. İlk açılışta migration'ları uygular; 15 demo kullanıcı, analiz kataloğu, 480 numune ve taslak bilgi tabanı belgeleri oluşturur. Bilgi tabanı belgelerinin vektörleştirilmesi için Ollama çalışıyor olmalıdır.

Seed kullanılmayacaksa migration'ları uygulama başlamadan önce ayrıca çalıştırın:

```shell
dotnet ef database update --project SampleAnalysisTracking.csproj
```

### 3. Uygulamayı çalıştırın

```shell
dotnet restore SampleAnalysisTracking.csproj
dotnet run --project SampleAnalysisTracking.csproj --launch-profile http
```

Uygulamayı `http://localhost:5011` adresinde açın. Seed açıksa kullanıcı adı `admin`, parola ise `SeedData:DefaultPassword` değeridir. İlk kurulum tamamlandıktan sonra seed ayarını kapatabilirsiniz.

## Roller ve erişim

| Rol | Mevcut yetkiler |
| --- | --- |
| `Admin` | Kullanıcılar, numuneler, analiz kataloğu, bilgi tabanı, AI raporu ve performans işlemleri |
| `Laboratory` | Kendisine atanmış numuneleri görüntüleme; analiz başlatma, sonuç girme, tamamlama/iptal ve AI raporu |
| `Manager` | Performans görünümü ve AI iş yükü özeti |
| `Field` | Kayıt başvurularında kullanılan rol; mevcut API'de numune iş akışı işlemi sunulmuyor |

Herkese açık kayıt uç noktası pasif bir `Field` hesabı oluşturur. Hesabın kullanılabilmesi için bir yöneticinin hesabı etkinleştirmesi gerekir.

## Derleme ve test

```shell
dotnet build SampleAnalysisTracking.csproj -c Release
dotnet test SampleAnalysisTracking.IntegrationTests/SampleAnalysisTracking.IntegrationTests.csproj -c Release
```

Entegrasyon testleri uygulamayı `WebApplicationFactory` ile ayağa kaldırır; EF Core InMemory veritabanı ve sahte AI istemcileri kullanır. Kimlik doğrulama, hız sınırlama, rol/atama kontrolleri, analiz yaşam döngüsü, bilgi tabanı kuralları ve AI yanıt güvenliği dahil 14 kritik akışı doğrular.

Bu testler gerçek PostgreSQL migration'larını, pgvector benzerlik sorgularını, gerçek Ollama modellerini ve tarayıcı tabanlı uçtan uca arayüz akışlarını kapsamaz.

## Temel API uç noktaları

Development ortamında OpenAPI belgesi `/openapi/v1.json` adresindedir. Örnek istekler [SampleAnalysisTracking.http](SampleAnalysisTracking.http) dosyasında bulunur.

| Yöntem | Uç nokta | Açıklama |
| --- | --- | --- |
| `POST` | `/api/auth/login` | Oturum açar |
| `POST` | `/api/auth/register` | Pasif Field hesabı oluşturur |
| `GET` | `/api/auth/me` | Aktif kullanıcıyı döndürür |
| `GET` | `/api/samples/paged` | Admin numune listesi |
| `GET` | `/api/samples/mine/paged` | Atanmış laboratuvar numuneleri |
| `POST` | `/api/samples` | Numune oluşturur |
| `PATCH` | `/api/samples/{id}/laboratory-worker` | Laboratuvar çalışanı atar |
| `PATCH` | `/api/samples/{id}/status` | Numune durumunu ilerletir |
| `POST` | `/api/samples/{id}/analyses` | Numuneye analiz atar |
| `PUT` | `/api/sample-analyses/{id}/results` | Ölçüm sonuçlarını kaydeder |
| `POST` | `/api/sample-analyses/{id}/complete` | Analizi tamamlar |
| `POST` | `/api/samples/{id}/ai-report` | RAG destekli numune raporu üretir |
| `POST` | `/api/knowledge-base/search` | Semantik bilgi tabanı araması yapar |
| `GET` | `/api/performance/overview` | Performans göstergelerini döndürür |
| `POST` | `/api/performance/ai-workload-insight` | İş yükü özeti üretir |

## Proje yapısı

```text
Controllers/                         HTTP ve yetkilendirme sınırı
Services/                            İş kuralları ve uygulama servisleri
Data/                                DbContext, EF yapılandırmaları ve seed
Models/                              Kalıcı veri modelleri
DTOs/                                API istek ve yanıt modelleri
Clients/                             Ollama metin ve embedding istemcileri
Options/                             Ollama ve RAG ayar modelleri
Views/ ve wwwroot/                   Razor arayüzü, JavaScript ve CSS
Migrations/                          PostgreSQL şema geçmişi
SampleAnalysisTracking.IntegrationTests/  Kritik HTTP akış testleri
```

Temel bağımlılık akışı `Controller -> Service -> DbContext/AI Client` şeklindedir.

## Belgeler

- [Proje durum ve teknik değerlendirme raporu](docs/PROJE_DURUM_DEGERLENDIRME_RAPORU.docx)
- [AI numune raporu modülü](docs/AI_SAMPLE_REPORT_MODULE.md)

## Mevcut sınırlar

- Üretim dağıtımı için Docker, CI/CD ve ortam bazlı operasyon kılavuzu bulunmuyor.
- Gerçek PostgreSQL, pgvector ve Ollama bileşenlerini birlikte doğrulayan test katmanı yok.
- RAG araması aktif taslak belgeleri de sonuçlara dahil ediyor; üretimde doğrulanmış kaynak politikası ayrıca belirlenmeli.
- Depoda henüz bir `LICENSE` dosyası bulunmuyor.
