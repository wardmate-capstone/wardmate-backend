using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Security;
using WardMate.UnitTests.Support;

namespace WardMate.UnitTests.Iam;

// IAM-01a: khóa đăng nhập theo tài khoản sau nhiều lần sai.
public sealed class LoginThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private static LoginThrottle Throttle(MutableTime time, Dictionary<string, string?>? settings = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build(), time, NullLogger<LoginThrottle>.Instance);

    [Fact]
    public void Blocks_only_after_ten_failures()
    {
        var throttle = Throttle(new MutableTime(Start));
        for (var i = 0; i < 9; i++) throttle.Failed("user:a");
        Assert.Null(throttle.Blocked("user:a"));
        throttle.Failed("user:a");
        var remaining = throttle.Blocked("user:a");
        Assert.NotNull(remaining);
        Assert.Equal(TimeSpan.FromMinutes(15), remaining.Value);
    }

    [Fact]
    public void Unblocks_when_window_ends()
    {
        var time = new MutableTime(Start);
        var throttle = Throttle(time);
        for (var i = 0; i < 10; i++) throttle.Failed("user:a");
        time.Advance(TimeSpan.FromMinutes(15));
        Assert.Null(throttle.Blocked("user:a"));
    }

    [Fact]
    public void Reset_clears_failures()
    {
        var throttle = Throttle(new MutableTime(Start));
        for (var i = 0; i < 10; i++) throttle.Failed("user:a");
        throttle.Reset("user:a");
        Assert.Null(throttle.Blocked("user:a"));
    }

    [Fact]
    public void Counts_keys_separately()
    {
        var throttle = Throttle(new MutableTime(Start));
        for (var i = 0; i < 10; i++) throttle.Failed("user:a");
        Assert.Null(throttle.Blocked("user:b"));
    }

    [Fact]
    public void Max_failures_is_configurable()
    {
        var throttle = Throttle(new MutableTime(Start), new() { ["Login:MaxFailures"] = "3" });
        for (var i = 0; i < 3; i++) throttle.Failed("user:a");
        Assert.NotNull(throttle.Blocked("user:a"));
    }

    [Fact]
    public void Username_and_email_of_same_user_share_key()
    {
        var id = Guid.NewGuid();
        Assert.Equal(LoginThrottleKey.For(id, "an"), LoginThrottleKey.For(id, "an@example.test"));
        Assert.NotEqual(LoginThrottleKey.For(null, "an"), LoginThrottleKey.For(null, "an@example.test"));
    }

    [Fact]
    public async Task Login_returns_429_lock_after_ten_wrong_attempts()
    {
        var throttle = Throttle(new MutableTime(Start));
        var handler = new LoginHandler(new NoUserStore(), null!, null!, TimeProvider.System, throttle);
        for (var i = 0; i < 10; i++)
        {
            var wrong = await handler.Handle(new LoginCommand("khong-ton-tai", "sai"), default);
            Assert.Equal(AuthErrors.InvalidCredentials, wrong.Error);
        }
        var locked = await handler.Handle(new LoginCommand(" KHONG-TON-TAI ", "sai"), default);
        Assert.Equal("iam.login_locked", locked.Error!.Code);
        Assert.Equal(ErrorKind.TooManyRequests, locked.Error.Kind);
    }

    private sealed class NoUserStore : IIdentityStore
    {
        public Task<User?> FindUser(string usernameOrEmail, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<bool> AccountExists(string username, string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> FindUser(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> GetCitizenRole(CancellationToken ct) => throw new NotSupportedException();
        public Task<RefreshToken?> FindRefreshToken(string tokenHash, CancellationToken ct) => throw new NotSupportedException();
        public void AddUser(User user) => throw new NotSupportedException();
        public void QueueWelcome(User user) => throw new NotSupportedException();
        public void AddRefreshToken(RefreshToken token) => throw new NotSupportedException();
        public Task<SaveOutcome> ChangePassword(Guid userId, string expectedHash, string newHash, DateTime now, CancellationToken ct) => throw new NotSupportedException();
        public Task<SaveOutcome> SaveChanges(CancellationToken ct) => throw new NotSupportedException();
    }
}
