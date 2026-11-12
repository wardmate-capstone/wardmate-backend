using WardMate.Services.AnalyticsSystem.Domain;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// NOTIFY-READALL-01: MarkAllRead đánh dấu đúng phạm vi của UnreadCountAsync, có mốc `before`.
// ExecuteUpdate cần PostgreSQL nên test kiểm bộ lọc dùng chung (NotificationService.Unread), không kiểm lệnh cập nhật.
public sealed class MarkAllReadScopeTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    private static Notification N(Guid user, string? ward = null, bool read = false, string channel = "IN_APP", int minutes = 0) => new()
    {
        Id = Guid.NewGuid(), RecipientUserId = user, RecipientWardCode = ward, IsRead = read, Channel = channel,
        Title = "t", Content = "c", Type = "SYSTEM_ALERT", CreatedAt = T0.AddMinutes(minutes)
    };

    private static List<Notification> Data() =>
    [
        N(Me), N(Me, minutes: 1), N(Me, ward: "W1"),        // 3 trong phạm vi khi đang ở W1
        N(Me, ward: "W2"),                                   // phường cũ → không đụng
        N(Me, read: true), N(Me, channel: "EMAIL"),          // đã đọc / không phải IN_APP
        N(Guid.NewGuid())                                    // người khác
    ];

    [Fact]
    public void Scope_matches_unread_count_filter_and_skips_other_ward_and_users()
    {
        var rows = NotificationService.Unread(Data().AsQueryable(), Me, "W1").ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, x => Assert.True(x.RecipientUserId == Me && !x.IsRead && x.RecipientWardCode is null or "W1"));
    }

    [Fact]
    public void Citizen_without_ward_only_marks_personal_notifications()
    {
        Assert.Equal(2, NotificationService.Unread(Data().AsQueryable(), Me, null).Count());
    }

    [Fact]
    public void Before_keeps_notifications_created_later_unread()
    {
        var rows = NotificationService.Unread(Data().AsQueryable(), Me, "W1", T0).ToList();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, x => x.CreatedAt > T0);
    }

    [Fact]
    public void Before_is_normalised_to_utc()
    {
        Assert.Null(NotificationService.AsUtc(null));
        var unspecified = new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(DateTimeKind.Utc, NotificationService.AsUtc(unspecified)!.Value.Kind);
        Assert.Equal(T0, NotificationService.AsUtc(T0));
        var local = T0.ToLocalTime();
        Assert.Equal(T0, NotificationService.AsUtc(local));
    }
}
