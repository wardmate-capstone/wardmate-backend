using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

internal static class CalendarLocks
{
    // Always before application row locks. Shared submit/resubmit; exclusive calendar writes.
    public static Task Read(WorkflowDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(1464680778)", ct);
    public static Task Write(WorkflowDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1464680778)", ct);
    public static async Task Load(WorkflowDbContext db, ApplicationRecord a, CancellationToken ct) =>
        a.CalendarClosures = (await db.CalendarClosures.AsNoTracking().Where(x => x.CancelledAtUtc == null && (x.WardCode == null || x.WardCode == a.WardCode)).ToArrayAsync(ct)).Select(x => x.Interval()).ToArray();
}
public sealed class CalendarClosureService(WorkflowDbContext db, TimeProvider clock) : ICalendarClosureService
{
    private static WorkflowResult<T> Fail<T>(int status,string code,string text) => WorkflowResult<T>.Fail(status,"staffing."+code,text);
    private static bool Ward(CalendarActor a) => !string.IsNullOrWhiteSpace(a.WardCode);
    private static bool Reason(string? s) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= 500;
    public async Task<WorkflowResult<ClosureDto[]>> List(CalendarActor a, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!a.CanManageNational && !(a.CanReadWard && Ward(a))) return Fail<ClosureDto[]>(403,"forbidden","Không có quyền xem lịch.");
        if (from == default || to == default || to < from || to.DayNumber-from.DayNumber > 399) return Fail<ClosureDto[]>(400,"invalid_date","Khoảng ngày tối đa 400 ngày, gồm cả hai đầu.");
        var rows = await db.CalendarClosures.AsNoTracking().Where(x => x.ClosureDate >= from && x.ClosureDate <= to &&
            (x.WardCode == null || !a.CanManageNational && x.WardCode == a.WardCode)).OrderBy(x=>x.ClosureDate).ThenBy(x=>x.Id).ToArrayAsync(ct);
        return WorkflowResult<ClosureDto[]>.Ok(rows.Select(ClosureDto.From).ToArray());
    }
    public async Task<WorkflowResult<ClosureChange>> Create(CalendarActor a, ClosureInput input, bool national, CancellationToken ct)
    {
        if (national ? !a.CanManageNational : !a.CanManageWard || !Ward(a)) return Fail<ClosureChange>(403,"forbidden","Không có quyền khai báo lịch trong phạm vi này.");
        if (!Reason(input.Reason)) return Fail<ClosureChange>(400,"reason_required","Cần lý do từ 1 đến 500 ký tự sau khi bỏ khoảng trắng.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CalendarLocks.Write(db,ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        if (input.Date < today || input.Date > today.AddDays(366)) return Fail<ClosureChange>(400,"invalid_date","Ngày phải từ hôm nay đến 366 ngày tiếp theo.");
        var ward = national ? null : a.WardCode;
        if (await db.CalendarClosures.AnyAsync(x=>x.WardCode==ward && x.ClosureDate==input.Date && x.CancelledAtUtc==null,ct))
            return Fail<ClosureChange>(409,"closure_exists","Đã có ngày đóng cửa đang hiệu lực.");
        var old = await db.CalendarClosures.AsNoTracking().Where(x=>x.CancelledAtUtc==null).ToArrayAsync(ct);
        var row = new CalendarClosure { WardCode=ward, ClosureDate=input.Date, Reason=input.Reason.Trim(), CreatedBy=a.Id, CreatedAtUtc=now,
            EffectiveFromUtc=input.Date==today ? now : DateTime.SpecifyKind(input.Date.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc) };
        db.CalendarClosures.Add(row);
        var count = await Reschedule(a,row,old,old.Append(row).ToArray(),now,false,ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return WorkflowResult<ClosureChange>.Ok(new(ClosureDto.From(row),count));
    }
    public async Task<WorkflowResult<ClosureChange>> Cancel(CalendarActor a, Guid id, ClosureCancelInput input, CancellationToken ct)
    {
        if (!a.CanManageNational && !(a.CanManageWard && Ward(a))) return Fail<ClosureChange>(403,"forbidden","Không có quyền hủy lịch.");
        if (!Reason(input.Reason)) return Fail<ClosureChange>(400,"reason_required","Cần lý do từ 1 đến 500 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CalendarLocks.Write(db,ct);
        var row = await db.CalendarClosures.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if (row is null) return Fail<ClosureChange>(404,"not_found","Không tìm thấy ngày đóng cửa.");
        if (row.WardCode is null ? !a.CanManageNational : !a.CanManageWard || row.WardCode != a.WardCode)
            return Fail<ClosureChange>(403,"forbidden","Không được hủy lịch ngoài phạm vi quản lý.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (row.CancelledAtUtc != null) return Fail<ClosureChange>(409,"closure_cancelled","Ngày đóng cửa đã hủy.");
        if (row.ClosureDate <= DateOnly.FromDateTime(now.AddHours(7))) return Fail<ClosureChange>(409,"closure_started","Chỉ hủy được ngày chưa bắt đầu.");
        var old = await db.CalendarClosures.AsNoTracking().Where(x=>x.CancelledAtUtc==null).ToArrayAsync(ct);
        var count = await Reschedule(a,row,old,old.Where(x=>x.Id!=id).ToArray(),now,true,ct);
        row.CancelledBy=a.Id; row.CancelledAtUtc=now; row.CancellationReason=input.Reason.Trim();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return WorkflowResult<ClosureChange>.Ok(new(ClosureDto.From(row),count));
    }
    private async Task<int> Reschedule(CalendarActor actor, CalendarClosure closure, CalendarClosure[] before, CalendarClosure[] after, DateTime now, bool cancel, CancellationToken ct)
    {
        // Exclusive calendar lock serializes writes and prevents submit/resubmit reading stale calendars.
        var rows = closure.WardCode is null
            ? await db.Applications.FromSqlRaw("SELECT * FROM applications WHERE deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND documents_ready_at IS NULL AND status IN ('SUBMITTED','UNDER_REVIEW','NEED_REVISION') ORDER BY id FOR UPDATE").ToArrayAsync(ct)
            : await db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE ward_code={closure.WardCode} AND deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND documents_ready_at IS NULL AND status IN ('SUBMITTED','UNDER_REVIEW','NEED_REVISION') ORDER BY id FOR UPDATE").ToArrayAsync(ct);
        var count=0;
        foreach(var app in rows)
        {
            var old = before.Where(x=>x.WardCode==null || x.WardCode==app.WardCode).Select(x=>x.Interval()).ToArray();
            var next = after.Where(x=>x.WardCode==null || x.WardCode==app.WardCode).Select(x=>x.Interval()).ToArray();
            if(ReviewDeadline.Recalculate(app,now,actor.Id,closure.Id,old,next,cancel)) { count++; app.UpdatedAt=now; }
        }
        return count;
    }
}
