using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure.Knowledge;

namespace WardMate.Services.AIOCR.API.Controllers;

[ApiController, Authorize, KnowledgeAccess, Route("api/v1/ai/knowledge"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403),
 ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class KnowledgeController(IKnowledgeBase knowledge) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("documents"), ProducesResponseType<Page<KnowledgeDocumentSummaryDto>>(200)]
    public async Task<IActionResult> List([FromQuery] KnowledgeListQuery query, CancellationToken ct) => Ok(await knowledge.List(query, ct));

    [HttpGet("documents/{id:guid}"), ProducesResponseType<KnowledgeDocumentDto>(200)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await knowledge.Get(id, ct));

    [HttpGet("documents/{id:guid}/chunks"), ProducesResponseType<Page<KnowledgeChunkDto>>(200)]
    public async Task<IActionResult> Chunks(Guid id, CancellationToken ct, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20) =>
        Ok(await knowledge.Chunks(id, pageNumber, pageSize, ct));

    [HttpPost("documents"), RequestSizeLimit(1024 * 1024), ProducesResponseType<KnowledgeDocumentDto>(201), ProducesResponseType<ProblemDetails>(422)]
    public async Task<IActionResult> Create(CreateKnowledgeTextInput input, CancellationToken ct)
    {
        var doc = await knowledge.CreateText(Actor, input, ct);
        return Created($"/api/v1/ai/knowledge/documents/{doc.Id}", doc);
    }

    [HttpPost("documents/pdf"), Consumes("multipart/form-data"), RequestSizeLimit(KnowledgePdfReader.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = KnowledgePdfReader.MaxBytes + 64 * 1024)]
    [ProducesResponseType<KnowledgeDocumentDto>(201), ProducesResponseType<ProblemDetails>(413), ProducesResponseType<ProblemDetails>(415), ProducesResponseType<ProblemDetails>(422)]
    public async Task<IActionResult> CreatePdf([FromForm] KnowledgePdfUpload input, CancellationToken ct)
    {
        if (input.File is null || input.File.Length <= 0) throw new AiFailure(400, "knowledge.validation", "Cần file PDF.");
        if (input.File.Length > KnowledgePdfReader.MaxBytes) throw new AiFailure(413, "knowledge.pdf_too_large", "PDF tối đa 10 MiB.");
        await using var stream = input.File.OpenReadStream();
        var doc = await knowledge.CreatePdf(Actor, new(input.Title, input.ProcedureId, input.File.FileName), stream, ct);
        return Created($"/api/v1/ai/knowledge/documents/{doc.Id}", doc);
    }

    [HttpPut("documents/{id:guid}"), RequestSizeLimit(1024 * 1024), ProducesResponseType<KnowledgeDocumentDto>(200), ProducesResponseType<ProblemDetails>(422)]
    public async Task<IActionResult> Update(Guid id, UpdateKnowledgeInput input, CancellationToken ct) => Ok(await knowledge.Update(Actor, id, input, ct));

    [HttpPost("documents/{id:guid}/activation"), RequestSizeLimit(4096), ProducesResponseType<KnowledgeDocumentDto>(200)]
    public async Task<IActionResult> Activation(Guid id, KnowledgeActivationInput input, CancellationToken ct) => Ok(await knowledge.SetActive(Actor, id, input, ct));

    [HttpPost("documents/{id:guid}/rechunk"), RequestSizeLimit(4096), ProducesResponseType<KnowledgeDocumentDto>(200)]
    public async Task<IActionResult> Rechunk(Guid id, KnowledgeRevisionInput input, CancellationToken ct) => Ok(await knowledge.Rechunk(Actor, id, input, ct));

    [HttpPost("documents/{id:guid}/reindex"), RequestSizeLimit(4096), ProducesResponseType<KnowledgeDocumentDto>(200)]
    public async Task<IActionResult> Reindex(Guid id, KnowledgeRevisionInput input, CancellationToken ct) => Ok(await knowledge.Reindex(Actor, id, input, ct));

    [HttpGet("index-status"), ProducesResponseType<KnowledgeIndexStatus>(200)]
    public async Task<IActionResult> IndexStatus(CancellationToken ct) => Ok(await knowledge.IndexStatus(ct));

    /// <summary>Thử tìm kiếm trên Knowledge Base để kiểm tra chất lượng nguồn trước khi bật RAG; mỗi lần gọi tạo một embedding.</summary>
    [HttpPost("search"), RequestSizeLimit(8192), ProducesResponseType<KnowledgeSearchHitDto[]>(200)]
    public async Task<IActionResult> Search(KnowledgeSearchInput input, CancellationToken ct) => Ok(await knowledge.Search(input, ct));

    [HttpPost("procedures/{procedureId:guid}/sync"), ProducesResponseType<ProcedureSyncResult>(200)]
    public async Task<IActionResult> SyncProcedure(Guid procedureId, CancellationToken ct) => Ok(await knowledge.SyncProcedure(Actor, procedureId, ct));

    [HttpPost("procedures/sync"), ProducesResponseType<ProcedureSyncAllResult>(200)]
    public async Task<IActionResult> SyncAll(CancellationToken ct) => Ok(await knowledge.SyncAllProcedures(Actor, ct));
}

public sealed class KnowledgePdfUpload
{
    public IFormFile? File { get; set; }
    public string? Title { get; set; }
    public Guid? ProcedureId { get; set; }
}
