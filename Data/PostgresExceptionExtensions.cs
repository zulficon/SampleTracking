using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SampleAnalysisTracking.Data;

public static class PostgresExceptionExtensions
{
    public static bool IsUniqueViolation(
        this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    public static string? GetConstraintName(
        this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            ? postgresException.ConstraintName
            : null;
    }
}
