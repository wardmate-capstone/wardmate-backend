using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed record ChecklistUpdateInput(DateTime ExpectedUpdatedAt, string DecisionNumber, DateOnly EffectiveDate,
    List<ChecklistItemSchema>? Items);
public sealed record UpdateProcedureChecklistCommand(Guid Id, ChecklistUpdateInput Input) : IRequest<ProcedureResult<ProcedureDetailDto>>;

public sealed class UpdateProcedureChecklistHandler(IProcedureManagementStore store, IValidator<ProcedureInput> validator)
    : IRequestHandler<UpdateProcedureChecklistCommand, ProcedureResult<ProcedureDetailDto>>
{
    public Task<ProcedureResult<ProcedureDetailDto>> Handle(UpdateProcedureChecklistCommand request, CancellationToken ct) =>
        store.Transaction(async () =>
        {
            var input = request.Input;
            if (input.Items is null || input.Items.Count > 500 || input.ExpectedUpdatedAt == default ||
                string.IsNullOrWhiteSpace(input.DecisionNumber) || input.DecisionNumber.Length > 100 || input.EffectiveDate == default)
                return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Cần danh sách tối đa 500 mục, thời điểm cập nhật, số quyết định (tối đa 100 ký tự) và ngày hiệu lực.", 400);
            var p = await store.LockProcedure(request.Id, ct);
            if (p is null) return ProcedureResult<ProcedureDetailDto>.NotFound();
            if (p.UpdatedAt != input.ExpectedUpdatedAt)
                return ProcedureResult<ProcedureDetailDto>.Fail("procedure.concurrent_update", "Thủ tục đã thay đổi. Hãy tải lại và đối chiếu trước khi lưu.", 409);
            p.Category = (await store.FindCategory(p.CategoryId, ct))!;
            var cases = p.ContentPayload.Cases.Select(x => x.CaseCode).ToHashSet(StringComparer.Ordinal);
            if (input.Items.Any(x => x is null || string.IsNullOrWhiteSpace(x.ChecklistId) || x.ChecklistId.Length > 255 ||
                x.ChecklistId != x.ChecklistId.Trim() || (!string.IsNullOrWhiteSpace(x.CaseCode) && !cases.Contains(x.CaseCode))) ||
                input.Items.Select(x => x.ChecklistId).Distinct(StringComparer.Ordinal).Count() != input.Items.Count)
                return ProcedureResult<ProcedureDetailDto>.Fail("procedure.invalid_checklist", "Mã checklist phải duy nhất, không có khoảng trắng đầu/cuối, tối đa 255 ký tự; mã trường hợp phải thuộc thủ tục.", 400);
            var validation = await validator.ValidateAsync(new ProcedureInput { ChecklistSchema = input.Items },
                options => options.IncludeProperties(x => x.ChecklistSchema), ct);
            if (!validation.IsValid)
                return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Checklist không hợp lệ.", 400, validation.ToDictionary());
            var utc = DateTime.UtcNow;
            var now = new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc);
            store.AddVersion(ProcedureVersion.Capture(p, await store.NextVersion(p.Id, ct), input.EffectiveDate, input.DecisionNumber.Trim(), now));
            p.ChecklistSchema = input.Items;
            p.ContentPayload.DecisionNumber = input.DecisionNumber.Trim();
            p.UpdatedAt = now;
            return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(p));
        }, ct);
}
