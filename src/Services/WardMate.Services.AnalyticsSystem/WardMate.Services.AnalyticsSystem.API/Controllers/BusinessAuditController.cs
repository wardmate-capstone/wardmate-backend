using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Auditing;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/system/audit-events")]
public sealed class BusinessAuditController(IBusinessAuditStore store) : ControllerBase
{
    [HttpGet, ProducesResponseType<AnalyticsPage<BusinessAuditMessage>>(200)]
    public async Task<IActionResult> List([FromQuery] BusinessAuditQuery query,CancellationToken ct)=>Ok(await store.List(query,ct));
    [HttpGet("{id:guid}"), ProducesResponseType<BusinessAuditMessage>(200)]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await store.Get(id,ct));
}

// Dedicated machine-auth endpoint. Always requires HMAC, including when reachable through Gateway's service prefix.
[ApiController, AllowAnonymous, ApiExplorerSettings(IgnoreApi=true), Route("internal/audit/events")]
public sealed class AuditIngestionController(IBusinessAuditStore store,IConfiguration config,TimeProvider clock) : ControllerBase
{
    [HttpPost, RequestSizeLimit(65536)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var source=Request.Headers["X-Audit-Source"].ToString();
        if(source is not ("iam" or "catalog" or "workflow" or "documentform" or "aiocr" or "analytics" or "notifications"))
            return Unauthorized();
        // Bounded read even when Transfer-Encoding is chunked or the action is called outside Kestrel.
        using var bytes=new MemoryStream();
        var buffer=new byte[4096];
        int n;
        while((n=await Request.Body.ReadAsync(buffer,ct))>0)
        { if(bytes.Length+n>65536) return StatusCode(413); await bytes.WriteAsync(buffer.AsMemory(0,n),ct); }
        var body=Encoding.UTF8.GetString(bytes.ToArray());
        if(!AuditSignature.Verify(config[$"AuditIngestion:Keys:{source}"]??"",source,
            Request.Headers["X-Audit-Time"].ToString(),body,Request.Headers["X-Audit-Signature"].ToString(),clock.GetUtcNow()))
            return Unauthorized();
        await store.Receive(source,body,ct);
        return NoContent();
    }
}
