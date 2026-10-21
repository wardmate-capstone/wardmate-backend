namespace WardMate.Services.ApplicationWorkflow.Application;
public sealed record DeadlineFact(Guid Id, string Status, DateTime? FirstSubmittedAtUtc, DateTime? DueAtUtc,
    DateTime? DocumentsReadyAtUtc, string DeadlineStatus, DateTime? PausedAtUtc);
public sealed record DeadlineFacts(int ContractVersion, DateTime AsOfUtc, DeadlineFact[] Items);
