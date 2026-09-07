using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;

namespace SampleAnalysisTracking.Services;

public sealed class LocationService(
    SampleAnalysisTrackingDbContext db)
{
    public async Task<IReadOnlyList<LocationItem>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await db.Locations
            .AsNoTracking()
            .OrderBy(location => location.Name)
            .Select(location => new LocationItem(
                location.Id,
                location.Name,
                location.Description))
            .ToListAsync(cancellationToken);
    }
}
