using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.API.Controllers;

/// <summary>
/// Tra cứu hồ sơ bằng mã QR — không cần đăng nhập.
/// Dùng khi người dùng hoặc cơ quan chức năng quét mã QR in trên tờ khai đã phê duyệt.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/public")]
[Produces("application/json")]
[Tags("QR Lookup — Tra cứu hồ sơ qua mã QR")]
public sealed class QrLookupController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Quét mã QR → redirect 302 thẳng đến file PDF mới nhất của hồ sơ (SAS URL 15 phút).
    /// Không cần đăng nhập. Mã QR được in trên tờ khai sau khi giấy tờ sẵn sàng.
    /// </summary>
    /// <param name="applicationId">ID hồ sơ được mã hoá trong QR</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("applications/{applicationId:guid}/pdf-redirect")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PdfRedirect(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetQrPdfRedirectQuery(applicationId), cancellationToken);
        if (!result.IsSuccess)
            return Failure(result.Error);

        // Redirect trình duyệt / viewer thẳng đến SAS URL của Azure Blob
        return Redirect(result.Value.SasUrl);
    }

    /// <summary>
    /// Tra cứu thông tin hồ sơ bằng token hoặc ApplicationId (query param).
    /// </summary>
    /// <param name="token">Giá trị chuỗi QR hoặc mã token</param>
    /// <param name="applicationId">ID hồ sơ cần tra cứu</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("qr-lookup")]
    [ProducesResponseType(typeof(QrLookupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupQuery(
        [FromQuery] string? token,
        [FromQuery] Guid? applicationId,
        CancellationToken cancellationToken)
    {
        var targetToken = !string.IsNullOrWhiteSpace(token)
            ? token
            : applicationId?.ToString();

        if (string.IsNullOrWhiteSpace(targetToken))
        {
            return Failure(Error.Validation("qr.token_required", "Vui lòng cung cấp token hoặc applicationId để tra cứu."));
        }

        var result = await mediator.Send(new QrLookupQuery(targetToken), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>
    /// Tra cứu thông tin hồ sơ bằng token QR truyền trên route path (hỗ trợ cả URL đầy đủ).
    /// </summary>
    /// <param name="token">Giá trị QR code đọc được từ camera hoặc ảnh</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("qr-lookup/{**token}")]
    [ProducesResponseType(typeof(QrLookupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Lookup(string token, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new QrLookupQuery(token), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    private ObjectResult Failure(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Forbidden => 403,
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
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
