namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record DeliveryQuery(string? State = null, string? Channel = null, Guid? NotificationId = null, string? ExternalMessageId = null, int Page = 1, int PageSize = 20);
public sealed record DeliveryAdminDto(Guid Id, Guid NotificationId, string Channel, string State, int Attempts,
    DateTime NextAttemptAt, string? ErrorCode, DateTime? SentAt, string? Provider, string? ExternalMessageId,
    bool? IsSandbox, Guid Revision, DateTime UpdatedAt, string ProviderRequestId);
public sealed record DeliveryPage(DeliveryAdminDto[] Items, int Page, int PageSize, int Total);
public sealed record DeliveryReplayInput(Guid ExpectedRevision, string Reason, bool AcknowledgeDuplicateRisk);
public sealed record DeliveryAuditDto(Guid Id, Guid ActorId, string Action, string BeforeState, string AfterState, int BeforeAttempts,
    Guid BeforeRevision, Guid AfterRevision, string Reason, string? Reference, string? BeforeMessageId, string? AfterMessageId, DateTime CreatedAt);
public interface IDeliveryAdministration
{
    Task<DeliveryPage> List(DeliveryQuery query, CancellationToken ct);
    Task<DeliveryAdminDto> Get(Guid id, CancellationToken ct);
    Task<DeliveryAuditDto[]> Audit(Guid id, int page, int pageSize, CancellationToken ct);
    Task<DeliveryAdminDto> Replay(Guid actor, Guid id, DeliveryReplayInput input, CancellationToken ct);
}
