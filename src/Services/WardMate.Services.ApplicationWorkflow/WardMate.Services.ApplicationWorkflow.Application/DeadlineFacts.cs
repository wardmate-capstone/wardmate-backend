namespace WardMate.Services.ApplicationWorkflow.Application;
public sealed record DeadlineFact(Guid Id, string Status, DateTime? FirstSubmittedAtUtc, DateTime? DueAtUtc,
    DateTime? DocumentsReadyAtUtc, string DeadlineStatus, DateTime? PausedAtUtc, Guid ProcedureId = default, Guid? AssignedOfficerId = null);
public sealed record DeadlineFacts(int ContractVersion, DateTime AsOfUtc, DeadlineFact[] Items);
