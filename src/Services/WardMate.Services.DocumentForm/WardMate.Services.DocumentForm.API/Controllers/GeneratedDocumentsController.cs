using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

[ApiController]
[Authorize(Policy = "DocumentUser")]
[Route("api/v1")]
[Produces("application/json")]
[Tags("Generated Documents — Văn bản kết xuất PDF")]
public sealed class GeneratedDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WardMate.Services.DocumentForm.API.Security.ApplicationAccess _access;

    public GeneratedDocumentsController(IMediator mediator, WardMate.Services.DocumentForm.API.Security.ApplicationAccess access)
    {
        _mediator = mediator;
        _access = access;
    }

    /// <summary>
    /// Kích hoạt Engine chuyển đổi DOCX sang PDF chính thức và lưu kết quả vào generated_documents.
    /// Chỉ định submissionId thuộc applicationId; dùng bản DOCX đã lưu, không nhận file thay thế.
    /// </summary>
    [HttpPost("applications/{applicationId:guid}/generate-pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(GeneratedDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GeneratePdf(
        Guid applicationId,
        [FromForm] GeneratePdfRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsOfficer)
        {
            var error = await _access.Check(applicationId, null, false, cancellationToken);
            if (error is not null) return error;
        }
        Stream? fileStream = null;
        string? fileName = null;

        if (request.File is not null && request.File.Length > 0)
        {
            fileStream = request.File.OpenReadStream();
            fileName = request.File.FileName;
        }

        var command = new GeneratePdfDocumentCommand(
            applicationId,
            request.DocumentType ?? "ORIGINAL_SUBMISSION",
            request.SubmissionId,
            fileStream,
            fileName,
            User.Identity?.Name,
            CallerUserId,
            IsOfficer);

        var result = await _mediator.Send(command, cancellationToken);

        if (fileStream is not null)
        {
            await fileStream.DisposeAsync();
        }

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ văn bản PDF kết xuất của một hồ sơ.
    /// </summary>
    [HttpGet("applications/{applicationId:guid}/generated-documents")]
    [ProducesResponseType(typeof(IReadOnlyList<GeneratedDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeneratedDocuments(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!IsOfficer)
        {
            var error = await _access.Check(applicationId, null, false, cancellationToken);
            if (error is not null) return error;
        }
        var result = await _mediator.Send(
            new GetGeneratedDocumentsQuery(applicationId, CallerUserId, IsOfficer),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// Tải xuống file PDF kết xuất từ Azure Blob Storage.
    /// </summary>
    [HttpGet("generated-documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadGeneratedDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!IsOfficer)
        {
            var error = await _access.Generated(documentId, cancellationToken);
            if (error is not null) return error;
        }
        var result = await _mediator.Send(
            new DownloadGeneratedDocumentQuery(documentId, CallerUserId, IsOfficer),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result.Error);
        }

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
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

public sealed class GeneratePdfRequest
{
    public string? DocumentType { get; set; } = "ORIGINAL_SUBMISSION";
    public Guid? SubmissionId { get; set; }
    public IFormFile? File { get; set; }
}
