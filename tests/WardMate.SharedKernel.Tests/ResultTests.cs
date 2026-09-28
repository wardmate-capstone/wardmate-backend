using WardMate.SharedKernel.Common;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithError()
    {
        var error = Error.Validation("TEST", "Validation failed");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_ContainsValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenAccessingValue()
    {
        var error = Error.NotFound("ITEM", "Not found");
        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_ReturnsSuccessResult()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_ReturnsFailureResult()
    {
        var error = Error.Conflict("CONFLICT", "Entity already exists");
        Result<int> result = error;

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }
}
