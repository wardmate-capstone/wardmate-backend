using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// Quản lý hồ sơ điện tử của người dân (UserSubmission).
/// Hỗ trợ: lưu nháp nhiều lần, nộp hồ sơ, cán bộ comment và duyệt, xuất file sạch.
/// </summary>
[ApiController]
[Route("api/v1/user-submissions")]
[Produces("application/json")]
public sealed class UserSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserSubmissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── NGƯỜI DÂN ────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách hồ sơ của người dân (theo applicantId).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserSubmissionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySubmissions(
        [FromQuery] Guid applicantId,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetMySubmissionsQuery(applicantId, status, page, pageSize), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
    }

    /// <summary>
    /// Lấy chi tiết một hồ sơ theo ID.
    /// </summary>
    [HttpGet("{submissionId:guid}")]
    [ProducesResponseType(typeof(UserSubmissionSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSubmissionByIdQuery(submissionId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tạo mới bản nháp hồ sơ (lần đầu người dân upload file đã điền một phần).
    /// FE gửi file .docx người dân đã điền + templateId + applicantId.
    /// </summary>
    [HttpPost("draft")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDraft(
        [FromForm] Guid templateId,
        [FromForm] Guid applicantId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "document.empty_file", message = "No file was uploaded." });

        using var stream = file.OpenReadStream();
        var command = new SaveDraftSubmissionCommand(
            SubmissionId: null,
            TemplateId: templateId,
            ApplicantId: applicantId,
            FileStream: stream,
            FileName: file.FileName,
            FileSizeBytes: file.Length,
            SavedBy: User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { code = result.Error.Code, message = result.Error.Description });

        return CreatedAtAction(nameof(GetById),
            new { submissionId = result.Value.SubmissionId },
            result.Value);
    }

    /// <summary>
    /// Cập nhật bản nháp đã tồn tại (người dân điền tiếp, lưu lại).
    /// FE gửi file .docx đã điền thêm để thay thế file nháp cũ.
    /// </summary>
    [HttpPut("{submissionId:guid}/draft")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDraft(
        Guid submissionId,
        [FromForm] Guid applicantId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "document.empty_file", message = "No file was uploaded." });

        using var stream = file.OpenReadStream();
        var command = new SaveDraftSubmissionCommand(
            SubmissionId: submissionId,
            TemplateId: Guid.Empty,          // không cần khi cập nhật
            ApplicantId: applicantId,
            FileStream: stream,
            FileName: file.FileName,
            FileSizeBytes: file.Length,
            SavedBy: User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : result.Error.Type == ErrorType.Forbidden
                    ? StatusCode(StatusCodes.Status403Forbidden, new { code = result.Error.Code, message = result.Error.Description })
                    : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Nộp hồ sơ chính thức (Draft → Submitted). Chuyển cho cán bộ xét duyệt.
    /// </summary>
    [HttpPost("{submissionId:guid}/submit")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(
        Guid submissionId,
        [FromBody] SubmitSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SubmitSubmissionCommand(
            submissionId,
            request.ApplicantId,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : result.Error.Type == ErrorType.Forbidden
                    ? StatusCode(StatusCodes.Status403Forbidden, new { code = result.Error.Code, message = result.Error.Description })
                    : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tải file DOCX của hồ sơ về (để điền tiếp hoặc cán bộ xem nội dung).
    /// </summary>
    [HttpGet("{submissionId:guid}/download-docx")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocx(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DownloadSubmissionDocxQuery(submissionId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }

    // ── CÁN BỘ ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cán bộ yêu cầu sửa lại — trả về hồ sơ kèm comment hướng dẫn (Submitted → RevisionRequested).
    /// </summary>
    [HttpPost("{submissionId:guid}/request-revision")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRevision(
        Guid submissionId,
        [FromBody] RequestRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new RequestRevisionCommand(
            submissionId,
            request.OfficerId,
            request.Comment,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cán bộ duyệt hồ sơ (Submitted → Approved). Người dân có thể xuất file sạch để in.
    /// </summary>
    [HttpPost("{submissionId:guid}/approve")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid submissionId,
        [FromBody] ApproveSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ApproveSubmissionCommand(
            submissionId,
            request.OfficerId,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }
}

// ─── Request Models ───────────────────────────────────────────────────────────

public sealed record SubmitSubmissionRequest
{
    public Guid ApplicantId { get; init; }
}

public sealed record RequestRevisionRequest
{
    public Guid OfficerId { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public sealed record ApproveSubmissionRequest
{
    public Guid OfficerId { get; init; }
}
