using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record CreateApplicationCommand(Guid UserId, CreateApplicationInput Input) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record GetApplicationQuery(Guid UserId, Guid Id) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record ListApplicationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<ApplicationPage>;
public sealed record UpdateChecklistCommand(Guid UserId, Guid Id, Guid ChecklistId, UpdateChecklistInput Input) : IRequest<WorkflowResult<ApplicationDto>>;
public sealed record SubmitApplicationCommand(Guid UserId, Guid Id, string? ApplicantName = null) : IRequest<WorkflowResult<ApplicationDto>>;

public sealed class WorkflowHandlers(IApplicationStore store, IProcedureCatalogClient catalog, TimeProvider clock, IWorkflowDirectory directory, ReviewDeadlinePolicy deadlinePolicy) :
    IRequestHandler<CreateApplicationCommand, WorkflowResult<ApplicationDto>>,
    IRequestHandler<GetApplicationQuery, WorkflowResult<ApplicationDto>>,
    IRequestHandler<ListApplicationsQuery, ApplicationPage>,
    IRequestHandler<UpdateChecklistCommand, WorkflowResult<ApplicationDto>>,
    IRequestHandler<SubmitApplicationCommand, WorkflowResult<ApplicationDto>>
{
    /// <summary>BE-H: trần tên giấy tờ/biểu mẫu, khớp ProcedureLimits.Line của Catalog.</summary>
    private const int MaxTextLength = 10_000;

    public async Task<WorkflowResult<ApplicationDto>> Handle(CreateApplicationCommand r, CancellationToken ct)
    {
        var ward = r.Input.WardCode!.Trim().ToUpperInvariant();
        try
        {
            if (!await directory.WardExists(ward, ct)) return WorkflowResult<ApplicationDto>.Fail(400, "application.invalid_ward", "Phường/xã tiếp nhận không tồn tại.");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { return WorkflowResult<ApplicationDto>.Fail(503, "application.iam_unavailable", "Chưa thể xác minh phường/xã với IAM."); }
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
        // BE-H (A.1): tên giấy tờ/biểu mẫu theo trần Catalog 10.000 ký tự (trước đây 255); thêm loại bản OTHER.
        if (schema.Any(x => x is null || x.IsMandatory is null || string.IsNullOrWhiteSpace(x.ChecklistId) || x.ChecklistId.Length > 255
                || string.IsNullOrWhiteSpace(x.ItemName) || x.ItemName.Length > MaxTextLength || x.Quantity < 1
                || x.SubmissionType is not (null or "NOP" or "XUAT_TRINH")
                || x.DocumentCopyType is not (null or "ORIGINAL" or "CERTIFIED_COPY" or "REGULAR_COPY" or "OTHER")
                || (x.FormCodes is { } formCodes && formCodes.Any(string.IsNullOrWhiteSpace))
                || (!string.IsNullOrWhiteSpace(x.CaseCode) && !cases.Any(c => c.CaseCode == x.CaseCode))))
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Cấu hình checklist của thủ tục không hợp lệ.");
        var selected = schema.Where(x => string.IsNullOrWhiteSpace(x.CaseCode) || x.CaseCode == caseCode).ToArray();
        if (selected.Select(x => x.ChecklistId).Distinct(StringComparer.Ordinal).Count() != selected.Length)
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Mã checklist của thủ tục bị trùng.");
        var now = clock.GetUtcNow().UtcDateTime;
        var application = new ApplicationRecord { WardCode = ward, ApplicationCode = await store.NextCode(now, ct), UserId = r.UserId, ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title, CategoryId = procedure.CategoryId, CategoryName = procedure.CategoryName, CaseCode = caseCode, FormData = r.Input.FormData.GetRawText(), CreatedAt = now, UpdatedAt = now };
        application.Checklists.AddRange(selected.Select((x, index) => new ApplicationChecklist { ApplicationId = application.Id,
            Code = x.ChecklistId, Title = x.ItemName, IsRequired = x.IsMandatory!.Value, CreatedAt = now, UpdatedAt = now,
            SortOrder = index, CaseCode = x.CaseCode, SubmissionType = x.SubmissionType,
            DocumentCopyType = x.DocumentCopyType, Quantity = x.Quantity, ConditionNote = x.ConditionNote,
            QuantityText = x.QuantityText, ItemNote = x.Note }));
        var forms = procedure.FormDefinitions ?? [];
        if (forms.Any(x => x is null || string.IsNullOrWhiteSpace(x.FormCode) || x.FormCode.Length > 255
            || string.IsNullOrWhiteSpace(x.FormName) || x.FormName.Length > MaxTextLength || x.FormTemplateId == Guid.Empty || x.Quantity < 1
            || (!string.IsNullOrWhiteSpace(x.CaseCode) && !cases.Any(c => c.CaseCode == x.CaseCode))))
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Cấu hình biểu mẫu của thủ tục không hợp lệ.");
        var selectedForms = forms.Where(x => string.IsNullOrWhiteSpace(x.CaseCode) || x.CaseCode == caseCode).ToArray();
        if (selectedForms.Select(x => x.FormCode).Distinct(StringComparer.Ordinal).Count() != selectedForms.Length)
            return WorkflowResult<ApplicationDto>.Fail(502, "application.invalid_schema", "Mã biểu mẫu của thủ tục bị trùng.");
        // BE-H (B.3.1): biểu mẫu được giấy tờ tham chiếu qua formCodes thì gắn vào giấy tờ đó, không tạo dòng mới;
        // tính đủ/bắt buộc/số lượng vẫn theo chính giấy tờ. Nộp đơn DOCX theo một trong các mẫu gắn kèm là đủ.
        var formsByCode = selectedForms.ToDictionary(x => x.FormCode, StringComparer.Ordinal);
        var itemLinks = new Dictionary<Guid, List<ProcedureForm>>();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var created = application.Checklists.ToArray();
        for (var i = 0; i < selected.Length; i++)
        {
            var linked = (selected[i].FormCodes ?? []).Distinct(StringComparer.Ordinal)
                .Select(code => formsByCode.GetValueOrDefault(code)).OfType<ProcedureForm>().ToList();
            if (linked.Count == 0) continue;
            itemLinks[created[i].Id] = linked;
            referenced.UnionWith(linked.Select(x => x.FormCode));
        }
        // Biểu mẫu không giấy tờ nào tham chiếu: giữ cách cũ — trùng mã giấy tờ thì gắn vào, không trùng thì tạo dòng mới.
        foreach (var form in selectedForms.Where(x => !referenced.Contains(x.FormCode)))
        {
            // Same explicit code means the form satisfies that checklist item. Never match by title.
            var item = application.Checklists.SingleOrDefault(x => x.Code == form.FormCode);
            if (item is null)
            {
                item = new ApplicationChecklist { ApplicationId = application.Id, Code = form.FormCode,
                    Title = form.FormName, CreatedAt = now, UpdatedAt = now,
                    SortOrder = application.Checklists.Count, CaseCode = form.CaseCode, Quantity = form.Quantity };
                application.Checklists.Add(item);
            }
            if (!itemLinks.TryGetValue(item.Id, out var list))
            {
                itemLinks[item.Id] = list = [];
                item.IsRequired |= form.IsMandatory;
                item.Quantity = Math.Max(item.Quantity, form.Quantity);
            }
            list.Add(form);
        }
        foreach (var item in application.Checklists)
        {
            if (!itemLinks.TryGetValue(item.Id, out var list)) continue;
            item.FormTemplateId = list.Select(x => x.FormTemplateId).FirstOrDefault(id => id.HasValue);
            item.FormLinks = ChecklistForms.Serialize(list.Select(x => new ChecklistFormDto(x.FormCode, x.FormName, x.FormTemplateId, x.IsMandatory)).ToArray());
        }
        application.History.Add(new() { ApplicationId = application.Id, ToStatus = ApplicationStates.Draft,
            ChangedBy = r.UserId, CreatedAt = now });
        await store.Add(application, ct);
        return WorkflowResult<ApplicationDto>.Ok(ApplicationDto.ForApplicant(application));
    }

    public async Task<WorkflowResult<ApplicationDto>> Handle(GetApplicationQuery r, CancellationToken ct)
    {
        var application = await store.Get(r.UserId, r.Id, ct);
        return application is null ? NotFound() : WorkflowResult<ApplicationDto>.Ok(ApplicationDto.ForApplicant(application));
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
            return Task.FromResult(WorkflowResult<ApplicationDto>.Ok(ApplicationDto.ForApplicant(application)));
        }, ct);

    public Task<WorkflowResult<ApplicationDto>> Handle(SubmitApplicationCommand r, CancellationToken ct) =>
        store.WithLock(r.UserId, r.Id, async application =>
        {
            if (application.Status != ApplicationStates.Draft) return NotDraft();
            if (string.IsNullOrWhiteSpace(application.WardCode)) return WorkflowResult<ApplicationDto>.Fail(409, "application.ward_required", "Hồ sơ cũ chưa có phường tiếp nhận; cần bổ sung trước khi nộp.");
            var missing = application.Checklists.Where(x => x.IsRequired && x.Status != ChecklistStates.Completed)
                .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Title, x.Status }).ToArray();
            if (missing.Length > 0) return WorkflowResult<ApplicationDto>.Fail(422, "application.checklist_incomplete",
                "Vui lòng hoàn tất tất cả giấy tờ bắt buộc trước khi nộp hồ sơ.", missing);
            var now = clock.GetUtcNow().UtcDateTime;
            var code = string.IsNullOrWhiteSpace(application.ApplicationCode) ? await store.NextCode(now, ct) : application.ApplicationCode;
            application.Submit(code, r.UserId, now);
            application.SnapshotApplicantName(r.ApplicantName);
            ReviewDeadline.Start(application, now, deadlinePolicy);
            application.Versions.Add(ApplicationSnapshots.Capture(application, r.UserId, now));
            return WorkflowResult<ApplicationDto>.Ok(ApplicationDto.ForApplicant(application));
        }, ct);

    private static WorkflowResult<ApplicationDto> NotFound() => WorkflowResult<ApplicationDto>.Fail(404,
        "application.not_found", "Không tìm thấy hồ sơ của bạn.");
    private static WorkflowResult<ApplicationDto> NotDraft() => WorkflowResult<ApplicationDto>.Fail(409,
        "application.invalid_status", "Chỉ được sửa hoặc nộp hồ sơ ở trạng thái DRAFT.");
}




