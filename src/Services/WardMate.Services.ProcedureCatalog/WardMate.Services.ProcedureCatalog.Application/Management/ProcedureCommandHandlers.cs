using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class CreateProcedureCommandHandler(IProcedureManagementStore store, IValidator<ProcedureInput> validator)
    : IRequestHandler<CreateProcedureCommand, ProcedureResult<ProcedureDetailDto>>
{
    public async Task<ProcedureResult<ProcedureDetailDto>> Handle(CreateProcedureCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request.Input, ct);
        if (!validation.IsValid) return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return await store.Transaction(async () =>
        {
            var category = await store.FindCategory(request.Input.CategoryId, ct);
            if (category is null) return ProcedureResult<ProcedureDetailDto>.Fail("procedure.category_not_found", "Danh mục thủ tục không tồn tại.", 400);
            if (await store.CodeExists(request.Input.ProcedureCode.Trim(), null, ct))
                return ProcedureResult<ProcedureDetailDto>.Fail("procedure.code_exists", "Mã thủ tục đã tồn tại.", 409);
            var now = DateTime.UtcNow;
            var procedure = new Procedure { Id = Guid.NewGuid(), IsActive = true, CreatedAt = now, UpdatedAt = now, Category = category };
            request.Input.ApplyTo(procedure);
            store.Add(procedure);
            store.AddVersion(ProcedureVersion.Capture(procedure, 1, DateOnly.FromDateTime(now), procedure.ContentPayload.DecisionNumber, now));
            return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(procedure));
        }, ct);
    }
}

public sealed class UpdateProcedureCommandHandler(IProcedureManagementStore store, IValidator<UpdateProcedureInput> validator)
    : IRequestHandler<UpdateProcedureCommand, ProcedureResult<ProcedureDetailDto>>
{
    public async Task<ProcedureResult<ProcedureDetailDto>> Handle(UpdateProcedureCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request.Input, ct);
        if (!validation.IsValid) return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return await store.Transaction(async () =>
        {
            var procedure = await store.LockProcedure(request.ProcedureId, ct);
            if (procedure is null) return ProcedureResult<ProcedureDetailDto>.NotFound();
            var category = await store.FindCategory(request.Input.CategoryId, ct);
            if (category is null) return ProcedureResult<ProcedureDetailDto>.Fail("procedure.category_not_found", "Danh mục thủ tục không tồn tại.", 400);
            if (await store.CodeExists(request.Input.ProcedureCode.Trim(), procedure.Id, ct))
                return ProcedureResult<ProcedureDetailDto>.Fail("procedure.code_exists", "Mã thủ tục đã tồn tại.", 409);
            var now = DateTime.UtcNow;
            store.AddVersion(ProcedureVersion.Capture(procedure, await store.NextVersion(procedure.Id, ct),
                request.Input.EffectiveDate, request.Input.DecisionNumber.Trim(), now));
            request.Input.ApplyTo(procedure);
            // The decision in the live content corresponds to the replacement data, not the frozen snapshot.
            procedure.ContentPayload.DecisionNumber = request.Input.DecisionNumber.Trim();
            procedure.Category = category;
            procedure.UpdatedAt = now;
            return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(procedure));
        }, ct);
    }
}

public sealed class ToggleProcedureStatusCommandHandler(IProcedureManagementStore store)
    : IRequestHandler<ToggleProcedureStatusCommand, ProcedureResult<ProcedureStatusDto>>
{
    public Task<ProcedureResult<ProcedureStatusDto>> Handle(ToggleProcedureStatusCommand request, CancellationToken ct) =>
        store.Transaction(async () =>
        {
            var procedure = await store.LockProcedure(request.ProcedureId, ct);
            if (procedure is null) return ProcedureResult<ProcedureStatusDto>.NotFound();
            procedure.IsActive = request.IsActive;
            procedure.StatusChangeReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            procedure.UpdatedAt = DateTime.UtcNow;
            return ProcedureResult<ProcedureStatusDto>.Ok(new(procedure.Id, procedure.IsActive, procedure.StatusChangeReason, procedure.UpdatedAt));
        }, ct);
}
