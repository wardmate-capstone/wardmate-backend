namespace WardMate.Services.IAM.Application.Common;

public enum ErrorKind { Conflict, Unauthorized, NotFound }
public sealed record Error(string Code, string Message, ErrorKind Kind);
public sealed record Result<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);
}
public static class AuthErrors
{
    public static readonly Error DuplicateAccount = new("iam.duplicate_account", "Username or email is already registered.", ErrorKind.Conflict);
    public static readonly Error InvalidCredentials = new("iam.invalid_credentials", "Invalid credentials or inactive account.", ErrorKind.Unauthorized);
    public static readonly Error InvalidToken = new("iam.invalid_token", "Invalid, expired or revoked token.", ErrorKind.Unauthorized);
    public static readonly Error UserUnavailable = new("iam.user_unavailable", "Account is unavailable.", ErrorKind.Unauthorized);
}
