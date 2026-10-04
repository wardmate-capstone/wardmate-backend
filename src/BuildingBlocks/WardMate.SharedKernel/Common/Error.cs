namespace WardMate.SharedKernel.Common;

/// <summary>
/// Represents a structured domain / application error.
/// </summary>
/// <param name="Code">Machine-readable error code (e.g. "iam.invalid_credentials").</param>
/// <param name="Description">Human-readable message suitable for logging; never leak internal details to the client.</param>
/// <param name="Type">Categorises the error so callers can map it to HTTP status codes.</param>
public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    // ── Well-known sentinel ───────────────────────────────────────────────────
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    // ── Factory helpers ───────────────────────────────────────────────────────
    public static Error Failure(string code, string description)
        => new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description)
        => new(code, description, ErrorType.NotFound);

    public static Error Validation(string code, string description)
        => new(code, description, ErrorType.Validation);

    public static Error Conflict(string code, string description)
        => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description)
        => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description)
        => new(code, description, ErrorType.Forbidden);
}

/// <summary>Categorises an error to help infrastructure map it to HTTP status codes.</summary>
public enum ErrorType
{
    Failure = 0,
    NotFound = 1,
    Validation = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}
