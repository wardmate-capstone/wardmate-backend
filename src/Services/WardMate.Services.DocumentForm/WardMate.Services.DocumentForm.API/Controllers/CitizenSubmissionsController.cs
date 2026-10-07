using System.Text.Json;
using MediatR;
using WardMate.Services.DocumentForm.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;
namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>Đơn điện tử thuộc một bộ hồ sơ; người dân điền trên web bằng schema và formData.</summary>
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

    /// <summary>Mở lại đơn cùng formData và schema phiên bản đã dùng để FE tiếp tục chỉnh sửa.</summary>
    [HttpGet("{submissionId:guid}")]
    [ProducesResponseType(typeof(UserSubmissionSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid submissionId, [FromQuery] Guid applicantId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSubmissionByIdQuery(submissionId, applicantId), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Lưu nháp JSON; backend điền bản sao DOCX gốc và lưu Blob. Không upload Word từ người dân.</summary>
    [HttpPost("draft")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status201Created)]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateDraft([FromBody] CreateOnlineDraftRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SaveDraftSubmissionCommand(null, request.TemplateId,
            request.ApplicantId, request.FormData, request.TemplateVersionId, User.Identity?.Name), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById),
            new { submissionId = result.Value.SubmissionId, applicantId = request.ApplicantId }, result.Value) : Failure(result.Error);
    }

    /// <summary>Thay thế toàn bộ formData của bản nháp bằng JSON mới; phiên bản mẫu được giữ cố định.</summary>
    [HttpPut("{submissionId:guid}/draft")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateDraft(Guid submissionId, [FromBody] UpdateOnlineDraftRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SaveDraftSubmissionCommand(submissionId, Guid.Empty,
            request.ApplicantId, request.FormData, request.TemplateVersionId, User.Identity?.Name), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Kiểm tra dữ liệu bắt buộc và chốt đơn. Không nộp hay xét duyệt toàn bộ hồ sơ hành chính.</summary>
    [HttpPost("{submissionId:guid}/submit")]
    [ProducesResponseType(typeof(SaveSubmissionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid submissionId, [FromBody] CitizenSubmitRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SubmitSubmissionCommand(submissionId, request.ApplicantId, User.Identity?.Name), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Tải DOCX đã sinh để xem/in; người dân không cần sửa và upload lại file.</summary>
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
        if (error.Code == "document.invalid_form_data" && error.Description.StartsWith("[", StringComparison.Ordinal))
        {
            problem.Detail = "Form data validation failed.";
            problem.Extensions["fieldErrors"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(error.Description);
        }
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
public sealed record CitizenSubmitRequest(Guid ApplicantId);
public sealed record CreateOnlineDraftRequest(Guid TemplateId, Guid ApplicantId, JsonElement FormData, Guid? TemplateVersionId = null);
public sealed record UpdateOnlineDraftRequest(Guid ApplicantId, JsonElement FormData, Guid? TemplateVersionId = null);
