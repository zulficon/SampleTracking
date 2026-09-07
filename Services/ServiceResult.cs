namespace SampleAnalysisTracking.Services;

public enum ServiceError
{
    None,
    NotFound,
    Forbidden,
    Conflict,
    Validation,
    ExternalService
}

public sealed record ServiceResult<T>(
    bool Succeeded,
    T? Value,
    ServiceError Error,
    string? ErrorMessage)
{
    public static ServiceResult<T> Success(T value) =>
        new(true, value, ServiceError.None, null);

    public static ServiceResult<T> Failure(ServiceError error, string message) =>
        new(false, default, error, message);
}
