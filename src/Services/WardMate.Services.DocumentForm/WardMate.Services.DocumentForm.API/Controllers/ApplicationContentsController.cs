using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.DocumentForm.API.Security;
using WardMate.Services.DocumentForm.Application.Queries;

namespace WardMate.Services.DocumentForm.API.Controllers;

[ApiController, Authorize(Policy = "DocumentCitizen")]
[Route("api/v1/applications/{applicationId:guid}/contents")]
[Tags("Application Contents — Đơn và giấy tờ của hồ sơ")]
public sealed class ApplicationContentsController(IMediator mediator, ApplicationAccess access) : ControllerBase
{
    /// <summary>Lấy toàn bộ đơn và giấy tờ thuộc hồ sơ của người đăng nhập, không phân trang bỏ sót.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApplicationContentsDto), 200)]
    public async Task<IActionResult> Get(Guid applicationId, CancellationToken ct)
    {
        var error = await access.Check(applicationId, null, false, ct);
        if (error is not null) return error;
        var result = await mediator.Send(new GetApplicationContentsQuery(applicationId, Guid.Parse(User.FindFirst("sub")!.Value)), ct);
        if (!result.IsSuccess) return new ObjectResult(new ProblemDetails
        {
            Status = 503, Title = result.Error.Description,
            Extensions = { ["code"] = result.Error.Code, ["traceId"] = HttpContext.TraceIdentifier }
        }) { StatusCode = 503 };
        return Ok(result.Value);
    }
}
