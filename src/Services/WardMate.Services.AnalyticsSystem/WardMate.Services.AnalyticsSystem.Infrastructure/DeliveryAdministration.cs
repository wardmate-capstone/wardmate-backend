using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class DeliveryAdministration(NotificationDbContext db, IConfiguration config, TimeProvider clock) : IDeliveryAdministration
{
    private static readonly string[] States = ["Pending", "Dispatching", "Sent", "Sandbox", "Blocked", "Unknown", "Dead", "Suppressed"];
    private static readonly string[] Channels = ["IN_APP", "EMAIL", "SMS"];
    private static void Check(bool valid, int status, string code, string message)
    { if (!valid) throw new AnalyticsException(status, "notifications." + code, message); }
    private static void Page(int page, int size) => Check(page is >= 1 and <= 1000000 && size is >= 1 and <= 100, 400, "validation", "Trang hoặc kích thước trang không hợp lệ.");
    private static bool Text(string? value, int min, int max) => value is not null && value.Trim().Length >= min && value.Trim().Length <= max && !value.Any(char.IsControl);
    private static bool MessageId(string? value) => value is null || Regex.IsMatch(value, "^[A-Za-z0-9_-]{1,100}$");
    private static DeliveryAdminDto Dto(NotificationDelivery x) => new(x.Id,x.NotificationId,x.Channel,x.State,x.Attempts,x.NextAttemptAt,x.ErrorCode,x.SentAt,x.Provider,x.ExternalMessageId,x.IsSandbox,x.Revision,x.UpdatedAt,x.Id.ToString("N"));
    public async Task<DeliveryPage> List(DeliveryQuery query, CancellationToken ct)
    {
        Page(query.Page,query.PageSize);
        Check((query.State is null || States.Contains(query.State)) && (query.Channel is null || Channels.Contains(query.Channel)) && MessageId(query.ExternalMessageId),400,"validation","Bộ lọc trạng thái/kênh/mã nhà cung cấp không hợp lệ.");
        var q = db.Deliveries.AsNoTracking();
        // Default view is actionable failures; callers can explicitly request another state.
        q = query.State is null ? q.Where(x => x.State == "Dead" || x.State == "Blocked" || x.State == "Unknown" || x.State == "Dispatching") : q.Where(x => x.State == query.State);
        if (query.Channel is not null) q=q.Where(x=>x.Channel==query.Channel);
        if (query.NotificationId is not null) q=q.Where(x=>x.NotificationId==query.NotificationId);
        if (query.ExternalMessageId is not null) q=q.Where(x=>x.ExternalMessageId==query.ExternalMessageId);
        var count=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.UpdatedAt).ThenBy(x=>x.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(),query.Page,query.PageSize,count);
    }
    public async Task<DeliveryAdminDto> Get(Guid id,CancellationToken ct)
    {
        var row=await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        Check(row is not null,404,"delivery_not_found","Không tìm thấy delivery."); return Dto(row!);
    }
    public async Task<DeliveryAuditDto[]> Audit(Guid id,int page,int pageSize,CancellationToken ct)
    {
        Page(page,pageSize); await Get(id,ct);
        return await db.Set<DeliveryAdminAudit>().AsNoTracking().Where(x=>x.DeliveryId==id).OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id)
            .Skip((page-1)*pageSize).Take(pageSize).Select(x=>new DeliveryAuditDto(x.Id,x.ActorId,x.Action,x.BeforeState,x.AfterState,x.BeforeAttempts,x.BeforeRevision,x.AfterRevision,x.Reason,x.Reference,x.BeforeMessageId,x.AfterMessageId,x.CreatedAt)).ToArrayAsync(ct);
    }
    public Task<DeliveryAdminDto> Replay(Guid actor,Guid id,DeliveryReplayInput input,CancellationToken ct)
    {
        Check(Text(input.Reason,5,500) && input.AcknowledgeDuplicateRisk,400,"validation","Cần lý do 5–500 ký tự và xác nhận rủi ro gửi trùng.");
        return Mutate(actor,id,input.ExpectedRevision,"REPLAY",input.Reason,null,row=>
        {
            Check(row.State is "Dead" or "Blocked",409,"invalid_state","Chỉ gửi lại Dead/Blocked.");
            Check(row.Channel!="SMS",409,"channel_removed","Kênh SMS đã bị gỡ khỏi hệ thống; không gửi lại được.");
            if(row.Channel=="EMAIL") Check(config.GetValue<bool>("Email:Enabled"),409,"channel_disabled","Email đang tắt.");
            // Attempts restart so the retry budget applies again; the audit keeps the previous count.
            row.State="Pending"; row.Attempts=0; row.NextAttemptAt=clock.GetUtcNow().UtcDateTime;
            row.ErrorCode=null; row.SentAt=null; row.ExternalMessageId=null;
        },ct);
    }
    private async Task<DeliveryAdminDto> Mutate(Guid actor,Guid id,Guid revision,string action,string reason,string? reference,Action<NotificationDelivery> change,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Do not wait behind an active sender and then silently act on its new state.
        var row=await db.Deliveries.FromSqlInterpolated($"SELECT * FROM notification_deliveries WHERE \"Id\" = {id} FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if(row is null)
        {
            var exists=await db.Deliveries.AsNoTracking().AnyAsync(x=>x.Id==id,ct);
            throw new AnalyticsException(exists?409:404,exists?"notifications.delivery_busy":"notifications.delivery_not_found",exists?"Delivery đang được xử lý.":"Không tìm thấy delivery.");
        }
        Check(row.Revision==revision,409,"revision_conflict","Delivery đã thay đổi. Hãy tải lại trước khi thao tác.");
        var audit=new DeliveryAdminAudit { DeliveryId=id,ActorId=actor,Action=action,BeforeState=row.State,BeforeAttempts=row.Attempts,BeforeRevision=row.Revision,BeforeMessageId=row.ExternalMessageId,Reason=reason.Trim(),Reference=reference?.Trim(),CreatedAt=clock.GetUtcNow().UtcDateTime };
        change(row);
        await db.SaveChangesAsync(ct);
        audit.AfterState=row.State; audit.AfterRevision=row.Revision; audit.AfterMessageId=row.ExternalMessageId;
        db.Add(audit); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(row);
    }
}
