using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationConnectionGuard(IServiceScopeFactory scopes, IHubContext<NotificationHub, INotificationClient> hub,
    IConfiguration configuration) : BackgroundService
{
    private sealed record Session(HubCallerContext Context, string Token, NotificationAccess Access);
    private readonly ConcurrentDictionary<string, Session> sessions = new();
    public void Register(HubCallerContext context, string token, NotificationAccess access) => sessions[context.ConnectionId] = new(context, token, access);
    public void Remove(string connectionId) => sessions.TryRemove(connectionId, out _);
    public string? Ward(string connectionId) => sessions.TryGetValue(connectionId, out var s) && s.Access.Roles.Contains("FRONT_DESK_OFFICER") &&
        (s.Access.Permissions?.Contains("workflow.queue.read") ?? false) ? s.Access.WardCode : null;
    public static bool CanJoinWard(NotificationAccess access) =>
        access.Roles.Contains("FRONT_DESK_OFFICER") && (access.Permissions?.Contains("workflow.queue.read") ?? false) ||
        access.Roles.Contains("MANAGER") && (access.Permissions?.Contains("iam.accounts.read") ?? false);
    private static string Fingerprint(NotificationAccess a) => $"{a.UserId}|{a.WardCode}|{string.Join(',', a.Roles.Order())}|{string.Join(',', (a.Permissions ?? []).Order())}";

    public async Task<bool> Validate(string connectionId, CancellationToken ct)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return false;
        try
        {
            using var scope = scopes.CreateScope();
            var access = await scope.ServiceProvider.GetRequiredService<INotificationDirectory>().GetAsync(session.Token, ct);
            if (access is not null && access.Roles is not null && Fingerprint(access) == Fingerprint(session.Access)) return true;
        }
        catch (Exception) { /* fail closed: never retain a stale authorization context */ }
        Remove(connectionId);
        try
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, NotificationHub.UserGroup(session.Access.UserId), CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(session.Access.WardCode))
                await hub.Groups.RemoveFromGroupAsync(connectionId, NotificationHub.WardGroup(session.Access.WardCode), CancellationToken.None);
        }
        finally { session.Context.Abort(); }
        return false;
    }
    public async Task RevalidateUser(Guid userId, CancellationToken ct)
    {
        foreach (var entry in sessions.Where(x => x.Value.Access.UserId == userId).ToArray()) await Validate(entry.Key, ct);
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Notifications:RevalidateSeconds", 15), 5, 300)));
        while (await timer.WaitForNextTickAsync(ct))
            await Parallel.ForEachAsync(sessions.Keys.ToArray(), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
                async (id, token) => { try { await Validate(id, token); } catch (Exception) { } });
    }
}

public sealed class NotificationAuthorizationFilter(NotificationConnectionGuard guard) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!await guard.Validate(context.Context.ConnectionId, context.Context.ConnectionAborted)) throw new HubException("Phiên thông báo đã thay đổi. Vui lòng kết nối lại.");
        return await next(context);
    }
}
