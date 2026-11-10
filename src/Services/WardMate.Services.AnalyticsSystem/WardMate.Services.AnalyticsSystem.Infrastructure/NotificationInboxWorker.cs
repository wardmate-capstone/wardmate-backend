using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationInboxWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationInboxWorker> logger) : BackgroundService
{
    // Assignment events replace the status event of the same change, so each action decides its own audience:
    // CLAIM -> applicant; ASSIGN -> applicant + new officer; TRANSFER -> new + previous officer; RELEASE -> ward pool.
    public static bool AssignmentRecipient(WorkflowEvent e, RecipientContact r)
    {
        if (r.UserId == e.ApplicantId) return e.AssignmentAction is "CLAIM" or "ASSIGN";
        if (!r.IsOfficer || r.WardCode != e.WardCode) return false;
        // WF-ASSIGN-REL-01: gỡ cán bộ khi hồ sơ còn chờ bổ sung chưa có việc cho phường; phường được báo khi người dân nộp lại.
        if (e.AssignmentAction == "RELEASE" && e.Status == "NEED_REVISION") return false;
        return e.AssignmentAction switch
        {
            "ASSIGN" => r.UserId == e.AssignedOfficerId,
            "TRANSFER" => r.UserId == e.AssignedOfficerId || r.UserId == e.PreviousOfficerId,
            "RELEASE" => r.UserId != e.PreviousOfficerId,
            _ => false
        };
    }
    // NOTIFY-COMMENT-01: nhận xét chỉ là phát hiện nội bộ khi cán bộ đang kiểm tra (có thể rút lại, người dân không thấy
    // nhận xét đã rút). Người dân được báo một lần qua sự kiện STATUS NEED_REVISION khi cán bộ yêu cầu bổ sung.
    public static bool StatusRecipient(WorkflowEvent e, RecipientContact r)
    {
        if (e.Kind == "COMMENT") return false;
        if (ResubmittedToPool(e) && !r.IsOfficer && r.IsStaffingManager && r.WardCode == e.WardCode) return true;
        return r.UserId == e.ApplicantId || r.IsOfficer && r.WardCode == e.WardCode &&
            e.Kind == "STATUS" && e.Status is "SUBMITTED" or "UNDER_REVIEW" && (e.AssignedOfficerId == null || r.UserId == e.AssignedOfficerId);
    }
    public static bool ResubmittedToPool(WorkflowEvent e) =>
        e is { Kind: "STATUS", Status: "SUBMITTED", AssignedOfficerId: null, Resubmitted: true };
    private static Guid[] AssignmentLookup(WorkflowEvent e) => e.AssignmentAction switch
    {
        "CLAIM" => [e.ApplicantId],
        "ASSIGN" => [e.ApplicantId, e.AssignedOfficerId!.Value],
        "TRANSFER" => [e.AssignedOfficerId!.Value, e.PreviousOfficerId!.Value],
        _ => []
    };
    public static Notification Create(WorkflowEvent e, Guid recipient, bool officer, bool manager = false) => new()
    {
        Id = Guid.NewGuid(), SourceEventId = e.EventId, RecipientUserId = recipient, ApplicationId = e.ApplicationId,
        // Manager không phải cán bộ một cửa: gắn mã phường sẽ bị bước gửi (NotificationDeliveryWorker) chặn.
        RecipientWardCode = officer && !manager ? e.WardCode : null,
        Title = manager ? "Hồ sơ nộp lại chưa có cán bộ thụ lý"
            : e.Kind == "APPLICATION_ASSIGNED" && officer
            ? recipient == e.AssignedOfficerId ? "Hồ sơ được giao cho bạn" : e.AssignmentAction == "RELEASE" ? "Có hồ sơ cần tiếp nhận" : "Hồ sơ đã chuyển cho cán bộ khác"
            : officer ? e.Status == "UNDER_REVIEW" ? "Hồ sơ được phân công" : "Có hồ sơ cần tiếp nhận" : e.Kind == "COMMENT" ? "Hồ sơ có nhận xét mới" : e.Status switch
        {
            "SUBMITTED" => "Hồ sơ đã được nộp", "UNDER_REVIEW" => "Giấy tờ đang được kiểm tra", "NEED_REVISION" => "Hồ sơ yêu cầu bổ sung",
            "OFFICIALLY_RECEIVED" => "Hồ sơ đã được tiếp nhận tại quầy",
            "CANCELLED" => "Hồ sơ đã được hủy",
            "DOCUMENTS_READY" => "Giấy tờ đã sẵn sàng để mang đến phường",
            "APPROVED" or "REJECTED" => "Cập nhật hồ sơ từ luồng cũ", _ => "Trạng thái hồ sơ đã thay đổi"
        },
        Content = $"Hồ sơ {e.ApplicationCode} có cập nhật. Vui lòng mở hồ sơ để xem chi tiết.",
        Type = e.Kind == "APPLICATION_ASSIGNED" && officer ? e.AssignmentAction == "RELEASE" ? "APPLICATION_STATUS_CHANGED" : "ASSIGNED_OFFICER"
            : officer && e.Status == "UNDER_REVIEW" ? "ASSIGNED_OFFICER" : e.Kind == "COMMENT" ? "INLINE_COMMENT_ADDED" : "APPLICATION_STATUS_CHANGED",
        ActionUrl = officer ? $"/officer/applications/{e.ApplicationId}/review" : $"/applications/{e.ApplicationId}/edit",
        CreatedAt = e.OccurredAt
    };

    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Inbox.FromSqlRaw("SELECT * FROM notification_inbox WHERE \"State\" = 'Pending' AND \"NextAttemptAt\" <= now() ORDER BY \"CreatedAt\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (row is null) return false;
        await tx.CreateSavepointAsync("inbox_processing", ct);
        try
        {
            var e = JsonSerializer.Deserialize<WorkflowEvent>(row.Payload)!;
            if (!WorkflowNotificationConsumer.Valid(e)) throw new InvalidOperationException("Sự kiện không hợp lệ.");
            if(e is { Version:3, Kind:"STAFFING", Staffing:{} staffing })
            {
                var audience=await scope.ServiceProvider.GetRequiredService<IRecipientContacts>().Find(
                    staffing.NotifyManagers?[]:[staffing.OfficerId], staffing.NotifyManagers?e.WardCode:null,ct,staffing.NotifyManagers);
                foreach(var recipient in audience.DistinctBy(x=>x.UserId).Where(x=>x.WardCode==e.WardCode &&
                    (staffing.NotifyManagers ? x.IsStaffingManager : x.UserId==staffing.OfficerId && x.IsStaffingOfficer)))
                {
                    var notification=new Notification {Id=Guid.NewGuid(),SourceEventId=e.EventId,RecipientUserId=recipient.UserId,
                        RecipientWardCode=e.WardCode,Type="SYSTEM_ALERT",Title="Cập nhật lịch và nghỉ phép",
                        Content=e.Status switch {
                            "LEAVE_SUBMITTED"=>"Có đơn nghỉ mới cần xem xét.",
                            "LEAVE_DECIDED"=>"Trạng thái đơn nghỉ đã thay đổi.",
                            "SHIFT_CHANGE_REQUESTED"=>"Có đề nghị đổi giờ ca cần xem xét.",
                            "SHIFT_CHANGE_DECIDED"=>"Đề nghị đổi giờ ca đã có kết quả.",
                            _=>"Lịch trực của bạn đã thay đổi."
                        }, CreatedAt=e.OccurredAt};
                    db.Notifications.Add(notification);
                    db.Deliveries.Add(new() {NotificationId=notification.Id});
                }
                row.State="Processed";row.ErrorCode=null;
                await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
            }
            var assignment = e.Kind == "APPLICATION_ASSIGNED";
            var directory = scope.ServiceProvider.GetRequiredService<IRecipientContacts>();
            var contacts = await directory.Find(
                assignment ? AssignmentLookup(e)
                    : e.AssignedOfficerId is { } assigned ? [e.ApplicantId, assigned] : [e.ApplicantId],
                assignment ? e.AssignmentAction == "RELEASE" ? e.WardCode : null
                    : e.Kind == "STATUS" && e.Status == "SUBMITTED" && e.AssignedOfficerId is null ? e.WardCode : null, ct);
            // NOTIFY-RESUBMIT-01: hồ sơ nộp lại mà đã bị gỡ cán bộ → báo thêm Manager của phường để theo dõi/giao người.
            if (!assignment && ResubmittedToPool(e))
                contacts = [.. contacts, .. await directory.Find([], e.WardCode, ct, staffingManagers: true)];
            foreach (var recipient in contacts.DistinctBy(r => r.UserId).Where(r => assignment ? AssignmentRecipient(e, r) : StatusRecipient(e, r)))
            {
                var notification = Create(e, recipient.UserId, recipient.UserId != e.ApplicantId, manager: !recipient.IsOfficer && recipient.IsStaffingManager);
                db.Notifications.Add(notification);
                db.Deliveries.Add(new() { NotificationId = notification.Id });
                if (configuration.GetValue<bool>("Email:Enabled")) db.Deliveries.Add(new() { NotificationId = notification.Id, Channel = "EMAIL" });
            }
            row.State = "Processed"; row.ErrorCode = null;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Roll back partial notification/delivery writes before persisting the bounded retry.
            await tx.RollbackToSavepointAsync("inbox_processing", ct);
            db.ChangeTracker.Clear();
            db.Inbox.Attach(row);
            row.Attempts++; row.State = row.Attempts >= 10 ? "Dead" : "Pending";
            row.NextAttemptAt = RetrySchedule.Next(row.Attempts); row.ErrorCode = "notification.resolve_failed";
            logger.LogWarning("Notification inbox {EventId} failed; attempt {Attempt}, state {State}.", row.Id, row.Attempts, row.State);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Notifications:WorkersEnabled")) return;
        while (!ct.IsCancellationRequested)
        {
            try { if (!await ProcessOne(ct)) await Task.Delay(500, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Inbox chưa xử lý được; giữ dữ liệu để thử lại."); await Task.Delay(5000, ct); }
        }
    }
}
