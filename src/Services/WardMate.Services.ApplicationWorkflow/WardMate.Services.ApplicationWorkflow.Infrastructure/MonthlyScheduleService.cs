using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class MonthlyScheduleService(WorkflowDbContext db, TimeProvider clock, IDispatchDirectory directory) : IMonthlySchedules
{
    private static readonly TimeSpan Zone = TimeSpan.FromHours(7);
    private static WorkflowResult<object> Ok(object value) => WorkflowResult<object>.Ok(value);
    private static WorkflowResult<object> Fail(int status,string code,string message) => WorkflowResult<object>.Fail(status,"schedule."+code,message);
    private static WorkflowResult<object> Forbidden() => Fail(403,"forbidden","Không có quyền thao tác lịch phường.");
    private static WorkflowResult<object> Conflict() => Fail(409,"changed","Ca hoặc lịch đã thay đổi; hãy tải lại.");
    private static bool Manager(ReviewActor a)=>a.IsManager && !string.IsNullOrWhiteSpace(a.WardCode);
    private static bool Officer(ReviewActor a)=>a.CanMutate && !string.IsNullOrWhiteSpace(a.WardCode);
    private static bool ValidMonth(DateOnly m)=>m.Day==1 && m.Year is >=2000 and <=2099;
    private static DateTime Utc(DateOnly day,TimeOnly time=default)=>new DateTimeOffset(day.ToDateTime(time),Zone).UtcDateTime;
    private static DateOnly LocalDay(DateTimeOffset time)=>DateOnly.FromDateTime(time.ToOffset(Zone).DateTime);
    private static bool Reason(string? reason)=>!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length<=2000;
    private static bool Range(DateTimeOffset from,DateTimeOffset to)=>to>from && to-from<=TimeSpan.FromHours(24);
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private IQueryable<OfficerShift> Official(string ward,DateOnly month) => db.Shifts.Where(x=>x.WardCode==ward &&
        x.StartsAtUtc>=Utc(month,default) && x.StartsAtUtc<Utc(month.AddMonths(1),default) && x.CancelledAtUtc==null);
    private static ScheduleSlot Slot(OfficerShift s)=>new(s.Id,s.OfficerId,new(s.StartsAtUtc),new(s.EndsAtUtc),s.Version);
    private static ScheduleSlot[] Slots(ScheduleRevision r)=>JsonSerializer.Deserialize<ScheduleSlot[]>(r.SlotsJson)!;
    private static string Fingerprint(IEnumerable<ScheduleSlot> slots)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        slots.OrderBy(s=>s.ShiftId).Select(s=>new {s.ShiftId,s.OfficerId,Start=s.StartsAt.UtcTicks,End=s.EndsAt.UtcTicks,s.ShiftVersion}))));
    private static bool Same(ScheduleSlot s,OfficerShift o)=>s.OfficerId==o.OfficerId && s.StartsAt.UtcDateTime==o.StartsAtUtc && s.EndsAt.UtcDateTime==o.EndsAtUtc && s.ShiftVersion==o.Version;
    private static MonthlyRevisionView Revision(ScheduleRevision r)=>new(r.Id,r.Month,r.Status,r.Version,r.BaseFingerprint,r.ActorId,r.Reason,r.PublishedAtUtc,Slots(r));
    private static bool Eligible(DispatchOfficer officer,string ward)=>officer.IsActive && officer.WardCode==ward &&
        officer.Roles.Contains("FRONT_DESK_OFFICER") && !officer.Roles.Contains("MANAGER");
    public async Task<WorkflowResult<object>> Month(ReviewActor a,DateOnly month,CancellationToken ct)
    {
        if(!Manager(a) && !Officer(a)) return Forbidden();
        if(!ValidMonth(month)) return Fail(400,"month","Dùng ngày đầu tháng.");
        var ward=a.WardCode!;
        var rows=await Official(ward,month).AsNoTracking().Where(x=>a.IsManager || x.OfficerId==a.Id).OrderBy(x=>x.StartsAtUtc).ToArrayAsync(ct);
        var slots=rows.Select(Slot).ToArray();
        var signature=Fingerprint(slots);
        var draft=a.IsManager ? await db.ScheduleRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.WardCode==ward && x.Month==month && x.Status=="DRAFT",ct) : null;
        var leaves=await db.Leaves.AsNoTracking().Where(x=>x.WardCode==ward && x.Status=="APPROVED" && (a.IsManager || x.OfficerId==a.Id) &&
            x.StartsAtUtc<Utc(month.AddMonths(1),default) && x.EndsAtUtc>Utc(month,default)).Select(x=>new ScheduleLeaveView(x.Id,x.OfficerId,x.StartsAtUtc,x.EndsAtUtc)).ToArrayAsync(ct);
        var closures=await db.CalendarClosures.AsNoTracking().Where(x=>(x.WardCode==null || x.WardCode==ward) && x.CancelledAtUtc==null &&
            x.ClosureDate>=month && x.ClosureDate<month.AddMonths(1)).Select(x=>new ScheduleClosureView(x.Id,x.ClosureDate,x.WardCode)).ToArrayAsync(ct);
        var receipts=await db.ScheduleReceipts.AsNoTracking().Where(x=>x.WardCode==ward && x.Month==month && (a.IsManager || x.OfficerId==a.Id)).ToArrayAsync(ct);
        var confirmations=slots.GroupBy(x=>x.OfficerId).Select(g=>new ScheduleReceiptView(g.Key,receipts.FirstOrDefault(r=>r.OfficerId==g.Key && r.Fingerprint==Fingerprint(g))?.ConfirmedAtUtc)).ToArray();
        return Ok(new MonthlyView(month,slots,signature,draft==null?null:Revision(draft),leaves,closures,confirmations));
    }
    public async Task<WorkflowResult<object>> Week(ReviewActor a,WeekEdit? edit,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        await using var tx=await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db,a.WardCode!,ct);
        var w=await db.WeekPatterns.SingleOrDefaultAsync(x=>x.WardCode==a.WardCode,ct);
        if(edit!=null)
        {
            if(edit.ExpectedVersion!=(w?.Version??0)) return Conflict();
            if(edit.Entries==null || edit.Entries.Length>200 || edit.Entries.Any(x=>x==null || x.OfficerId==Guid.Empty || !Enum.IsDefined(x.Day) || x.Start==x.End))
                return Fail(400,"invalid_week","Mẫu tuần không hợp lệ hoặc quá 200 dòng.");
            if(w==null){w=new WeekPattern {WardCode=a.WardCode!};db.WeekPatterns.Add(w);}
            w.Version++; w.EntriesJson=JsonSerializer.Serialize(edit.Entries); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new WeekView(w?.Version??0,JsonSerializer.Deserialize<WeekEntry[]>(w?.EntriesJson??"[]")!));
    }
    public async Task<WorkflowResult<object>> Preview(ReviewActor a,DateOnly month,DateOnly? copyFrom,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        if(!ValidMonth(month) || copyFrom is {} src && (!ValidMonth(src) || src==month)) return Fail(400,"month","Tháng nguồn/đích không hợp lệ.");
        var current=(await Official(a.WardCode!,month).AsNoTracking().ToArrayAsync(ct)).Select(Slot).ToArray();
        var generated=new List<ScheduleSlot>();
        var closures=await db.CalendarClosures.AsNoTracking().Where(x=>(x.WardCode==a.WardCode || x.WardCode==null) && x.CancelledAtUtc==null &&
            x.ClosureDate>=month && x.ClosureDate<=month.AddMonths(1)).Select(x=>x.ClosureDate).Distinct().ToArrayAsync(ct);
        void Generate(Guid officer,DateTime start,DateTime end)
        {
            if(start<=Now || ClosureHit(start,end,closures)) return;
            if(!Range(start,end)) return;
            if(!current.Concat(generated).Any(x=>x.OfficerId==officer && x.StartsAt==start && x.EndsAt==end))
                generated.Add(new(Guid.NewGuid(),officer,start,end));
        }
        if(copyFrom is {} source)
        {
            var original=await Official(a.WardCode!,source).AsNoTracking().ToArrayAsync(ct);
            foreach(var shift in original)
            {
                var local=new DateTimeOffset(shift.StartsAtUtc).ToOffset(Zone);
                if(local.Day>DateTime.DaysInMonth(month.Year,month.Month)) continue;
                var start=Utc(month.AddDays(local.Day-1),TimeOnly.FromDateTime(local.DateTime));
                Generate(shift.OfficerId,start,start+(shift.EndsAtUtc-shift.StartsAtUtc));
            }
        }
        else
        {
            var w=await db.WeekPatterns.AsNoTracking().SingleOrDefaultAsync(x=>x.WardCode==a.WardCode,ct);
            var entries=JsonSerializer.Deserialize<WeekEntry[]>(w?.EntriesJson??"[]")!;
            for(var day=month;day<month.AddMonths(1);day=day.AddDays(1))
                foreach(var e in entries.Where(x=>x.Day==day.DayOfWeek))
                    Generate(e.OfficerId,Utc(day,e.Start),Utc(e.End<=e.Start?day.AddDays(1):day,e.End));
        }
        var slots=current.Concat(generated).ToArray();
        if(slots.Length>500) return Fail(400,"batch_limit","Mỗi tháng tối đa 500 ca.");
        var conflicts=await Conflicts(a.WardCode!,month,slots,current,ct);
        return Ok(new PreviewView(month,slots,closures,conflicts,Fingerprint(current)));
    }
    private static bool ClosureHit(DateTime start,DateTime end,IEnumerable<DateOnly> closures)=>
        closures.Any(day=>start<Utc(day.AddDays(1)) && end>Utc(day));
    private async Task<List<string>> Conflicts(string ward,DateOnly month,ScheduleSlot[] slots,ScheduleSlot[] current,CancellationToken ct)
    {
        var errors=new List<string>();
        if(slots.Any(x=>x==null || x.ShiftId==Guid.Empty || x.OfficerId==Guid.Empty || !Range(x.StartsAt,x.EndsAt) ||
            LocalDay(x.StartsAt)<month || LocalDay(x.StartsAt)>=month.AddMonths(1)) || slots.Select(x=>x.ShiftId).Distinct().Count()!=slots.Length)
        { errors.Add("INVALID_SLOT");return errors; }
        var outside=await db.Shifts.AsNoTracking().Where(x=>x.WardCode==ward && x.CancelledAtUtc==null &&
            (x.StartsAtUtc<Utc(month,default) || x.StartsAtUtc>=Utc(month.AddMonths(1),default)) &&
            x.EndsAtUtc>Utc(month,default) && x.StartsAtUtc<Utc(month.AddMonths(1).AddDays(1),default)).ToArrayAsync(ct);
        var leaves=await db.Leaves.AsNoTracking().Where(x=>x.WardCode==ward && x.Status=="APPROVED" &&
            x.EndsAtUtc>Utc(month,default) && x.StartsAtUtc<Utc(month.AddMonths(1).AddDays(1),default)).ToArrayAsync(ct);
        foreach(var s in slots)
        {
            if(slots.Any(o=>o.ShiftId!=s.ShiftId && o.OfficerId==s.OfficerId && o.StartsAt<s.EndsAt && o.EndsAt>s.StartsAt) ||
                outside.Any(o=>o.OfficerId==s.OfficerId && o.StartsAtUtc<s.EndsAt.UtcDateTime && o.EndsAtUtc>s.StartsAt.UtcDateTime))
                errors.Add($"SHIFT_OVERLAP:{s.ShiftId}");
            if(current.Contains(s)) continue;
            if(s.StartsAt<=Now || leaves.Any(l=>l.OfficerId==s.OfficerId && l.StartsAtUtc<s.EndsAt.UtcDateTime && l.EndsAtUtc>s.StartsAt.UtcDateTime))
                errors.Add($"PAST_OR_APPROVED_LEAVE:{s.ShiftId}");
        }
        if(current.Any(s=>s.StartsAt<=Now && !slots.Contains(s))) errors.Add("PAST_SHIFT_CHANGED");
        return errors;
    }
    public async Task<WorkflowResult<object>> Draft(ReviewActor a,DateOnly month,ScheduleDraftInput input,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        if(!ValidMonth(month) || input.Slots==null || input.Slots.Length>500 || !Reason(input.Reason)) return Fail(400,"invalid_input","Kiểm tra tháng, lý do và số lượng ca.");
        await using var tx=await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db,a.WardCode!,ct);
        var current=(await Official(a.WardCode!,month).AsNoTracking().ToArrayAsync(ct)).Select(Slot).ToArray();
        var signature=Fingerprint(current);
        if(signature!=input.BaseFingerprint) return Conflict();
        var d=await db.ScheduleRevisions.SingleOrDefaultAsync(x=>x.WardCode==a.WardCode && x.Month==month && x.Status=="DRAFT",ct);
        if((d?.Version??0)!=input.ExpectedVersion) return Conflict();
        var errors=await Conflicts(a.WardCode!,month,input.Slots,current,ct);
        if(errors.Count!=0) return WorkflowResult<object>.Fail(409,"schedule.conflicts","Cần sửa các ca bị xung đột.",errors.ToArray());
        if(d==null){d=new ScheduleRevision { WardCode=a.WardCode!,Month=month,Version=0 };db.ScheduleRevisions.Add(d);}
        d.ActorId=a.Id;d.Reason=input.Reason.Trim();d.BaseFingerprint=signature;d.Version++;d.SlotsJson=JsonSerializer.Serialize(input.Slots);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(Revision(d));
    }
    public async Task<WorkflowResult<object>> Publish(ReviewActor a,DateOnly month,PublishSchedule input,string token,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        if(!ValidMonth(month) || !Reason(input.Reason)) return Fail(400,"invalid_input","Cần tháng và lý do công bố.");
        var officers=await directory.Officers(token,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await CalendarLocks.Read(db,ct);await StaffingLocks.Ward(db,a.WardCode!,ct);
        var d=await db.ScheduleRevisions.SingleOrDefaultAsync(x=>x.Month==month && x.WardCode==a.WardCode && x.Id==input.RevisionId,ct);
        if(d==null) return Fail(404,"not_found","Không tìm thấy bản lịch.");
        if(d.Status=="PUBLISHED" && d.Version==input.ExpectedVersion+1 && d.ActorId==a.Id && d.Reason==input.Reason.Trim()) return Ok(Revision(d));
        if(d.Status!="DRAFT" || d.Version!=input.ExpectedVersion) return Conflict();
        var rows=await Official(a.WardCode!,month).ToArrayAsync(ct);
        var current=rows.Select(Slot).ToArray();
        if(Fingerprint(current)!=d.BaseFingerprint) return Conflict();
        var slots=Slots(d);
        var errors=await Conflicts(a.WardCode!,month,slots,current,ct);
        if(errors.Count!=0) return WorkflowResult<object>.Fail(409,"schedule.conflicts","Lịch đã phát sinh xung đột.",errors.ToArray());
        var closures=await db.CalendarClosures.AsNoTracking().Where(x=>x.CancelledAtUtc==null && (x.WardCode==a.WardCode || x.WardCode==null) &&
            x.ClosureDate>=month && x.ClosureDate<=month.AddMonths(1)).Select(x=>x.ClosureDate).ToArrayAsync(ct);
        var newIds=slots.Select(x=>x.ShiftId).Except(current.Select(x=>x.ShiftId)).ToArray();
        if(await db.Shifts.AnyAsync(x=>newIds.Contains(x.Id),ct)) return Conflict();
        var published = new List<OfficerShift>();
        foreach(var s in slots)
        {
            var row=rows.SingleOrDefault(x=>x.Id==s.ShiftId);
            if(row!=null && Same(s,row)) { published.Add(row); continue; }
            if(!officers.Any(x=>x.Id==s.OfficerId && Eligible(x,a.WardCode!))) return Fail(422,"officer_ineligible","Cán bộ không đủ điều kiện.");
            if(!input.AllowClosure && ClosureHit(s.StartsAt.UtcDateTime,s.EndsAt.UtcDateTime,closures)) return Fail(409,"closure","Cần xác nhận ngoại lệ ngày đóng cửa.");
            if(row==null){row=new OfficerShift {Id=s.ShiftId,WardCode=a.WardCode!,CreatedBy=a.Id,CreatedAtUtc=Now};db.Shifts.Add(row);}
            else if(row.Version!=s.ShiftVersion) return Conflict();
            row.OfficerId=s.OfficerId;row.StartsAtUtc=s.StartsAt.UtcDateTime;row.EndsAtUtc=s.EndsAt.UtcDateTime;row.Version++;
            published.Add(row);
        }
        foreach(var row in rows.Where(x=>slots.All(s=>s.ShiftId!=x.Id)))
        {row.CancelledAtUtc=Now;row.CancelledBy=a.Id;row.CancellationReason=input.Reason.Trim();row.Version++;}
        d.Status="PUBLISHED";d.Version++;d.ActorId=a.Id;d.PublishedAtUtc=Now;d.Reason=input.Reason.Trim();
        d.SlotsJson=JsonSerializer.Serialize(published.Select(Slot));
        foreach(var officerId in current.Except(slots).Concat(slots.Except(current)).Select(s=>s.OfficerId).Distinct())
            WorkflowOutboxCapture.Staffing(db,a.WardCode!,d.Id,officerId,"SCHEDULE_PUBLISHED",false,d.PublishedAtUtc.Value);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(Revision(d));
    }
    public async Task<WorkflowResult<object>> History(ReviewActor a,DateOnly month,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        if(!ValidMonth(month)) return Fail(400,"month","Dùng ngày đầu tháng.");
        var rows=await db.ScheduleRevisions.AsNoTracking().Where(x=>x.WardCode==a.WardCode && x.Month==month && (x.Status=="PUBLISHED" || x.Status=="BASELINE"))
            .OrderByDescending(x=>x.PublishedAtUtc).ToArrayAsync(ct);
        return Ok(rows.Select(Revision).ToArray());
    }
    public async Task<WorkflowResult<object>> Confirm(ReviewActor a,DateOnly month,ScheduleConfirmation input,CancellationToken ct)
    {
        if(!Officer(a)) return Forbidden();
        if(!ValidMonth(month)) return Fail(400,"month","Dùng ngày đầu tháng.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await StaffingLocks.Ward(db,a.WardCode!,ct);
        var slots=(await Official(a.WardCode!,month).Where(x=>x.OfficerId==a.Id).ToArrayAsync(ct)).Select(Slot).ToArray();
        var hash=Fingerprint(slots);
        if(slots.Length==0 || hash!=input.Fingerprint) return Conflict();
        var receipt=await db.ScheduleReceipts.SingleOrDefaultAsync(x=>x.WardCode==a.WardCode && x.OfficerId==a.Id && x.Month==month && x.Fingerprint==hash,ct);
        if(receipt==null){receipt=new OfficerScheduleReceipt {WardCode=a.WardCode!,OfficerId=a.Id,Month=month,Fingerprint=hash,ConfirmedAtUtc=Now};db.ScheduleReceipts.Add(receipt);await db.SaveChangesAsync(ct);}
        await tx.CommitAsync(ct);return Ok(receipt);
    }
    public async Task<WorkflowResult<object>> Changes(ReviewActor a,int page,int pageSize,CancellationToken ct)
    {
        if(!Manager(a) && !Officer(a)) return Forbidden();
        if(page is <1 or >100000 || pageSize is <1 or >100) return Fail(400,"page","Phân trang không hợp lệ.");
        var q=db.ShiftChanges.AsNoTracking().Where(x=>x.WardCode==a.WardCode && (a.IsManager || x.OfficerId==a.Id));
        return Ok(new ChangePage(await q.OrderBy(x=>x.OriginalStart).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct),page,pageSize,await q.CountAsync(ct)));
    }
    public async Task<WorkflowResult<object>> RequestChange(ReviewActor a,ChangeShiftInput input,CancellationToken ct)
    {
        if(!Officer(a)) return Forbidden();
        if(!Range(input.StartsAt,input.EndsAt) || input.StartsAt<=Now || !Reason(input.Reason)) return Fail(400,"invalid_input","Kiểm tra thời gian mới và lý do.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);await StaffingLocks.Ward(db,a.WardCode!,ct);
        var shift=await db.Shifts.SingleOrDefaultAsync(x=>x.Id==input.ShiftId && x.WardCode==a.WardCode && x.OfficerId==a.Id,ct);
        if(shift==null) return Fail(404,"not_found","Không tìm thấy ca của bạn.");
        if(shift.CancelledAtUtc.HasValue || shift.Version!=input.ExpectedVersion || shift.StartsAtUtc<=Now ||
            await db.ShiftChanges.AnyAsync(x=>x.ShiftId==shift.Id && x.Status=="PENDING",ct)) return Conflict();
        var row=new ShiftChangeRequest {ShiftId=shift.Id,OfficerId=a.Id,WardCode=a.WardCode!,OriginalVersion=shift.Version,OriginalStart=shift.StartsAtUtc,
            OriginalEnd=shift.EndsAtUtc,ProposedStart=input.StartsAt.UtcDateTime,ProposedEnd=input.EndsAt.UtcDateTime,Reason=input.Reason.Trim()};
        db.ShiftChanges.Add(row);
        WorkflowOutboxCapture.Staffing(db,a.WardCode!,row.Id,a.Id,"SHIFT_CHANGE_REQUESTED",true,Now);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(row);
    }
    public async Task<WorkflowResult<object>> DecideChange(ReviewActor a,Guid id,DecideShift input,string token,CancellationToken ct)
    {
        if(!Manager(a)) return Forbidden();
        if(input.Decision is not ("APPROVED" or "REJECTED") || !Reason(input.Reason)) return Fail(400,"invalid_input","Cần quyết định và lý do.");
        var officers=input.Decision=="APPROVED"?await directory.Officers(token,ct):[];
        await using var tx=await db.Database.BeginTransactionAsync(ct);await CalendarLocks.Read(db,ct);await StaffingLocks.Ward(db,a.WardCode!,ct);
        var change=await db.ShiftChanges.SingleOrDefaultAsync(x=>x.Id==id && x.WardCode==a.WardCode,ct);
        if(change==null) return Fail(404,"not_found","Không tìm thấy yêu cầu.");
        if(change.Status!="PENDING") return Conflict();
        if(input.Decision=="APPROVED")
        {
            var s=await db.Shifts.SingleAsync(x=>x.Id==change.ShiftId,ct);
            if(s.Version!=change.OriginalVersion || s.OfficerId!=change.OfficerId || s.CancelledAtUtc!=null || s.StartsAtUtc<=Now ||
                change.ProposedStart<=Now || s.StartsAtUtc!=change.OriginalStart || s.EndsAtUtc!=change.OriginalEnd) return Conflict();
            if(!officers.Any(o=>o.Id==s.OfficerId && Eligible(o,a.WardCode!))) return Fail(422,"officer_ineligible","Cán bộ không đủ điều kiện.");
            if(await db.Shifts.AnyAsync(x=>x.Id!=s.Id && x.WardCode==a.WardCode && x.OfficerId==s.OfficerId && x.CancelledAtUtc==null &&
                x.StartsAtUtc<change.ProposedEnd && x.EndsAtUtc>change.ProposedStart,ct) ||
                await db.Leaves.AnyAsync(x=>x.WardCode==a.WardCode && x.OfficerId==s.OfficerId && x.Status=="APPROVED" &&
                x.StartsAtUtc<change.ProposedEnd && x.EndsAtUtc>change.ProposedStart,ct)) return Conflict();
            var closures=await db.CalendarClosures.Where(x=>x.CancelledAtUtc==null && (x.WardCode==null || x.WardCode==a.WardCode) &&
                x.ClosureDate>=LocalDay(change.ProposedStart) && x.ClosureDate<=LocalDay(change.ProposedEnd)).Select(x=>x.ClosureDate).ToArrayAsync(ct);
            if(ClosureHit(change.ProposedStart,change.ProposedEnd,closures)) return Fail(409,"closure","Ngày đề xuất đã đóng cửa.");
            s.StartsAtUtc=change.ProposedStart;s.EndsAtUtc=change.ProposedEnd;s.Version++;
        }
        change.DecidedBy=a.Id;change.DecidedAtUtc=Now;change.DecisionReason=input.Reason.Trim();change.Status=input.Decision;
        WorkflowOutboxCapture.Staffing(db,a.WardCode!,change.Id,change.OfficerId,"SHIFT_CHANGE_DECIDED",false,change.DecidedAtUtc.Value);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(change);
    }
}
