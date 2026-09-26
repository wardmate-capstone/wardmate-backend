using WardMate.SharedKernel.Common;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void None_HasEmptyCodeAndDescription()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Description);
        Assert.Equal(ErrorType.Failure, Error.None.Type);
    }

    [Fact]
    public void NotFound_SetsCorrectType()
    {
        var error = Error.NotFound("res.not_found", "Resource was not found");

        Assert.Equal("res.not_found", error.Code);
        Assert.Equal("Resource was not found", error.Description);
        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public void Validation_SetsCorrectType()
    {
        var error = Error.Validation("val.invalid", "Field is required");

        Assert.Equal("val.invalid", error.Code);
        Assert.Equal("Field is required", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void Conflict_SetsCorrectType()
    {
        var error = Error.Conflict("res.duplicate", "Resource already exists");

        Assert.Equal("res.duplicate", error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Fact]
    public void Unauthorized_SetsCorrectType()
    {
        var error = Error.Unauthorized("auth.unauthorized", "Access denied");

        Assert.Equal("auth.unauthorized", error.Code);
        Assert.Equal(ErrorType.Unauthorized, error.Type);
    }

    [Fact]
    public void StructuralEquality_WorksForRecord()
    {
        var e1 = Error.Validation("code", "desc");
        var e2 = Error.Validation("code", "desc");

        Assert.Equal(e1, e2);
        Assert.True(e1 == e2);
    }
}
