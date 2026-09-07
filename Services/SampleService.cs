using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;


namespace SampleAnalysisTracking.Services;

public sealed class SampleService(SampleAnalysisTrackingDbContext db)
{

   private static readonly IReadOnlyDictionary<SampleStatus, SampleStatus>
    NextStatus = new Dictionary<SampleStatus, SampleStatus>
    {
        [SampleStatus.Created] = SampleStatus.Collected,
        [SampleStatus.Collected] = SampleStatus.Transferred,
        [SampleStatus.Transferred] = SampleStatus.Received,
        [SampleStatus.Received] = SampleStatus.Analyzing,
        [SampleStatus.Analyzing] = SampleStatus.Completed
    };



        public async Task<SampleDetail?> GetByIdAsync( long id, CancellationToken cancellationToken,long? assignedToId=null)
    {
        var sample = await db.Samples
        .AsNoTracking()
        .Include(item => item.Location)
        .Include(item=>item.CreatedBy)
        .Include(item => item.AssignedTo)
        .Include(item=>item.History)
            .ThenInclude(history=>history.ChangedBy)
        .SingleOrDefaultAsync(
        item => item.Id == id && (!assignedToId.HasValue || item.AssignedToId == assignedToId.Value),cancellationToken);

        return sample is null ? null : ToDetail(sample);
    }
    private static SampleDetail ToDetail(Sample sample)
    {
        return new SampleDetail(
            sample.Id,
            sample.SampleCode,
            sample.SampleType,
            sample.Status,
            sample.LocationId,
            sample.Location?.Name,
            sample.Description,
            sample.CreatedById,
            sample.CreatedBy?.Username,
            sample.AssignedToId,
            sample.AssignedTo?.Username,
            sample.CreatedAt,
            sample.UpdatedAt,
            sample.History
            .OrderByDescending(history=>history.ChangedAt)
            .Select(history=> new SampleHistoryItem(
                history.Id,
                history.OldStatus,
                history.NewStatus,
                history.ChangedBy?.Username,
                history.ChangedAt,
                history.Note))
            .ToList());
}

public async Task<IReadOnlyList<SampleListItem>> GetAllAsync(
    SampleStatus? status,
    long? locationId,
    CancellationToken cancellationToken)
    {
        var query = db.Samples
        .AsNoTracking()
        .Include(sample=>sample.Location)
        .Include(sample=>sample.CreatedBy)
        .AsQueryable();

        if(status.HasValue)
        {
            query = query.Where(sample=>sample.Status == status.Value);
        }

        if(locationId.HasValue)
        {
            query = query.Where(sample=>sample.LocationId == locationId);
        }

        return await query
        .OrderByDescending(sample=>sample.CreatedAt)
        .Select(sample=> new SampleListItem(
            sample.Id,
            sample.SampleCode,
            sample.SampleType,
            sample.Status,
            sample.Location == null ? null : sample.Location.Name,
            sample.CreatedBy == null ? null : sample.CreatedBy.Username,
            sample.CreatedAt,
            sample.UpdatedAt)).ToListAsync(cancellationToken);


    }

public async Task<PagedResult<SampleListItem>> GetPagedAsync(
    SampleListQuery request,
    CancellationToken cancellationToken,
    long? assignedToId = null
)
    {
        var query =db.Samples
        .AsNoTracking()
        .AsQueryable();

        if (assignedToId.HasValue)
        {
            query=query.Where(sample=>sample.AssignedToId == assignedToId.Value);
        }

        if(request.Status.HasValue)
        {
            var status = request.Status.Value;
            query = query.Where(sample => sample.Status == status);
        }
        if(request.LocationId.HasValue)
        {
            query = query.Where(sample => sample.LocationId == request.LocationId);
        }

       if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var SearchText = $"%{request.Search.Trim()}%";
            query = query.Where(sample =>
            EF.Functions.ILike(sample.SampleCode, SearchText) ||
            EF.Functions.ILike(sample.SampleType, SearchText) ||
            (sample.Location != null &&
             EF.Functions.ILike(sample.Location.Name, SearchText)));
        }
        var TotalCount = await query.CountAsync(cancellationToken);
        var items = await query
        .OrderByDescending(sample => sample.UpdatedAt)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(sample => new SampleListItem(
            sample.Id,
            sample.SampleCode,
            sample.SampleType,
            sample.Status,
            sample.Location == null ? null : sample.Location.Name,
            sample.CreatedBy == null ? null: sample.CreatedBy.Username,
            sample.CreatedAt,
            sample.UpdatedAt))
            .ToListAsync(cancellationToken);

            return new PagedResult<SampleListItem>(
                items,
                request.Page,
                request.PageSize,
                TotalCount);
    }

public async Task<SampleDashboardSummary> GetSampleDashboardSummaryAsync(
    CancellationToken cancellationToken
)
    {
        var statusRows = await db.Samples
        .AsNoTracking()
        .GroupBy(sample=>sample.Status)
        .Select(group=> new
        {
            Status = group.Key,
            Count = group.Count()
        }).ToListAsync(cancellationToken);

        var statusCounts = statusRows.ToDictionary(
            item => item.Status,
            item => item.Count);

        var total = statusCounts.Values.Sum();
        var completed = statusCounts.GetValueOrDefault(SampleStatus.Completed,0);
        var analyzing = statusCounts.GetValueOrDefault(SampleStatus.Analyzing,0);

        return new SampleDashboardSummary(
            Total: total,
            Active: total - completed,
            Analyzing: analyzing,
            Completed: completed,
            StatusCounts: statusCounts
        );

    }



