# Numune Analiz Takip

Numunelerin yaşam döngüsünü, analiz sonuçlarını ve kullanıcı yetkilerini takip eden ASP.NET Core uygulaması. Yerel Ollama modelleriyle numune raporu, bilgi tabanı araması (RAG) ve iş yükü özeti üretir.

## Özellikler

- Numune oluşturma, çalışan atama, durum takibi ve işlem geçmişi.
- Analiz kataloğu, parametreler, sonuç girişi ve zorunlu sonuç kontrolü.
- Cookie tabanlı oturum, rol ve numune sahipliğine göre erişim kontrolü.
- Kullanıcı/profil yönetimi ve performans görünümü.
- Yerel AI raporları ve pgvector tabanlı bilgi araması.

AI çıktıları inceleme desteğidir; analiz onayı veya durum değişikliği yapmaz. Uygulamanın resmî ISO 17025 uygunluk ya da akreditasyon iddiası yoktur.

## Gereksinimler

- .NET 10 SDK.
- PostgreSQL ve sunucuya kurulmuş pgvector uzantısı.
- AI özellikleri için çalışan Ollama ve şu modeller:

```shell
ollama pull qwen3:4b-instruct
ollama pull qwen3-embedding:0.6b
```

Ollama'nın varsayılan adresi `http://localhost:11434/`. Model ayarları `appsettings.json` içindedir; embedding boyutu mevcut veritabanı şemasında 1024'tür. Arayüz Razor, JavaScript ve CSS kullanır; npm kurulumu gerekmez.

## Yerel kurulum ve ilk giriş

1. Depoyu klonlayıp proje klasörünü açın. PostgreSQL'de yerel deneme için boş bir `numune_analiz_db` veritabanı oluşturun. Bağlantı kullanıcısının tablo ve `vector` uzantısını oluşturma yetkisi olmalıdır.
2. `appsettings.example.json` dosyasını `appsettings.Development.json` adıyla kopyalayın. Mevcut bir geliştirme ayar dosyanız varsa üzerine yazmayın. `ConnectionStrings:SampleDb` değerini kendi veritabanı adı, kullanıcısı ve parolasıyla düzenleyin.
3. İlk deneme kurulumu için aynı dosyada `SeedData:Enabled` değerini `true` yapın ve `SeedData:DefaultPassword` alanına kendi demo parolanızı yazın. Ollama'yı ve yukarıdaki modelleri hazırlayın.
4. Proje klasöründe çalıştırın:

```shell
dotnet restore SampleAnalysisTracking.csproj
dotnet run --project SampleAnalysisTracking.csproj --launch-profile http
```

Development ortamında seed açıkken uygulama migration'ları uygular; `admin` dahil demo kullanıcılarını, analiz kataloğunu ve 480 örnek numuneyi oluşturur. Bilgi tabanı demo belgeleri için yerel embedding modelini kullanır; ilk açılış zaman alabilir.

Tarayıcıda `http://localhost:5011` adresini açın. Kullanıcı adı **admin**, parola ise belirlediğiniz `SeedData:DefaultPassword` değeridir. İlk kurulumdan sonra `SeedData:Enabled` değerini `false` yapabilirsiniz. Demo parolası yeni oluşturulan demo hesapları içindir; bu ayarı değiştirmek mevcut hesapların parolasını değiştirmez.

Seed işlemini yalnız yerel deneme veritabanında kullanın. Ekrandan yapılan yeni kayıt başvuruları pasif `Field` hesabı oluşturur ve admin tarafından etkinleştirilir.

`appsettings.example.json` otomatik yüklenmez; yalnız şablondur. Gerçek bağlantı ve parolalar `appsettings.Development.json` içinde yerelde kalır ve Git'e alınmaz. `appsettings.json` içine gerçek parola yazmayın.

## Derleme ve test

```shell
dotnet build SampleAnalysisTracking.csproj -c Release
dotnet test SampleAnalysisTracking.IntegrationTests/SampleAnalysisTracking.IntegrationTests.csproj -c Release
```

Testler her test için ayrı uygulama/veritabanı kullanır. Gerçek PostgreSQL veya Ollama bağlantısı gerektirmez; EF Core InMemory ve sahte AI istemcileriyle HTTP akışlarını doğrular. PostgreSQL migration'ları ve pgvector benzerlik sorguları bu test kapsamına dahil değildir.

## Kod ve API

Temel akış: `Controller -> Service -> DbContext -> PostgreSQL`. Kaynak kod `Controllers`, `Services`, `Data`, `Models`, `DTOs`, `Clients` ve `Options` klasörlerinde; arayüz `Views` ve `wwwroot` altındadır. `Migrations` veritabanı şemasının kurulum ve değişiklik geçmişidir.

Development ortamında OpenAPI belgesi `/openapi/v1.json` adresindedir. API istek örnekleri [SampleAnalysisTracking.http](SampleAnalysisTracking.http), AI raporu açıklaması [AI Numune Raporu Modülü](docs/AI_SAMPLE_REPORT_MODULE.md) dosyasındadır.
