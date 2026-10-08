using System.Text.Json.Nodes;
using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed class ReviewHandlers(IApplicationReviewStore store, TimeProvider clock) :
    IRequestHandler<AssignOfficerCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<AddApplicationCommentCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<RequestRevisionCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<ResubmitApplicationCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<GetOfficerPendingApplicationsPagedQuery, ApplicationPage>,
    IRequestHandler<GetApplicationCommentsQuery, WorkflowResult<CommentDto[]>>,
    IRequestHandler<GetApplicationVersionsQuery, WorkflowResult<VersionDto[]>>,
    IRequestHandler<CompareApplicationVersionsQuery, WorkflowResult<FieldDiffDto[]>>
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static WorkflowResult<ReviewResultDto> Ok(ApplicationRecord a) => WorkflowResult<ReviewResultDto>.Ok(
        new(ApplicationDto.From(a), a.AssignedOfficerId, a.ResubmitCount, a.Versions.Max(x => x.VersionNumber)));
    private static WorkflowResult<ReviewResultDto> Fail(int status, string code, string message) =>
        WorkflowResult<ReviewResultDto>.Fail(status, "application." + code, message);
    private static bool Assigned(ApplicationRecord a, ReviewActor actor) => actor.IsOfficer && a.AssignedOfficerId == actor.Id && a.UserId != actor.Id;
    private static void Transition(ApplicationRecord a, string state, Guid actor, DateTime now, string? reason = null)
    {
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = state, ChangedBy = actor, Reason = reason, CreatedAt = now });
        a.Status = state; a.UpdatedAt = now;
    }
    public Task<WorkflowResult<ReviewResultDto>> Handle(AssignOfficerCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!r.Actor.IsOfficer || a.UserId == r.Actor.Id) return Fail(403, "forbidden", "Bạn không được thẩm định hồ sơ này.");
        if (a.Status != ApplicationStates.Submitted || (a.AssignedOfficerId is not null && a.AssignedOfficerId != r.Actor.Id))
            return Fail(409, "invalid_status", "Hồ sơ không còn chờ tiếp nhận hoặc đã có cán bộ khác thụ lý.");
        if (a.Versions.Count == 0) return Fail(409, "snapshot_missing", "Hồ sơ cũ chưa có snapshot nộp; cần kiểm tra dữ liệu trước khi thụ lý.");
        a.AssignedOfficerId = r.Actor.Id;
        Transition(a, ApplicationStates.UnderReview, r.Actor.Id, Now);
        return Ok(a);
    }, ct);

    public Task<WorkflowResult<ReviewResultDto>> Handle(AddApplicationCommentCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!Assigned(a, r.Actor)) return Fail(403, "forbidden", "Chỉ cán bộ đang thụ lý được bắt lỗi hồ sơ.");
        if (a.Status != ApplicationStates.UnderReview) return Fail(409, "invalid_status", "Chỉ bắt lỗi hồ sơ đang UNDER_REVIEW.");
        var version = a.Versions.MaxBy(x => x.VersionNumber)!;
        if (version.VersionNumber != r.Input.VersionNumber) return Fail(409, "version_conflict", "Phiên bản đã thay đổi. Vui lòng tải lại hồ sơ.");
        var root = JsonNode.Parse(version.SnapshotData)!;
        var validTarget = r.Input.TargetType == "FORM_FIELD"
            ? ApplicationSnapshots.Flatten(root["formData"]).ContainsKey(r.Input.TargetId)
            : Guid.TryParse(r.Input.TargetId, out var checklistId) && a.Checklists.Any(x => x.Id == checklistId);
        if (!validTarget) return Fail(400, "invalid_target", "Ô dữ liệu hoặc mục checklist không có trong phiên bản hiện tại.");
        var now = Now;
        a.Comments.Add(new() { ApplicationId = a.Id, ApplicationVersionId = version.Id, OfficerId = r.Actor.Id,
            TargetType = r.Input.TargetType, TargetId = r.Input.TargetType == "CHECKLIST_ITEM" ? Guid.Parse(r.Input.TargetId).ToString() : r.Input.TargetId,
            FieldLabel = r.Input.FieldLabel.Trim(), CommentText = r.Input.CommentText.Trim(), CreatedAt = now, UpdatedAt = now });
        a.UpdatedAt = now;
        return Ok(a);
    }, ct);

    public Task<WorkflowResult<ReviewResultDto>> Handle(RequestRevisionCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!Assigned(a, r.Actor)) return Fail(403, "forbidden", "Chỉ cán bộ đang thụ lý được yêu cầu sửa.");
        if (a.Status != ApplicationStates.UnderReview) return Fail(409, "invalid_status", "Hồ sơ phải đang UNDER_REVIEW.");
        var version = a.Versions.MaxBy(x => x.VersionNumber)!;
        if (!a.Comments.Any(x => x.ApplicationVersionId == version.Id && x.Status == "OPEN"))
            return Fail(409, "comments_required", "Cần ít nhất một nhận xét OPEN trên phiên bản hiện tại.");
        a.Notes = r.Input.Reason.Trim();
        Transition(a, ApplicationStates.NeedRevision, r.Actor.Id, Now, a.Notes);
        return Ok(a);
    }, ct);

    public Task<WorkflowResult<ReviewResultDto>> Handle(ResubmitApplicationCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (a.UserId != r.UserId) return Fail(404, "not_found", "Không tìm thấy hồ sơ của bạn.");
        if (a.Status != ApplicationStates.NeedRevision) return Fail(409, "invalid_status", "Chỉ được nộp lại hồ sơ NEED_REVISION.");
        var version = a.Versions.MaxBy(x => x.VersionNumber)!;
        if (version.VersionNumber != r.Input.ExpectedVersionNumber) return Fail(409, "version_conflict", "Phiên bản đã thay đổi. Vui lòng tải lại hồ sơ.");
        if (r.Input.Checklists.Length != a.Checklists.Count || !r.Input.Checklists.Select(x => x.Id).ToHashSet().SetEquals(a.Checklists.Select(x => x.Id)))
            return Fail(400, "invalid_checklist", "Phải gửi đủ các mục checklist thuộc hồ sơ, không thêm hoặc bỏ mục.");
        var inputs = r.Input.Checklists.ToDictionary(x => x.Id);
        var missing = a.Checklists.Where(x => x.IsRequired && inputs[x.Id].Status != ChecklistStates.Completed)
            .Select(x => new { x.Id, x.Code, x.Title }).ToArray();
        if (missing.Length > 0) return WorkflowResult<ReviewResultDto>.Fail(422, "application.checklist_incomplete", "Vui lòng hoàn tất checklist bắt buộc.", missing);
        var now = Now;
        a.FormData = r.Input.FormData.GetRawText();
        foreach (var item in a.Checklists)
        {
            var input = inputs[item.Id]; item.Status = input.Status; item.FileUrl = input.FileUrl; item.Note = input.Note; item.UpdatedAt = now;
        }
        a.ResubmitCount++;
        a.SubmittedAt = now;
        a.Versions.Add(ApplicationSnapshots.Capture(a, r.UserId, now));
        foreach (var comment in a.Comments.Where(x => x.Status == "OPEN" && x.ApplicationVersionId == version.Id))
        { comment.Status = "RESOLVED"; comment.UpdatedAt = now; }
        Transition(a, ApplicationStates.Submitted, r.UserId, now, "Công dân nộp lại hồ sơ đã chỉnh sửa.");
        return Ok(a);
    }, ct);

    public Task<ApplicationPage> Handle(GetOfficerPendingApplicationsPagedQuery r, CancellationToken ct) => store.Pending(r.Actor, r.Input, ct);
    public async Task<WorkflowResult<CommentDto[]>> Handle(GetApplicationCommentsQuery r, CancellationToken ct)
    {
        var a = await store.Read(r.Actor, r.Id, ct);
        if (a is null) return WorkflowResult<CommentDto[]>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ trong phạm vi truy cập.");
        if (r.VersionNumber is not null && !a.Versions.Any(x => x.VersionNumber == r.VersionNumber))
            return WorkflowResult<CommentDto[]>.Fail(404, "application.version_not_found", "Không tìm thấy phiên bản.");
        return WorkflowResult<CommentDto[]>.Ok(a.Comments.Select(x => ApplicationSnapshots.ToDto(x, a.Versions))
            .Where(x => (r.VersionNumber is null || x.VersionNumber == r.VersionNumber) && (r.Status is null || x.Status == r.Status))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray());
    }
    public async Task<WorkflowResult<VersionDto[]>> Handle(GetApplicationVersionsQuery r, CancellationToken ct)
    {
        var a = await store.Read(r.Actor, r.Id, ct);
        return a is null ? WorkflowResult<VersionDto[]>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ trong phạm vi truy cập.")
            : WorkflowResult<VersionDto[]>.Ok(a.Versions.OrderBy(x => x.VersionNumber).Select(x => new VersionDto(x.Id, x.VersionNumber,
                x.SubmittedBy, x.SubmittedAt, System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(x.SnapshotData))).ToArray());
    }
    public async Task<WorkflowResult<FieldDiffDto[]>> Handle(CompareApplicationVersionsQuery r, CancellationToken ct)
    {
        var a = await store.Read(r.Actor, r.Id, ct);
        if (a is null) return WorkflowResult<FieldDiffDto[]>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ trong phạm vi truy cập.");
        var from = a.Versions.SingleOrDefault(x => x.VersionNumber == r.FromVersionNumber);
        var to = a.Versions.SingleOrDefault(x => x.VersionNumber == r.ToVersionNumber);
        if (from is null || to is null) return WorkflowResult<FieldDiffDto[]>.Fail(404, "application.version_not_found", "Không tìm thấy phiên bản của hồ sơ.");
        var comments = a.Comments.Where(x => x.ApplicationVersionId == from.Id || x.ApplicationVersionId == to.Id)
            .Select(x => ApplicationSnapshots.ToDto(x, a.Versions)).ToArray();
        return WorkflowResult<FieldDiffDto[]>.Ok(ApplicationSnapshots.Compare(from, to, comments));
    }
}
