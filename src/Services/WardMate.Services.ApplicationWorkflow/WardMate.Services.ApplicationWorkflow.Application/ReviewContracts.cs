using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ReviewActor(Guid Id, bool IsOfficer, string? WardCode = null, bool IsManager = false, int[]? AssignedCategories = null, string? FullName = null)
{
    public bool CanReadWard => IsOfficer || IsManager;
    public bool CanMutate => IsOfficer && !IsManager;
}
public sealed record AddCommentInput(int VersionNumber, string TargetType, string TargetId, string FieldLabel, string CommentText);
public sealed record RequestRevisionInput(string Reason);
public sealed record ResubmitChecklistInput(Guid Id, string Status, string? FileUrl, string? Note);
public sealed record ResubmitInput(int ExpectedVersionNumber, JsonElement FormData, ResubmitChecklistInput[] Checklists);
public sealed record ReviewResultDto(ApplicationDto Application, Guid? AssignedOfficerId, int ResubmitCount, int CurrentVersionNumber);
public sealed record VersionDto(Guid Id, int VersionNumber, Guid SubmittedBy, DateTime SubmittedAt, JsonElement SnapshotData);
public sealed record CommentDto(Guid Id, Guid ApplicationVersionId, int VersionNumber, Guid OfficerId,
    string TargetType, string TargetId, string FieldLabel, string CommentText, string Status, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record FieldDiffDto(string FieldKey, string FieldLabel, JsonNode? OldValue, JsonNode? NewValue,
    bool HasChanged, CommentDto[] AssociatedComments, bool OldExists, bool NewExists);
public sealed record OfficerPendingInput(string? Status = null, Guid? ProcedureId = null,
    DateTimeOffset? SubmittedFrom = null, DateTimeOffset? SubmittedTo = null, int Page = 1, int PageSize = 20, string? CategoryIds = null, string? Scope = null, string? Assignee = null, bool? Overdue = null, string? Sort = null);

// Dùng cho GET /api/v1/officer/applications — trả toàn bộ hồ sơ phường (kể cả đã duyệt/từ chối)
public sealed record OfficerListInput(
    string? Status = null,
    Guid? ProcedureId = null,
    string? ApplicationCode = null,
    DateTimeOffset? SubmittedFrom = null,
    DateTimeOffset? SubmittedTo = null,
    int Page = 1,
    int PageSize = 20, string? CategoryIds = null, string? Scope = null, string? Assignee = null, bool? Overdue = null, string? Sort = null);

// Manager workload board: officers absent from Officers have no active file.
public sealed record OfficerWorkloadDto(Guid OfficerId, int ActiveCount, int OverdueCount);
public sealed record AssignmentSummaryDto(int UnassignedSubmitted, int Overdue, OfficerWorkloadDto[] Officers);
// One officer action from status history (STATUS) or assignment history (ASSIGNMENT).
public sealed record OfficerActivityInput(int Page = 1, int PageSize = 20, DateTimeOffset? From = null, DateTimeOffset? To = null, string? Action = null, Guid? OfficerId = null);
public sealed record OfficerActivityDto(Guid Id, string Source, string Action, Guid ApplicationId, string? ApplicationCode,
    string ProcedureTitle, string? ApplicantName, string? Reason, DateTime CreatedAt);
public sealed record OfficerActivityPage(OfficerActivityDto[] Items, int Page, int PageSize, int Total);

public sealed record AssignOfficerCommand(ReviewActor Actor, Guid Id) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record AddApplicationCommentCommand(ReviewActor Actor, Guid Id, AddCommentInput Input) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record RequestRevisionCommand(ReviewActor Actor, Guid Id, RequestRevisionInput Input) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record ResubmitApplicationCommand(Guid UserId, Guid Id, ResubmitInput Input, string? ApplicantName = null) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record GetOfficerPendingApplicationsPagedQuery(ReviewActor Actor, OfficerPendingInput Input) : IRequest<ApplicationPage>;
public sealed record GetOfficerApplicationsPagedQuery(ReviewActor Actor, OfficerListInput Input) : IRequest<ApplicationPage>;
public sealed record GetApplicationCommentsQuery(ReviewActor Actor, Guid Id, int? VersionNumber = null, string? Status = null) : IRequest<WorkflowResult<CommentDto[]>>;
public sealed record GetApplicationVersionsQuery(ReviewActor Actor, Guid Id) : IRequest<WorkflowResult<VersionDto[]>>;
public sealed record CompareApplicationVersionsQuery(ReviewActor Actor, Guid Id, int FromVersionNumber, int ToVersionNumber) : IRequest<WorkflowResult<FieldDiffDto[]>>;

public interface IApplicationReviewStore
{
    Task<WorkflowResult<ReviewResultDto>> Mutate(Guid id, Func<ApplicationRecord, WorkflowResult<ReviewResultDto>> action, CancellationToken ct);
    Task<ApplicationRecord?> Read(ReviewActor actor, Guid id, CancellationToken ct);
    Task<ApplicationPage> ListAll(ReviewActor actor, OfficerListInput input, CancellationToken ct);
    Task<ApplicationPage> Pending(ReviewActor actor, OfficerPendingInput input, CancellationToken ct);
    Task<AssignmentSummaryDto> AssignmentSummary(ReviewActor actor, CancellationToken ct);
    Task<OfficerActivityPage> Activity(ReviewActor actor, Guid officerId, OfficerActivityInput input, CancellationToken ct);

}

public static class ApplicationSnapshots
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ApplicationVersion Capture(ApplicationRecord a, Guid actor, DateTime now) => new()
    {
        ApplicationId = a.Id, VersionNumber = a.ResubmitCount + 1, SubmittedBy = actor, SubmittedAt = now,
        SnapshotData = JsonSerializer.Serialize(new
        {
            a.Id, a.ApplicationCode, a.WardCode, a.UserId, a.ProcedureId, a.ProcedureTitle, a.CaseCode,
            formData = JsonSerializer.Deserialize<JsonElement>(a.FormData),
            checklists = a.Checklists.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new { x.Id, x.Code, x.Title, x.IsRequired, x.Status, x.FileUrl, x.Note, x.FormTemplateId,
                x.SortOrder, x.CaseCode, x.SubmissionType, x.DocumentCopyType, x.Quantity, x.ConditionNote })
        }, Options)
    };

    // RFC 6901 paths avoid ambiguity between nested keys, array indexes and literal dots/slashes.
    public static Dictionary<string, JsonNode?> Flatten(JsonNode? node, string path = "/formData")
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        Visit(node, path, result);
        return result;
    }
    private static void Visit(JsonNode? node, string path, Dictionary<string, JsonNode?> result)
    {
        if (node is JsonObject obj && obj.Count > 0)
            foreach (var pair in obj) Visit(pair.Value, path + "/" + pair.Key.Replace("~", "~0").Replace("/", "~1"), result);
        else if (node is JsonArray array && array.Count > 0)
            for (var i = 0; i < array.Count; i++) Visit(array[i], path + "/" + i, result);
        else result[path] = node;
    }
    public static Dictionary<string, JsonNode?> Fields(string snapshot)
    {
        var root = JsonNode.Parse(snapshot)!;
        var fields = Flatten(root["formData"]);
        foreach (var item in root["checklists"]!.AsArray())
        {
            var path = "/checklists/" + item!["id"]!.GetValue<string>();
            foreach (var pair in Flatten(item, path)) fields[pair.Key] = pair.Value;
        }
        return fields;
    }
    public static CommentDto ToDto(ApplicationComment c, IEnumerable<ApplicationVersion> versions) => new(c.Id,
        c.ApplicationVersionId, versions.Single(x => x.Id == c.ApplicationVersionId).VersionNumber, c.OfficerId,
        c.TargetType, c.TargetId, c.FieldLabel, c.CommentText, c.Status, c.CreatedAt, c.UpdatedAt);
    public static FieldDiffDto[] Compare(ApplicationVersion from, ApplicationVersion to, CommentDto[] comments)
    {
        var oldFields = Fields(from.SnapshotData); var newFields = Fields(to.SnapshotData);
        return oldFields.Keys.Union(newFields.Keys).Order(StringComparer.Ordinal).Select(key =>
        {
            var oldExists = oldFields.TryGetValue(key, out var oldValue);
            var newExists = newFields.TryGetValue(key, out var newValue);
            var related = comments.Where(c => c.TargetType == "FORM_FIELD" ? c.TargetId == key
                : key.StartsWith("/checklists/" + c.TargetId + "/", StringComparison.Ordinal)).ToArray();
            return new FieldDiffDto(key, related.FirstOrDefault()?.FieldLabel ?? key, oldValue, newValue,
                oldExists != newExists || !JsonNode.DeepEquals(oldValue, newValue), related, oldExists, newExists);
        }).Where(x => x.HasChanged).ToArray();
    }
}

