namespace WardMate.Services.AIOCR.Application;

public sealed record KnowledgeListQuery(string? Search = null, string? SourceType = null, bool? IsActive = null,
    Guid? ProcedureId = null, int PageNumber = 1, int PageSize = 20);
public sealed record KnowledgeDocumentSummaryDto(Guid Id, string Title, string SourceType, Guid? ProcedureId,
    string? SourceReference, string? SourceVersion, string Status, bool IsActive, int ChunkCount, int ContentLength,
    Guid Revision, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ChunkedAt);
public sealed record KnowledgeDocumentDto(Guid Id, string Title, string SourceType, Guid? ProcedureId,
    string? SourceReference, string? SourceVersion, string Status, bool IsActive, int ChunkCount, string ChunkingVersion,
    string Content, string ContentSha256, Guid CreatedBy, Guid UpdatedBy, Guid Revision,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ChunkedAt, string[]? Warnings = null,
    string? EmbeddingModel = null, int EmbeddingAttempts = 0, DateTime? NextEmbeddingAt = null, string? EmbeddingError = null, DateTime? EmbeddedAt = null);
public sealed record KnowledgeChunkDto(Guid Id, int ChunkIndex, string? Heading, string Content, int CharCount, string ContentSha256);

public sealed record CreateKnowledgeTextInput(string Title, string SourceType, string Content, Guid? ProcedureId = null);
public sealed record UpdateKnowledgeInput(Guid ExpectedRevision, string Title, string? Content = null);
public sealed record KnowledgeActivationInput(Guid ExpectedRevision, bool IsActive);
public sealed record KnowledgeRevisionInput(Guid ExpectedRevision);
public sealed record KnowledgePdfInput(string? Title, Guid? ProcedureId, string FileName);

public sealed record ProcedureSyncResult(string Outcome, KnowledgeDocumentSummaryDto? Document);
public sealed record ProcedureSyncAllResult(int Scanned, int Created, int Updated, int Unchanged, int Deactivated,
    int Failed, bool Complete, Guid[] FailedProcedureIds);

public interface IKnowledgeBase
{
    Task<Page<KnowledgeDocumentSummaryDto>> List(KnowledgeListQuery query, CancellationToken ct);
    Task<KnowledgeDocumentDto> Get(Guid id, CancellationToken ct);
    Task<Page<KnowledgeChunkDto>> Chunks(Guid id, int pageNumber, int pageSize, CancellationToken ct);
    Task<KnowledgeDocumentDto> CreateText(Guid actor, CreateKnowledgeTextInput input, CancellationToken ct);
    Task<KnowledgeDocumentDto> CreatePdf(Guid actor, KnowledgePdfInput input, Stream pdf, CancellationToken ct);
    Task<KnowledgeDocumentDto> Update(Guid actor, Guid id, UpdateKnowledgeInput input, CancellationToken ct);
    Task<KnowledgeDocumentDto> SetActive(Guid actor, Guid id, KnowledgeActivationInput input, CancellationToken ct);
    Task<KnowledgeDocumentDto> Rechunk(Guid actor, Guid id, KnowledgeRevisionInput input, CancellationToken ct);
    Task<ProcedureSyncResult> SyncProcedure(Guid actor, Guid procedureId, CancellationToken ct);
    Task<ProcedureSyncAllResult> SyncAllProcedures(Guid actor, CancellationToken ct);
    Task<KnowledgeDocumentDto> Reindex(Guid actor, Guid id, KnowledgeRevisionInput input, CancellationToken ct);
    Task<KnowledgeIndexStatus> IndexStatus(CancellationToken ct);
    Task<KnowledgeSearchHitDto[]> Search(KnowledgeSearchInput input, CancellationToken ct);
}

// Integration contract with ProcedureCatalog's public detail API; not a reference to its database.
public sealed record CatalogProcedure(Guid Id, string? ProcedureCode, string Title, string? CategoryName,
    string? IssuingAuthority, string? ExecutingAgency, string? LevelOfImplementation, string? TargetAudience,
    string? FeeSummary, string? ProcessingTimeSummary, bool IsActive, CatalogContent? ContentPayload,
    CatalogChecklistItem[]? ChecklistSchema, CatalogForm[]? FormDefinitions, DateTime UpdatedAt);
public sealed record CatalogContent(string? DecisionNumber, string? ReceivingAddress, CatalogMethod[]? SubmissionMethods,
    CatalogLegal[]? LegalReferences, string[]? Results, CatalogCase[]? Cases);
public sealed record CatalogMethod(string? MethodName, decimal FeeAmount, string? FeeUnit, decimal EstimatedDays, string? Note);
public sealed record CatalogLegal(string? DocumentNumber, string? DocumentName, DateOnly? IssueDate, string? Authority);
public sealed record CatalogCase(string? CaseCode, string? CaseName, CatalogStep[]? Steps);
public sealed record CatalogStep(int StepOrder, string? StepName, string? Executor, string? ActionDetails);
public sealed record CatalogChecklistItem(string? CaseCode, string? SubmissionType, string? ItemName, string? DocumentCopyType,
    int Quantity, string? ConditionNote, bool IsMandatory);
public sealed record CatalogForm(string? CaseCode, string? FormCode, string? FormName, int Quantity, bool IsMandatory);
public sealed record CatalogListing(Guid[] Ids, bool Complete);

public interface IProcedureCatalogSource
{
    // Null means the procedure is missing or inactive in the catalog (the public detail API hides inactive ones).
    Task<CatalogProcedure?> Get(Guid procedureId, CancellationToken ct);
    Task<CatalogListing> ListActive(int max, CancellationToken ct);
}

public sealed record PdfTextResult(string Text, int PageCount, int[] PagesWithoutText);
public interface IKnowledgePdfReader
{
    Task<PdfTextResult> Read(Stream pdf, CancellationToken ct);
}
