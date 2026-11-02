namespace WardMate.Services.AIOCR.Application;

// AI-OFFICER-ASSIST-01: reference-only review of one dossier for its assigned officer. The result is never an
// administrative decision; it does not change the application, and the officer writes the actual comment.
public sealed record DossierAssistOptions(bool IncludeSubmissions = true, bool IncludeSupportingDocuments = true);

public static class DossierAssessments
{
    public const string NoIssuesFound = "NO_ISSUES_FOUND";
    public const string NeedsAttention = "NEEDS_ATTENTION";
    public const string InsufficientData = "INSUFFICIENT_DATA";
    public static readonly string[] All = [NoIssuesFound, NeedsAttention, InsufficientData];
}

public static class CrossCheckResults
{
    public const string Match = "MATCH";
    public const string Mismatch = "MISMATCH";
    public const string NotFound = "NOT_FOUND";
    public static readonly string[] All = [Match, Mismatch, NotFound];
}

// One file the officer is allowed to read. Text is set for DOCX (extracted on the server); Bytes for PDF/PNG/JPEG.
public sealed record DossierFile(string Kind, Guid Id, Guid? ChecklistId, string? ChecklistTitle, string FileName,
    string? Text, byte[]? Bytes, string? SkipReason);
public sealed record DossierChecklistItem(Guid Id, string Title, bool IsRequired);
public sealed record DossierBundle(Guid ApplicationId, string ApplicationStatus, string ProcedureTitle,
    IReadOnlyList<DossierChecklistItem> Checklist, IReadOnlyList<DossierFile> Files);

public sealed record DossierFileSummary(string Kind, Guid Id, Guid? ChecklistId, string? ChecklistTitle, string FileName,
    bool Analyzed, string? SkipReason);
public sealed record DossierField(string Name, string? Value, string? Source);
public sealed record DossierCrossCheck(string Field, string? ValueInForm, string? ValueInDocument, string? SourceDocument,
    string Result, string? Note);
public sealed record DossierModelReview(string OverallAssessment, string Summary, DossierField[] ExtractedFields,
    DossierCrossCheck[] CrossChecks, string[] MissingItems, string[] Issues, string? SuggestedComment);
public sealed record DossierAssistResult(Guid ApplicationId, DateTime AnalyzedAt, string ApplicationStatus, string Model,
    string OverallAssessment, string Summary, IReadOnlyList<DossierFileSummary> Files, DossierField[] ExtractedFields,
    DossierCrossCheck[] CrossChecks, string[] MissingItems, string[] Issues, string? SuggestedComment, string Disclaimer);

public interface IDossierSource
{
    // Reads with the caller's bearer token only; Workflow and DocumentForm decide what the caller may see.
    Task<DossierBundle> Load(Guid applicationId, Guid officerId, DossierAssistOptions options, string bearer, CancellationToken ct);
}

public interface IDossierReviewer
{
    Task<(DossierModelReview Review, string Model)> Review(DossierBundle bundle, CancellationToken ct);
}
