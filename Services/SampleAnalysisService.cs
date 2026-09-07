using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Services;

public sealed class SampleAnalysisService(SampleAnalysisTrackingDbContext db)
{
    public async Task<ServiceResult<IReadOnlyList<SampleAnalysisListItem>>> GetForSampleAsync(
        long sampleId,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var sample = await db.Samples
            .AsNoTracking()
            .Where(item => item.Id == sampleId)
            .Select(item => new { item.Id, item.AssignedToId })
            .SingleOrDefaultAsync(cancellationToken);

        if (sample is null)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.NotFound,
                "The sample was not found.");
        }

        if (!isAdmin && sample.AssignedToId != currentUserId)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Forbidden,
                "This sample is not assigned to the current laboratory worker.");
        }

        var items = await db.SampleAnalyses
            .AsNoTracking()
            .Where(item => item.SampleId == sampleId)
            .OrderByDescending(item => item.RequestedAt)
            .Select(item => new SampleAnalysisListItem(
                item.Id,
                item.SampleId,
                item.AnalysisCodeId,
                item.AnalysisCode.Code,
                item.AnalysisCode.Name,
                item.Status,
                item.RequestedBy == null ? null : item.RequestedBy.Username,
                item.RequestedAt,
                item.StartedBy == null ? null : item.StartedBy.Username,
                item.StartedAt,
                item.CompletedBy == null ? null : item.CompletedBy.Username,
                item.CompletedAt))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Success(items);
    }

    public async Task<ServiceResult<SampleAnalysisDetail>> GetByIdAsync(
        long id,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var analysis = await LoadDetailEntityAsync(id, cancellationToken);
        if (analysis is null)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.NotFound,
                "The sample analysis was not found.");
        }

        if (!CanAccess(analysis.Sample, currentUserId, isAdmin))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Forbidden,
                "This analysis is not assigned to the current laboratory worker.");
        }

        return ServiceResult<SampleAnalysisDetail>.Success(ToDetail(analysis));
    }

    public async Task<ServiceResult<IReadOnlyList<SampleAnalysisListItem>>> AssignAsync(
        long sampleId,
        AssignSampleAnalysesRequest request,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var requestedIds = request.AnalysisCodeIds.Distinct().ToArray();
        if (requestedIds.Length == 0 || requestedIds.Length != request.AnalysisCodeIds.Count)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Validation,
                "Select at least one analysis code and do not repeat selections.");
        }

        var sample = await db.Samples
            .Include(item => item.Analyses)
            .SingleOrDefaultAsync(item => item.Id == sampleId, cancellationToken);

        if (sample is null)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.NotFound,
                "The sample was not found.");
        }

        if (!CanAccess(sample, currentUserId, isAdmin))
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Forbidden,
                "This sample is not assigned to the current laboratory worker.");
        }

        if (sample.Status == SampleStatus.Completed)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Validation,
                "A completed sample cannot receive a new analysis.");
        }

        var existingIds = sample.Analyses.Select(item => item.AnalysisCodeId).ToHashSet();
        if (requestedIds.Any(existingIds.Contains))
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Conflict,
                "One or more selected analyses are already assigned to this sample.");
        }

        var analysisCodes = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .Where(item => requestedIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);

        if (analysisCodes.Count != requestedIds.Length)
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Validation,
                "One or more selected analysis codes do not exist or are inactive.");
        }

        if (analysisCodes.Any(item => !item.Parameters.Any(parameter => parameter.IsActive)))
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Validation,
                "Every assigned analysis code must have at least one active parameter.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var analysisCode in analysisCodes)
        {
            var analysis = new SampleAnalysis
            {
                Sample = sample,
                AnalysisCode = analysisCode,
                Status = AnalysisStatus.Requested,
                RequestedById = currentUserId,
                RequestedAt = now
            };

            analysis.History.Add(new SampleAnalysisHistory
            {
                OldStatus = null,
                NewStatus = AnalysisStatus.Requested,
                ChangedById = currentUserId,
                ChangedAt = now,
                Note = "Analiz numuneye atandı."
            });

            db.SampleAnalyses.Add(analysis);
        }

        sample.UpdatedAt = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueViolation())
        {
            return ServiceResult<IReadOnlyList<SampleAnalysisListItem>>.Failure(
                ServiceError.Conflict,
                "One or more selected analyses are already assigned to this sample.");
        }

        return await GetForSampleAsync(sampleId, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<ServiceResult<SampleAnalysisDetail>> StartAsync(
        long id,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var analysis = await db.SampleAnalyses
            .Include(item => item.Sample)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysis is null)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.NotFound,
                "The sample analysis was not found.");
        }

        if (!CanAccess(analysis.Sample, currentUserId, isAdmin))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Forbidden,
                "This analysis is not assigned to the current laboratory worker.");
        }

        if (analysis.Status != AnalysisStatus.Requested)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Conflict,
                "Only a requested analysis can be started.");
        }

        if (analysis.Sample.Status is not (SampleStatus.Received or SampleStatus.Analyzing))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Validation,
                "The sample must be received by the laboratory before analysis can start.");
        }

        var now = DateTimeOffset.UtcNow;
        analysis.Status = AnalysisStatus.InProgress;
        analysis.StartedById = currentUserId;
        analysis.StartedAt = now;
        AddAnalysisHistory(analysis, AnalysisStatus.Requested, AnalysisStatus.InProgress, currentUserId, now, "Analiz başlatıldı.");

        if (analysis.Sample.Status == SampleStatus.Received)
        {
            MoveSampleToStatus(analysis.Sample, SampleStatus.Analyzing, currentUserId, now, "İlk analiz başlatıldı.");
        }

        analysis.Sample.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return await ReloadDetailAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<ServiceResult<SampleAnalysisDetail>> SaveResultsAsync(
        long id,
        SaveAnalysisResultsRequest request,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var requestedParameterIds = request.Results.Select(item => item.AnalysisParameterId).ToArray();
        if (requestedParameterIds.Length == 0 || requestedParameterIds.Distinct().Count() != requestedParameterIds.Length)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Validation,
                "Provide at least one result and do not repeat parameters.");
        }

        var analysis = await db.SampleAnalyses
            .AsSplitQuery()
            .Include(item => item.Sample)
            .Include(item => item.AnalysisCode)
                .ThenInclude(code => code.Parameters)
            .Include(item => item.Results)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysis is null)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.NotFound,
                "The sample analysis was not found.");
        }

        if (!CanAccess(analysis.Sample, currentUserId, isAdmin))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Forbidden,
                "This analysis is not assigned to the current laboratory worker.");
        }

        if (analysis.Status != AnalysisStatus.InProgress)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Conflict,
                "Results can only be entered for an analysis in progress.");
        }

        var parameters = analysis.AnalysisCode.Parameters
            .Where(item => item.IsActive && requestedParameterIds.Contains(item.Id))
            .ToDictionary(item => item.Id);

        if (parameters.Count != requestedParameterIds.Length)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Validation,
                "One or more parameters do not belong to this analysis or are inactive.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var input in request.Results)
        {
            var parameter = parameters[input.AnalysisParameterId];
            var result = analysis.Results.SingleOrDefault(item => item.AnalysisParameterId == parameter.Id);

            if (result is null)
            {
                result = new AnalysisResult
                {
                    AnalysisParameterId = parameter.Id,
                    Unit = parameter.DefaultUnit,
                    ReferenceMin = parameter.ReferenceMin,
                    ReferenceMax = parameter.ReferenceMax
                };
                analysis.Results.Add(result);
            }

            result.NumericValue = input.NumericValue;
            result.ResultNote = NormalizeOptional(input.ResultNote);
            result.MeasuredAt = now;
            result.EnteredById = currentUserId;
        }

        analysis.Sample.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return await ReloadDetailAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<ServiceResult<SampleAnalysisDetail>> CompleteAsync(
        long id,
        CompleteSampleAnalysisRequest request,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var analysis = await db.SampleAnalyses
            .AsSplitQuery()
            .Include(item => item.Sample)
                .ThenInclude(sample => sample.Analyses)
            .Include(item => item.AnalysisCode)
                .ThenInclude(code => code.Parameters)
            .Include(item => item.Results)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysis is null)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.NotFound,
                "The sample analysis was not found.");
        }

        if (!CanAccess(analysis.Sample, currentUserId, isAdmin))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Forbidden,
                "This analysis is not assigned to the current laboratory worker.");
        }

        if (analysis.Status != AnalysisStatus.InProgress)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Conflict,
                "Only an analysis in progress can be completed.");
        }

        var resultParameterIds = analysis.Results.Select(item => item.AnalysisParameterId).ToHashSet();
        var missingRequired = analysis.AnalysisCode.Parameters
            .Where(item => item.IsActive && item.IsRequired && !resultParameterIds.Contains(item.Id))
            .Select(item => item.Name)
            .ToArray();

        if (missingRequired.Length > 0)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Validation,
                $"Required results are missing: {string.Join(", ", missingRequired)}.");
        }

        var now = DateTimeOffset.UtcNow;
        analysis.Status = AnalysisStatus.Completed;
        analysis.ResultNote = NormalizeOptional(request.ResultNote);
        analysis.CompletedById = currentUserId;
        analysis.CompletedAt = now;
        AddAnalysisHistory(analysis, AnalysisStatus.InProgress, AnalysisStatus.Completed, currentUserId, now, "Analiz tamamlandı.");

        SynchronizeSampleStatusAfterTerminalChange(analysis.Sample, currentUserId, now);
        analysis.Sample.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return await ReloadDetailAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<ServiceResult<SampleAnalysisDetail>> CancelAsync(
        long id,
        CancelSampleAnalysisRequest request,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var analysis = await db.SampleAnalyses
            .Include(item => item.Sample)
                .ThenInclude(sample => sample.Analyses)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysis is null)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.NotFound,
                "The sample analysis was not found.");
        }

        if (!CanAccess(analysis.Sample, currentUserId, isAdmin))
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Forbidden,
                "This analysis is not assigned to the current laboratory worker.");
        }

        if (analysis.Status is AnalysisStatus.Completed or AnalysisStatus.Cancelled)
        {
            return ServiceResult<SampleAnalysisDetail>.Failure(
                ServiceError.Conflict,
                "A completed or cancelled analysis cannot be cancelled again.");
        }

        var oldStatus = analysis.Status;
        var now = DateTimeOffset.UtcNow;
        analysis.Status = AnalysisStatus.Cancelled;
        AddAnalysisHistory(analysis, oldStatus, AnalysisStatus.Cancelled, currentUserId, now, request.Note.Trim());

        SynchronizeSampleStatusAfterTerminalChange(analysis.Sample, currentUserId, now);
        analysis.Sample.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return await ReloadDetailAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    private async Task<ServiceResult<SampleAnalysisDetail>> ReloadDetailAsync(
        long id,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return await GetByIdAsync(id, currentUserId, isAdmin, cancellationToken);
    }

    private async Task<SampleAnalysis?> LoadDetailEntityAsync(long id, CancellationToken cancellationToken) =>
        await db.SampleAnalyses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Sample)
            .Include(item => item.AnalysisCode)
                .ThenInclude(code => code.Parameters)
            .Include(item => item.RequestedBy)
            .Include(item => item.StartedBy)
            .Include(item => item.CompletedBy)
            .Include(item => item.Results)
                .ThenInclude(result => result.AnalysisParameter)
            .Include(item => item.Results)
                .ThenInclude(result => result.EnteredBy)
            .Include(item => item.History)
                .ThenInclude(history => history.ChangedBy)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private static bool CanAccess(Sample sample, long currentUserId, bool isAdmin) =>
        isAdmin || sample.AssignedToId == currentUserId;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddAnalysisHistory(
        SampleAnalysis analysis,
        AnalysisStatus? oldStatus,
        AnalysisStatus newStatus,
        long changedById,
        DateTimeOffset changedAt,
        string? note)
    {
        analysis.History.Add(new SampleAnalysisHistory
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedById = changedById,
            ChangedAt = changedAt,
            Note = NormalizeOptional(note)
        });
    }

    private void MoveSampleToStatus(
        Sample sample,
        SampleStatus newStatus,
        long changedById,
        DateTimeOffset changedAt,
        string note)
    {
        var oldStatus = sample.Status;
        sample.Status = newStatus;
        sample.UpdatedAt = changedAt;

        db.SampleHistories.Add(new SampleHistory
        {
            SampleId = sample.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedById = changedById,
            ChangedAt = changedAt,
            Note = note
        });
    }

    private void SynchronizeSampleStatusAfterTerminalChange(
        Sample sample,
        long changedById,
        DateTimeOffset changedAt)
    {
        if (sample.Status != SampleStatus.Analyzing
            || sample.Analyses.Count == 0)
        {
            return;
        }

        var hasCompletedAnalysis = sample.Analyses.Any(item => item.Status == AnalysisStatus.Completed);
        var allTerminal = sample.Analyses.All(
            item => item.Status is AnalysisStatus.Completed or AnalysisStatus.Cancelled);

        if (!allTerminal)
        {
            return;
        }

        if (hasCompletedAnalysis)
        {
            MoveSampleToStatus(
                sample,
                SampleStatus.Completed,
                changedById,
                changedAt,
                "Numunedeki bütün aktif analizler tamamlandı.");
            return;
        }

        var allCancelled = sample.Analyses.All(
            item => item.Status == AnalysisStatus.Cancelled);

        if (allCancelled)
        {
            MoveSampleToStatus(
                sample,
                SampleStatus.Received,
                changedById,
                changedAt,
                "Tüm analizler iptal edildiği için numune laboratuvar kabul durumuna çekildi.");
        }
    }

    private static SampleAnalysisDetail ToDetail(SampleAnalysis analysis)
    {
        var resultsByParameter = analysis.Results.ToDictionary(item => item.AnalysisParameterId);
        var parameters = analysis.AnalysisCode.Parameters
            .Where(parameter => parameter.IsActive || resultsByParameter.ContainsKey(parameter.Id))
            .OrderBy(parameter => parameter.DisplayOrder)
            .ThenBy(parameter => parameter.Name)
            .Select(parameter =>
            {
                resultsByParameter.TryGetValue(parameter.Id, out var result);
                return new SampleAnalysisParameterItem(
                    parameter.Id,
                    parameter.Code,
                    parameter.Name,
                    parameter.DefaultUnit,
                    parameter.ReferenceMin,
                    parameter.ReferenceMax,
                    parameter.IsRequired,
                    parameter.DisplayOrder,
                    result is null ? null : ToResult(result));
            })
            .ToList();

        return new SampleAnalysisDetail(
            analysis.Id,
            analysis.SampleId,
            analysis.Sample.SampleCode,
            analysis.AnalysisCodeId,
            analysis.AnalysisCode.Code,
            analysis.AnalysisCode.Name,
            analysis.Status,
            analysis.ResultNote,
            analysis.RequestedBy?.Username,
            analysis.RequestedAt,
            analysis.StartedBy?.Username,
            analysis.StartedAt,
            analysis.CompletedBy?.Username,
            analysis.CompletedAt,
            parameters,
            analysis.History
                .OrderByDescending(history => history.ChangedAt)
                .Select(history => new SampleAnalysisHistoryItem(
                    history.Id,
                    history.OldStatus,
                    history.NewStatus,
                    history.ChangedBy?.Username,
                    history.ChangedAt,
                    history.Note))
                .ToList());
    }

    private static AnalysisResultItem ToResult(AnalysisResult result)
    {
        var isOutsideReference =
            (result.ReferenceMin.HasValue && result.NumericValue < result.ReferenceMin.Value)
            || (result.ReferenceMax.HasValue && result.NumericValue > result.ReferenceMax.Value);

        return new AnalysisResultItem(
            result.Id,
            result.AnalysisParameterId,
            result.AnalysisParameter.Code,
            result.AnalysisParameter.Name,
            result.NumericValue,
            result.Unit,
            result.ReferenceMin,
            result.ReferenceMax,
            isOutsideReference,
            result.ResultNote,
            result.MeasuredAt,
            result.EnteredBy?.Username);
    }
}
