using System.ComponentModel.DataAnnotations;
using MediatR;
using WardMate.Services.DocumentForm.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;
namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>Đơn điện tử thuộc một bộ hồ sơ; người dân chỉnh sửa DOCX bằng editor trên web.</summary>
[ApiController]
[Route("api/v1/citizen/submissions")]
[Produces("application/json")]
[Tags("Citizen — Đơn điện tử online")]
public sealed class CitizenSubmissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserSubmissionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySubmissions([FromQuery] Guid applicantId,
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetMySubmissionsQuery(applicantId, status, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Lấy metadata đơn; tải download-docx để mở lại bản đã lưu trong editor.</summary>
    [HttpGet("{submissionId:guid}")]
    [ProducesResponseType(typeof(UserSubmissionSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid submissionId, [FromQuery] Guid applicantId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSubmissionByIdQuery(submissionId, applicantId), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Lưu nguyên file DOCX do editor xuất ra; không thay đổi mẫu gốc.</summary>
    [HttpPost("draft")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status201Created)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateDraft([FromForm] CreateOnlineDraftRequest request, CancellationToken ct)
    {
        using var stream = request.File.OpenReadStream();
        var result = await mediator.Send(new SaveDraftSubmissionCommand(null, request.TemplateId,
            request.ApplicantId, stream, request.File.FileName, request.File.Length, User.Identity?.Name), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById),
            new { submissionId = result.Value.SubmissionId, applicantId = request.ApplicantId }, result.Value) : Failure(result.Error);
    }

    /// <summary>Lưu phiên bản DOCX mới của bản nháp hiện có.</summary>
    [HttpPut("{submissionId:guid}/draft")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateDraft(Guid submissionId, [FromForm] UpdateOnlineDraftRequest request, CancellationToken ct)
    {
        using var stream = request.File.OpenReadStream();
        var result = await mediator.Send(new SaveDraftSubmissionCommand(submissionId, Guid.Empty,
            request.ApplicantId, stream, request.File.FileName, request.File.Length, User.Identity?.Name), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Người dân xác nhận hoàn thành và chốt bản DOCX đã lưu. Không nộp hay xét duyệt toàn bộ hồ sơ hành chính.</summary>
    [HttpPost("{submissionId:guid}/submit")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid submissionId, [FromBody] CitizenSubmitRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SubmitSubmissionCommand(submissionId, request.ApplicantId, User.Identity?.Name), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Tải nguyên DOCX đã lưu để editor mở lại, hoặc để xem/in.</summary>
    [HttpGet("{submissionId:guid}/download-docx")]
    public async Task<IActionResult> DownloadDocx(Guid submissionId, [FromQuery] Guid applicantId, CancellationToken ct)
    {
        var result = await mediator.Send(new DownloadSubmissionDocxQuery(submissionId, applicantId), ct);
        return result.IsSuccess ? File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName) : Failure(result.Error);
    }

    private ObjectResult Failure(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => 404, ErrorType.Forbidden => 403,
            ErrorType.Conflict => 409, ErrorType.Failure => 503, _ => 400
        };
        var problem = new ProblemDetails { Status = status, Title = error.Code,
            Detail = error.Description, Instance = HttpContext.Request.Path };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
public sealed record CitizenSubmitRequest(Guid ApplicantId);
public sealed class CreateOnlineDraftRequest
{
    public Guid TemplateId { get; init; }
    public Guid ApplicantId { get; init; }
    [Required] public IFormFile File { get; init; } = null!;
}
public sealed class UpdateOnlineDraftRequest
{
    public Guid ApplicantId { get; init; }
    [Required] public IFormFile File { get; init; } = null!;
}
