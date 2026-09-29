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
            : Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest, extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
    }

    /// <summary>Lấy chi tiết biểu mẫu điện tử kèm tất cả phiên bản schema.</summary>
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

    /// <summary>Tạo mới biểu mẫu điện tử (có thể kèm schema phiên bản đầu tiên).</summary>
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
            request.InitialSchemaDefinition,
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

    /// <summary>Thêm phiên bản schema mới cho biểu mẫu.</summary>
    [HttpPost("{templateId:guid}/versions")]
    [ProducesResponseType(typeof(FormTemplateVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddVersion(
        Guid templateId,
        [FromBody] AddSchemaVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateFormTemplateVersionCommand(
            templateId,
            request.SchemaDefinition,
            CreatedBy: User.Identity?.Name);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound
                ? NotFound(new { code = result.Error.Code, message = result.Error.Description })
                : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { templateId },
            result.Value);
    }

    /// <summary>Upload file Word DOCX phôi mẫu và bóc tách danh sách placeholder tự động.</summary>
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

    /// <summary>Bóc tách danh sách placeholder từ file Word DOCX (không lưu).</summary>
    [HttpPost("extract-placeholders")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExtractPlaceholders(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { code = "document.empty_file", message = "No file was uploaded." });
        }

        using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new ExtractDocxPlaceholdersQuery(stream, file.FileName), cancellationToken);

        return result.IsSuccess
            ? Ok(new { fileName = file.FileName, placeholders = result.Value })
            : BadRequest(new { code = result.Error.Code, message = result.Error.Description });
    }
}

public sealed record CreateFormTemplateRequest
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? InitialSchemaDefinition { get; init; }
}

public sealed record AddSchemaVersionRequest
{
    public string SchemaDefinition { get; init; } = string.Empty;
}
