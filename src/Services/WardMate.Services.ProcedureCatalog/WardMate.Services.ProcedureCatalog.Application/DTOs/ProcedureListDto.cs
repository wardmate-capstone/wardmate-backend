namespace WardMate.Services.ProcedureCatalog.Application.DTOs;

public sealed record ProcedureListItemDto(Guid Id, int CategoryId, string CategoryName, string ProcedureCode,
    string Title, string? IssuingAuthority, string? ExecutingAgency, string LevelOfImplementation,
    string TargetAudience, string FeeSummary, string ProcessingTimeSummary, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ProcedureListDto(IReadOnlyList<ProcedureListItemDto> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
