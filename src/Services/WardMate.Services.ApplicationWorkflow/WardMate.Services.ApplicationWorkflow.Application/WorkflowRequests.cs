using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record CreateApplicationCommand(Guid UserId, CreateApplicationInput Input) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record GetApplicationQuery(Guid UserId, Guid Id) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record ListApplicationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<ApplicationPage>;
public sealed record UpdateChecklistCommand(Guid UserId, Guid Id, Guid ChecklistId, UpdateChecklistInput Input) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record SubmitApplicationCommand(Guid UserId, Guid Id) : IRequest<WorkflowResult<ApplicationDto>>;

public sealed class WorkflowHandlers(IApplicationStore store, IProcedureCatalogClient catalog, TimeProvider clock) :
    IRequestHandler<CreateApplicationCommand, WorkflowResult<ApplicationDto>>,
    IRequestHandler<GetApplicationQuery, WorkflowResult<ApplicationDto>>,
    IRequestHandler<ListApplicationsQuery, ApplicationPage>,
    IRequestHandler<UpdateChecklistCommand, WorkflowResult<ApplicationDto>>,
    IRequestHandler<SubmitApplicationCommand, WorkflowResult<ApplicationDto>>
{
    public async Task<WorkflowResult<ApplicationDto>> Handle(CreateApplicationCommand r, CancellationToken ct)
    {
        var source = await catalog.Get(r.Input.ProcedureId, ct);
        if (source.Error is not null) return new(null, source.Error);
        var procedure = source.Value!;
        if (!procedure.IsActive) return WorkflowResult<ApplicationDto>.Fail(409, "application.procedure_inactive", "Thủ tục đã ngừng tiếp nhận hồ sơ.");
        var cases = procedure.ContentPayload?.Cases ?? [];
        var caseCode = string.IsNullOrWhiteSpace(r.Input.CaseCode) ? null : r.Input.CaseCode.Trim();
        if (caseCode is null && cases.Length == 1) caseCode = cases[0].CaseCode;
        if ((caseCode is null && cases.Length > 1) || (caseCode is not null && !cases.Any(x => x.CaseCode == caseCode)))
            return WorkflowResult<ApplicationDto>.Fail(400, "application.invalid_case", "Vui lòng chọn đúng trường hợp của thủ tục.");
        var schema = procedure.ChecklistSchema ?? [];
        if (schema.Any(x => x is null || x.IsMandatory is null || string.IsNullOrWhiteSpace(x.ChecklistId) || x.ChecklistId.Length > 255
                || string.IsNullOrWhiteSpace(x.ItemName) || x.ItemName.Length > 255
                || (!string.IsNullOrWhiteSpace(x.CaseCode) && !cases.Any(c => c.CaseCode == x.CaseCode))))
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Cấu hình checklist của thủ tục không hợp lệ.");
        var selected = schema.Where(x => string.IsNullOrWhiteSpace(x.CaseCode) || x.CaseCode == caseCode).ToArray();
        if (selected.Select(x => x.ChecklistId).Distinct(StringComparer.Ordinal).Count() != selected.Length)
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Mã checklist của thủ tục bị trùng.");
        var now = clock.GetUtcNow().UtcDateTime;
        var application = new ApplicationRecord { UserId = r.UserId, ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title, CaseCode = caseCode, FormData = r.Input.FormData.GetRawText(), CreatedAt = now, UpdatedAt = now };
        application.Checklists.AddRange(selected.Select(x => new ApplicationChecklist { ApplicationId = application.Id,
            Code = x.ChecklistId, Title = x.ItemName, IsRequired = x.IsMandatory!.Value, CreatedAt = now, UpdatedAt = now }));
        application.History.Add(new() { ApplicationId = application.Id, ToStatus = ApplicationStates.Draft,
            ChangedBy = r.UserId, CreatedAt = now });
        await store.Add(application, ct);
        return WorkflowResult<ApplicationDto>.Ok(ApplicationDto.From(application));
    }

    public async Task<WorkflowResult<ApplicationDto>> Handle(GetApplicationQuery r, CancellationToken ct)
    {
        var application = await store.Get(r.UserId, r.Id, ct);
        return application is null ? NotFound() : WorkflowResult<ApplicationDto>.Ok(ApplicationDto.From(application));
    }
    public Task<ApplicationPage> Handle(ListApplicationsQuery r, CancellationToken ct) => store.List(r.UserId, r.Page, r.PageSize, ct);

    public Task<WorkflowResult<ApplicationDto>> Handle(UpdateChecklistCommand r, CancellationToken ct) =>
        store.WithLock(r.UserId, r.Id, application =>
        {
            if (application.Status != ApplicationStates.Draft) return Task.FromResult(NotDraft());
            var item = application.Checklists.SingleOrDefault(x => x.Id == r.ChecklistId);
            if (item is null) return Task.FromResult(WorkflowResult<ApplicationDto>.Fail(404, "application.checklist_not_found", "Không tìm thấy mục checklist của hồ sơ."));
            item.Status = r.Input.Status;
            if (r.Input.FileUrl is not null) item.FileUrl = string.IsNullOrWhiteSpace(r.Input.FileUrl) ? null : r.Input.FileUrl.Trim();
            if (r.Input.Note is not null) item.Note = string.IsNullOrWhiteSpace(r.Input.Note) ? null : r.Input.Note.Trim();
            item.UpdatedAt = application.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            return Task.FromResult(WorkflowResult<ApplicationDto>.Ok(ApplicationDto.From(application)));
        }, ct);

    public Task<WorkflowResult<ApplicationDto>> Handle(SubmitApplicationCommand r, CancellationToken ct) =>
        store.WithLock(r.UserId, r.Id, async application =>
        {
            if (application.Status != ApplicationStates.Draft) return NotDraft();
            var missing = application.Checklists.Where(x => x.IsRequired && x.Status != ChecklistStates.Completed)
                .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Title, x.Status }).ToArray();
            if (missing.Length > 0) return WorkflowResult<ApplicationDto>.Fail(422, "application.checklist_incomplete",
                "Vui lòng hoàn tất tất cả giấy tờ bắt buộc trước khi nộp hồ sơ.", missing);
            var now = clock.GetUtcNow().UtcDateTime;
            application.Submit(await store.NextCode(now, ct), r.UserId, now);
            return WorkflowResult<ApplicationDto>.Ok(ApplicationDto.From(application));
        }, ct);

    private static WorkflowResult<ApplicationDto> NotFound() => WorkflowResult<ApplicationDto>.Fail(404,
        "application.not_found", "Không tìm thấy hồ sơ của bạn.");
    private static WorkflowResult<ApplicationDto> NotDraft() => WorkflowResult<ApplicationDto>.Fail(409,
        "application.invalid_status", "Chỉ được sửa hoặc nộp hồ sơ ở trạng thái DRAFT.");
}
