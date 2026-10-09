using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedure-manager/drafts"), Authorize]
public sealed class ProcedureDraftsController(IProcedureDraftService drafts, IProcedureExtractor extractor, ISender sender) : ControllerBase
{
    private string Actor => User.FindFirstValue("sub") ?? User.Identity!.Name ?? "unknown";

    [HttpPost("extract-preview"), Consumes("multipart/form-data"), RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType<ExtractionResult>(200)]
    [Authorize(Policy = "procedure.drafts.extract")]
    public async Task<IActionResult> Preview([FromForm] UploadPdfInput input, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (input.File is null || input.File.Length is < 5 or > 20971520 || !input.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Reply(ProcedureResult<ExtractionResult>.Fail("draft.invalid_pdf", "Cần file PDF dung lượng tối đa 20 MB.", 400));
        await using var stream = input.File.OpenReadStream();
        var signature = new byte[5];
        await stream.ReadExactlyAsync(signature, ct);
        if (!signature.AsSpan().SequenceEqual("%PDF-"u8))
            return Reply(ProcedureResult<ExtractionResult>.Fail("draft.invalid_pdf", "File không có định dạng PDF.", 400));
        stream.Position = 0;
        try { return Ok(await extractor.Extract(stream, ct)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Reply(ProcedureResult<ExtractionResult>.Fail("draft.extraction_timeout", "Đọc PDF quá thời gian cho phép.", 504)); }
        catch (InvalidOperationException)
        { return Reply(ProcedureResult<ExtractionResult>.Fail("draft.extraction_not_configured", "Cần cấu hình địa chỉ và khóa kết nối AIOCR.", 503)); }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or InvalidDataException)
        { return Reply(ProcedureResult<ExtractionResult>.Fail("draft.extraction_failed", "Không đọc được PDF. Kiểm tra dịch vụ AIOCR và file PDF không khóa, tối đa 100 trang/100.000 ký tự.", 502)); }
    }

    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType<DraftDto>(202)]
    [Authorize(Policy = "procedure.drafts.upload")]
    public async Task<IActionResult> Upload([FromForm] UploadPdfInput input, CancellationToken ct)
    {
        if (input.File is null || input.File.Length is < 5 or > 20971520)
            return Reply(ProcedureResult<DraftDto>.Fail("draft.invalid_pdf", "Cần file PDF dung lượng tối đa 20 MB.", 400));
        await using var stream = input.File.OpenReadStream();
        var result = await drafts.Upload(stream, input.File.FileName, Actor, ct);
        return result.IsSuccess ? AcceptedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : Reply(result);
    }
    [HttpGet]
    [Authorize(Policy = "procedure.drafts.read")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50)
            return Reply(ProcedureResult<DraftDto>.Fail("validation.failed", "Trang phải từ 1 đến 100000; số bản ghi mỗi trang từ 1 đến 50.", 400));
        return Ok(await drafts.List(page, pageSize, ct));
    }
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procedure.drafts.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Reply(await drafts.Get(id, ct));
    [HttpPut("{id:guid}"), RequestSizeLimit(1024 * 1024)]
    [Authorize(Policy = "procedure.drafts.update")]
    public async Task<IActionResult> Save(Guid id, SaveDraftInput input, CancellationToken ct) => Reply(await drafts.Save(id, input, ct));
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = "procedure.drafts.extract")]
    public async Task<IActionResult> Retry(Guid id, DraftRevisionInput input, CancellationToken ct) => Reply(await drafts.Retry(id, input.Revision, ct));
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "procedure.drafts.publish")]
    public async Task<IActionResult> Publish(Guid id, ConfirmDraftInput input, CancellationToken ct) => Reply(await drafts.Publish(id, input, Actor, ct));
    [HttpGet("{id:guid}/source")]
    [Authorize(Policy = "procedure.drafts.read")]
    public async Task<IActionResult> Source(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await drafts.ReadUrl(id, ct);
        return result.IsSuccess ? Ok(new { url = result.Value, expiresInSeconds = 600 }) : Reply(result);
    }
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(503)]
    [Authorize(Policy = "procedure.drafts.delete")]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DiscardDraftCommand(id), ct);
        return result.IsSuccess ? NoContent() : Reply(result);
    }
    private IActionResult Reply<T>(ProcedureResult<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var e = result.Error!;
        return new ObjectResult(new ProblemDetails { Status = e.Status, Title = e.Message, Instance = Request.Path,
            Extensions = { ["code"] = e.Code, ["errors"] = e.Errors, ["traceId"] = HttpContext.TraceIdentifier } })
        { StatusCode = e.Status, ContentTypes = { "application/problem+json" } };
    }
}
public sealed class UploadPdfInput { public IFormFile? File { get; set; } }
public sealed record DraftRevisionInput(Guid Revision);

