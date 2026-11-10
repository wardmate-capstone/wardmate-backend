using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WardMate.Services.DocumentForm.Application.Queries;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// DOC-TPL-PUB-01 (BE-J): tải phôi mẫu đơn/tờ khai trống không cần đăng nhập, như Cổng DVC.
/// FE dùng cùng API cho nút "Tải mẫu" và "Xem" (xem trước bằng file tải về). Chỉ mở đúng API tải file của mẫu
/// đang hoạt động; API chi tiết và quản trị mẫu vẫn bắt đăng nhập.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-forms")]
[Route("api/v1/public/form-templates")]
[Tags("Public form templates — Tải phôi mẫu đơn công khai")]
public sealed class PublicFormTemplatesController(IMediator mediator) : ControllerBase
{
    /// <summary>Tải file .docx trống của mẫu đang hoạt động. Mẫu ẩn, đã xóa, không tồn tại hoặc chưa có file → 404.</summary>
    [HttpGet("{templateId:guid}/download-docx")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DownloadDocx(Guid templateId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadFormTemplateDocxQuery(templateId, PublicOnly: true), cancellationToken);
        if (!result.IsSuccess)
        {
            Response.Headers.CacheControl = "no-store";
            var problem = new ProblemDetails
            {
                Status = 404,
                Title = "document.template_not_found",
                Detail = "Không tìm thấy mẫu đơn hoặc mẫu đã ngừng sử dụng.",
                Instance = Request.Path
            };
            problem.Extensions["code"] = "document.template_not_found";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return new ObjectResult(problem) { StatusCode = 404, ContentTypes = { "application/problem+json" } };
        }
        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }
}
