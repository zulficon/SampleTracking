using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;
using SampleAnalysisTracking.Services;

namespace SampleAnalysisTracking.Data.Seed;

public static class DevelopmentDataSeeder
{
    private const string PasswordConfigurationKey =
        "SeedData:DefaultPassword";

    private const int TotalSeedSampleCount = 480;
    private const int BatchSize = 50;

    private static readonly SampleStatus[] Workflow =
    [
        SampleStatus.Created,
        SampleStatus.Collected,
        SampleStatus.Transferred,
        SampleStatus.Received,
        SampleStatus.Analyzing,
        SampleStatus.Completed
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SampleAnalysisTrackingDbContext>();

        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<AppUser>>();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DevelopmentDataSeeder));
        var knowledgeBaseService = scope.ServiceProvider
            .GetRequiredService<KnowledgeBaseService>();

        var defaultPassword = configuration[PasswordConfigurationKey];

        if (string.IsNullOrWhiteSpace(defaultPassword))
        {
            throw new InvalidOperationException(
                $"{PasswordConfigurationKey} is missing. " +
                "Store the development seed password with user-secrets.");
        }

        await db.Database.MigrateAsync(cancellationToken);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken);

        var users = await SeedUsersAsync(
            db,
            passwordHasher,
            defaultPassword,
            cancellationToken);

        var locations = await SeedLocationsAsync(
            db,
            cancellationToken);

        var analysisCodes = await SeedAnalysisCodesAsync(
            db,
            cancellationToken);

        var addedSampleCount = await SeedSamplesAsync(
            db,
            users,
            locations,
            analysisCodes,
            cancellationToken);

        await BackfillAnalysisDataAsync(db, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var addedKnowledgeDocumentCount = await SeedKnowledgeDocumentsAsync(
            db,
            knowledgeBaseService,
            users["admin"].Id,
            logger,
            cancellationToken);

        logger.LogInformation(
            "Development seed completed. {SampleCount} new samples and {KnowledgeDocumentCount} knowledge documents were added (total sample target: {TotalCount}).",
            addedSampleCount,
            addedKnowledgeDocumentCount,
            TotalSeedSampleCount);
    }

    private static async Task<int> SeedKnowledgeDocumentsAsync(
        SampleAnalysisTrackingDbContext db,
        KnowledgeBaseService knowledgeBaseService,
        long adminUserId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var definitions = GetKnowledgeDocumentDefinitions();
        var existingTitles = await db.KnowledgeDocuments
            .AsNoTracking()
            .Select(document => document.Title)
            .ToHashSetAsync(cancellationToken);
        var analysisCodeIds = await db.AnalysisCodes
            .AsNoTracking()
            .ToDictionaryAsync(code => code.Code, code => code.Id, cancellationToken);
        var addedCount = 0;

        foreach (var definition in definitions.Where(definition =>
                     !existingTitles.Contains(definition.Title)))
        {
            var linkedCodeIds = definition.AnalysisCodes
                .Where(analysisCodeIds.ContainsKey)
                .Select(code => analysisCodeIds[code])
                .ToArray();
            var result = await knowledgeBaseService.CreateAsync(
                new CreateKnowledgeDocumentRequest
                {
                    Title = definition.Title,
                    Category = definition.Category,
                    SourceStatus = KnowledgeSourceStatus.Draft,
                    SourceReference = "Uygulama veri modeli ve iş akışı · demo içeriği",
                    SourceVersion = "Draft 1.0",
                    SourceText = definition.SourceText,
                    AnalysisCodeIds = linkedCodeIds
                },
                adminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Draft knowledge document {Title} could not be seeded: {Message}",
                    definition.Title,
                    result.ErrorMessage);
                continue;
            }

            addedCount++;
        }

        return addedCount;
    }

    private static IReadOnlyList<KnowledgeDocumentSeed> GetKnowledgeDocumentDefinitions() =>
    [
        new(
            "Numune zinciri, etiketleme ve izlenebilirlik",
            KnowledgeDocumentCategory.Procedure,
            [],
            """
            Demo / taslak prosedür bağlamı: Numune kaydı oluşturulurken numune kodu, türü, açıklaması, oluşturan kullanıcı ve zaman bilgisi birlikte değerlendirilir. Numunenin durum geçmişi kayıtla ilişkili olmalıdır. Bu metin resmî numune alma veya taşıma talimatı değildir; uygulamadaki izlenebilirlik alanlarının değerlendirilmesi için hazırlanmıştır.
            """),
        new(
            "Zorunlu parametre sonuçlarının tamamlanması",
            KnowledgeDocumentCategory.QualityGuideline,
            [],
            """
            Demo / taslak kalite bağlamı: Bir analiz tamamlanmadan önce aktif ve zorunlu olarak tanımlanmış parametrelerin sonuçları kayıt altında olmalıdır. Uygulama eksik zorunlu sonucu analiz tamamlama kontrolünde gösterir. Bu belge herhangi bir akreditasyon kriteri veya teknik kabul kararı üretmez; yalnız kayıt bütünlüğü incelemesine yardımcı olur.
            """),
        new(
            "Referans dışı ölçüm değerlerinin incelenmesi",
            KnowledgeDocumentCategory.QualityGuideline,
            [],
            """
            Demo / taslak kalite bağlamı: Ölçüm sonucu, analiz anında kaydedilmiş referans alt ve üst sınırlarıyla birlikte görüntülenir. Referans dışı işareti kullanıcıya inceleme uyarısı verir; uygulama otomatik uygunluk, ret veya resmî karar vermez. Sonuç notu, ölçüm zamanı ve kaydı giren kullanıcı denetlenebilirlik için korunur.
            """),
        new(
            "Tekrar ölçüm, paralel okuma ve kalite notları",
            KnowledgeDocumentCategory.QualityGuideline,
            [],
            """
            Demo / taslak kalite bağlamı: Tekrar ölçüm, paralel okuma veya seyreltme gibi işlemler gerçekleştiğinde bunların açıklaması sonuç notunda kayıt altına alınabilir. RAG raporu bu notları yalnız özetleme amacıyla kullanır. Metin, hangi kalite kontrol işleminin zorunlu olduğunu belirlemez ve laboratuvar prosedürünün yerine geçmez.
            """),
        new(
            "Analiz iptali, yeniden talep ve numune yeterliliği",
            KnowledgeDocumentCategory.Procedure,
            [],
            """
            Demo / taslak süreç bağlamı: Analiz iptal edildiğinde iptal durumu ve açıklama analiz geçmişinde korunur. İptal edilmiş analiz, tamamlanmış analiz gibi yorumlanmamalıdır. Numunenin tüm analizleri iptal olduğunda uygulama numuneyi laboratuvar kabul durumuna geri çekebilir. Yeniden analiz talebi yetkili kullanıcının yeni analiz atamasıyla yürütülür.
            """),
        new(
            "Mineral ve iyon dengesi için inceleme çerçevesi",
            KnowledgeDocumentCategory.AnalysisMethod,
            ["ION_MINERAL", "NITRATE", "PHOSPHATE"],
            """
            Demo / taslak analiz bağlamı: Mineral ve iyon denge paketi; kalsiyum, magnezyum, toplam sertlik, klorür, sülfat, nitrat, nitrit ve florür gibi kayıtlı parametreleri bir arada incelemek için kullanılır. RAG özeti yalnız numuneye atanmış analizlerdeki güncel ölçüm, birim, referans aralığı ve sonuç notuna dayanmalıdır. Bu metin mevzuat limiti veya teknik uygunluk kararı değildir.
            """),
        new(
            "Ölçüm birimi, sonuç notu ve veri bütünlüğü",
            KnowledgeDocumentCategory.QualityGuideline,
            [],
            """
            Demo / taslak veri bağlamı: Bir sonuçta sayısal değer, ölçüm birimi, ölçüm zamanı, sonucu giren kullanıcı ve gerekirse açıklama notu birlikte saklanır. Aynı parametre için aynı analiz altında ikinci bir sonuç oluşturulmaz. Birim veya referans bilgisi, sonuç kaydedildiği andaki değer olarak korunur; katalogdaki sonraki değişiklikler geçmiş sonucu değiştirmemelidir.
            """),
        new(
            "Analiz tamamlanma ve numune durum senkronizasyonu",
            KnowledgeDocumentCategory.Procedure,
            [],
            """
            Demo / taslak süreç bağlamı: Analiz başlatılmadan önce numunenin laboratuvar tarafından kabul edilmiş olması beklenir. İlk analiz başladığında numune analiz sürecine alınır. İptal edilmemiş analizlerin tamamı tamamlandığında numune tamamlandı durumuna geçebilir. Bu açıklama uygulama iş akışını anlatır; fiziksel laboratuvar sürecinin resmî prosedürü değildir.
            """)
    ];

    private static async Task<Dictionary<string, AppUser>> SeedUsersAsync(
        SampleAnalysisTrackingDbContext db,
        IPasswordHasher<AppUser> passwordHasher,
        string defaultPassword,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new UserSeed("admin", "Sistem Yöneticisi", "admin@example.test", UserRole.Admin),
            new UserSeed("manager.ayse", "Ayşe Demir", "ayse.demir@example.test", UserRole.Manager),
            new UserSeed("manager.kemal", "Kemal Varol", "kemal.varol@example.test", UserRole.Manager),
            new UserSeed("field.emre", "Emre Kaya", "emre.kaya@example.test", UserRole.Field),
            new UserSeed("field.zeynep", "Zeynep Şahin", "zeynep.sahin@example.test", UserRole.Field),
            new UserSeed("field.burak", "Burak Yurt", "burak.yurt@example.test", UserRole.Field),
            new UserSeed("field.selin", "Selin Korkmaz", "selin.korkmaz@example.test", UserRole.Field),
            new UserSeed("lab.mehmet", "Mehmet Yılmaz", "mehmet.yilmaz@example.test", UserRole.Laboratory),
            new UserSeed("lab.elif", "Elif Aydın", "elif.aydin@example.test", UserRole.Laboratory),
            new UserSeed("lab.merve", "Merve Koç", "merve.koc@example.test", UserRole.Laboratory),
            new UserSeed("lab.can", "Can Arslan", "can.arslan@example.test", UserRole.Laboratory),
            new UserSeed("lab.deniz", "Deniz Çelik", "deniz.celik@example.test", UserRole.Laboratory),
            new UserSeed("lab.hakan", "Hakan Öztürk", "hakan.ozturk@example.test", UserRole.Laboratory),
            new UserSeed("lab.gamze", "Gamze Akın", "gamze.akin@example.test", UserRole.Laboratory),
            new UserSeed("lab.tolga", "Tolga Şen", "tolga.sen@example.test", UserRole.Laboratory)
        };

        var existingUsers = await db.Users
            .ToListAsync(cancellationToken);

        var users = existingUsers.ToDictionary(
            user => user.Username,
            StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;

        foreach (var definition in definitions)
        {
            if (users.ContainsKey(definition.Username))
            {
                continue;
            }

            var user = new AppUser
            {
                Username = definition.Username,
                FullName = definition.FullName,
                Email = definition.Email,
                Role = definition.Role,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = passwordHasher.HashPassword(
                user,
                defaultPassword);

            db.Users.Add(user);
            users.Add(user.Username, user);
        }

        await db.SaveChangesAsync(cancellationToken);

        return users;
    }

    private static async Task<Dictionary<string, Location>> SeedLocationsAsync(
        SampleAnalysisTrackingDbContext db,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new LocationSeed("Kabul Birimi", "Numunelerin teslim alındığı ilk birim."),
            new LocationSeed("Numune Hazırlama", "Analiz öncesi hazırlık işlemlerinin yapıldığı alan."),
            new LocationSeed("Kimya Laboratuvarı", "Genel kimyasal analizlerin yürütüldüğü laboratuvar."),
            new LocationSeed("Mikrobiyoloji Laboratuvarı", "Mikrobiyolojik analizlerin yürütüldüğü laboratuvar."),
            new LocationSeed("Enstrümantal Analiz", "Cihaz tabanlı ölçümlerin yapıldığı laboratuvar."),
            new LocationSeed("Saha Deposu", "Sahadan gelen ekipman ve numunelerin geçici alanı."),
            new LocationSeed("Soğuk Oda", "Sıcaklık kontrollü numune saklama alanı."),
            new LocationSeed("Arşiv", "Tamamlanan numunelerin saklandığı alan.")
        };

        var existingLocations = await db.Locations
            .ToListAsync(cancellationToken);

        var locations = existingLocations.ToDictionary(
            location => location.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (locations.ContainsKey(definition.Name))
            {
                continue;
            }

            var location = new Location
            {
                Name = definition.Name,
                Description = definition.Description
            };

            db.Locations.Add(location);
            locations.Add(location.Name, location);
        }

        await db.SaveChangesAsync(cancellationToken);

        return locations;
    }

    private static async Task<Dictionary<string, AnalysisCode>> SeedAnalysisCodesAsync(
        SampleAnalysisTrackingDbContext db,
        CancellationToken cancellationToken)
    {
        var definitions = GetAnalysisCodeDefinitions();

        var existingCodes = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .ToListAsync(cancellationToken);

        var analysisCodes = existingCodes.ToDictionary(
            analysisCode => analysisCode.Code,
            StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!analysisCodes.TryGetValue(definition.Code, out var analysisCode))
            {
                analysisCode = new AnalysisCode
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Description = definition.Description,
                    IsActive = true
                };

                db.AnalysisCodes.Add(analysisCode);
                analysisCodes.Add(analysisCode.Code, analysisCode);
            }

            foreach (var parameterDef in definition.Parameters)
            {
                var existingParameter = analysisCode.Parameters.FirstOrDefault(p =>
                    string.Equals(p.Code, parameterDef.Code, StringComparison.OrdinalIgnoreCase));

                if (existingParameter is null)
                {
                    analysisCode.Parameters.Add(new AnalysisParameter
                    {
                        Code = parameterDef.Code,
                        Name = parameterDef.Name,
                        DefaultUnit = parameterDef.DefaultUnit,
                        ReferenceMin = parameterDef.ReferenceMin,
                        ReferenceMax = parameterDef.ReferenceMax,
                        IsRequired = parameterDef.IsRequired,
                        IsActive = true,
                        DisplayOrder = parameterDef.DisplayOrder
                    });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return analysisCodes;
    }

    private static IReadOnlyList<AnalysisCodeSeed> GetAnalysisCodeDefinitions() =>
    [
        new AnalysisCodeSeed(
            "WATER_GEN",
            "Su Kalitesi Genel Analiz Paketi",
            "Fiziksel ve temel kimyasal su kalitesi parametrelerini belirler.",
            [
                new AnalysisParameterSeed("PH", "pH Değeri", "pH", 6.5m, 8.5m, true, 1),
                new AnalysisParameterSeed("EC", "Elektriksel İletkenlik", "µS/cm", 100m, 2500m, true, 2),
                new AnalysisParameterSeed("DO", "Çözünmüş Oksijen", "mg/L", 4.0m, 12.0m, true, 3),
                new AnalysisParameterSeed("TURB", "Bulanıklık", "NTU", 0m, 5.0m, true, 4),
                new AnalysisParameterSeed("TEMP", "Sıcaklık", "°C", 10.0m, 25.0m, false, 5),
                new AnalysisParameterSeed("TDS", "Toplam Çözünmüş Katı Madde", "mg/L", 50m, 1000m, true, 6)
            ]),

        new AnalysisCodeSeed(
            "HEAVY_METALS",
            "Ağır Metal Analiz Paketi",
            "Numunedeki toksik ve iz ağır metallerin konsantrasyonunu tayin eder.",
            [
                new AnalysisParameterSeed("PB", "Kurşun (Pb)", "mg/L", 0m, 0.010m, true, 1),
                new AnalysisParameterSeed("CD", "Kadmiyum (Cd)", "mg/L", 0m, 0.005m, true, 2),
                new AnalysisParameterSeed("HG", "Cıva (Hg)", "mg/L", 0m, 0.001m, true, 3),
                new AnalysisParameterSeed("AS", "Arsenik (As)", "mg/L", 0m, 0.010m, true, 4),
                new AnalysisParameterSeed("CR", "Toplam Krom (Cr)", "mg/L", 0m, 0.050m, true, 5),
                new AnalysisParameterSeed("CU", "Bakır (Cu)", "mg/L", 0m, 2.0m, true, 6),
                new AnalysisParameterSeed("FE", "Demir (Fe)", "mg/L", 0m, 0.30m, true, 7),
                new AnalysisParameterSeed("ZN", "Çinko (Zn)", "mg/L", 0m, 3.0m, false, 8),
                new AnalysisParameterSeed("NI", "Nikel (Ni)", "mg/L", 0m, 0.070m, false, 9)
            ]),

        new AnalysisCodeSeed(
            "MICROBIO",
            "Mikrobiyolojik Analiz Paketi",
            "Patojen ve indikatör mikroorganizma varlığını inceler.",
            [
                new AnalysisParameterSeed("TCOLI", "Toplam Koliform", "EMS/100 mL", 0m, 0m, true, 1),
                new AnalysisParameterSeed("ECOLI", "Escherichia coli (E. coli)", "CFU/100 mL", 0m, 0m, true, 2),
                new AnalysisParameterSeed("FECAL", "Fekal Koliform", "CFU/100 mL", 0m, 0m, true, 3),
                new AnalysisParameterSeed("ENTERO", "Enterokok (Fekal Streptokok)", "CFU/100 mL", 0m, 0m, false, 4),
                new AnalysisParameterSeed("SALM", "Salmonella Tayini", "CFU/250 mL", 0m, 0m, false, 5)
            ]),

        new AnalysisCodeSeed(
            "WASTEWATER_CHEM",
            "Atık Su Karakterizasyon Paketi",
            "Endüstriyel ve evsel atık su kirlilik yükü parametrelerini belirler.",
            [
                new AnalysisParameterSeed("COD", "Kimyasal Oksijen İhtiyacı (KOİ)", "mg/L", 0m, 250m, true, 1),
                new AnalysisParameterSeed("BOD5", "Biyokimyasal Oksijen İhtiyacı (BOİ5)", "mg/L", 0m, 100m, true, 2),
                new AnalysisParameterSeed("TSS", "Askıda Katı Madde (AKM)", "mg/L", 0m, 200m, true, 3),
                new AnalysisParameterSeed("TN", "Toplam Azot (TN)", "mg/L", 0m, 30m, true, 4),
                new AnalysisParameterSeed("TP", "Toplam Fosfor (TP)", "mg/L", 0m, 5.0m, true, 5),
                new AnalysisParameterSeed("OIL_GREASE", "Yağ ve Gres", "mg/L", 0m, 20m, false, 6),
                new AnalysisParameterSeed("SURF", "Anyonik Yüzey Aktif Madde", "mg/L", 0m, 5.0m, false, 7)
            ]),

        new AnalysisCodeSeed(
            "ION_MINERAL",
            "Mineral ve İyon Denge Paketi",
            "Majör anyon ve katyon dağılımı ile suyun sertliğini tayin eder.",
            [
                new AnalysisParameterSeed("CA", "Kalsiyum (Ca)", "mg/L", 20m, 200m, true, 1),
                new AnalysisParameterSeed("MG", "Magnezyum (Mg)", "mg/L", 10m, 100m, true, 2),
                new AnalysisParameterSeed("HARD", "Toplam Sertlik (CaCO3)", "mg/L", 50m, 400m, true, 3),
                new AnalysisParameterSeed("CL", "Klorür (Cl)", "mg/L", 0m, 250m, true, 4),
                new AnalysisParameterSeed("SO4", "Sülfat (SO4)", "mg/L", 0m, 250m, true, 5),
                new AnalysisParameterSeed("NO3", "Nitrat (NO3)", "mg/L", 0m, 50m, true, 6),
                new AnalysisParameterSeed("NO2", "Nitrit (NO2)", "mg/L", 0m, 0.50m, false, 7),
                new AnalysisParameterSeed("F", "Florür (F)", "mg/L", 0m, 1.5m, false, 8)
            ]),

        new AnalysisCodeSeed(
            "SOIL_CHEM",
            "Toprak ve Çamur Analiz Paketi",
            "Toprak, sediment ve arıtma çamuru kimyasal özelliklerini inceler.",
            [
                new AnalysisParameterSeed("SOIL_PH", "Toprak pH (1:2.5 H2O)", "pH", 6.0m, 8.0m, true, 1),
                new AnalysisParameterSeed("ORG_MATTER", "Organik Madde Miktarı", "%", 1.5m, 6.0m, true, 2),
                new AnalysisParameterSeed("MOISTURE", "Nem Oranı", "%", 5.0m, 35.0m, true, 3),
                new AnalysisParameterSeed("CEC", "Katyon Değişim Kapasitesi", "meq/100g", 15.0m, 40.0m, false, 4),
                new AnalysisParameterSeed("TOT_SULFUR", "Toplam Kükürt", "mg/kg", 0m, 500m, false, 5)
            ]),

        new AnalysisCodeSeed(
            "VOC_TOX",
            "Uçucu Organik Bileşikler (VOC)",
            "Numunedeki çözücü ve uçucu aromatik hidrokarbon kalıntılarını inceler.",
            [
                new AnalysisParameterSeed("BENZ", "Benzen", "µg/L", 0m, 1.0m, true, 1),
                new AnalysisParameterSeed("TOL", "Toluen", "µg/L", 0m, 700m, true, 2),
                new AnalysisParameterSeed("XYL", "Ksilen (Toplam)", "µg/L", 0m, 500m, true, 3),
                new AnalysisParameterSeed("TCE", "Trikloretilen (TCE)", "µg/L", 0m, 10.0m, true, 4)
            ]),

        // Legacy codes preserved for backwards compatibility
        new AnalysisCodeSeed("PH", "pH Analizi", "Numunenin asitlik ve bazlık düzeyini belirler.",
            [new AnalysisParameterSeed("PH", "pH", "pH", 6.5m, 8.5m)]),
        new AnalysisCodeSeed("COND", "İletkenlik Analizi", "Elektriksel iletkenlik değerini belirler.",
            [new AnalysisParameterSeed("EC", "İletkenlik", "µS/cm", 0m, 2500m)]),
        new AnalysisCodeSeed("COD", "Kimyasal Oksijen İhtiyacı", "Oksitlenebilir madde miktarını belirler.",
            [new AnalysisParameterSeed("COD", "Kimyasal oksijen ihtiyacı", "mg/L", 0m, 250m)]),
        new AnalysisCodeSeed("BOD5", "Biyokimyasal Oksijen İhtiyacı", "Beş günlük oksijen tüketimini belirler.",
            [new AnalysisParameterSeed("BOD5", "Biyokimyasal oksijen ihtiyacı", "mg/L", 0m, 100m)]),
        new AnalysisCodeSeed("TSS", "Askıda Katı Madde", "Askıda bulunan katı madde miktarını belirler.",
            [new AnalysisParameterSeed("TSS", "Askıda katı madde", "mg/L", 0m, 200m)]),
        new AnalysisCodeSeed("IRON", "Demir Analizi", "Toplam demir miktarını belirler.",
            [new AnalysisParameterSeed("FE", "Demir", "mg/L", 0m, 0.3m)]),
        new AnalysisCodeSeed("COPPER", "Bakır Analizi", "Toplam bakır miktarını belirler.",
            [new AnalysisParameterSeed("CU", "Bakır", "mg/L", 0m, 2m)]),
        new AnalysisCodeSeed("LEAD", "Kurşun Analizi", "Toplam kurşun miktarını belirler.",
            [new AnalysisParameterSeed("PB", "Kurşun", "mg/L", 0m, 0.01m)]),
        new AnalysisCodeSeed("NITRATE", "Nitrat Analizi", "Nitrat konsantrasyonunu belirler.",
            [new AnalysisParameterSeed("NO3", "Nitrat", "mg/L", 0m, 50m)]),
        new AnalysisCodeSeed("PHOSPHATE", "Fosfat Analizi", "Fosfat konsantrasyonunu belirler.",
            [new AnalysisParameterSeed("PO4", "Fosfat", "mg/L", 0m, 5m)])
    ];

    private static async Task<int> SeedSamplesAsync(
        SampleAnalysisTrackingDbContext db,
        IReadOnlyDictionary<string, AppUser> users,
        IReadOnlyDictionary<string, Location> locations,
        IReadOnlyDictionary<string, AnalysisCode> analysisCodes,
        CancellationToken cancellationToken)
    {
        var existingCodes = await db.Samples
            .Where(sample => sample.SampleCode.StartsWith("SMP-DEMO-"))
            .Select(sample => sample.SampleCode)
            .ToHashSetAsync(cancellationToken);

        var fieldUsers = new[]
        {
            users["field.emre"],
            users["field.zeynep"],
            users["field.burak"],
            users["field.selin"]
        };

        var laboratoryUsers = new[]
        {
            users["lab.mehmet"],
            users["lab.elif"],
            users["lab.merve"],
            users["lab.can"],
            users["lab.deniz"],
            users["lab.hakan"],
            users["lab.gamze"],
            users["lab.tolga"]
        };

        var locationValues = locations.Values
            .OrderBy(location => location.Name)
            .ToArray();

        var analysisCodeValues = analysisCodes.Values
            .OrderBy(analysisCode => analysisCode.Code)
            .ToArray();

        var sampleTypes = new[]
        {
            "Su",
            "Atık Su",
            "Toprak",
            "Çamur",
            "Hammadde",
            "Yüzey Suyu",
            "İçme Suyu",
            "Kuyu Suyu",
            "Karasal Sediment",
            "Endüstriyel Deşarj Suyu"
        };

        var seedNow = DateTimeOffset.UtcNow;
        var addedSampleCount = 0;
        var pendingSamples = new List<Sample>(BatchSize);

        for (var index = 1; index <= TotalSeedSampleCount; index++)
        {
            var sampleCode = $"SMP-DEMO-{index:000}";

            if (existingCodes.Contains(sampleCode))
            {
                continue;
            }

            // Realistic status distribution:
            // ~50% Completed, ~20% Analyzing, ~10% Received, ~10% Transferred, ~5% Collected, ~5% Created
            var status = DetermineSampleStatus(index);

            var createdBy = fieldUsers[(index - 1) % fieldUsers.Length];
            var assignedTo = laboratoryUsers[(index - 1) % laboratoryUsers.Length];

            // Spread createdAt over the past 180 days
            var dayOffset = 180 - (index * 180 / TotalSeedSampleCount);
            var hourOffset = (index * 7) % 24;
            var minuteOffset = (index * 17) % 60;
            var createdAt = seedNow
                .AddDays(-dayOffset)
                .AddHours(-hourOffset)
                .AddMinutes(-minuteOffset);

            var sampleType = sampleTypes[(index - 1) % sampleTypes.Length];
            var location = locationValues[(index - 1) % locationValues.Length];

            var sample = new Sample
            {
                SampleCode = sampleCode,
                SampleType = sampleType,
                Location = location,
                Status = status,
                Description = $"Geliştirme ortamı için oluşturulan {sampleCode} ({sampleType}) numunesi. {location.Name} kaynaklı.",
                CreatedBy = createdBy,
                AssignedTo = status == SampleStatus.Created
                    ? null
                    : assignedTo,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            AddHistory(sample, status, createdBy, assignedTo, createdAt);
            AddAnalyses(
                sample,
                status,
                users["admin"],
                assignedTo,
                analysisCodeValues,
                index,
                createdAt);

            pendingSamples.Add(sample);
            existingCodes.Add(sampleCode);
            addedSampleCount++;

            if (pendingSamples.Count >= BatchSize)
            {
                db.Samples.AddRange(pendingSamples);
                await db.SaveChangesAsync(cancellationToken);
                pendingSamples.Clear();
            }
        }

        if (pendingSamples.Count > 0)
        {
            db.Samples.AddRange(pendingSamples);
            await db.SaveChangesAsync(cancellationToken);
            pendingSamples.Clear();
        }

        return addedSampleCount;
    }

    private static SampleStatus DetermineSampleStatus(int index)
    {
        var mod = index % 20;
        return mod switch
        {
            < 10 => SampleStatus.Completed,   // 50%
            < 14 => SampleStatus.Analyzing,   // 20%
            < 16 => SampleStatus.Received,    // 10%
            < 18 => SampleStatus.Transferred, // 10%
            18 => SampleStatus.Collected,     // 5%
            _ => SampleStatus.Created         // 5%
        };
    }

    private static void AddHistory(
        Sample sample,
        SampleStatus currentStatus,
        AppUser createdBy,
        AppUser assignedTo,
        DateTimeOffset createdAt)
    {
        for (var index = 0; index <= (int)currentStatus - 1; index++)
        {
            var newStatus = Workflow[index];
            var changedAt = createdAt.AddHours(index * 6 + (index % 4));

            sample.History.Add(new SampleHistory
            {
                OldStatus = index == 0
                    ? null
                    : Workflow[index - 1],
                NewStatus = newStatus,
                ChangedBy = index < 3
                    ? createdBy
                    : assignedTo,
                ChangedAt = changedAt,
                Note = GetHistoryNote(newStatus)
            });

            sample.UpdatedAt = changedAt;
        }
    }

    private static void AddAnalyses(
        Sample sample,
        SampleStatus sampleStatus,
        AppUser requestedBy,
        AppUser assignedLaboratoryUser,
        IReadOnlyList<AnalysisCode> analysisCodes,
        int sampleIndex,
        DateTimeOffset createdAt)
    {
        if (sampleStatus < SampleStatus.Received)
        {
            return;
        }

        var analysisCount = 2 + (sampleIndex % 3);

        for (var index = 0; index < analysisCount; index++)
        {
            var isCancelled = (sampleIndex + index) % 23 == 0; // occasional cancelled analysis
            var analysisStatus = sampleStatus switch
            {
                SampleStatus.Completed => isCancelled ? AnalysisStatus.Cancelled : AnalysisStatus.Completed,
                SampleStatus.Analyzing => (index == 0 ? AnalysisStatus.InProgress : (index == 1 ? AnalysisStatus.Completed : AnalysisStatus.Requested)),
                _ => AnalysisStatus.Requested
            };

            var requestedAt = createdAt.AddHours(12 + index);
            var analysisCode = analysisCodes[(sampleIndex * 3 + index) % analysisCodes.Count];
            var startedAt = analysisStatus is AnalysisStatus.InProgress or AnalysisStatus.Completed
                ? requestedAt.AddHours(1 + ((sampleIndex + index) % 4))
                : (DateTimeOffset?)null;

            DateTimeOffset? completedAt = analysisStatus == AnalysisStatus.Completed
                ? startedAt!.Value.AddHours(2 + ((sampleIndex * 5 + index) % 18))
                : null;

            var analysis = new SampleAnalysis
            {
                AnalysisCode = analysisCode,
                Status = analysisStatus,
                ResultNote = null,
                RequestedBy = requestedBy,
                RequestedAt = requestedAt,
                StartedBy = startedAt.HasValue ? assignedLaboratoryUser : null,
                StartedAt = startedAt,
                CompletedBy = analysisStatus == AnalysisStatus.Completed
                    ? assignedLaboratoryUser
                    : null,
                CompletedAt = completedAt
            };

            AddSeedAnalysisHistory(analysis, requestedBy, assignedLaboratoryUser, isCancelled);

            if (analysisStatus != AnalysisStatus.Cancelled)
            {
                AddSeedResults(analysis, analysisCode, assignedLaboratoryUser, sampleIndex + index, completedAt ?? startedAt);
            }

            if (analysisStatus == AnalysisStatus.Completed)
            {
                var hasOutside = analysis.Results.Any(r =>
                    (r.ReferenceMin.HasValue && r.NumericValue < r.ReferenceMin.Value) ||
                    (r.ReferenceMax.HasValue && r.NumericValue > r.ReferenceMax.Value));

                analysis.ResultNote = hasOutside
                    ? $"{analysisCode.Name} tamamlandı. Bazı parametrelerde referans sınır sapması gözlendi; parametre notlarını inceleyin."
                    : $"{analysisCode.Name} başarıyla tamamlandı. Ölçüm değerleri tanımlı referans aralıklarındadır.";
            }
            else if (analysisStatus == AnalysisStatus.Cancelled)
            {
                analysis.ResultNote = "Numune miktarı yetersizliği veya matris çakışması nedeniyle analiz iptal edildi.";
            }

            sample.Analyses.Add(analysis);

            if (completedAt.HasValue && completedAt.Value > sample.UpdatedAt)
            {
                sample.UpdatedAt = completedAt.Value;
            }
        }
    }

    private static void AddSeedAnalysisHistory(
        SampleAnalysis analysis,
        AppUser requestedBy,
        AppUser laboratoryWorker,
        bool isCancelled = false)
    {
        analysis.History.Add(new SampleAnalysisHistory
        {
            OldStatus = null,
            NewStatus = AnalysisStatus.Requested,
            ChangedBy = requestedBy,
            ChangedAt = analysis.RequestedAt,
            Note = "Analiz numuneye atandı."
        });

        if (analysis.Status is AnalysisStatus.InProgress or AnalysisStatus.Completed)
        {
            analysis.History.Add(new SampleAnalysisHistory
            {
                OldStatus = AnalysisStatus.Requested,
                NewStatus = AnalysisStatus.InProgress,
                ChangedBy = laboratoryWorker,
                ChangedAt = analysis.StartedAt!.Value,
                Note = "Analiz başlatıldı."
            });
        }

        if (analysis.Status == AnalysisStatus.Completed)
        {
            analysis.History.Add(new SampleAnalysisHistory
            {
                OldStatus = AnalysisStatus.InProgress,
                NewStatus = AnalysisStatus.Completed,
                ChangedBy = laboratoryWorker,
                ChangedAt = analysis.CompletedAt!.Value,
                Note = "Analiz tamamlandı."
            });
        }
        else if (isCancelled)
        {
            analysis.History.Add(new SampleAnalysisHistory
            {
                OldStatus = AnalysisStatus.Requested,
                NewStatus = AnalysisStatus.Cancelled,
                ChangedBy = laboratoryWorker,
                ChangedAt = analysis.RequestedAt.AddHours(3),
                Note = "Numune matris problemi nedeniyle analiz iptal edildi."
            });
        }
    }

    private static void AddSeedResults(
        SampleAnalysis analysis,
        AnalysisCode analysisCode,
        AppUser enteredBy,
        int seedFactor,
        DateTimeOffset? measuredAt)
    {
        if (!measuredAt.HasValue || analysis.Status == AnalysisStatus.Requested)
        {
            return;
        }

        AddSeedResultsForParameters(
            analysis,
            analysisCode.Parameters.Where(item => item.IsActive).ToList(),
            enteredBy,
            seedFactor,
            measuredAt.Value);
    }

    private static void AddSeedResultsForParameters(
        SampleAnalysis analysis,
        IReadOnlyList<AnalysisParameter> parameters,
        AppUser enteredBy,
        int seedFactor,
        DateTimeOffset measuredAt)
    {
        foreach (var parameter in parameters)
        {
            var (value, note) = GenerateSeedResult(parameter, seedFactor);

            analysis.Results.Add(new AnalysisResult
            {
                AnalysisParameter = parameter,
                NumericValue = value,
                Unit = parameter.DefaultUnit,
                ReferenceMin = parameter.ReferenceMin,
                ReferenceMax = parameter.ReferenceMax,
                ResultNote = note,
                MeasuredAt = measuredAt,
                EnteredBy = enteredBy
            });
        }
    }

    private static (decimal Value, string Note) GenerateSeedResult(
        AnalysisParameter parameter,
        int seedFactor)
    {
        var seed = Math.Abs(seedFactor * 37 + parameter.DisplayOrder * 19 + parameter.Code.GetHashCode() % 100);

        // Special handling for microbiology / zero tolerance parameters
        if (parameter.ReferenceMin == 0m && parameter.ReferenceMax == 0m)
        {
            if (seed % 5 == 0)
            {
                var count = (seed % 28) + 2;
                return (count, "Pozitif koloni üremesi tespit edildi; referans sınırını aştı.");
            }

            return (0m, "0 CFU / 100 mL; patojen veya koloni üremesi saptanmadı.");
        }

        var min = parameter.ReferenceMin ?? 0m;
        var max = parameter.ReferenceMax ?? (min + 100m);
        var range = max - min;
        if (range <= 0) range = 10m;

        var mode = seed % 10;

        switch (mode)
        {
            case 0 when min > 0:
                return (
                    decimal.Round(min * 0.85m, 4),
                    "Referans alt sınırının altında ölçüldü; matris seyreltmesi teyit edildi.");

            case 1:
            case 2:
                return (
                    decimal.Round(max + range * 0.20m, 4),
                    "Referans üst sınırını aştı. Seyreltme (1:10) yapılarak tekrar okundu.");

            case 3:
                return (
                    decimal.Round(max - range * 0.05m, 4),
                    "Referans üst limitine çok yakın seviyede; teyit amaçlı çift ölçüm yapıldı.");

            case 4:
                return (
                    decimal.Round(min + range * 0.35m, 4),
                    "Standart kalibrasyon eğrisi dahilinde ölçüldü; kararlı okuma.");

            case 5:
                return (
                    decimal.Round(min + range * 0.55m, 4),
                    "Ölçüm sıcaklık ve pH düzeltmesi uygulanarak tamamlandı.");

            case 6:
                return (
                    decimal.Round(min + range * 0.70m, 4),
                    "Çift paralel okuma ile doğrulandı; sapma tolerans dahilinde.");

            default:
                var ratio = (seed % 55 + 25) / 100m;
                return (
                    decimal.Round(min + range * ratio, 4),
                    "Ölçüm standart laboratuvar prosedürüne uygun olarak gerçekleştirildi.");
        }
    }

    private static async Task BackfillAnalysisDataAsync(
        SampleAnalysisTrackingDbContext db,
        CancellationToken cancellationToken)
    {
        var analyses = await db.SampleAnalyses
            .AsSplitQuery()
            .Include(item => item.AnalysisCode)
                .ThenInclude(code => code.Parameters)
            .Include(item => item.Results)
            .Include(item => item.History)
            .Include(item => item.RequestedBy)
            .Include(item => item.Sample)
                .ThenInclude(sample => sample.AssignedTo)
            .Where(item => item.Sample.SampleCode.StartsWith("SMP-DEMO-"))
            .ToListAsync(cancellationToken);

        foreach (var analysis in analyses)
        {
            var worker = analysis.Sample.AssignedTo ?? analysis.RequestedBy;
            if (worker is null)
            {
                continue;
            }

            if (analysis.Status is AnalysisStatus.InProgress or AnalysisStatus.Completed)
            {
                analysis.StartedBy ??= worker;
                analysis.StartedAt ??= analysis.RequestedAt.AddHours(2);
            }

            if (analysis.History.Count == 0 && analysis.RequestedBy is not null)
            {
                AddSeedAnalysisHistory(analysis, analysis.RequestedBy, worker);
            }

            var numericSuffix = int.TryParse(
                analysis.Sample.SampleCode.Split('-').LastOrDefault(),
                out var index)
                ? index
                : 1;

            if (analysis.Status is AnalysisStatus.InProgress or AnalysisStatus.Completed)
            {
                var existingParamIds = analysis.Results.Select(r => r.AnalysisParameterId).ToHashSet();
                var missingParameters = analysis.AnalysisCode.Parameters
                    .Where(p => p.IsActive && !existingParamIds.Contains(p.Id))
                    .ToList();

                if (missingParameters.Count > 0)
                {
                    var measuredAt = analysis.CompletedAt ?? analysis.StartedAt ?? analysis.RequestedAt.AddHours(3);
                    AddSeedResultsForParameters(analysis, missingParameters, worker, numericSuffix, measuredAt);
                }

                if (analysis.Status == AnalysisStatus.Completed && string.IsNullOrWhiteSpace(analysis.ResultNote))
                {
                    var hasOutside = analysis.Results.Any(r =>
                        (r.ReferenceMin.HasValue && r.NumericValue < r.ReferenceMin.Value) ||
                        (r.ReferenceMax.HasValue && r.NumericValue > r.ReferenceMax.Value));

                    analysis.ResultNote = hasOutside
                        ? $"{analysis.AnalysisCode.Name} tamamlandı. Bazı parametrelerde referans sınır sapması gözlendi; parametre notlarını inceleyin."
                        : $"{analysis.AnalysisCode.Name} başarıyla tamamlandı. Ölçüm değerleri tanımlı referans aralıklarındadır.";
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string GetHistoryNote(SampleStatus status) =>
        status switch
        {
            SampleStatus.Created => "Numune kaydı oluşturuldu.",
            SampleStatus.Collected => "Numune saha ekibi tarafından toplandı.",
            SampleStatus.Transferred => "Numune laboratuvara transfer edildi.",
            SampleStatus.Received => "Numune laboratuvar tarafından teslim alındı.",
            SampleStatus.Analyzing => "Numune analiz sürecine alındı.",
            SampleStatus.Completed => "Numune işlemleri tamamlandı.",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private sealed record UserSeed(
        string Username,
        string FullName,
        string Email,
        UserRole Role);

    private sealed record LocationSeed(
        string Name,
        string Description);

    private sealed record AnalysisParameterSeed(
        string Code,
        string Name,
        string DefaultUnit,
        decimal? ReferenceMin,
        decimal? ReferenceMax,
        bool IsRequired = true,
        int DisplayOrder = 1);

    private sealed record AnalysisCodeSeed(
        string Code,
        string Name,
        string Description,
        IReadOnlyList<AnalysisParameterSeed> Parameters);

    private sealed record KnowledgeDocumentSeed(
        string Title,
        KnowledgeDocumentCategory Category,
        IReadOnlyList<string> AnalysisCodes,
        string SourceText);
}
