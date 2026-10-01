using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class ReviewedProcedureInput : ProcedureInput
{
    public ReviewedProcedureInput()
    {
        // A reviewed draft must supply these values; do not silently invent fees or processing times.
        FeeSummary = string.Empty;
        ProcessingTimeSummary = string.Empty;
    }
}

public sealed record PublishReviewedProcedureCommand(ReviewedProcedureInput Input) : IRequest<ProcedureResult<ProcedureDetailDto>>;

public sealed class PublishReviewedProcedureHandler(IProcedureManagementStore store, IValidator<ProcedureInput> validator)
    : IRequestHandler<PublishReviewedProcedureCommand, ProcedureResult<ProcedureDetailDto>>
{
    public async Task<ProcedureResult<ProcedureDetailDto>> Handle(PublishReviewedProcedureCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request.Input, ct);
        if (!validation.IsValid) return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return await store.Transaction(async () =>
        {
            var category = await store.FindCategory(request.Input.CategoryId, ct);
            if (category is null) return ProcedureResult<ProcedureDetailDto>.Fail("procedure.category_not_found", "Danh mục thủ tục không tồn tại.", 400);
            var procedure = await store.LockProcedureByCode(request.Input.ProcedureCode.Trim(), ct);
            var now = DateTime.UtcNow;
            if (procedure is null)
            {
                procedure = new Procedure { Id = Guid.NewGuid(), IsActive = true, CreatedAt = now, UpdatedAt = now, Category = category };
                request.Input.ApplyTo(procedure);
                store.Add(procedure);
                store.AddVersion(ProcedureVersion.Capture(procedure, 1, DateOnly.FromDateTime(now), procedure.ContentPayload.DecisionNumber, now));
            }
            else
            {
                store.AddVersion(ProcedureVersion.Capture(procedure, await store.NextVersion(procedure.Id, ct),
                    DateOnly.FromDateTime(now), request.Input.ContentPayload.DecisionNumber, now));
                request.Input.ApplyTo(procedure);
                procedure.Category = category;
                procedure.UpdatedAt = now;
            }
            return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(procedure));
        }, ct);
    }
}
