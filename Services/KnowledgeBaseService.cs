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

public sealed class KnowledgeBaseService(
    SampleAnalysisTrackingDbContext db,
    IEmbeddingClient embeddingClient,
    IOptions<RagOptions> ragOptions,
    ILogger<KnowledgeBaseService> logger)
{
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

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(sourceText))
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Document title and text cannot be blank.");
        }

        if (request.SourceStatus == KnowledgeSourceStatus.Verified
            && sourceReference is null)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Verified documents require a source reference.");
        }

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

        var textChunks = SplitIntoChunks(sourceText);
        if (textChunks.Count == 0)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                ServiceError.Validation,
                "Document text could not be divided into searchable chunks.");
        }

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

        var chunksResult = await CreateEmbeddedChunksAsync(textChunks, cancellationToken);
        if (!chunksResult.Succeeded)
        {
            return ServiceResult<KnowledgeDocumentItem>.Failure(
                chunksResult.Error,
                chunksResult.ErrorMessage!);
        }

        var chunks = chunksResult.Value!;

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

        foreach (var chunk in chunks)
        {
            document.Chunks.Add(chunk);
        }

        foreach (var analysisCode in analysisCodes)
        {
            document.AnalysisCodeLinks.Add(new KnowledgeDocumentAnalysisCode
            {
                AnalysisCode = analysisCode
            });
        }

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

        var textChunks = SplitIntoChunks(sourceText);
        if (textChunks.Count == 0)
        {
            return ServiceResult<KnowledgeDocumentDetail>.Failure(
                ServiceError.Validation,
                "Document text could not be divided into searchable chunks.");
        }

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

        db.KnowledgeChunks.RemoveRange(document.Chunks);
        db.KnowledgeDocumentAnalysisCodes.RemoveRange(document.AnalysisCodeLinks);
        await db.SaveChangesAsync(cancellationToken);

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

    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        SearchKnowledgeRequest request,
        CancellationToken cancellationToken) =>
        await SearchAsync(request.Query, cancellationToken);

    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
        => await SearchAsync(query, [], cancellationToken);

    public async Task<ServiceResult<IReadOnlyList<KnowledgeSearchItem>>> SearchAsync(
        string query,
        IReadOnlyCollection<long> preferredAnalysisCodeIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Failure(
                ServiceError.Validation,
                "Search text cannot be blank.");
        }

        var hasSearchableKnowledge = await db.KnowledgeChunks
            .AsNoTracking()
            .AnyAsync(chunk => chunk.KnowledgeDocument.IsActive, cancellationToken);

        if (!hasSearchableKnowledge)
        {
            return ServiceResult<IReadOnlyList<KnowledgeSearchItem>>.Success([]);
        }

        var requestedAnalysisCodeIds = preferredAnalysisCodeIds.Distinct().ToArray();

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

        var candidateLimit = Math.Max(ragOptions.Value.SearchResultLimit * 8, 24);
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
                Similarity = 1 - chunk.Embedding.CosineDistance(queryEmbedding),
                IsAnalysisCodeMatch = requestedAnalysisCodeIds.Length > 0
                    && chunk.KnowledgeDocument.AnalysisCodeLinks.Any(link =>
                        requestedAnalysisCodeIds.Contains(link.AnalysisCodeId))
            })
            .OrderByDescending(candidate => candidate.IsAnalysisCodeMatch)
            .ThenByDescending(candidate => candidate.Similarity)
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);

        var matches = candidates
            .GroupBy(candidate => candidate.KnowledgeDocumentId)
            .Select(group => group.First())
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

    private IReadOnlyList<string> SplitIntoChunks(string text)
    {
        var settings = ragOptions.Value;
        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(settings.ChunkSizeCharacters, text.Length - start);
            var end = start + length;

            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOfAny([' ', '\n', '\r', '\t'], end - 1, length);
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

            start = Math.Max(start + 1, end - settings.ChunkOverlapCharacters);
        }

        return chunks;
    }

    private async Task<ServiceResult<List<KnowledgeChunk>>> CreateEmbeddedChunksAsync(
        IReadOnlyList<string> textChunks,
        CancellationToken cancellationToken)
    {
        var chunks = new List<KnowledgeChunk>(textChunks.Count);

        try
        {
            for (var index = 0; index < textChunks.Count; index++)
            {
                var embedding = await embeddingClient.CreateEmbeddingAsync(
                    textChunks[index],
                    cancellationToken);

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
