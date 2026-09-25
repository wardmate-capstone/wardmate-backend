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
    public static readonly Error DuplicateAccount = new("iam.duplicate_account", "Tên đăng nhập hoặc email đã được đăng ký.", ErrorKind.Conflict);
    public static readonly Error InvalidCredentials = new("iam.invalid_credentials", "Thông tin đăng nhập không đúng hoặc tài khoản chưa được kích hoạt.", ErrorKind.Unauthorized);
    public static readonly Error InvalidToken = new("iam.invalid_token", "Mã xác thực không hợp lệ, đã hết hạn hoặc đã bị thu hồi.", ErrorKind.Unauthorized);
    public static readonly Error UserUnavailable = new("iam.user_unavailable", "Tài khoản không khả dụng.", ErrorKind.Unauthorized);
}
