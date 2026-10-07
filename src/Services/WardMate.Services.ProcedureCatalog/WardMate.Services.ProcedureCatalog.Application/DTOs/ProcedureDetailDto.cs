using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.DTOs;

public sealed record ProcedureDetailDto(Guid Id, int CategoryId, string CategoryName, string ProcedureCode,
    string Title, string? IssuingAuthority, string? ExecutingAgency, string LevelOfImplementation,
    string TargetAudience, string FeeSummary, string ProcessingTimeSummary, bool IsActive,
    ProcedureContentPayload ContentPayload, List<ChecklistItemSchema>? ChecklistSchema,
    List<FormDefinitionSchema>? FormDefinitions, DateTime CreatedAt, DateTime UpdatedAt,
    string? OriginalPdfUrl, string? PdfFileName)
{
    public static ProcedureDetailDto From(Procedure procedure) => new(procedure.Id, procedure.CategoryId,
        procedure.Category.CategoryName, procedure.ProcedureCode, procedure.Title, procedure.IssuingAuthority,
        procedure.ExecutingAgency, procedure.LevelOfImplementation, procedure.TargetAudience, procedure.FeeSummary,
        procedure.ProcessingTimeSummary, procedure.IsActive, procedure.ContentPayload, procedure.ChecklistSchema,
        procedure.FormDefinitions, procedure.CreatedAt, procedure.UpdatedAt, procedure.OriginalPdfUrl, procedure.PdfFileName);
}
