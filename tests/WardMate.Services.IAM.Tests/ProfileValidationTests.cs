using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Application.Accounts;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class ProfileValidationTests
{
    private static readonly ProfileInput Valid = new("Nguyễn Văn An", "012345678901", "+84901234567", new DateOnly(2000, 1, 2), "Nam", null, null);

    [Theory]
    [InlineData("name")]
    [InlineData("long-name")]
    [InlineData("identity")]
    [InlineData("phone")]
    [InlineData("long-phone")]
    [InlineData("gender")]
    [InlineData("birth")]
    [InlineData("address")]
    public void InvalidProfileIsRejected(string field)
    {
        var input = field switch
        {
            "name" => Valid with { FullName = " " },
            "long-name" => Valid with { FullName = new string('a', 256) },
            "identity" => Valid with { IdentityNumber = new string('1', 21) },
            "phone" => Valid with { PhoneNumber = "abcdef" },
            "long-phone" => Valid with { PhoneNumber = new string('1', 21) },
            "gender" => Valid with { Gender = new string('a', 11) },
            "birth" => Valid with { DateOfBirth = new DateOnly(2999, 1, 1) },
            _ => Valid with { PermanentAddress = new string('a', 4001) }
        };
        var validator = new ProfileInputValidator(TimeProvider.System);
        Assert.False(validator.Validate(input).IsValid);
    }

    [Fact]
    public void VietnameseProfileAndOptionalFieldsAreAccepted()
    {
        var validator = new ProfileInputValidator(TimeProvider.System);
        Assert.True(validator.Validate(Valid).IsValid);
        Assert.True(validator.Validate(new ProfileInput("Nguyễn Văn An", null, null, null, null, null, null)).IsValid);
    }

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 100, true)]
    public void AccountPaginationIsBounded(int page, int size, bool valid) =>
        Assert.Equal(valid, new ListAccountsValidator().Validate(new ListAccountsQuery(page, size)).IsValid);
}
