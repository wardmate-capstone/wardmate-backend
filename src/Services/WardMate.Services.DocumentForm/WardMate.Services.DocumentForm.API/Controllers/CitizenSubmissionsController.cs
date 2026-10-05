using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// [CITIZEN] Luồng người dân: quản lý hồ sơ điện tử của bản thân.
/// Bao gồm: lưu nháp, cập nhật nháp, nộp hồ sơ chính thức, tải file về để điền tiếp.
/// Base path: /api/v1/citizen/submissions
/// </summary>
[ApiController]
[Route("api/v1/citizen/submissions")]
[Produces("application/json")]
[Tags("Citizen — Hồ sơ người dân")]
public sealed class CitizenSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CitizenSubmissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách tất cả hồ sơ của người dân theo applicantId (có phân trang, lọc theo trạng thái).
    /// </summary>
    /// <remarks>
    /// Ví dụ: GET /api/v1/citizen/submissions?applicantId=...&amp;status=Draft&amp;page=1&amp;pageSize=20
    ///
    /// Các trạng thái hợp lệ: Draft | Submitted | RevisionRequested | Approved
    /// </remarks>
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
    /// Xem chi tiết một hồ sơ theo ID.
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
    /// Tạo mới bản nháp hồ sơ (POST multipart/form-data).
    /// Người dân chọn biểu mẫu, upload file .docx đã điền một phần và lưu tạm.
    /// Có thể gọi lại nhiều lần để cập nhật file trước khi nộp chính thức.
    /// </summary>
    /// <remarks>
    /// Form fields:
    /// - templateId (Guid): ID biểu mẫu gốc
    /// - applicantId (Guid): ID người dân
    /// - file (IFormFile): File .docx đã điền (một phần hoặc toàn bộ)
    ///
    /// Response: { submissionId, status: "Draft", blobUrl, ... }
    /// </remarks>
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
    /// Cập nhật bản nháp hiện có (PUT multipart/form-data).
    /// Dùng khi người dân điền thêm và muốn lưu tiến độ trước khi nộp chính thức.
    /// Cũng dùng sau khi cán bộ trả hồ sơ (RevisionRequested) để upload file đã sửa.
    /// </summary>
    /// <remarks>
    /// Form fields:
    /// - applicantId (Guid): xác minh chủ sở hữu
    /// - file (IFormFile): File .docx đã cập nhật
    ///
    /// Response: { submissionId, status: "Draft", blobUrl, ... }
    /// </remarks>
    [HttpPut("{submissionId:guid}/draft")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            TemplateId: Guid.Empty,
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
    /// Nộp hồ sơ chính thức (Draft → Submitted).
    /// Sau khi nộp, hồ sơ chuyển sang hàng đợi cán bộ xét duyệt.
    /// </summary>
    /// <remarks>
    /// Body: { "applicantId": "..." }
    ///
    /// Response: { submissionId, status: "Submitted", submittedAt, ... }
    /// </remarks>
    [HttpPost("{submissionId:guid}/submit")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(
        Guid submissionId,
        [FromBody] CitizenSubmitRequest request,
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
    /// Tải file DOCX của hồ sơ về máy.
    /// Người dân dùng để mở lại trong Word/LibreOffice, điền thêm rồi upload lại.
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
}

// ─── Request Models ───────────────────────────────────────────────────────────

public sealed record CitizenSubmitRequest
{
    public Guid ApplicantId { get; init; }
}
