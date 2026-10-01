namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed record ProcedureError(string Code, string Message, int Status,
    IDictionary<string, string[]>? Errors = null);

public sealed record ProcedureResult<T>(T? Value, ProcedureError? Error)
{
    public bool IsSuccess => Error is null;
    public static ProcedureResult<T> Ok(T value) => new(value, null);
    public static ProcedureResult<T> Fail(string code, string message, int status,
        IDictionary<string, string[]>? errors = null) => new(default, new(code, message, status, errors));
    public static ProcedureResult<T> NotFound() => Fail("procedure.not_found", "Không tìm thấy thủ tục.", 404);
}
