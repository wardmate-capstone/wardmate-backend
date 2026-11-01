using System.Text.Json;

namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class ReviewDeadlinePolicy
{
    public bool Enabled { get; set; }
    public bool Applicable { get; set; } = true;
    public string Code { get; set; } = "DOCUMENT_PREPARATION";
    public string Version { get; set; } = "1";
    public string CalendarCode { get; set; } = "VN_WEEKDAYS";
    public string CalendarVersion { get; set; } = "1";
    public int WorkingMinutes { get; set; } = 2700;
    public bool PauseOnRevision { get; set; } = true;
    public int StartMinute { get; set; } = 450;
    public int EndMinute { get; set; } = 990;
    public DayOfWeek[] WorkingDays { get; set; } = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    public DateOnly[] Holidays { get; set; } = [];
    public void Validate()
    {
        if (new[] { Code, Version, CalendarCode, CalendarVersion }.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)
            || WorkingMinutes is < 1 or > 100000 || StartMinute < 0 || EndMinute > 1440 || StartMinute >= EndMinute
            || WorkingDays is null || WorkingDays.Length == 0 || WorkingDays.Any(x => !Enum.IsDefined(x)) || Holidays is null || Holidays.Length > 3660)
            throw new InvalidOperationException("ReviewDeadline policy/calendar is invalid.");
    }
}
public sealed record DeadlineEvent(string Action, DateTime AtUtc, Guid ActorId, DateTime? DueBeforeUtc, DateTime? DueAfterUtc, Guid? ClosureId = null);
public static class ReviewDeadline
{
    // Version 1 calendar is explicitly Vietnam UTC+07; no daylight saving interpretation.
    private static (DateTime Start, DateTime End) Window(DateOnly local, ReviewDeadlinePolicy p) =>
        (DateTime.SpecifyKind(local.ToDateTime(TimeOnly.MinValue).AddMinutes(p.StartMinute - 420), DateTimeKind.Utc),
         DateTime.SpecifyKind(local.ToDateTime(TimeOnly.MinValue).AddMinutes(p.EndMinute - 420), DateTimeKind.Utc));
    private static bool Working(DateOnly d, ReviewDeadlinePolicy p) => p.WorkingDays.Contains(d.DayOfWeek) && !p.Holidays.Contains(d);
    private static IEnumerable<(DateTime Start, DateTime End)> Segments(DateOnly day, ReviewDeadlinePolicy p, IReadOnlyList<ClosureInterval>? closed)
    {
        var (start, end) = Window(day, p);
        var cursor = start;
        foreach (var c in (closed ?? []).Where(x => x.StartsAtUtc < end && x.EndsAtUtc > start).OrderBy(x => x.StartsAtUtc))
        {
            if (c.StartsAtUtc > cursor) yield return (cursor, c.StartsAtUtc);
            if (c.EndsAtUtc > cursor) cursor = c.EndsAtUtc;
            if (cursor >= end) yield break;
        }
        if (cursor < end) yield return (cursor, end);
    }
    public static bool Recalculate(ApplicationRecord a, DateTime now, Guid actor, Guid closureId,
        IReadOnlyList<ClosureInterval> oldCalendar, IReadOnlyList<ClosureInterval> newCalendar, bool cancelled)
    {
        var anchor = a.DeadlinePausedAtUtc ?? now;
        if (a.DeadlineStatus != "KNOWN" || a.DueAtUtc is not DateTime due || due <= anchor) return false;
        var policy = JsonSerializer.Deserialize<ReviewDeadlinePolicy>(a.DeadlinePolicySnapshot!)!;
        var remaining = WorkingTicks(anchor, due, policy, oldCalendar);
        var next = AddWorkingTicks(anchor, remaining, policy, newCalendar);
        if (next == due) return false;
        a.DueAtUtc = next;
        var history = JsonSerializer.Deserialize<List<DeadlineEvent>>(a.DeadlineHistory) ?? [];
        history.Add(new(cancelled ? "CLOSURE_CANCEL" : "CLOSURE", now, actor, due, next, closureId));
        a.DeadlineHistory = JsonSerializer.Serialize(history);
        return true;
    }
    public static DateTime AddWorkingTicks(DateTime utc, long ticks, ReviewDeadlinePolicy p, IReadOnlyList<ClosureInterval>? closed = null)
    {
        if (utc.Kind != DateTimeKind.Utc || ticks < 0) throw new InvalidOperationException("Invalid deadline UTC/ticks.");
        if (ticks == 0) return utc;
        var date = DateOnly.FromDateTime(utc.AddHours(7));
        for (var i = 0; i < 36600; i++, date = date.AddDays(1))
        {
            if (!Working(date, p)) continue;
            foreach (var (start, end) in Segments(date, p, closed))
            {
                var cursor = utc > start ? utc : start;
                if (cursor >= end) continue;
                var available = (end - cursor).Ticks;
                if (ticks <= available) return cursor.AddTicks(ticks);
                ticks -= available;
            }
        }
        throw new InvalidOperationException("Deadline calendar exceeds supported horizon.");
    }
    public static long WorkingTicks(DateTime from, DateTime to, ReviewDeadlinePolicy p, IReadOnlyList<ClosureInterval>? closed = null)
    {
        if (to < from) throw new InvalidOperationException("Resume precedes pause.");
        var last = DateOnly.FromDateTime(to.AddHours(7)); long ticks = 0;
        for (var day = DateOnly.FromDateTime(from.AddHours(7)); day <= last; day = day.AddDays(1))
        {
            if (!Working(day, p)) continue;
            foreach (var segment in Segments(day, p, closed))
            {
                var start = segment.Start < from ? from : segment.Start;
                var end = segment.End > to ? to : segment.End;
                if (end > start) ticks += (end - start).Ticks;
            }
        }
        return ticks;
    }
    public static void Start(ApplicationRecord a, DateTime now, ReviewDeadlinePolicy p)
    {
        if (a.FirstSubmittedAtUtc is not null) return;
        a.FirstSubmittedAtUtc = now;
        if (!p.Enabled) return; // Old/unknown records are never assigned today's policy on resubmission.
        p.Validate();
        a.DeadlinePolicySnapshot = JsonSerializer.Serialize(p);
        a.DeadlinePolicyCode = p.Code; a.DeadlinePolicyVersion = p.Version;
        a.DeadlineCalendarCode = p.CalendarCode; a.DeadlineCalendarVersion = p.CalendarVersion;
        a.DeadlineStatus = p.Applicable ? "KNOWN" : "NOT_APPLICABLE";
        a.DueAtUtc = p.Applicable ? AddWorkingTicks(now, TimeSpan.FromMinutes(p.WorkingMinutes).Ticks, p, a.CalendarClosures) : null;
    }
    private static void Log(ApplicationRecord a, string action, DateTime now, Guid actor, DateTime? before)
    {
        var history = JsonSerializer.Deserialize<List<DeadlineEvent>>(a.DeadlineHistory) ?? [];
        history.Add(new(action, now, actor, before, a.DueAtUtc));
        a.DeadlineHistory = JsonSerializer.Serialize(history);
    }
    public static void Pause(ApplicationRecord a, DateTime now, Guid actor)
    {
        if (a.DeadlineStatus != "KNOWN" || a.DeadlinePausedAtUtc is not null) return;
        var p = JsonSerializer.Deserialize<ReviewDeadlinePolicy>(a.DeadlinePolicySnapshot!);
        if (p?.PauseOnRevision != true) return;
        a.DeadlinePausedAtUtc = now; Log(a, "PAUSE", now, actor, a.DueAtUtc);
    }
    public static void Resume(ApplicationRecord a, DateTime now, Guid actor)
    {
        if (a.DeadlinePausedAtUtc is not DateTime paused) return;
        var p = JsonSerializer.Deserialize<ReviewDeadlinePolicy>(a.DeadlinePolicySnapshot!)!;
        var before = a.DueAtUtc;
        // Preserve the budget left at pause; closed intervals must not be counted twice.
        if (a.DueAtUtc is DateTime due && due > paused)
            a.DueAtUtc = AddWorkingTicks(now, WorkingTicks(paused, due, p, a.CalendarClosures), p, a.CalendarClosures);
        a.DeadlinePausedAtUtc = null; Log(a, "RESUME", now, actor, before);
    }
    public static void CancelPause(ApplicationRecord a, DateTime now, Guid actor)
    {
        if (a.DeadlinePausedAtUtc is null) return;
        a.DeadlinePausedAtUtc = null; Log(a, "CANCEL", now, actor, a.DueAtUtc);
    }
}
