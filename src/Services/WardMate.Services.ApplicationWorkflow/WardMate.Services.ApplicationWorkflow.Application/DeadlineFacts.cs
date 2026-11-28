namespace WardMate.Services.ApplicationWorkflow.Application;
public sealed record DeadlineFact(Guid Id, string Status, DateTime? FirstSubmittedAtUtc, DateTime? DueAtUtc,
    DateTime? DocumentsReadyAtUtc, string DeadlineStatus, DateTime? PausedAtUtc, Guid ProcedureId = default, Guid? AssignedOfficerId = null);
public sealed record DeadlineFacts(int ContractVersion, DateTime AsOfUtc, DeadlineFact[] Items, ProcedureName[]? Procedures = null);
// ANA-11: tên thủ tục theo bản chụp trong hồ sơ của phường, có cả thủ tục đã ẩn khỏi danh mục công khai.
public sealed record ProcedureName(Guid ProcedureId, string Title);
