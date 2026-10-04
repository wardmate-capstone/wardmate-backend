using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Commands;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

[ApiController]
[Route("api/v1/form-templates")]
[Produces("application/json")]
public sealed class FormTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FormTemplatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách biểu mẫu điện tử (có phân trang).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FormTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? searchCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetFormTemplatesQuery(page, pageSize, isActive, searchCode), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
    }

    /// <summary>Lấy chi tiết biểu mẫu điện tử.</summary>
    [HttpGet("{templateId:guid}")]
    [ProducesResponseType(typeof(FormTemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetFormTemplateByIdQuery(templateId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(result.Value);
    }

    /// <summary>Tạo mới biểu mẫu điện tử (chỉ tạo tên/mã, chưa có file).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FormTemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFormTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateFormTemplateCommand(
            request.Code,
            request.Title,
            CreatedBy: User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.Conflict
                ? Conflict(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { templateId = result.Value.Id },
            result.Value);
    }

    /// <summary>Upload file Word DOCX phôi mẫu gốc của nhà nước (không cần chỉnh sửa).</summary>
    [HttpPost("{templateId:guid}/upload-docx")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDocxResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadDocx(
        Guid templateId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { code = "document.empty_file", message = "No file was uploaded." });
        }

        using var stream = file.OpenReadStream();
        var command = new UploadFormTemplateDocxCommand(
            templateId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            UpdatedBy: User.Identity?.Name);

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
    /// Tải file DOCX phôi mẫu về dưới dạng binary (FE dùng để load vào trình editor).
    /// FE nhận được file .docx thực sự, load vào Syncfusion / OnlyOffice / EditDocx.
    /// </summary>
    [HttpGet("{templateId:guid}/download-docx")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocx(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new DownloadFormTemplateDocxQuery(templateId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return File(
            result.Value.FileStream,
            result.Value.ContentType,
            result.Value.FileName);
    }

    /// <summary>
    /// Lấy URL tạm thời (SAS URL) để FE nhúng trực tiếp vào trình soạn thảo Word Online.
    /// URL hết hạn sau validForMinutes phút (mặc định 60 phút).
    /// </summary>
    [HttpGet("{templateId:guid}/docx-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocxUrl(
        Guid templateId,
        [FromQuery] int validForMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetFormTemplateDocxSasUrlQuery(templateId, validForMinutes), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return Ok(new
        {
            templateId,
            docxUrl = result.Value,
            expiresInMinutes = validForMinutes
        });
    }
}

public sealed record CreateFormTemplateRequest
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}
