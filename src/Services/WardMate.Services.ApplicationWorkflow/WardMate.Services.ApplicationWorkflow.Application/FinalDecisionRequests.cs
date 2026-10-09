using FluentValidation;
using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ApproveApplicationCommand(ReviewActor Actor, Guid Id) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record RejectApplicationCommand(ReviewActor Actor, Guid Id, string Reason) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed class FinalDecisionHandlers(IApplicationReviewStore store, TimeProvider clock) :
    IRequestHandler<ApproveApplicationCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<RejectApplicationCommand, WorkflowResult<ReviewResultDto>>
{
    public Task<WorkflowResult<ReviewResultDto>> Handle(ApproveApplicationCommand r, CancellationToken ct) => Decide(r.Actor, r.Id, true, null, ct);
    public Task<WorkflowResult<ReviewResultDto>> Handle(RejectApplicationCommand r, CancellationToken ct) => Decide(r.Actor, r.Id, false, r.Reason.Trim(), ct);
    private Task<WorkflowResult<ReviewResultDto>> Decide(ReviewActor actor, Guid id, bool approve, string? reason, CancellationToken ct) => store.Mutate(id, a =>
    {
        if (!actor.IsOfficer || actor.WardCode is null || a.WardCode != actor.WardCode || a.AssignedOfficerId != actor.Id || a.UserId == actor.Id)
            return WorkflowResult<ReviewResultDto>.Fail(403, "application.forbidden", "Chỉ cán bộ thụ lý thuộc phường tiếp nhận được quyết định hồ sơ.");
        if (a.Status != ApplicationStates.UnderReview)
            return WorkflowResult<ReviewResultDto>.Fail(409, "application.invalid_status", "Chỉ quyết định hồ sơ đang UNDER_REVIEW.");
        var version = a.Versions.MaxBy(x => x.VersionNumber);
        if (version is null) return WorkflowResult<ReviewResultDto>.Fail(409, "application.snapshot_missing", "Hồ sơ chưa có phiên bản nộp.");
        if (approve && a.Comments.Any(x => x.ApplicationVersionId == version.Id && x.Status == "OPEN"))
            return WorkflowResult<ReviewResultDto>.Fail(409, "application.open_comments", "Hồ sơ còn lỗi OPEN; cần yêu cầu công dân sửa trước khi duyệt.");
        var now = clock.GetUtcNow().UtcDateTime;
        var state = approve ? ApplicationStates.Approved : ApplicationStates.Rejected;
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = state, ChangedBy = actor.Id, Reason = reason, CreatedAt = now });
        a.Status = state; a.UpdatedAt = now; a.Notes = reason;
        if (approve) a.ApprovedAt = now;
        return WorkflowResult<ReviewResultDto>.Ok(new(ApplicationDto.From(a), a.AssignedOfficerId, a.ResubmitCount, version.VersionNumber));
    }, ct);
}
public sealed class RejectApplicationValidator : AbstractValidator<RejectApplicationCommand>
{
    public RejectApplicationValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Phải nhập lý do từ chối chính thức.")
            .MaximumLength(4000).WithMessage("Lý do tối đa 4000 ký tự.");
    }
}
