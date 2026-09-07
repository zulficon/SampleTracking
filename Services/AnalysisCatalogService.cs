using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Services;

public sealed class AnalysisCatalogService(SampleAnalysisTrackingDbContext db)
{
    public async Task<IReadOnlyList<AnalysisCodeItem>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.AnalysisCodes.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.Code)
            .Select(item => new AnalysisCodeItem(
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<AnalysisCodeDetail?> GetByIdAsync(
        long id,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.AnalysisCodes.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .Where(item => item.Id == id)
            .Select(item => new AnalysisCodeDetail(
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.IsActive,
                item.Parameters
                    .Where(parameter => includeInactive || parameter.IsActive)
                    .OrderBy(parameter => parameter.DisplayOrder)
                    .ThenBy(parameter => parameter.Name)
                    .Select(parameter => new AnalysisParameterItem(
                        parameter.Id,
                        parameter.Code,
                        parameter.Name,
                        parameter.DefaultUnit,
                        parameter.ReferenceMin,
                        parameter.ReferenceMax,
                        parameter.IsRequired,
                        parameter.IsActive,
                        parameter.DisplayOrder))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> CreateAsync(
        CreateAnalysisCodeRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Validation,
                "Analysis code and name cannot be blank.");
        }

        if (await db.AnalysisCodes.AnyAsync(item => item.Code == code, cancellationToken))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Conflict,
                "This analysis code already exists.");
        }

        var analysisCode = new AnalysisCode
        {
            Code = code,
            Name = name,
            Description = NormalizeOptional(request.Description),
            IsActive = true
        };

        db.AnalysisCodes.Add(analysisCode);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> UpdateAsync(
        long id,
        UpdateAnalysisCodeRequest request,
        CancellationToken cancellationToken)
    {
        var analysisCode = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysisCode is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis code was not found.");
        }

        var code = NormalizeCode(request.Code);
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Validation,
                "Analysis code and name cannot be blank.");
        }

        if (await db.AnalysisCodes.AnyAsync(
                item => item.Id != id && item.Code == code,
                cancellationToken))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Conflict,
                "This analysis code already exists.");
        }

        analysisCode.Code = code;
        analysisCode.Name = name;
        analysisCode.Description = NormalizeOptional(request.Description);

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> SetActiveAsync(
        long id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var analysisCode = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (analysisCode is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis code was not found.");
        }

        analysisCode.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> AddParameterAsync(
        long analysisCodeId,
        CreateAnalysisParameterRequest request,
        CancellationToken cancellationToken)
    {
        var analysisCode = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .SingleOrDefaultAsync(item => item.Id == analysisCodeId, cancellationToken);

        if (analysisCode is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis code was not found.");
        }

        var validationError = ValidateParameterRange(request.ReferenceMin, request.ReferenceMax);
        if (validationError is not null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(ServiceError.Validation, validationError);
        }

        var code = NormalizeCode(request.Code);
        if (analysisCode.Parameters.Any(item => item.Code == code))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Conflict,
                "This parameter code already exists for the analysis.");
        }

        analysisCode.Parameters.Add(new AnalysisParameter
        {
            Code = code,
            Name = request.Name.Trim(),
            DefaultUnit = request.DefaultUnit.Trim(),
            ReferenceMin = request.ReferenceMin,
            ReferenceMax = request.ReferenceMax,
            IsRequired = request.IsRequired,
            IsActive = true,
            DisplayOrder = request.DisplayOrder
        });

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> UpdateParameterAsync(
        long analysisCodeId,
        long parameterId,
        UpdateAnalysisParameterRequest request,
        CancellationToken cancellationToken)
    {
        var analysisCode = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .SingleOrDefaultAsync(item => item.Id == analysisCodeId, cancellationToken);

        if (analysisCode is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis code was not found.");
        }

        var parameter = analysisCode.Parameters.SingleOrDefault(item => item.Id == parameterId);
        if (parameter is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis parameter was not found.");
        }

        var validationError = ValidateParameterRange(request.ReferenceMin, request.ReferenceMax);
        if (validationError is not null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(ServiceError.Validation, validationError);
        }

        var code = NormalizeCode(request.Code);
        if (analysisCode.Parameters.Any(item => item.Id != parameterId && item.Code == code))
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.Conflict,
                "This parameter code already exists for the analysis.");
        }

        parameter.Code = code;
        parameter.Name = request.Name.Trim();
        parameter.DefaultUnit = request.DefaultUnit.Trim();
        parameter.ReferenceMin = request.ReferenceMin;
        parameter.ReferenceMax = request.ReferenceMax;
        parameter.IsRequired = request.IsRequired;
        parameter.DisplayOrder = request.DisplayOrder;

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    public async Task<ServiceResult<AnalysisCodeDetail>> SetParameterActiveAsync(
        long analysisCodeId,
        long parameterId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var analysisCode = await db.AnalysisCodes
            .Include(item => item.Parameters)
            .SingleOrDefaultAsync(item => item.Id == analysisCodeId, cancellationToken);

        if (analysisCode is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis code was not found.");
        }

        var parameter = analysisCode.Parameters.SingleOrDefault(item => item.Id == parameterId);
        if (parameter is null)
        {
            return ServiceResult<AnalysisCodeDetail>.Failure(
                ServiceError.NotFound,
                "The analysis parameter was not found.");
        }

        parameter.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<AnalysisCodeDetail>.Success(ToDetail(analysisCode));
    }

    private static AnalysisCodeDetail ToDetail(AnalysisCode analysisCode) =>
        new(
            analysisCode.Id,
            analysisCode.Code,
            analysisCode.Name,
            analysisCode.Description,
            analysisCode.IsActive,
            analysisCode.Parameters
                .OrderBy(parameter => parameter.DisplayOrder)
                .ThenBy(parameter => parameter.Name)
                .Select(parameter => new AnalysisParameterItem(
                    parameter.Id,
                    parameter.Code,
                    parameter.Name,
                    parameter.DefaultUnit,
                    parameter.ReferenceMin,
                    parameter.ReferenceMax,
                    parameter.IsRequired,
                    parameter.IsActive,
                    parameter.DisplayOrder))
                .ToList());

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ValidateParameterRange(decimal? minimum, decimal? maximum) =>
        minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value
            ? "Reference minimum cannot be greater than reference maximum."
            : null;
}
