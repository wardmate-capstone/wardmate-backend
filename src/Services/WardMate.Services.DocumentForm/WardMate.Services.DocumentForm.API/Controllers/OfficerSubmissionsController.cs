using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// [OFFICER / FRONT-DESK] Luồng cán bộ: xét duyệt, yêu cầu sửa, phê duyệt hồ sơ.
/// Sau khi cán bộ duyệt (Approved), người dân mới được phép kích hoạt xuất file PDF.
/// Base path: /api/v1/officer/submissions
/// </summary>
[ApiController]
[Authorize(Policy = "DocumentUser")]
[Route("api/v1/officer/submissions")]
[Produces("application/json")]
[Tags("Officer — Xét duyệt hồ sơ (Cán bộ)")]
public sealed class OfficerSubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OfficerSubmissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Cán bộ] Trả hồ sơ lại cho người dân kèm ghi chú yêu cầu sửa (Submitted → RevisionRequested).
    /// </summary>
    [HttpPost("{submissionId:guid}/request-revision")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
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

        var officerId = request.OfficerId ?? CallerUserId ?? Guid.Empty;
        if (officerId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "officer.missing_id",
                Detail = "Officer ID must be specified or present in the token claims."
            });
        }

        var command = new RequestRevisionCommand(
            submissionId,
            officerId,
            request.Comment,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// [Cán bộ] Duyệt hồ sơ hoàn tất (Submitted → Approved).
    /// Sau khi duyệt, người dân mới có quyền xuất và tải file PDF chính thức.
    /// </summary>
    [HttpPost("{submissionId:guid}/approve")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid submissionId,
        [FromBody] OfficerApproveRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer)
        {
            return Forbid();
        }

        var officerId = request?.OfficerId ?? CallerUserId ?? Guid.Empty;
        if (officerId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "officer.missing_id",
                Detail = "Officer ID must be specified or present in the token claims."
            });
        }

        var command = new ApproveSubmissionCommand(
            submissionId,
            officerId,
            User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    private Guid? CallerUserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
    private bool IsOfficer => User.IsInRole("FRONT_DESK_OFFICER")
                           || User.IsInRole("MANAGER")
                           || User.IsInRole("PROCEDURE_MANAGER")
                           || User.IsInRole("IT_ADMIN");

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

public sealed record OfficerApproveRequest
{
    public Guid? OfficerId { get; init; }
}
