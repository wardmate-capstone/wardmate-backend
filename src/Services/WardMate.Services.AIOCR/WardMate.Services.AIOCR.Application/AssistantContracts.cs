namespace WardMate.Services.AIOCR.Application;

public sealed record CccdResult(string? CitizenId, string? FullName, string? DateOfBirth, string? Gender,
    string? Nationality, string? PlaceOfOrigin, string? PlaceOfResidence, string? DateOfExpiry, double ConfidenceScore);
public sealed record VerificationResult(bool IsValid, string DocumentTypeDetected, string[] QualityIssues, double Confidence);
public sealed record ChatInput(string Message, Guid? ConversationId, string WardCode);
public sealed record ChatResult(Guid ConversationId, string Answer, object[] RelatedProcedures);
public sealed record SessionDto(Guid Id, string WardCode, DateTime CreatedAt);
public sealed record MessageDto(Guid Id, string Role, string Content, DateTime CreatedAt);
public sealed record ChatTurn(string Role, string Content);
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
public sealed class AiFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public interface ICccdExtractionService
{
    Task<CccdResult> Extract(Stream front, Stream? back, CancellationToken ct);
}
public interface IDocumentPreValidationService
{
    Task<VerificationResult> Verify(Stream file, string expectedType, CancellationToken ct);
}
public interface IAssistantModel
{
    Task<string> Answer(IReadOnlyList<ChatTurn> history, string wardCode, CancellationToken ct);
}
public interface IChatService
{
    Task<ChatResult> Complete(Guid userId, ChatInput input, CancellationToken ct);
    Task<Page<SessionDto>> Sessions(Guid userId, int page, int size, CancellationToken ct);
    Task<Page<MessageDto>> Messages(Guid userId, Guid sessionId, int page, int size, CancellationToken ct);
}
public interface IAuthorizedDocumentSource
{
    Task<byte[]> Download(Guid applicationId, Guid documentId, string bearer, CancellationToken ct);
}
