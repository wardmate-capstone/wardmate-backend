using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.API.Controllers;

[ApiController, Authorize, EnableRateLimiting("assistant"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssistantController(ICccdExtractionService cccd, IDocumentPreValidationService validator,
    IAuthorizedDocumentSource documents, IChatService chat, IDossierSource dossiers, IDossierReviewer reviewer) : ControllerBase
{
    private const string DossierDisclaimer = "Kết quả do AI tạo để cán bộ tham khảo khi kiểm tra giấy tờ; không phải quyết định hành chính. Cán bộ tự kiểm tra và quyết định nội dung nhận xét.";
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("/api/v1/ai-ocr/cccd/extract"), Consumes("multipart/form-data"), RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 17 * 1024 * 1024)]
    public async Task<ActionResult<CccdResult>> Extract([FromForm] CccdUpload input, CancellationToken ct)
    {
        if (input.FrontImage.Length is <= 0 or > 8 * 1024 * 1024 || input.BackImage?.Length is <= 0 or > 8 * 1024 * 1024)
            throw new AiFailure(400, "ai.invalid_file_size", "Mỗi ảnh phải có dữ liệu và không quá 8 MiB.");
        await using var front = input.FrontImage.OpenReadStream();
        await using var back = input.BackImage?.OpenReadStream();
        return Ok(await cccd.Extract(front, back, ct));
    }
    [HttpPost("/api/v1/ai-ocr/documents/verify"), RequestSizeLimit(4096)]
    public async Task<ActionResult<VerificationResult>> Verify(VerifyInput input, CancellationToken ct)
    {
        if (input.ApplicationId == Guid.Empty || input.DocumentId == Guid.Empty)
            throw new AiFailure(400, "ai.invalid_input", "Cần ApplicationId và DocumentId hợp lệ.");
        var document = await documents.Download(input.ApplicationId, input.DocumentId, Request.Headers.Authorization.ToString(), ct);
        await using var stream = new MemoryStream(document.Bytes, writable: false);
        var result = await validator.Verify(stream, document.ExpectedType, ct);
        return Ok(result with { ChecklistId = document.ChecklistId, ExpectedType = document.ExpectedType });
    }
    // AI-OFFICER-ASSIST-01: reference-only review of the whole dossier (DOCX forms + supporting files) for the assigned officer.
    [HttpPost("/api/v1/ai-ocr/applications/{applicationId:guid}/dossier-assist"), RequestSizeLimit(4096)]
    public async Task<ActionResult<DossierAssistResult>> DossierAssist(Guid applicationId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DossierAssistRequest? input, CancellationToken ct)
    {
        var options = new DossierAssistOptions(input?.IncludeSubmissions ?? true, input?.IncludeSupportingDocuments ?? true);
        if (!options.IncludeSubmissions && !options.IncludeSupportingDocuments)
            throw new AiFailure(400, "ai.invalid_input", "Cần chọn ít nhất tờ khai hoặc giấy tờ kèm theo.");
        var bundle = await dossiers.Load(applicationId, UserId, options, Request.Headers.Authorization.ToString(), ct);
        if (!bundle.Files.Any(f => f.SkipReason is null))
            throw new AiFailure(422, "ai.dossier_empty", "Hồ sơ chưa có tờ khai hoặc giấy tờ nào phân tích được.");
        var (review, model) = await reviewer.Review(bundle, ct);
        var files = bundle.Files.Select(f => new DossierFileSummary(f.Kind, f.Id, f.ChecklistId, f.ChecklistTitle, f.FileName, f.SkipReason is null, f.SkipReason)).ToArray();
        return Ok(new DossierAssistResult(applicationId, DateTime.UtcNow, bundle.ApplicationStatus, model, review.OverallAssessment, review.Summary,
            files, review.ExtractedFields, review.CrossChecks, review.MissingItems, review.Issues, review.SuggestedComment, DossierDisclaimer));
    }
    // Public: citizens may ask without signing in. Guest turns are kept under ChatOwners.Guest and can only be continued
    // with the returned conversationId; history endpoints stay signed-in only.
    [HttpPost("/api/v1/ai/chat/completions"), AllowAnonymous, RequestSizeLimit(32768)]
    public async Task<ActionResult<ChatResult>> Complete(ChatRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || string.IsNullOrWhiteSpace(input.WardCode) || input.ConversationId == Guid.Empty)
            throw new AiFailure(400, "chat.invalid_input", "Cần message, wardCode và conversationId hợp lệ hoặc null.");
        var owner = User.Identity?.IsAuthenticated == true ? UserId : ChatOwners.Guest;
        return Ok(await chat.Complete(owner, new(input.Message, input.ConversationId, input.WardCode), ct));
    }
    // CHAT-FEEDBACK-01: đánh giá câu trả lời (khách dùng chat ẩn danh đánh giá được tin nhắn của phiên khách).
    [HttpPost("/api/v1/ai/chat/messages/{messageId:guid}/feedback"), AllowAnonymous, RequestSizeLimit(8192)]
    public async Task<IActionResult> Feedback(Guid messageId, ChatFeedbackInput input, CancellationToken ct)
    {
        var owner = User.Identity?.IsAuthenticated == true ? UserId : ChatOwners.Guest;
        await chat.Feedback(owner, messageId, input, ct);
        return NoContent();
    }
    [HttpGet("/api/v1/ai/chat/sessions")]
    public async Task<ActionResult<Page<SessionDto>>> Sessions([FromQuery, Range(1, 100000)] int pageNumber = 1,
        [FromQuery, Range(1, 50)] int pageSize = 20, CancellationToken ct = default) => Ok(await chat.Sessions(UserId, pageNumber, pageSize, ct));
    [HttpGet("/api/v1/ai/chat/sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult<Page<MessageDto>>> Messages(Guid sessionId, [FromQuery, Range(1, 100000)] int pageNumber = 1,
        [FromQuery, Range(1, 50)] int pageSize = 20, CancellationToken ct = default) => Ok(await chat.Messages(UserId, sessionId, pageNumber, pageSize, ct));
}
public sealed class CccdUpload
{
    [Required] public IFormFile FrontImage { get; set; } = null!;
    public IFormFile? BackImage { get; set; }
}
public sealed record DossierAssistRequest(bool? IncludeSubmissions = null, bool? IncludeSupportingDocuments = null);
public sealed record VerifyInput(Guid ApplicationId, Guid DocumentId, [StringLength(255)] string? ExpectedType = null);
public sealed record ChatRequest([Required, StringLength(4000)] string Message, Guid? ConversationId,
    [Required, RegularExpression("^[A-Za-z0-9_-]{1,50}$")] string WardCode);
