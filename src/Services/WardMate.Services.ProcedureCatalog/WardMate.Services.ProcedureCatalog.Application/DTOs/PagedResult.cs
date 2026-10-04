namespace WardMate.Services.ProcedureCatalog.Application.DTOs;

public sealed record ProcedureSummaryDto(Guid Id, string ProcedureCode, string Title, string CategoryName,
    string LevelOfImplementation, string FeeSummary, string ProcessingTimeSummary, string? OriginalPdfUrl, DateTime UpdatedAt);

public sealed record ProcedureManagerSummaryDto(Guid Id, string ProcedureCode, string Title, string CategoryName,
    string LevelOfImplementation, string FeeSummary, string ProcessingTimeSummary, string? OriginalPdfUrl,
    bool IsActive, int VersionCount, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int CurrentPage, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}
