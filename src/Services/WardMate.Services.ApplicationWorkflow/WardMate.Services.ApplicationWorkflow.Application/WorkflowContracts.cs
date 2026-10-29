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
public sealed record CreateApplicationInput(Guid ProcedureId, JsonElement FormData, string? CaseCode = null, string? WardCode = null);
public sealed record UpdateChecklistInput(string Status, string? FileUrl = null, string? Note = null);
public sealed record ChecklistDto(Guid Id, string Code, string Title, bool IsRequired, string Status,
    string? FileUrl, string? Note, DateTime CreatedAt, DateTime UpdatedAt, Guid? FormTemplateId = null,
    int SortOrder = 0, string? CaseCode = null, string? SubmissionType = null,
    string? DocumentCopyType = null, int Quantity = 1, string? ConditionNote = null);
public sealed record AssignmentHistoryDto(Guid Id, string Action, Guid? FromOfficerId, Guid? ToOfficerId, Guid ActorId, string? Reason, DateTime CreatedAt);
public sealed record HistoryDto(Guid Id, string? FromStatus, string ToStatus, Guid ChangedBy, string? Reason, DateTime CreatedAt);
public sealed record ApplicationDto(Guid Id, string? ApplicationCode, Guid UserId, Guid ProcedureId,
    string ProcedureTitle, string? CaseCode, string Status, JsonElement FormData, DateTime? SubmittedAt,
    DateTime CreatedAt, DateTime UpdatedAt, ChecklistDto[] Checklists, HistoryDto[] History, Guid? AssignedOfficerId = null, int ResubmitCount = 0, int CurrentVersionNumber = 0, string? Notes = null, DateTime? DocumentsReadyAt = null, string? WardCode = null, string? ReceiptNumber = null, DateTime? ReceivedAt = null, DateTime? AppointmentDate = null, DateTime? FirstSubmittedAtUtc = null, DateTime? DueAtUtc = null, DateTime? DocumentsReadyAtUtc = null, string DeadlineStatus = "UNKNOWN", string? DeadlinePolicyCode = null, string? DeadlinePolicyVersion = null, string? DeadlineCalendarCode = null, string? DeadlineCalendarVersion = null, DateTime? DeadlinePausedAtUtc = null, DeadlineEvent[]? DeadlineHistory = null, int? CategoryId = null, string? CategoryName = null, string? AssignedOfficerName = null, DateTime? AssignedAt = null, long AssignmentVersion = 0, AssignmentHistoryDto[]? AssignmentHistory = null, string? ApplicantName = null)
{
    // Applicants see who handles the file but not internal assignment reasons or the officer timeline.
    public static ApplicationDto ForApplicant(ApplicationRecord a)
    {
        var dto = From(a);
        return dto with
        {
            AssignmentHistory = null,
            // Also protect legacy rows that duplicated assignment reasons in status history.
            // Classify by transition, without depending on assignment-history rows being loaded.
            History = dto.History.Select(h =>
                (h.FromStatus == ApplicationStates.Submitted && h.ToStatus == ApplicationStates.UnderReview)
                || (h.FromStatus == ApplicationStates.UnderReview && h.ToStatus == ApplicationStates.Submitted)
                    ? h with { Reason = null } : h).ToArray()
        };
    }
    public static ApplicationDto From(ApplicationRecord a) => new(a.Id, a.ApplicationCode, a.UserId, a.ProcedureId,
        a.ProcedureTitle, a.CaseCode, a.Status, JsonSerializer.Deserialize<JsonElement>(a.FormData),
        a.SubmittedAt, a.CreatedAt, a.UpdatedAt,
        a.Checklists.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).Select(x => new ChecklistDto(x.Id, x.Code, x.Title, x.IsRequired,
            x.Status, x.FileUrl, x.Note, x.CreatedAt, x.UpdatedAt, x.FormTemplateId,
            x.SortOrder, x.CaseCode, x.SubmissionType, x.DocumentCopyType, x.Quantity, x.ConditionNote)).ToArray(),
        a.History.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new HistoryDto(x.Id,
            x.FromStatus, x.ToStatus, x.ChangedBy, x.Reason, x.CreatedAt)).ToArray(),
        a.AssignedOfficerId, a.ResubmitCount, a.SubmittedAt is null ? 0 : a.ResubmitCount + 1, a.Notes, a.DocumentsReadyAt, a.WardCode, a.ReceiptNumber, a.ReceivedAt, a.AppointmentDate, a.FirstSubmittedAtUtc, a.DueAtUtc, a.DocumentsReadyAt, a.DeadlineStatus, a.DeadlinePolicyCode, a.DeadlinePolicyVersion, a.DeadlineCalendarCode, a.DeadlineCalendarVersion, a.DeadlinePausedAtUtc, JsonSerializer.Deserialize<DeadlineEvent[]>(a.DeadlineHistory), a.CategoryId, a.CategoryName, a.AssignedOfficerName, a.AssignedAt, a.AssignmentVersion, a.AssignmentHistory.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new AssignmentHistoryDto(x.Id, x.Action, x.FromOfficerId, x.ToOfficerId, x.ActorId, x.Reason, x.CreatedAt)).ToArray(), a.ApplicantName);
}
public sealed record ApplicationSummaryDto(Guid Id, string? ApplicationCode, Guid ProcedureId,
    string ProcedureTitle, string Status, DateTime CreatedAt, DateTime? SubmittedAt, string? WardCode = null, Guid? AssignedOfficerId = null, DateTime? FirstSubmittedAtUtc = null, DateTime? DueAtUtc = null, DateTime? DocumentsReadyAtUtc = null, string DeadlineStatus = "UNKNOWN", DateTime? DeadlinePausedAtUtc = null, int? CategoryId = null, string? CategoryName = null, string? AssignedOfficerName = null, DateTime? AssignedAt = null, int ResubmitCount = 0, long AssignmentVersion = 0, string? ApplicantName = null);
public sealed record ApplicationPage(ApplicationSummaryDto[] Items, int Page, int PageSize, int Total);

// Integration contract, not a reference to another service's domain or database.
public sealed record ProcedureSnapshot(Guid Id, string Title, bool IsActive,
    ProcedureContent? ContentPayload, ProcedureChecklist[]? ChecklistSchema, ProcedureForm[]? FormDefinitions = null, int? CategoryId = null, string? CategoryName = null);
public sealed record ProcedureForm(Guid? FormTemplateId, string? CaseCode, string FormCode, string FormName, bool IsMandatory = true, int Quantity = 1);
public sealed record ProcedureContent(ProcedureCase[]? Cases);
public sealed record ProcedureCase(string CaseCode);
public sealed record ProcedureChecklist(string ChecklistId, string ItemName, bool? IsMandatory, string? CaseCode,
    string? SubmissionType = null, string? DocumentCopyType = null, int Quantity = 1, string? ConditionNote = null);

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




