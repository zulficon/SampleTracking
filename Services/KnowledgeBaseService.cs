using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SampleAnalysisTracking.Clients;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;
using SampleAnalysisTracking.Options;

namespace SampleAnalysisTracking.Services;

// RAG (Geri Getirme Destekli Üretim) bilgi tabanı yönetim servisi.
// Dokümanların metin parçalarına (chunk) ayrılması, embedding modeline gönderilerek
// vektörleştirilmesi ve PostgreSQL pgvector üzerinde kosinüs benzerlik araması yapılmasını sağlar.
public sealed class KnowledgeBaseService(
    SampleAnalysisTrackingDbContext db,
    IEmbeddingClient embeddingClient,
    IOptions<RagOptions> ragOptions,
    ILogger<KnowledgeBaseService> logger)
{
    // Yeni bir bilgi dokümanını sisteme kaydeder, parçalara ayırır (chunking) ve her parçayı vektörleştirir.
    public async Task<ServiceResult<KnowledgeDocumentItem>> CreateAsync(
        CreateKnowledgeDocumentRequest request,
        long uploadedById,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var sourceText = request.SourceText.Trim();
        var sourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
            ? null
            : request.SourceReference.Trim();
        var sourceVersion = string.IsNullOrWhiteSpace(request.SourceVersion)
            ? null
            : request.SourceVersion.Trim();

        // Başlık veya metin boş olamaz
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sourceText))
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Document title and text cannot be blank.");
        }

        // Doğrulanmış kurumsal dokümanlar için resmî kaynak referansı zorunludur
        if (request.SourceStatus == KnowledgeSourceStatus.Verified
            && sourceReference is null)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Verified documents require a source reference.");
        }

        // Dokümanı yükleyen kullanıcının geçerliliğini denetle
        var uploadedByUsername = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == uploadedById && user.IsActive)
            .Select(user => user.Username)
            .SingleOrDefaultAsync(cancellationToken);

        if (uploadedByUsername is null)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Forbidden,
                "The current user cannot add knowledge documents.");
        }

        // 1. Chunking: Uzun metni yapılandırmadaki boyut (örn. 1200 karakter) ve örtüşme (overlap) ayarlarına göre parçalara ayır
        var textChunks = SplitIntoChunks(sourceText);
        if (textChunks.Count == 0)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Document text could not be divided into searchable chunks.");
        }

        // İlgili analiz kodlarını kontrol et ve doğrula
        var analysisCodeIds = request.AnalysisCodeIds.Distinct().ToArray();
        var analysisCodes = analysisCodeIds.Length == 0
            ? []
            : await db.AnalysisCodes
                .Where(code => analysisCodeIds.Contains(code.Id))
                .ToListAsync(cancellationToken);

        if (analysisCodes.Count != analysisCodeIds.Length)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "One or more selected analysis codes do not exist.");
        }

        // 2. Embedding: Ayrıştırılan her bir metin parçasını embedding modeline göndererek sayısal vektörlerini üret
        var chunksResult = await CreateEmbeddedChunksAsync(textChunks, cancellationToken);
        if (!chunksResult.Succeeded)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                chunksResult.Error,
                chunksResult.ErrorMessage!);
        }

        var chunks = chunksResult.Value!;

        // 3. Doküman ana kaydını oluştur
        var document = new KnowledgeDocument
        {
            Title = title,
            Category = request.Category,
            SourceStatus = request.SourceStatus,
            SourceReference = sourceReference,
            SourceVersion = sourceVersion,
            SourceText = sourceText,
            UploadedById = uploadedById,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // 4. Vektörleştirilmiş parçacıkları (chunks) dokümana ekle
        foreach (var chunk in chunks)
        {
            document.Chunks.Add(chunk);
        }

        // 5. Dokümanı ilgili analiz kodlarına bağla
        foreach (var analysisCode in analysisCodes)
        {
            document.AnalysisCodeLinks.Add(new KnowledgeDocumentAnalysisCode
            {
                AnalysisCode = analysisCode
            });
        }

        // 6. Dokümanı ve vektörleri veritabanına kaydet (pgvector tablosuna yazılır)
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<KnowledgeDocumentItem>.Success(
            new KnowledgeDocumentItem(
                document.Id,
                document.Title,
                document.Category,
                document.SourceStatus,
                document.SourceReference,
                document.SourceVersion,
                document.IsActive,
                chunks.Count,
                document.CreatedAt,
                uploadedByUsername));
    }

    // Bilgi tabanındaki tüm dokümanları parça sayıları ile birlikte listeler
    public async Task<IReadOnlyList<KnowledgeDocumentItem>> GetAllAsync(
        CancellationToken cancellationToken) =>
        await db.KnowledgeDocuments
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new KnowledgeDocumentItem(
                document.Id,
                document.Title,
                document.Category,
                document.SourceStatus,
                document.SourceReference,
                document.SourceVersion,
                document.IsActive,
                document.Chunks.Count,
                document.CreatedAt,
                document.UploadedBy.Username))
            .ToListAsync(cancellationToken);

    // Belirtilen kimlikteki bilgi dokümanının tüm detaylarını getirir
    public async Task<ServiceResult<KnowledgeDocumentDetail>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var detail = await db.KnowledgeDocuments
            .AsNoTracking()
            .Where(document => document.Id == id)
            .Select(document => new KnowledgeDocumentDetail(
                document.Id,
                document.Title,
                document.Category,
                document.SourceStatus,
                document.SourceReference,
                document.SourceVersion,
                document.SourceText,
                document.IsActive,
                document.Chunks.Count,
                document.CreatedAt,
                document.UploadedBy.Username,
                document.AnalysisCodeLinks
                    .OrderBy(link => link.AnalysisCode.Code)
                    .Select(link => new KnowledgeDocumentAnalysisCodeItem(
                        link.AnalysisCodeId,
                        link.AnalysisCode.Code,
                        link.AnalysisCode.Name))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return detail is null
            ? ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.NotFound,
                "Knowledge document was not found.")
            : ServiceResult<KnowledgeDocumentDetail>.Success(detail);
    }

    // Taslak durumdaki bir bilgi dokümanını günceller, eski parçalarını siler ve yeni metin için vektörleri baştan üretir
    public async Task<ServiceResult<KnowledgeDocumentDetail>> UpdateDraftAsync(
        long id,
        UpdateKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var document = await db.KnowledgeDocuments
            .Include(item => item.Chunks)
            .Include(item => item.AnalysisCodeLinks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (document is null)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.NotFound,
                "Knowledge document was not found.");
        }

        // Sadece taslak (Draft) belgelerin içeriği ve vektörleri değiştirilebilir
        if (document.SourceStatus != KnowledgeSourceStatus.Draft)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.Conflict,
                "Only draft knowledge documents can be edited.");
        }

        var title = request.Title.Trim();
        var sourceText = request.SourceText.Trim();
        var sourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
            ? null
            : request.SourceReference.Trim();
        var sourceVersion = string.IsNullOrWhiteSpace(request.SourceVersion)
            ? null
            : request.SourceVersion.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sourceText))
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.Validation,
                "Document title and text cannot be blank.");
        }

        var analysisCodeIds = request.AnalysisCodeIds.Distinct().ToArray();
        var existingAnalysisCodeCount = analysisCodeIds.Length == 0
            ? 0
            : await db.AnalysisCodes.CountAsync(
                code => analysisCodeIds.Contains(code.Id),
                cancellationToken);

        if (existingAnalysisCodeCount != analysisCodeIds.Length)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.Validation,
                "One or more selected analysis codes do not exist.");
        }

        // Yeni metni parçalara (chunks) ayır
        var textChunks = SplitIntoChunks(sourceText);
        if (textChunks.Count == 0)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.Validation,
                "Document text could not be divided into searchable chunks.");
        }

        // Yeni metin parçaları için embedding vektörlerini baştan oluştur
        var chunksResult = await CreateEmbeddedChunksAsync(textChunks, cancellationToken);
        if (!chunksResult.Succeeded)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                chunksResult.Error,
                chunksResult.ErrorMessage!);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        document.Title = title;
        document.Category = request.Category;
        document.SourceReference = sourceReference;
        document.SourceVersion = sourceVersion;
        document.SourceText = sourceText;

        // Eski chunk'ları ve analiz bağlantılarını temizle
        db.KnowledgeChunks.RemoveRange(document.Chunks);
        db.KnowledgeDocumentAnalysisCodes.RemoveRange(document.AnalysisCodeLinks);
        await db.SaveChangesAsync(cancellationToken);

        // Yeni üretilen vektörlü parçaları ekle
        foreach (var chunk in chunksResult.Value!)
        {
            chunk.KnowledgeDocumentId = document.Id;
            db.KnowledgeChunks.Add(chunk);
        }

        foreach (var analysisCodeId in analysisCodeIds)
        {
            db.KnowledgeDocumentAnalysisCodes.Add(new KnowledgeDocumentAnalysisCode
            {
                KnowledgeDocumentId = document.Id,
                AnalysisCodeId = analysisCodeId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetByIdAsync(document.Id, cancellationToken);
    }

    // Arama isteği nesnesi ile serbest metin araması yapar
    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        SearchKnowledgeRequest request,
        CancellationToken cancellationToken) =>
        await SearchAsync(request.Query, cancellationToken);

    // Yalnızca arama metni ile bilgi tabanında semantik vektör araması yapar
    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
        => await SearchAsync(query, [], cancellationToken);

    // RAG Semantik Arama Motoru:
    // 1. Arama sorgusunu embedding modeline ileterek anlamsal vektöre dönüştürür.
    // 2. PostgreSQL pgvector eklentisinin CosineDistance fonksiyonu ile parçaların kosinüs benzerliğini hesaplar.
    // 3. Analiz kodu eşleşmesine ve benzerlik skoruna göre adayları sıralar ve en alakalı dokümanları döndürür.
    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        string query,
        IReadOnlyCollection<long> preferredAnalysisCodeIds,
        CancellationToken cancellationToken)
    {
        // Arama metni boş olamaz
        if (string.IsNullOrWhiteSpace(query))
        {
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Failure(
                ServiceError.Validation,
                "Search text cannot be blank.");
        }

        // Bilgi tabanında aranabilir aktif doküman olup olmadığını kontrol et
        var hasSearchableKnowledge = await db.KnowledgeChunks
            .AsNoTracking()
            .AnyAsync(chunk => chunk.KnowledgeDocument.IsActive, cancellationToken);

        if (!hasSearchableKnowledge)
        {
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Success([]);
        }

        var requestedAnalysisCodeIds = preferredAnalysisCodeIds.Distinct().ToArray();

        // Arama sorgusunun anlamsal temsilini taşıyan embedding vektörünü oluştur
        Vector queryEmbedding;
        try
        {
            queryEmbedding = new Vector(await embeddingClient.CreateEmbeddingAsync(
                query.Trim(),
                cancellationToken));
        }
        catch (OllamaClientException exception)
        {
            logger.LogWarning(exception, "Knowledge search embedding could not be created.");
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Failure(
                ServiceError.ExternalService,
                "The local embedding model could not process this search.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Ollama could not be reached while searching knowledge.");
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Failure(
                ServiceError.ExternalService,
                "The local Ollama service could not be reached.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Ollama timed out while searching knowledge.");
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Failure(
                ServiceError.ExternalService,
                "The local embedding model timed out.");
        }

        // Vektör uzayında taranacak aday parça sayısı sınırı
        var candidateLimit = Math.Max(ragOptions.Value.SearchResultLimit * 8, 24);

        // pgvector Vektör Benzerliği Sorgusu:
        // Cosine Similarity = 1 - Cosine Distance formülü ile 0.0 (en uzak) ile 1.0 (tam eşleşme) arası benzerlik hesaplanır.
        var candidates = await db.KnowledgeChunks
            .AsNoTracking()
            .Where(chunk => chunk.KnowledgeDocument.IsActive)
            .Select(chunk => new
            {
                chunk.KnowledgeDocumentId,
                DocumentTitle = chunk.KnowledgeDocument.Title,
                Category = chunk.KnowledgeDocument.Category,
                SourceStatus = chunk.KnowledgeDocument.SourceStatus,
                SourceReference = chunk.KnowledgeDocument.SourceReference,
                chunk.ChunkIndex,
                chunk.Content,
                // Kosinüs benzerliği: 1 - CosineDistance(v1, v2)
                Similarity = 1 - chunk.Embedding.CosineDistance(queryEmbedding),
                // Numunenin analiz kodları ile bu dokümanın doğrudan etiketlenip etiketlenmediği
                IsAnalysisCodeMatch = requestedAnalysisCodeIds.Length > 0
                    && chunk.KnowledgeDocument.AnalysisCodeLinks.Any(link =>
                        requestedAnalysisCodeIds.Contains(link.AnalysisCodeId))
            })
            // Öncelik 1: İlgili analiz koduna bağlı dokümanlar
            .OrderByDescending(candidate => candidate.IsAnalysisCodeMatch)
            // Öncelik 2: En yüksek kosinüs benzerliği
            .ThenByDescending(candidate => candidate.Similarity)
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);

        // Aynı dokümandan birden fazla parça gelirse en yüksek benzerliğe sahip tek bir parçayı seç (Distinct doküman)
        var matches = candidates
            .GroupBy(candidate => candidate.KnowledgeDocumentId)
            .Select(group => group.First())
            // Yapılandırmadaki sonuç limiti kadar al (örn. en iyi 4 doküman)
            .Take(ragOptions.Value.SearchResultLimit)
            .Select(candidate => new KnowledgeSearchItem(
                candidate.KnowledgeDocumentId,
                candidate.DocumentTitle,
                candidate.Category,
                candidate.SourceStatus,
                candidate.SourceReference,
                candidate.ChunkIndex,
                candidate.Content,
                candidate.Similarity,
                candidate.IsAnalysisCodeMatch))
            .ToArray();

        return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Success(matches);
    }

    // Metin Parçalama Algoritması (Sliding Window Chunking):
    // Uzun dokümanları LLM ve embedding modellerinin bağlam sınırlarına uygun boyutlara böler.
    // Cümle ve kelime bütünlüğünü korumak için son boşluk karakterini bulur ve parçalar arasında örtüşme (overlap) bırakır.
    private IReadOnlyList<string> SplitIntoChunks(string text)
    {
        var settings = ragOptions.Value;
        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(settings.ChunkSizeCharacters, text.Length - start);
            var end = start + length;

            // Metin sonuna ulaşılmadıysa kelimenin ortasından bölmemek için son boşluk/satır başı karakterini ara
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOfAny([' ', '\n', '\r', '\t'], end - 1, length);
                // Eğer boşluk karakteri parça ortasından sonraysa kesme noktasını o boşluğa çek
                if (lastSpace > start + (settings.ChunkSizeCharacters / 2))
                {
                    end = lastSpace + 1;
                }
            }

            var chunk = text[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (end >= text.Length)
            {
                break;
            }

            // Bir sonraki parçanın başlangıcı: Anlamsal süreklilik için örtüşme miktarı (overlap) kadar geriden başla
            start = Math.Max(start + 1, end - settings.ChunkOverlapCharacters);
        }

        return chunks;
    }

    // Her bir metin parçası için Ollama embedding istemcisini çağırarak pgvector Vector nesnelerini üretir
    private async Task<ServiceResult<List<KnowledgeChunk>>> CreateEmbeddedChunksAsync(
        IReadOnlyList<string> textChunks,
        CancellationToken cancellationToken)
    {
        var chunks = new List<KnowledgeChunk>(textChunks.Count);

        try
        {
            for (var index = 0; index < textChunks.Count; index++)
            {
                // Embedding modeline metin parçasını ilet ve float[] vektörünü al
                var embedding = await embeddingClient.CreateEmbeddingAsync(
                    textChunks[index],
                    cancellationToken);

                // pgvector'ın Vector tipiyle eşleştirip listeye ekle
                chunks.Add(new KnowledgeChunk
                {
                    ChunkIndex = index,
                    Content = textChunks[index],
                    Embedding = new Vector(embedding)
                });
            }

            return ServiceResult<List<KnowledgeChunk>>.Success(chunks);
        }
        catch (OllamaClientException exception)
        {
            logger.LogWarning(exception, "Knowledge document embedding could not be created.");
            return ServiceResult<List<KnowledgeChunk>>.Failure(
                ServiceError.ExternalService,
                "The local embedding model could not process this document.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Ollama could not be reached while embedding a document.");
            return ServiceResult<List<KnowledgeChunk>>.Failure(
                ServiceError.ExternalService,
                "The local Ollama service could not be reached.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Ollama timed out while embedding a document.");
            return ServiceResult<List<KnowledgeChunk>>.Failure(
                ServiceError.ExternalService,
                "The local embedding model timed out.");
        }
    }
}

