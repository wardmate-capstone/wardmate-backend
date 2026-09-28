using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.IAM.Application;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Application.Queries;
using WardMate.Services.IAM.Infrastructure.Security;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class AuthFlowTests : IDisposable
{
    private readonly TestIdentityStore store = new();
    private readonly ServiceProvider provider;
    private readonly ITokenService tokens = SecurityTests.Tokens();
    private ISender Sender => provider.GetRequiredService<ISender>();
    public AuthFlowTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIamApplication();
        services.AddSingleton<IIdentityStore>(store);
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton(tokens);
        services.AddSingleton(TimeProvider.System);
        provider = services.BuildServiceProvider();
    }
    public void Dispose() => provider.Dispose();
    private static RegisterCitizenCommand Registration() => new("citizen", "citizen@example.test", "SecurePassword123!", "Nguyen Van A");
    private async Task<WardMate.Services.IAM.Application.DTOs.AuthResponseDto> Login()
    {
        await Sender.Send(Registration());
        return (await Sender.Send(new LoginCommand("citizen", "SecurePassword123!"))).Value!;
    }

    [Fact]
    public async Task RegistrationCreatesProfileAndOnlyCitizenRole()
    {
        var result = await Sender.Send(Registration() with { Username = "Citizen", Email = "CITIZEN@example.test" });
        Assert.True(result.IsSuccess);
        var user = Assert.Single(store.Users);
        Assert.Equal("citizen", user.Username);
        Assert.Equal("citizen@example.test", user.Email);
        Assert.Equal("Nguyen Van A", user.Profile.FullName);
        Assert.Equal("REGISTERED_CITIZEN", Assert.Single(result.Value!.Roles));
        Assert.Contains("iam.profile.read", result.Value.Permissions);
        Assert.NotEqual(Registration().Password, user.PasswordHash);
    }
    [Theory]
    [InlineData("citizen", "another@example.test")]
    [InlineData("another", "CITIZEN@example.test")]
    public async Task DuplicateRegistrationIsConflict(string username, string email)
    {
        await Sender.Send(Registration());
        var result = await Sender.Send(Registration() with { Username = username, Email = email });
        Assert.Equal("iam.duplicate_account", result.Error?.Code);
        Assert.Single(store.Users);
    }
    [Theory]
    [InlineData("", "citizen@example.test", "SecurePassword123!", "Citizen")]
    [InlineData("user@domain", "citizen@example.test", "SecurePassword123!", "Citizen")]
    [InlineData("citizen", "invalid", "SecurePassword123!", "Citizen")]
    [InlineData("citizen", "citizen@example.test", "short", "Citizen")]
    [InlineData("citizen", "citizen@example.test", "SecurePassword123!", " ")]
    public async Task InvalidRegistrationIsStoppedBeforePersistence(string username, string email, string password, string fullName)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Sender.Send(new RegisterCitizenCommand(username, email, password, fullName)));
        Assert.Empty(store.Users);
    }
    [Fact]
    public async Task ConcurrentDuplicateRegistrationReturnsConflict()
    {
        store.Outcome = SaveOutcome.Duplicate;
        Assert.Equal("iam.duplicate_account", (await Sender.Send(Registration())).Error?.Code);
    }
    [Theory]
    [InlineData("Abcdef!", false)]
    [InlineData("abcdefg!", false)]
    [InlineData("Abcdefgh", false)]
    [InlineData("Abcdefg ", false)]
    [InlineData("Abcdefg!", true)]
    [InlineData("ABCDEFG$", true)]
    [InlineData(null, false)]
    public async Task RegistrationEnforcesPasswordStrength(string? password, bool valid)
    {
        var command = Registration() with { Password = password! };
        if (valid)
        {
            Assert.True((await Sender.Send(command)).IsSuccess);
            Assert.Single(store.Users);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => Sender.Send(command));
            Assert.Contains(error.Errors, e => e.PropertyName == "Password");
            Assert.Empty(store.Users);
        }
    }
    [Theory]
    [InlineData(35, true)]
    [InlineData(36, false)]
    public async Task RegistrationRespectsUtf8ByteLimit(int accentedCharacters, bool valid)
    {
        var command = Registration() with { Password = "A!" + new string('é', accentedCharacters) };
        if (valid) Assert.True((await Sender.Send(command)).IsSuccess);
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => Sender.Send(command));
            Assert.Empty(store.Users);
        }
    }
    [Theory]
    [InlineData("citizen")]
    [InlineData("CITIZEN@EXAMPLE.TEST")]
    public async Task LoginAcceptsUsernameOrEmail(string identity)
    {
        await Sender.Send(Registration());
        var result = await Sender.Send(new LoginCommand(identity, Registration().Password));
        Assert.True(result.IsSuccess);
        Assert.Equal(tokens.HashRefreshToken(result.Value!.RefreshToken), Assert.Single(store.Tokens).Token);
    }
    [Theory]
    [InlineData("citizen", "wrong-password")]
    [InlineData("missing", "SecurePassword123!")]
    public async Task InvalidLoginReturnsSameError(string identity, string password)
    {
        await Sender.Send(Registration());
        Assert.Equal("iam.invalid_credentials", (await Sender.Send(new LoginCommand(identity, password))).Error?.Code);
        Assert.Empty(store.Tokens);
    }
    [Fact]
    public async Task InactiveUserCannotLoginOrReadProfile()
    {
        await Sender.Send(Registration());
        store.Users[0].IsActive = false;
        Assert.False((await Sender.Send(new LoginCommand("citizen", Registration().Password))).IsSuccess);
        Assert.False((await Sender.Send(new GetCurrentUserQuery(store.Users[0].Id))).IsSuccess);
    }
    [Fact]
    public async Task RefreshRotatesAndRejectsReuse()
    {
        var login = await Login();
        var request = new RefreshTokenCommand(login.AccessToken, login.RefreshToken);
        var result = await Sender.Send(request);
        Assert.True(result.IsSuccess);
        Assert.NotEqual(login.RefreshToken, result.Value!.RefreshToken);
        Assert.True(store.Tokens[0].IsRevoked);
        Assert.False((await Sender.Send(request)).IsSuccess);
    }
    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("inactive")]
    [InlineData("other-user")]
    [InlineData("unknown")]
    public async Task RefreshRejectsInvalidStates(string state)
    {
        var login = await Login();
        if (state == "expired") store.Tokens[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        if (state == "revoked") store.Tokens[0].IsRevoked = true;
        if (state == "inactive") store.Users[0].IsActive = false;
        if (state == "other-user") store.Tokens[0].UserId = Guid.NewGuid();
        var result = await Sender.Send(new RefreshTokenCommand(login.AccessToken, state == "unknown" ? "unknown" : login.RefreshToken));
        Assert.Equal("iam.invalid_token", result.Error?.Code);
    }
    [Fact]
    public async Task RefreshConcurrencyFailureDoesNotReturnTokens()
    {
        var login = await Login();
        store.Outcome = SaveOutcome.ConcurrentUpdate;
        var result = await Sender.Send(new RefreshTokenCommand(login.AccessToken, login.RefreshToken));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }
    [Fact]
    public async Task LogoutIsIdempotentAndPreventsRefresh()
    {
        var login = await Login();
        var request = new RevokeTokenCommand(store.Users[0].Id, login.RefreshToken);
        Assert.True((await Sender.Send(request)).IsSuccess);
        Assert.True((await Sender.Send(request)).IsSuccess);
        Assert.False((await Sender.Send(new RefreshTokenCommand(login.AccessToken, login.RefreshToken))).IsSuccess);
    }
    [Fact]
    public async Task CannotRevokeAnotherUsersToken()
    {
        var login = await Login();
        Assert.False((await Sender.Send(new RevokeTokenCommand(Guid.NewGuid(), login.RefreshToken))).IsSuccess);
        Assert.False(store.Tokens[0].IsRevoked);
    }
    [Fact]
    public async Task CurrentUserIncludesPersistedProfileAndPermissions()
    {
        await Sender.Send(Registration());
        var result = await Sender.Send(new GetCurrentUserQuery(store.Users[0].Id));
        Assert.True(result.IsSuccess);
        Assert.Equal("Nguyen Van A", result.Value!.Profile!.FullName);
        Assert.Contains("iam.profile.read", result.Value.Permissions);
    }
}
