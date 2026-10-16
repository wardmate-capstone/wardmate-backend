using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// [OFFICER / FRONT-DESK] Luồng cán bộ: xét duyệt, yêu cầu sửa, xác nhận giấy tờ sẵn sàng hồ sơ.
/// Sau khi cán bộ xác nhận sẵn sàng (ReadyForDownload), người dân mới được phép kích hoạt xuất file PDF.
/// Base path: /api/v1/officer/submissions
/// </summary>
[ApiController]
[Authorize(Policy = "DocumentUser")]
[Route("api/v1/officer/submissions")]
[Produces("application/json")]
[Tags("Officer — Xét kiểm tra giấy tờ (Cán bộ)")]
public sealed class OfficerSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WardMate.Services.DocumentForm.API.Security.ApplicationAccess _access;

    public OfficerSubmissionsController(IMediator mediator, WardMate.Services.DocumentForm.API.Security.ApplicationAccess access)
    {
        _mediator = mediator;
        _access = access;
    }

    /// <summary>
    /// [Cán bộ] Lấy danh sách các đơn điện tử (submissions) thuộc hồ sơ để biết submissionId thực hiện review / download / annotate / confirm-readiness.
    /// </summary>
    [HttpGet("by-application/{applicationId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<UserSubmissionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionsByApplication(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer) return Forbid();
        var error = await _access.Read(applicationId, null, cancellationToken);
        if (error is not null) return error;

        var result = await _mediator.Send(new WardMate.Services.DocumentForm.Application.Queries.GetSubmissionsByApplicationQuery(applicationId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// [Cán bộ] Trả hồ sơ lại cho người dân kèm ghi chú yêu cầu sửa (Submitted → RevisionRequested).
    /// </summary>
    [HttpPost("{submissionId:guid}/request-revision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRevision(
        Guid submissionId,
        [FromBody] OfficerRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer)
        {
            return Forbid();
        }

        if (request.OfficerId.HasValue && request.OfficerId != CallerUserId) return Forbid();
        var error = await _access.Decide(submissionId, false, request.Comment, cancellationToken);
        return error is null ? Ok(new { submissionId, message = "Workflow đã yêu cầu bổ sung hồ sơ." }) : error;
    }

    /// <summary>
    /// [Cán bộ] Duyệt hồ sơ hoàn tất (Submitted → ReadyForDownload).
    /// Sau khi xác nhận sẵn sàng, người dân mới có quyền xuất và tải file PDF giấy tờ chuẩn bị.
    /// </summary>
    [HttpPost("{submissionId:guid}/confirm-readiness")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmReadiness(
        Guid submissionId,
        [FromBody] OfficerReadinessRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer)
        {
            return Forbid();
        }

        if (request?.OfficerId.HasValue == true && request.OfficerId != CallerUserId) return Forbid();
        var error = await _access.Decide(submissionId, true, null, cancellationToken);
        return error is null ? Ok(new { submissionId, message = "Workflow đã xác nhận giấy tờ sẵn sàng và đồng bộ đơn." }) : error;
    }

    /// <summary>
    /// [Cán bộ] Lưu lại file DOCX đã được thêm comment/ghi chú trực tiếp trên web editor.
    /// DOCX mới (có comment) thay thế bản hiện có. Status hồ sơ KHÔNG thay đổi.
    /// Người dân khi mở lại file (download-docx) sẽ thấy toàn bộ comment của cán bộ để sửa cho đúng.
    /// Sau khi annotate, cán bộ nên gọi request-revision để thông báo người dân cần sửa.
    /// </summary>
    [HttpPut("{submissionId:guid}/annotate-docx")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnnotateDocx(
        Guid submissionId,
        [FromForm] OfficerAnnotateDocxRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer)
            return Forbid();

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "document.empty_file",
                Detail = "File DOCX không được để trống."
            });
        }

        var access = await _access.OfficerSubmission(submissionId, true, cancellationToken);
        if (access.Error is not null) return access.Error;
        var officerId = CallerUserId ?? Guid.Empty;
        using var stream = request.File.OpenReadStream();

        var command = new SaveOfficerAnnotatedDocxCommand(
            submissionId,
            stream,
            request.File.FileName,
            request.File.Length,
            officerId,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// [Cán bộ] Tải file DOCX hiện tại của hồ sơ (bao gồm comment đã thêm nếu có).
    /// Dùng để mở lại trong web editor để đọc nội dung người dân điền, thêm comment rồi lưu lại.
    /// </summary>
    [HttpGet("{submissionId:guid}/download-docx")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocx(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer)
            return Forbid();

        var access = await _access.OfficerSubmission(submissionId, false, cancellationToken);
        if (access.Error is not null) return access.Error;
        var result = await _mediator.Send(
            new WardMate.Services.DocumentForm.Application.Queries.DownloadSubmissionDocxQuery(
                submissionId, Guid.Empty), cancellationToken);

        if (!result.IsSuccess)
            return Failure(result.Error);

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }

    private Guid? CallerUserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    private bool IsOfficer => User.IsInRole("FRONT_DESK_OFFICER");

    private ObjectResult Failure(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Forbidden => 403,
            ErrorType.Conflict => 409,
            ErrorType.Failure => 503,
            _ => 400
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Description,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}

public sealed record OfficerRevisionRequest
{
    public Guid? OfficerId { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public sealed record OfficerReadinessRequest
{
    public Guid? OfficerId { get; init; }
}

public sealed class OfficerAnnotateDocxRequest
{
    /// <summary>File DOCX đã được cán bộ thêm comment/ghi chú trên web editor.</summary>
    public IFormFile? File { get; set; }
}
