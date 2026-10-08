using System.Text.Json;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record WorkflowError(int Status, string Code, string Message, object? Details = null);
public sealed record WorkflowResult<T>(T? Value, WorkflowError? Error)
{
    public static WorkflowResult<T> Ok(T value) => new(value, null);
    public static WorkflowResult<T> Fail(int status, string code, string message, object? details = null) =>
        new(default, new(status, code, message, details));
}
public sealed record CreateApplicationInput(Guid ProcedureId, JsonElement FormData, string? CaseCode = null);
public sealed record UpdateChecklistInput(string Status, string? FileUrl = null, string? Note = null);
public sealed record ChecklistDto(Guid Id, string Code, string Title, bool IsRequired, string Status,
    string? FileUrl, string? Note, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record HistoryDto(Guid Id, string? FromStatus, string ToStatus, Guid ChangedBy, string? Reason, DateTime CreatedAt);
public sealed record ApplicationDto(Guid Id, string? ApplicationCode, Guid UserId, Guid ProcedureId,
    string ProcedureTitle, string? CaseCode, string Status, JsonElement FormData, DateTime? SubmittedAt,
    DateTime CreatedAt, DateTime UpdatedAt, ChecklistDto[] Checklists, HistoryDto[] History, Guid? AssignedOfficerId = null, int ResubmitCount = 0, int CurrentVersionNumber = 0, string? Notes = null, DateTime? ApprovedAt = null)
{
    public static ApplicationDto From(ApplicationRecord a) => new(a.Id, a.ApplicationCode, a.UserId, a.ProcedureId,
        a.ProcedureTitle, a.CaseCode, a.Status, JsonSerializer.Deserialize<JsonElement>(a.FormData),
        a.SubmittedAt, a.CreatedAt, a.UpdatedAt,
        a.Checklists.OrderBy(x => x.Code).Select(x => new ChecklistDto(x.Id, x.Code, x.Title, x.IsRequired,
            x.Status, x.FileUrl, x.Note, x.CreatedAt, x.UpdatedAt)).ToArray(),
        a.History.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new HistoryDto(x.Id,
            x.FromStatus, x.ToStatus, x.ChangedBy, x.Reason, x.CreatedAt)).ToArray(),
        a.AssignedOfficerId, a.ResubmitCount, a.Status == ApplicationStates.Draft ? 0 : a.ResubmitCount + 1, a.Notes, a.ApprovedAt);
}
public sealed record ApplicationSummaryDto(Guid Id, string? ApplicationCode, Guid ProcedureId,
    string ProcedureTitle, string Status, DateTime CreatedAt, DateTime? SubmittedAt);
public sealed record ApplicationPage(ApplicationSummaryDto[] Items, int Page, int PageSize, int Total);

// Integration contract, not a reference to another service's domain or database.
public sealed record ProcedureSnapshot(Guid Id, string Title, bool IsActive,
    ProcedureContent? ContentPayload, ProcedureChecklist[]? ChecklistSchema);
public sealed record ProcedureContent(ProcedureCase[]? Cases);
public sealed record ProcedureCase(string CaseCode);
public sealed record ProcedureChecklist(string ChecklistId, string ItemName, bool? IsMandatory, string? CaseCode);

public interface IProcedureCatalogClient
{
    Task<WorkflowResult<ProcedureSnapshot>> Get(Guid procedureId, CancellationToken ct);
}
public interface IApplicationStore
{
    Task Add(ApplicationRecord application, CancellationToken ct);
    Task<ApplicationRecord?> Get(Guid userId, Guid applicationId, CancellationToken ct);
    Task<ApplicationPage> List(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<WorkflowResult<ApplicationDto>> WithLock(Guid userId, Guid applicationId,
        Func<ApplicationRecord, Task<WorkflowResult<ApplicationDto>>> operation, CancellationToken ct);
    Task<string> NextCode(DateTime now, CancellationToken ct);
}

