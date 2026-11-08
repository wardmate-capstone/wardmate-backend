namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed record ClosureInterval(DateTime StartsAtUtc, DateTime EndsAtUtc);
public sealed class CalendarClosure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? WardCode { get; set; }
    public DateOnly ClosureDate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public string Reason { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    // Display snapshots (2026-11-08); null on rows created earlier and WardName null for national days.
    public string? WardName { get; set; }
    public string? CreatedByName { get; set; }
    public string? CancelledByName { get; set; }
    public ClosureInterval Interval() => new(EffectiveFromUtc, DateTime.SpecifyKind(ClosureDate.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc));
}
