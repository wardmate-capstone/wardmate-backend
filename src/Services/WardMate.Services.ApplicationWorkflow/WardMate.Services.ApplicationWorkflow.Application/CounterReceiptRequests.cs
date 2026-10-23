using FluentValidation;
using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;
namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record OfficialReceiptInput(bool PaperDossierVerified, string? Notes, DateTimeOffset? AppointmentDate);
public sealed record OfficialReceiptDto(Guid ApplicationId, string Status, string? ReceiptNumber, DateTime? ReceivedAt, DateTime? AppointmentDate);
public sealed record ReceiveApplicationCommand(ReviewActor Actor, Guid Id, OfficialReceiptInput Input) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed class ReceiveApplicationValidator : AbstractValidator<ReceiveApplicationCommand>
{
    public ReceiveApplicationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input.PaperDossierVerified).Equal(true).WithMessage("Phải xác nhận đã đối chiếu hồ sơ giấy.");
        RuleFor(x => x.Input.Notes).MaximumLength(4000);
    }
}
public sealed class ReceiveApplicationHandler(IApplicationReviewStore store, TimeProvider clock) : IRequestHandler<ReceiveApplicationCommand, WorkflowResult<ReviewResultDto>>
{
    public Task<WorkflowResult<ReviewResultDto>> Handle(ReceiveApplicationCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!r.Actor.CanMutate || string.IsNullOrWhiteSpace(r.Actor.WardCode) || a.WardCode != r.Actor.WardCode || a.AssignedOfficerId != r.Actor.Id || a.UserId == r.Actor.Id)
            return WorkflowResult<ReviewResultDto>.Fail(403, "application.forbidden", "Chỉ cán bộ thụ lý đúng phường được tiếp nhận tại quầy.");
        if (a.Status != ApplicationStates.DocumentsReady) return WorkflowResult<ReviewResultDto>.Fail(409, "application.invalid_status", "Hồ sơ phải ở trạng thái DOCUMENTS_READY.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (!r.Input.PaperDossierVerified || r.Input.AppointmentDate?.UtcDateTime < now)
            return WorkflowResult<ReviewResultDto>.Fail(400, "application.receipt_invalid", "Cần đối chiếu giấy tờ và ngày hẹn không được trong quá khứ.");
        a.ReceiptNumber = $"BN-{now:yyyyMMdd}-{a.Id:N}";
        a.ReceivedAt = a.UpdatedAt = now; a.AppointmentDate = r.Input.AppointmentDate?.UtcDateTime;
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.OfficiallyReceived, ChangedBy = r.Actor.Id, Reason = r.Input.Notes, CreatedAt = now });
        a.Status = ApplicationStates.OfficiallyReceived;
        return WorkflowResult<ReviewResultDto>.Ok(new(ApplicationDto.From(a), a.AssignedOfficerId, a.ResubmitCount, a.ResubmitCount + 1));
    }, ct);
}
public sealed record CancelApplicationCommand(Guid UserId, Guid Id) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed class CancelApplicationHandler(IApplicationStore store, TimeProvider clock) : IRequestHandler<CancelApplicationCommand, WorkflowResult<ApplicationDto>>
{
    public Task<WorkflowResult<ApplicationDto>> Handle(CancelApplicationCommand r, CancellationToken ct) => store.WithLock(r.UserId, r.Id, a =>
    {
        if (a.Status is not (ApplicationStates.Draft or ApplicationStates.NeedRevision))
            return Task.FromResult(WorkflowResult<ApplicationDto>.Fail(409, "application.invalid_status", "Chỉ hủy hồ sơ DRAFT hoặc NEED_REVISION."));
        var now = clock.GetUtcNow().UtcDateTime;
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.Cancelled, ChangedBy = r.UserId, Reason = "Công dân hủy hồ sơ tiền kiểm.", CreatedAt = now });
        ReviewDeadline.CancelPause(a, now, r.UserId);
        a.Status = ApplicationStates.Cancelled; a.UpdatedAt = now;
        return Task.FromResult(WorkflowResult<ApplicationDto>.Ok(ApplicationDto.ForApplicant(a)));
    }, ct);
}

