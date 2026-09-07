using SampleAnalysisTracking.DTOs;

namespace SampleAnalysisTracking.Services;

public enum UserOperationError
{
    None = 0,
    NotFound = 1,
    UsernameAlreadyExists = 2,
    EmailAlreadyExists = 3,
    CannotDeactivateSelf = 4,
    CannotRemoveLastAdmin = 5,
    CurrentPasswordIncorrect = 6,
    NewPasswordSameAsCurrent = 7
}

public sealed record UserOperationResult(
    UserOperationError Error,
    UserCreated? User = null)
{
    public bool Succeeded =>
        Error == UserOperationError.None;

    public static UserOperationResult Success(
        UserCreated? user = null)
    {
        return new UserOperationResult(
            UserOperationError.None,
            user);
    }

    public static UserOperationResult Failure(
        UserOperationError error)
    {
        return new UserOperationResult(
            Error: error,
            User: null);
    }
}
