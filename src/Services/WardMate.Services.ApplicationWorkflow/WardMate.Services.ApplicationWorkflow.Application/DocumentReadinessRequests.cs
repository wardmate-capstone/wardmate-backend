using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ConfirmDocumentReadinessCommand(ReviewActor Actor, Guid Id) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed class ConfirmDocumentReadinessHandler(IApplicationReviewStore store, TimeProvider clock)
    : IRequestHandler<ConfirmDocumentReadinessCommand, WorkflowResult<ReviewResultDto>>
{
    public Task<WorkflowResult<ReviewResultDto>> Handle(ConfirmDocumentReadinessCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        var actor = r.Actor;
        if (!actor.CanMutate || string.IsNullOrWhiteSpace(actor.WardCode) || a.WardCode != actor.WardCode || a.AssignedOfficerId != actor.Id || a.UserId == actor.Id)
            return WorkflowResult<ReviewResultDto>.Fail(403, "application.forbidden", "Chỉ cán bộ được phân công đúng phường được xác nhận giấy tờ; không được tự xác nhận hồ sơ của mình.");
        if (a.Status != ApplicationStates.UnderReview)
            return WorkflowResult<ReviewResultDto>.Fail(409, "application.invalid_status", "Chỉ xác nhận giấy tờ của hồ sơ đang UNDER_REVIEW.");
        var version = a.Versions.MaxBy(x => x.VersionNumber);
        if (version is null) return WorkflowResult<ReviewResultDto>.Fail(409, "application.snapshot_missing", "Hồ sơ chưa có phiên bản nộp.");
        if (a.Comments.Any(x => x.ApplicationVersionId == version.Id && x.Status == "OPEN"))
            return WorkflowResult<ReviewResultDto>.Fail(409, "application.open_comments", "Hồ sơ còn nhận xét OPEN; cần yêu cầu công dân bổ sung và kiểm tra lại.");
        if (a.Checklists.Any(x => x.IsRequired && x.Status != ChecklistStates.Completed))
            return WorkflowResult<ReviewResultDto>.Fail(409, "application.checklist_incomplete", "Chưa hoàn thành đủ các mục checklist bắt buộc.");
        var now = clock.GetUtcNow().UtcDateTime;
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.DocumentsReady,
            ChangedBy = actor.Id, Reason = "Giấy tờ đã sẵn sàng để người dân mang đến phường; không phải quyết định giải quyết thủ tục.", CreatedAt = now });
        a.Status = ApplicationStates.DocumentsReady;
        a.DocumentsReadyAt = now;
        a.UpdatedAt = now;
        return WorkflowResult<ReviewResultDto>.Ok(new(ApplicationDto.From(a), a.AssignedOfficerId, a.ResubmitCount, version.VersionNumber));
    }, ct);
}
