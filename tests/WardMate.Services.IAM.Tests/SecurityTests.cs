using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Security;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class SecurityTests
{
    internal static JwtOptions Settings() => new() { Key = "unit-test-signing-key-at-least-32-characters", Issuer = "wardmate-tests", Audience = "wardmate-tests-client" };
    internal static JwtTokenService Tokens(TimeProvider? clock = null) => new(Options.Create(Settings()), clock ?? TimeProvider.System);
    internal static User Citizen() => new()
    {
        Username = "citizen",
        Email = "citizen@example.test",
        Profile = new UserProfile { FullName = "Citizen" },
        UserRoles = [new UserRole { RoleId = 1, Role = TestIdentityStore.CitizenRole() }]
    };

    [Fact]
    public void BCryptUsesRandomSaltAndVerifiesOnlyCorrectPassword()
    {
        var hasher = new BcryptPasswordHasher();
        const string password = "Correct-password-123";
        var first = hasher.Hash(password);
        var second = hasher.Hash(password);
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(password, first));
        Assert.False(hasher.Verify("incorrect", first));
        Assert.StartsWith("$2", first);
    }
    [Fact]
    public void BCryptRejectsOverlongUtf8PasswordsInsteadOfTruncating()
    {
        var hasher = new BcryptPasswordHasher();
        Assert.Throws<ArgumentException>(() => hasher.Hash(new string('ế', 30)));
    }
    [Fact]
    public void JwtContainsSubjectEmailRolesAndPermissionArrays()
    {
        var user = Citizen();
        var token = Tokens().CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.Email, jwt.Claims.Single(x => x.Type == "email").Value);
        Assert.Contains(jwt.Claims, x => x.Type == "role" && x.Value == "REGISTERED_CITIZEN");
        Assert.Contains(jwt.Claims, x => x.Type == "permissions" && x.Value == "iam.profile.read");
        Assert.Equal(user.Id, Tokens().ValidateAccessTokenForRefresh(token.Value));
    }
    [Fact]
    public void RefreshTokensAreRandomAndOnlyDigestIsPersisted()
    {
        var tokens = Tokens();
        var first = tokens.CreateRefreshToken();
        var second = tokens.CreateRefreshToken();
        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(64, Convert.FromBase64String(first.Value).Length);
        Assert.NotEqual(first.Value, first.Hash);
        Assert.Equal(tokens.HashRefreshToken(first.Value), first.Hash);
    }
    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("")]
    public void InvalidJwtIsRejected(string value) => Assert.Null(Tokens().ValidateAccessTokenForRefresh(value));

    [Fact]
    public void JwtSignedByAnotherKeyIsRejected()
    {
        var settings = Settings();
        settings.Key = "different-key-at-least-32-characters-long";
        var other = new JwtTokenService(Options.Create(settings), TimeProvider.System);
        Assert.Null(Tokens().ValidateAccessTokenForRefresh(other.CreateAccessToken(Citizen()).Value));
    }
    [Fact]
    public void JwtWithWrongAudienceIsRejected()
    {
        var settings = Settings();
        settings.Audience = "another-audience";
        var other = new JwtTokenService(Options.Create(settings), TimeProvider.System);
        Assert.Null(Tokens().ValidateAccessTokenForRefresh(other.CreateAccessToken(Citizen()).Value));
    }
}
