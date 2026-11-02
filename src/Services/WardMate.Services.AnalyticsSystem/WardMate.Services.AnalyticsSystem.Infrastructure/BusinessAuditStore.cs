using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class BusinessAuditStore(AnalyticsDbContext db, IConfiguration config, TimeProvider clock) : IBusinessAuditStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string,string[]> Tables = new()
    {
        ["iam"]=["users","user_profiles","roles","permissions","user_roles","role_permissions","wards"],
        ["catalog"]=["procedures","procedure_categories","procedure_drafts","procedure_versions"],
        ["workflow"]=["applications","application_checklists","application_comments","application_consultations","application_assignment_history","application_status_history","application_versions","officer_shifts","officer_leaves","officer_leave_decisions","calendar_closures"],
        ["documentform"]=["application_forms","approved_qrs","form_templates","form_template_versions","generated_documents","supporting_documents","user_submissions"],
        ["aiocr"]=["chat_sessions","chat_messages","knowledge_documents","knowledge_chunks"],
        ["analytics"]=["citizen_feedback","system_settings"],
        ["notifications"]=["notifications","notification_deliveries"]
    };
    private void Enabled()
    {
        if (!config.GetValue("SystemAdministration:Enabled",false))
            throw new AnalyticsException(503,"system.administration_disabled","Áp dụng migration và bật quản trị hệ thống trước.");
    }
    public async Task Receive(string source,string body,CancellationToken ct)
    {
        Enabled();
        BusinessAuditMessage m;
        try { m=JsonSerializer.Deserialize<BusinessAuditMessage>(body,Json) ?? throw new JsonException(); }
        catch(JsonException) { throw Invalid(); }
        if(m.Source!=source || m.EventId==Guid.Empty || !Tables.TryGetValue(source,out var tables) || !tables.Contains(m.Entity) ||
            m.Schema!=(source=="documentform"?"document":source=="analytics"?"analytics":"public") ||
            m.Action is not ("INSERT" or "UPDATE" or "DELETE") || m.OccurredAtUtc.Offset!=TimeSpan.Zero ||
            m.OccurredAtUtc < new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero) || m.OccurredAtUtc>clock.GetUtcNow().AddMinutes(5) ||
            m.TraceId?.Length>128 || m.ClientIp is not null && !IPAddress.TryParse(m.ClientIp,out _) ||
            !Identity(m) || !Redacted(m.Before) || !Redacted(m.After)) throw Invalid();
        // Persist a canonical DTO only: unrecognized incoming fields are never retained.
        var payload=JsonSerializer.Serialize(m,Json);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var received=clock.GetUtcNow().UtcDateTime;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO analytics.business_audit_events ("Id","Source","Entity","Action","ActorId","TraceId","OccurredAtUtc","ReceivedAtUtc","Payload","PayloadHash")
            VALUES ({m.EventId},{source},{m.Entity},{m.Action},{m.ActorId},{m.TraceId},{m.OccurredAtUtc.UtcDateTime},{received},CAST({payload} AS jsonb),{hash})
            ON CONFLICT ("Id") DO NOTHING
            """,ct);
        var stored=await db.BusinessAudits.AsNoTracking().Where(x=>x.Id==m.EventId).Select(x=>x.PayloadHash).SingleAsync(ct);
        if(stored!=hash) throw new AnalyticsException(409,"audit.event_conflict","EventId đã có nội dung khác; không ghi đè nhật ký.");
    }
    private static bool Object(JsonElement element) => element.ValueKind==JsonValueKind.Object &&
        element.EnumerateObject().Count()<=200 && element.EnumerateObject().All(p=>p.Name.Length<=100 &&
            p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null && p.Value.GetRawText().Length<=256);
    private static bool Redacted(JsonElement element)
    {
        string[] allowed=["id","userid","applicationid","procedureid","templateid","checklistid","submissionid","officerid",
            "assignedofficerid","actorid","roleid","permissionid","categoryid","wardid","status","state","isactive",
            "isdeleted","isread","isrequired","version","versionnumber","assignmentversion","rating"];
        return Object(element) && element.EnumerateObject().All(p=>allowed.Contains(p.Name.Replace("_", "").ToLowerInvariant()) ||
            p.Value.ValueKind==JsonValueKind.String && p.Value.GetString()=="[REDACTED]");
    }
    private static bool Identity(BusinessAuditMessage m)
    {
        if(!Object(m.EntityKey) || !m.EntityKey.EnumerateObject().Any()) return false;
        if(m.Source=="analytics" && m.Entity=="system_settings")
            return m.EntityKey.EnumerateObject().Count()==1 && m.EntityKey.TryGetProperty("Key",out var key) &&
                key.ValueKind==JsonValueKind.String && key.GetString()==SystemAdministration.LookbackKey;
        return m.EntityKey.EnumerateObject().All(p=>new[]{"id","userid","roleid","permissionid"}.Contains(p.Name.Replace("_", "").ToLowerInvariant()) &&
            (p.Value.ValueKind==JsonValueKind.Number && p.Value.TryGetInt64(out _) ||
             p.Value.ValueKind==JsonValueKind.String && Guid.TryParse(p.Value.GetString(),out _)));
    }
    private static AnalyticsException Invalid()=>new(400,"audit.invalid_event","Sự kiện audit không đúng hợp đồng.");
    public async Task<AnalyticsPage<BusinessAuditMessage>> List(BusinessAuditQuery q,CancellationToken ct)
    {
        Enabled();
        if(q.Page is <1 or >100000 || q.PageSize is <1 or >100 || q.From>q.To ||
            q.From is {Kind:not DateTimeKind.Utc} || q.To is {Kind:not DateTimeKind.Utc} ||
            q.Source is not null && !Tables.ContainsKey(q.Source) || q.Entity?.Length>100 || q.TraceId?.Length>128)
            throw new AnalyticsException(400,"audit.invalid_query","Kiểm tra nguồn, khoảng UTC, page và pageSize (1–100).");
        var rows=db.BusinessAudits.AsNoTracking().AsQueryable();
        if(q.From.HasValue) rows=rows.Where(x=>x.OccurredAtUtc>=q.From);
        if(q.To.HasValue) rows=rows.Where(x=>x.OccurredAtUtc<=q.To);
        if(q.Source is not null) rows=rows.Where(x=>x.Source==q.Source);
        if(q.Entity is not null) rows=rows.Where(x=>x.Entity==q.Entity);
        if(q.ActorId.HasValue) rows=rows.Where(x=>x.ActorId==q.ActorId);
        if(q.TraceId is not null) rows=rows.Where(x=>x.TraceId==q.TraceId);
        var total=await rows.CountAsync(ct);
        var items=await rows.OrderByDescending(x=>x.OccurredAtUtc).ThenByDescending(x=>x.Id)
            .Skip((q.Page-1)*q.PageSize).Take(q.PageSize).Select(x=>x.Payload).ToArrayAsync(ct);
        return new(items.Select(x=>JsonSerializer.Deserialize<BusinessAuditMessage>(x,Json)!).ToArray(),q.Page,q.PageSize,total);
    }
    public async Task<BusinessAuditMessage> Get(Guid id,CancellationToken ct)
    {
        Enabled();
        var payload=await db.BusinessAudits.AsNoTracking().Where(x=>x.Id==id).Select(x=>x.Payload).SingleOrDefaultAsync(ct)
            ?? throw new AnalyticsException(404,"audit.not_found","Không tìm thấy sự kiện audit.");
        return JsonSerializer.Deserialize<BusinessAuditMessage>(payload,Json)!;
    }
}
