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
[Tags("Supporting Documents — Giấy tờ minh chứng")]
public sealed class SupportingDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WardMate.Services.DocumentForm.API.Security.ApplicationAccess _access;

    public SupportingDocumentsController(IMediator mediator, WardMate.Services.DocumentForm.API.Security.ApplicationAccess access)
    {
        _mediator = mediator;
        _access = access;
    }

    /// <summary>
    /// Tải lên một giấy tờ minh chứng đính kèm hồ sơ theo checklist.
    /// </summary>
    [HttpPost("applications/{applicationId:guid}/supporting-documents")]
    [Authorize(Policy = "DocumentCitizen")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SupportingDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSupportingDocument(
        Guid applicationId,
        [FromForm] UploadSupportingDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return Failure(Error.Validation("document.empty_file", "File upload is required."));
        }

        if (!request.ChecklistId.HasValue || request.ChecklistId == Guid.Empty)
            return Failure(Error.Validation("document.checklist_required", "ChecklistId is required."));
        var error = await _access.Check(applicationId, request.ChecklistId, true, cancellationToken, supporting: true);
        if (error is not null) return error;
        await using var stream = request.File.OpenReadStream();
        var command = new UploadSupportingDocumentCommand(
            applicationId,
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            request.ChecklistId,
            User.Identity?.Name, request.SubmissionType, request.DocumentCopyType, request.Quantity);

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result.Error);
        }

        return CreatedAtAction(
            nameof(GetSupportingDocuments),
            new { applicationId },
            result.Value);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ giấy tờ minh chứng đã tải lên của một hồ sơ.
    /// </summary>
    [HttpGet("applications/{applicationId:guid}/supporting-documents")]
    [ProducesResponseType(typeof(IReadOnlyList<SupportingDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupportingDocuments(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var error = await _access.Check(applicationId, null, false, cancellationToken,
            officer: User.IsInRole("FRONT_DESK_OFFICER"));
        if (error is not null) return error;
        var result = await _mediator.Send(
            new GetSupportingDocumentsQuery(applicationId),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// Xóa một giấy tờ minh chứng (soft-delete).
    /// </summary>
    [HttpDelete("supporting-documents/{documentId:guid}")]
    [Authorize(Policy = "DocumentCitizen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSupportingDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var error = await _access.Supporting(documentId, true, cancellationToken);
        if (error is not null) return error;
        var result = await _mediator.Send(
            new DeleteSupportingDocumentCommand(documentId, User.Identity?.Name),
            cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    /// <summary>
    /// Tải xuống file giấy tờ minh chứng từ Azure Blob Storage.
    /// </summary>
    [HttpGet("supporting-documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSupportingDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var error = await _access.Supporting(documentId, false, cancellationToken);
        if (error is not null) return error;
        var result = await _mediator.Send(
            new DownloadSupportingDocumentQuery(documentId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result.Error);
        }

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }

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

public sealed class UploadSupportingDocumentRequest
{
    public string? SubmissionType { get; set; }
    public string? DocumentCopyType { get; set; }
    public int Quantity { get; set; } = 1;
    public IFormFile File { get; set; } = null!;
    public Guid? ChecklistId { get; set; }
}
