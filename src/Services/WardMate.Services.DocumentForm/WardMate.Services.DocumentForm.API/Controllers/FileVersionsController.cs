using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.API.Security;
using WardMate.Services.DocumentForm.Application.FileVersions;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>DOC-FILEVER-01: tệp tờ khai/giấy tờ theo từng lần gửi hồ sơ, để cán bộ so sánh giữa các lần gửi.</summary>
[ApiController, Authorize(Policy = "DocumentUser")]
[Route("api/v1/applications/{applicationId:guid}/file-versions")]
[Tags("File Versions — Tệp theo lần gửi")]
public sealed class FileVersionsController(IMediator mediator, ApplicationAccess access) : ControllerBase
{
    /// <summary>
    /// Workflow gọi (bằng token của người nộp) ngay trước khi tạo lần gửi <paramref name="versionNumber"/>. Chỉ chủ hồ sơ khi hồ sơ
    /// đang DRAFT/NEED_REVISION và đúng lần gửi kế tiếp; sau khi lần gửi được tạo, bản chụp không sửa được nữa.
    /// </summary>
    [HttpPut("{versionNumber:int}"), Authorize(Policy = "DocumentCitizen")]
    [ProducesResponseType(typeof(FileVersionDto[]), 200), ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Capture(Guid applicationId, int versionNumber, CancellationToken ct)
    {
        var error = await access.Check(applicationId, null, true, ct);
        if (error is not null) return error;
        if (access.CurrentVersion(applicationId) is not { } current || versionNumber != current + 1)
            return Problem(409, "document.invalid_version", "Chỉ chụp tệp cho lần gửi kế tiếp của hồ sơ.");
        return Respond(await mediator.Send(new CaptureFileVersionsCommand(applicationId, versionNumber), ct));
    }

    /// <summary>Danh sách tệp theo lần gửi (bỏ <c>versionNumber</c> để lấy mọi lần gửi). Chỉ cán bộ thụ lý/được tham vấn.</summary>
    [HttpGet, ProducesResponseType(typeof(FileVersionDto[]), 200)]
    public async Task<IActionResult> List(Guid applicationId, [FromQuery] int? versionNumber, CancellationToken ct)
    {
        if (versionNumber is < 1) return Problem(400, "document.invalid_version", "versionNumber phải từ 1.");
        var error = await access.Officer(applicationId, ct);
        if (error is not null) return error;
        return Respond(await mediator.Send(new ListFileVersionsQuery(applicationId, versionNumber), ct));
    }

    /// <summary>Tải tệp của một lần gửi cũ hoặc mới.</summary>
    [HttpGet("{fileVersionId:guid}/download"), ProducesResponseType(200), ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Download(Guid applicationId, Guid fileVersionId, CancellationToken ct)
    {
        var error = await access.Officer(applicationId, ct);
        if (error is not null) return error;
        var result = await mediator.Send(new DownloadFileVersionQuery(applicationId, fileVersionId), ct);
        if (!result.IsSuccess) return Failure(result.Error);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>So sánh tờ khai của một mục giữa hai lần gửi: theo ô điền (tờ khai trực tuyến) hoặc theo đoạn văn (DOCX). Chỉ trả dòng thay đổi.</summary>
    [HttpGet("diff"), ProducesResponseType(typeof(FileFieldDiffDto[]), 200)]
    public async Task<IActionResult> Diff(Guid applicationId, [FromQuery] int fromVersion, [FromQuery] int toVersion, [FromQuery] Guid checklistId, CancellationToken ct)
    {
        if (checklistId == Guid.Empty) return Problem(400, "document.invalid_checklist", "Cần checklistId.");
        var error = await access.Officer(applicationId, ct);
        if (error is not null) return error;
        return Respond(await mediator.Send(new DiffFileVersionsQuery(applicationId, fromVersion, toVersion, checklistId), ct));
    }

    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) : Failure(result.Error);

    private ObjectResult Failure(Error error) => Problem(error.Type switch
    {
        ErrorType.NotFound => 404, ErrorType.Forbidden => 403, ErrorType.Conflict => 409, ErrorType.Failure => 503, _ => 400
    }, error.Code, error.Description);

    private ObjectResult Problem(int status, string code, string title) => new(new ProblemDetails
    {
        Status = status, Title = title, Instance = Request.Path,
        Extensions = { ["code"] = code, ["traceId"] = HttpContext.TraceIdentifier }
    }) { StatusCode = status, ContentTypes = { "application/problem+json" } };
}
