using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController]
[Route("api/v1/procedure-manager/procedures")]
[Authorize(Policy = "ProcedureManager")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ProcedureManagerController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ProcedureDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ProcedureInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateProcedureCommand(input), ct);
        return result.IsSuccess ? Created($"/api/v1/procedures/{result.Value!.Id}", result.Value) : Failure(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProcedureDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateProcedureInput input, CancellationToken ct) =>
        Respond(await sender.Send(new UpdateProcedureCommand(id, input), ct));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ProcedureStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, ToggleStatusRequest input, CancellationToken ct)
    {
        if (input.IsActive is null)
            return Failure(new("validation.failed", "Dữ liệu không hợp lệ.", 400,
                new Dictionary<string, string[]> { ["isActive"] = ["Trạng thái hoạt động không được để trống."] }));
        return Respond(await sender.Send(new ToggleProcedureStatusCommand(id, input.IsActive.Value, input.Reason), ct));
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcedureVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Versions(Guid id, CancellationToken ct) =>
        Respond(await sender.Send(new GetProcedureVersionsQuery(id), ct));

    private IActionResult Respond<T>(ProcedureResult<T> result) => result.IsSuccess ? Ok(result.Value) : Failure(result.Error!);

    private ObjectResult Failure(ProcedureError error)
    {
        var problem = new ProblemDetails { Status = error.Status, Title = error.Message, Instance = Request.Path };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        if (error.Errors is not null) problem.Extensions["errors"] = error.Errors;
        var response = new ObjectResult(problem) { StatusCode = error.Status };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }
}

public sealed record ToggleStatusRequest(bool? IsActive, string? Reason);
