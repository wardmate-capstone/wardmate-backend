using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.API.Controllers;

[ApiController, Authorize, EnableRateLimiting("assistant"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssistantController(ICccdExtractionService cccd, IDocumentPreValidationService validator,
    IAuthorizedDocumentSource documents, IChatService chat) : ControllerBase
{
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
    [HttpPost("/api/v1/ai/chat/completions"), RequestSizeLimit(32768)]
    public async Task<ActionResult<ChatResult>> Complete(ChatRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || string.IsNullOrWhiteSpace(input.WardCode) || input.ConversationId == Guid.Empty)
            throw new AiFailure(400, "chat.invalid_input", "Cần message, wardCode và conversationId hợp lệ hoặc null.");
        return Ok(await chat.Complete(UserId, new(input.Message, input.ConversationId, input.WardCode), ct));
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
public sealed record VerifyInput(Guid ApplicationId, Guid DocumentId, [StringLength(255)] string? ExpectedType = null);
public sealed record ChatRequest([Required, StringLength(4000)] string Message, Guid? ConversationId,
    [Required, RegularExpression("^[A-Za-z0-9_-]{1,50}$")] string WardCode);
