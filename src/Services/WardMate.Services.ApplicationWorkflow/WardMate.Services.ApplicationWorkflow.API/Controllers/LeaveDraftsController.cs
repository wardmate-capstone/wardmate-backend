using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/staffing"), Authorize(Policy="workflow.staffing.read")]
public sealed class LeaveDraftsController(ILeaveDrafts drafts,IConfiguration config) : ReviewControllerBase
{
    private const string DocxMime="application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        if(string.IsNullOrWhiteSpace(Actor.WardCode)) return Forbid();
        var settings=LeaveTemplateSettings.Read(config,Actor.WardCode);
        return Ok(new {wordRequired=settings.WordRequired,templateConfigured=settings.Template!=null,maxDocxBytes=LeaveDraftService.MaxBytes});
    }
    private async Task<IActionResult> Execute<T>(Func<Task<WorkflowResult<T>>> action)
    {
        try
        {
            var result=await action();
            if(result.Value is byte[] file && result.Error is null)
            {
                Response.Headers.CacheControl="private, no-store";
                return File(file,DocxMime,"don-nghi.docx");
            }
            return Respond(result);
        }
        catch(Exception ex) when(ex is RequestFailedException or HttpRequestException ||
            ex is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Respond(WorkflowResult<T>.Fail(503,"leave.storage_unavailable","Kho tài liệu chưa sẵn sàng."));
        }
    }
    [HttpGet("leave-templates/current"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveTemplateDescriptor),200)]
    public IActionResult Template()=>Respond(drafts.Template(Actor));
    [HttpGet("leave-templates/{id:guid}/docx"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(Stream),200,DocxMime)]
    public Task<IActionResult> TemplateFile(Guid id,CancellationToken ct)=>Execute(()=>drafts.TemplateFile(Actor,id,ct));
    [HttpPost("leave-drafts"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveDraftView),200)]
    public Task<IActionResult> Create(LeaveDraftInput input,[FromHeader(Name="Idempotency-Key")] Guid key,CancellationToken ct)=>
        Execute(()=>drafts.Create(Actor,input,key,ct));
    [HttpGet("leave-drafts"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeavePage),200)]
    public Task<IActionResult> List(CancellationToken ct,int page=1,int pageSize=20)=>Execute(()=>drafts.List(Actor,page,pageSize,ct));
    [HttpGet("leave-drafts/{id:guid}"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveDraftView),200)]
    public Task<IActionResult> Get(Guid id,CancellationToken ct)=>Execute(()=>drafts.Get(Actor,id,ct));
    [HttpPatch("leave-drafts/{id:guid}"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveDraftView),200)]
    public Task<IActionResult> Edit(Guid id,LeaveDraftEdit input,CancellationToken ct)=>Execute(()=>drafts.Edit(Actor,id,input,ct));
    [HttpDelete("leave-drafts/{id:guid}"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveDraftView),200)]
    public Task<IActionResult> Delete(Guid id,int expectedVersion,CancellationToken ct)=>Execute(()=>drafts.Delete(Actor,id,expectedVersion,ct));
    [HttpGet("leave-drafts/{id:guid}/docx"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(Stream),200,DocxMime)]
    public Task<IActionResult> Read(Guid id,CancellationToken ct)=>Execute(()=>drafts.Read(Actor,id,false,ct));
    [HttpPut("leave-drafts/{id:guid}/docx"), Authorize(Policy="workflow.leave.request"),Consumes("multipart/form-data"),RequestSizeLimit(11*1024*1024)]
    [ProducesResponseType(typeof(LeaveDraftView),200)]
    public async Task<IActionResult> Upload(Guid id,[FromForm] LeaveUpload form,CancellationToken ct)
    {
        using var stream=form.File.OpenReadStream();
        return await Execute(()=>drafts.Upload(Actor,id,form.ExpectedVersion,form.MetadataVersion,stream,form.File.FileName,ct));
    }
    [HttpPost("leave-drafts/{id:guid}/submit"), Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(LeaveSubmitReceipt),200)]
    public Task<IActionResult> Submit(Guid id,LeaveDraftSubmit input,[FromHeader(Name="Idempotency-Key")] Guid key,CancellationToken ct)=>
        Execute(()=>drafts.Submit(Actor,id,input,key,ct));
    [HttpGet("leaves/{id:guid}/submitted-docx")]
    [ProducesResponseType(typeof(Stream),200,DocxMime)]
    public Task<IActionResult> Submitted(Guid id,CancellationToken ct)=>Execute(()=>drafts.Read(Actor,id,true,ct));
}
public sealed class LeaveUpload
{
    public required IFormFile File { get; init; }
    public int ExpectedVersion { get; init; }
    public int MetadataVersion { get; init; }
}
