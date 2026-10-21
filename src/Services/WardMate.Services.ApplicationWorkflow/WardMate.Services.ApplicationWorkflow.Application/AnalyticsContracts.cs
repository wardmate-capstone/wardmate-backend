namespace WardMate.Services.ApplicationWorkflow.Application;

// Versioned HTTP contract; no cross-service database access or applicant data.
public sealed record ProcedureDay(Guid ProcedureId, string WardCode, int ReceivedCount,
    int ReadyCount, int RevisionRequestedCount, int ResubmittedCount, int CancelledCount,
    int LegacyApprovedCount, int LegacyRejectedCount);
public sealed record OfficerDay(Guid OfficerId, Guid ProcedureId, string WardCode, int ReadyCount,
    int RevisionRequestedCount, int DurationSampleCount, double TotalProcessingMinutes);
public sealed record AnalyticsDay(int ContractVersion, DateOnly Date, DateTime GeneratedAtUtc,
    string TimeZone, ProcedureDay[] Procedures, OfficerDay[] Officers);
public interface IWorkflowAnalyticsSource
{
    Task<AnalyticsDay> ReadDay(DateOnly date, CancellationToken ct);
}