public async Task<ServiceResult<Sample>> CreateAsync(
    CreateSampleRequest request,
    long createdById,
    CancellationToken cancellationToken)
{
    var sampleCode = request.SampleCode.Trim();
    var sampleType = request.SampleType.Trim();

    if (string.IsNullOrWhiteSpace(sampleCode) || string.IsNullOrWhiteSpace(sampleType))
    {
        return ServiceResult<Sample>.Failure(
            ServiceError.Validation,
            "Sample code and sample type cannot be blank.");
    }

    if (await db.Samples.AnyAsync(sample => sample.SampleCode == sampleCode, cancellationToken))
    {
        return ServiceResult<Sample>.Failure(
            ServiceError.Conflict,
            "This sample code already exists.");
    }

    if (request.LocationId.HasValue
        && !await db.Locations.AnyAsync(location => location.Id == request.LocationId, cancellationToken))
    {
        return ServiceResult<Sample>.Failure(
            ServiceError.Validation,
            "The specified location does not exist.");
    }

    var now = DateTimeOffset.UtcNow;

    var sample = new Sample
    {
        SampleCode = sampleCode,
        SampleType = sampleType,
        LocationId = request.LocationId,
        Status = SampleStatus.Created,
        Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim(),
        CreatedById = createdById,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Samples.Add(sample);

    db.SampleHistories.Add(new SampleHistory
    {
        Sample = sample,
        OldStatus = null,
        NewStatus = SampleStatus.Created,
        ChangedById = createdById,
        ChangedAt = now,
        Note = string.IsNullOrWhiteSpace(request.InitialNote)
            ? null
            : request.InitialNote.Trim()
    });

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception)
        when (exception.IsUniqueViolation())
    {
        return ServiceResult<Sample>.Failure(
            ServiceError.Conflict,
            "A sample with this code already exists.");
    }

    return ServiceResult<Sample>.Success(sample);
}

public async Task<ServiceResult<bool>> UpdateAsync(
    long id,
    UpdateSampleRequest request,
    CancellationToken cancellationToken)
{
    var sampleType = request.SampleType.Trim();

    if (string.IsNullOrWhiteSpace(sampleType))
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            "Sample type cannot be blank.");
    }

    var sample = await db.Samples
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (sample is null)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.NotFound,
            "Sample not found.");
    }

    if (request.LocationId.HasValue
        && !await db.Locations.AnyAsync(
            location => location.Id == request.LocationId,
            cancellationToken))
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            "The specified location does not exist.");
    }

    sample.SampleType = sampleType;
    sample.LocationId = request.LocationId;
    sample.Description = string.IsNullOrWhiteSpace(request.Description)
        ? null
        : request.Description.Trim();
    sample.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(cancellationToken);

    return ServiceResult<bool>.Success(true);
}

public async Task<ServiceResult<bool>> AssignLaboratoryWorkerAsync(
    long id,
    AssignLaboratoryWorkerRequest request,
    CancellationToken cancellationToken)
{
    var sample = await db.Samples
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (sample is null)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.NotFound,
            "Sample not found.");
    }

    if (request.LaboratoryWorkerId.HasValue)
    {
        var laboratoryWorker = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == request.LaboratoryWorkerId.Value,
                cancellationToken);

        if (laboratoryWorker is null)
        {
            return ServiceResult<bool>.Failure(
                ServiceError.Validation,
                "The specified laboratory worker does not exist.");
        }

        if (laboratoryWorker.Role != UserRole.Laboratory || !laboratoryWorker.IsActive)
    {
            return ServiceResult<bool>.Failure(
                ServiceError.Validation,
                "The selected user is not an active laboratory worker.");
    }
    }

    sample.AssignedToId = request.LaboratoryWorkerId;
    sample.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(cancellationToken);

    return ServiceResult<bool>.Success(true);
}

public async Task<ServiceResult<bool>> ChangeStatusAsync(
    long id,
    ChangeSampleStatusRequest request,
    long changedById,
    bool requireAssignment,
    CancellationToken cancellationToken)
{
    var newStatus = request.NewStatus;

    if (!Enum.IsDefined(typeof(SampleStatus),newStatus))
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            "The status is not valid for the sample workflow.");
    }

    var sample = await db.Samples
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (sample is null)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.NotFound,
            "Sample not found.");
    }
    if (requireAssignment
    && sample.AssignedToId != changedById)
{
    return ServiceResult<bool>.Failure(
        ServiceError.Forbidden,
        "This sample is not assigned to the current laboratory worker.");
}
    if (sample.Status == newStatus)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            "The sample already has this status.");
    }

    if (!NextStatus.TryGetValue(
            sample.Status,
            out var expectedStatus))
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            $"A sample in {sample.Status} status has no further status.");
    }

    if (newStatus != expectedStatus)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            $"A sample in {sample.Status} status can only transition to {expectedStatus}.");
    }

    if (newStatus is SampleStatus.Analyzing or SampleStatus.Completed)
    {
        return ServiceResult<bool>.Failure(
            ServiceError.Validation,
            newStatus == SampleStatus.Analyzing
                ? "Start an assigned analysis to move the sample into analysis."
                : "Complete all assigned analyses to complete the sample.");
    }


    var oldStatus = sample.Status;
    var now = DateTimeOffset.UtcNow;

    sample.Status = newStatus;
    sample.UpdatedAt = now;

    db.SampleHistories.Add(new SampleHistory
    {
        SampleId = sample.Id,
        OldStatus = oldStatus,
        NewStatus = newStatus,
        ChangedById = changedById,
        ChangedAt = now,
        Note = string.IsNullOrWhiteSpace(request.Note)
            ? null
            : request.Note.Trim()
    });

    await db.SaveChangesAsync(cancellationToken);

    return ServiceResult<bool>.Success(true);



}



}
