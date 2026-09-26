namespace WardMate.SharedKernel.Common;

/// <summary>
/// Discriminated-union result type for use-cases that can succeed or fail.
/// Avoids throwing exceptions for predictable business failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot contain an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must contain an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    // ── Implicit conversion ───────────────────────────────────────────────────
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Generic variant that carries a value on the happy path.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>Returns the value. Throws if the result is a failure.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    // ── Implicit conversions ──────────────────────────────────────────────────
    public static implicit operator Result<TValue>(TValue value) => Result.Success(value);
    public static implicit operator Result<TValue>(Error error) => Result.Failure<TValue>(error);
}
