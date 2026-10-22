namespace WardMate.Services.ApplicationWorkflow.Application;

// Internal HTTP contracts; no cross-service database access. Email context requires a service key.
public sealed record ProcedureDay(Guid ProcedureId, string WardCode, int ReceivedCount,
    int ReadyCount, int RevisionRequestedCount, int ResubmittedCount, int CancelledCount,
    int LegacyApprovedCount, int LegacyRejectedCount);
public sealed record OfficerDay(Guid OfficerId, Guid ProcedureId, string WardCode, int ReadyCount,
    int RevisionRequestedCount, int DurationSampleCount, double TotalProcessingMinutes);
public sealed record AnalyticsDay(int ContractVersion, DateOnly Date, DateTime GeneratedAtUtc,
    string TimeZone, ProcedureDay[] Procedures, OfficerDay[] Officers);
public interface IWorkflowAnalyticsSource
{
    Task<NotificationEmailContext?> ReadEmailContext(Guid applicationId, Guid eventId, CancellationToken ct);
    Task<DeadlineFacts> ReadDeadlines(string ward, DateOnly from, DateOnly to, Guid? procedureId, CancellationToken ct);
    Task<AnalyticsDay> ReadDay(DateOnly date, CancellationToken ct);
}

public sealed record NotificationEmailContext(Guid ApplicantId, string ApplicationCode, string ProcedureTitle, string Status, string? Notes, string? ReceiptNumber, DateTime? AppointmentDate, DateTime UpdatedAtUtc);
