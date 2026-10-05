using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// [OFFICER / FRONT-DESK] Luồng cán bộ: xét duyệt, trả hồ sơ, duyệt hoàn tất.
/// Bao gồm: xem danh sách hồ sơ chờ duyệt, xem chi tiết, tải file để kiểm tra,
/// trả hồ sơ kèm ghi chú, duyệt hồ sơ hoàn tất.
/// Base path: /api/v1/officer/submissions
/// </summary>
[ApiController]
[Route("api/v1/officer/submissions")]
[Produces("application/json")]
[Tags("Officer — Xét duyệt hồ sơ (Front-Desk)")]
public sealed class OfficerSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OfficerSubmissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Cán bộ] Lấy danh sách hồ sơ (có thể lọc theo trạng thái, phân trang).
    /// Dùng để hiển thị hàng đợi hồ sơ cần xét duyệt.
    /// </summary>
    /// <remarks>
    /// Ví dụ: GET /api/v1/officer/submissions?status=Submitted&amp;page=1&amp;pageSize=20
    ///
    /// Lọc hồ sơ đang chờ duyệt: status=Submitted
    /// Lọc hồ sơ đã trả về: status=RevisionRequested
    /// Lọc hồ sơ đã duyệt: status=Approved
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserSubmissionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissions(
        [FromQuery] Guid? applicantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Nếu không có applicantId thì dùng Guid.Empty để lấy tất cả hồ sơ
        var queryApplicantId = applicantId ?? Guid.Empty;
        var result = await _mediator.Send(
            new GetMySubmissionsQuery(queryApplicantId, status, page, pageSize), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
    }

    /// <summary>
    /// [Cán bộ] Xem chi tiết một hồ sơ đã nộp — bao gồm thông tin người nộp, trạng thái, ghi chú.
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
    /// [Cán bộ] Tải file DOCX của hồ sơ về để kiểm tra nội dung người dân đã điền.
    /// Cán bộ mở file, đọc thông tin, đưa ra quyết định duyệt hoặc trả về.
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

    /// <summary>
    /// [Cán bộ] Trả hồ sơ lại cho người dân kèm ghi chú yêu cầu sửa (Submitted → RevisionRequested).
    /// Người dân sẽ thấy comment và trạng thái "Cần chỉnh sửa", tải file về, sửa rồi nộp lại.
    /// </summary>
    /// <remarks>
    /// Body:
    /// ```json
    /// {
    ///   "officerId": "22222222-...",
    ///   "comment": "Vui lòng đính kèm thêm bản sao CMND/CCCD có công chứng."
    /// }
    /// ```
    ///
    /// Response: { submissionId, status: "RevisionRequested", officerComment, reviewedAt, ... }
    /// </remarks>
    [HttpPost("{submissionId:guid}/request-revision")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRevision(
        Guid submissionId,
        [FromBody] OfficerRevisionRequest request,
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
    /// [Cán bộ] Duyệt hồ sơ hoàn tất (Submitted → Approved).
    /// Sau khi duyệt, người dân có thể tải bản sạch (chỉ nội dung đã điền) về in và nộp giấy.
    /// </summary>
    /// <remarks>
    /// Body:
    /// ```json
    /// {
    ///   "officerId": "22222222-..."
    /// }
    /// ```
    ///
    /// Response: { submissionId, status: "Approved", reviewedAt, ... }
    /// </remarks>
    [HttpPost("{submissionId:guid}/approve")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid submissionId,
        [FromBody] OfficerApproveRequest request,
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

public sealed record OfficerRevisionRequest
{
    public Guid OfficerId { get; init; }
    public string Comment { get; init; } = string.Empty;
}

public sealed record OfficerApproveRequest
{
    public Guid OfficerId { get; init; }
}
