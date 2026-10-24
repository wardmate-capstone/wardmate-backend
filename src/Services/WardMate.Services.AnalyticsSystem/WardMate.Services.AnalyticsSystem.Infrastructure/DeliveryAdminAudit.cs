namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class DeliveryAdminAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string BeforeState { get; set; } = "";
    public string AfterState { get; set; } = "";
    public int BeforeAttempts { get; set; }
    public Guid BeforeRevision { get; set; }
    public Guid AfterRevision { get; set; }
    public string Reason { get; set; } = "";
    public string? Reference { get; set; }
    public string? BeforeMessageId { get; set; }
    public string? AfterMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}
