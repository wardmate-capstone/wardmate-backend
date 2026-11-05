using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class LeaveDraftService(WorkflowDbContext db, IConfiguration config, TimeProvider clock,
    IBlobStorageClient? storage = null, Azure.Storage.Blobs.BlobServiceClient? blobService=null) : ILeaveDrafts
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private const string Container = LeaveBlobMaintenance.Container;
    private static WorkflowResult<T> Fail<T>(int status, string code) =>
        WorkflowResult<T>.Fail(status, "leave." + code, code switch
        {
            "not_found" => "Không tìm thấy đơn trong phạm vi.",
            "version_conflict" => "Đơn đã thay đổi; hãy tải lại trước khi lưu.",
            "invalid_docx" => "Tệp không phải DOCX được hỗ trợ.",
            "storage_unavailable" => "Chưa đọc hoặc lưu được tài liệu nghỉ phép.",
            "overlap" => "Khoảng nghỉ trùng với đơn đang chờ hoặc đã duyệt.",
            "revision_required" => "Cần lưu bản Word tương ứng với thông tin nghỉ mới nhất.",
            "key_conflict" => "Khóa gửi lại đã được dùng cho nội dung khác.",
            "template_unavailable" => "Chưa cấu hình mẫu đơn nghỉ cho phường.",
            "forbidden" => "Không có quyền thao tác đơn nghỉ.",
            "too_large" => "DOCX vượt quá 10 MiB.",
            _ => "Thông tin yêu cầu hoặc trạng thái đơn không hợp lệ."
        });
    private static WorkflowResult<T> Error<T>(WorkflowError error) => new(default, error);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Fingerprint<T>(T value) => Digest(JsonSerializer.SerializeToUtf8Bytes(value));
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private void Audit(ReviewActor actor,LeaveDraft draft,string action)=>db.Set<LeaveDraftAudit>().Add(new()
    {
        DraftId=draft.Id,ActorId=actor.Id,WardCode=draft.WardCode,Action=action,Version=draft.Version,
        RevisionId=draft.CurrentRevisionId,OccurredAtUtc=Now
    });
    private static bool Owner(ReviewActor a) => a.CanMutate && !a.IsManager && !string.IsNullOrWhiteSpace(a.WardCode);
    private IQueryable<LeaveDraft> Own(ReviewActor a) => db.LeaveDrafts.Where(x =>
        x.OfficerId == a.Id && x.WardCode == a.WardCode && x.DeletedAtUtc == null);
    private static LeaveDraftView View(LeaveDraft d) => new(d.Id,d.Version,d.MetadataVersion,d.TemplateVersionId,
        d.StartsAtUtc,d.EndsAtUtc,d.Reason,d.CurrentRevisionId,d.SubmittedLeaveId,d.UpdatedAtUtc);
    private bool Valid(DateTimeOffset from, DateTimeOffset to, string? reason) => to > from && to - from <= TimeSpan.FromDays(93)
        && to.UtcDateTime > Now && !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 2000;
    private static string TemplateKey(Guid id) => $"templates/{id:N}.docx";
    private static string RevisionKey(Guid draft, Guid revision) => $"drafts/{draft:N}/{revision:N}.docx";
    public WorkflowResult<LeaveTemplateDescriptor> Template(ReviewActor actor)
    {
        if (!Owner(actor)) return Fail<LeaveTemplateDescriptor>(403,"forbidden");
        var section = config.GetSection($"WorkSchedule:Wards:{actor.WardCode}");
        var hash = section["TemplateSha256"];
        return Guid.TryParse(section["TemplateVersionId"],out var id) && id != Guid.Empty &&
            hash is { Length: 64 } && hash.All(Uri.IsHexDigit)
            ? WorkflowResult<LeaveTemplateDescriptor>.Ok(new(id,hash.ToUpperInvariant()))
            : Fail<LeaveTemplateDescriptor>(503,"template_unavailable");
    }
    private static async Task<byte[]?> Bounded(Stream stream, CancellationToken ct)
    {
        using var data = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer,ct)) > 0)
        {
            if (data.Length + read > MaxBytes) return null;
            data.Write(buffer,0,read);
        }
        return data.ToArray();
    }
    private static bool Docx(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content,false));
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > 64L * 1024 * 1024) return false;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool mainType = false, body = false, rootLink = false;
            foreach (var e in zip.Entries)
            {
                var name = e.FullName;
                if (!names.Add(name) || name.StartsWith('/') || name.Contains('\\') || name.Split('/').Contains("..")
                    || name.Contains("vbaProject",StringComparison.OrdinalIgnoreCase)) return false;
                if (name != "[Content_Types].xml" && name != "word/document.xml" && !name.EndsWith(".rels",StringComparison.OrdinalIgnoreCase)) continue;
                using var input=e.Open();
                using var xml=XmlReader.Create(input,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=16*1024*1024 });
                var doc=XDocument.Load(xml);
                if(name=="[Content_Types].xml")
                {
                    if(doc.Descendants().Any(x=>((string?)x.Attribute("ContentType") ?? "").Contains("macroEnabled",StringComparison.OrdinalIgnoreCase))) return false;
                    mainType=doc.Descendants().Any(x=>(string?)x.Attribute("PartName")=="/word/document.xml" &&
                        (string?)x.Attribute("ContentType")=="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
                }
                else if(name=="word/document.xml")
                {
                    XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    body=doc.Root?.Name==w+"document" && doc.Root.Element(w+"body")!=null;
                }
                else
                {
                    if(doc.Descendants().Any(x=>string.Equals((string?)x.Attribute("TargetMode"),"External",StringComparison.OrdinalIgnoreCase))) return false;
                    if(name=="_rels/.rels") rootLink=doc.Descendants().Any(x=>
                        ((string?)x.Attribute("Type"))?.EndsWith("/officeDocument",StringComparison.Ordinal)==true &&
                        ((string?)x.Attribute("Target"))?.TrimStart('/')=="word/document.xml");
                }
            }
            return mainType && body && rootLink;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or XmlException or OverflowException) { return false; }
    }
    private async Task<WorkflowResult<byte[]>> Fetch(string key, string hash, CancellationToken ct)
    {
        if(storage is null || blobService is not null && !await LeaveBlobMaintenance.Private(blobService,ct)) return Fail<byte[]>(503,"storage_unavailable");
        await using var stream=await storage.DownloadAsync(key,Container,ct);
        if(stream is null) return Fail<byte[]>(503,"storage_unavailable");
        var bytes=await Bounded(stream,ct);
        return bytes is not null && Digest(bytes)==hash && Docx(bytes) ? WorkflowResult<byte[]>.Ok(bytes) : Fail<byte[]>(503,"storage_unavailable");
    }
    public async Task<WorkflowResult<byte[]>> TemplateFile(ReviewActor a, Guid id, CancellationToken ct)
    {
        var template=Template(a);
        if(template.Error is {} error) return Error<byte[]>(error);
        return template.Value!.VersionId==id ? await Fetch(TemplateKey(id),template.Value.Sha256,ct) : Fail<byte[]>(404,"not_found");
    }
    public async Task<WorkflowResult<LeaveDraftView>> Create(ReviewActor a, LeaveDraftInput input, Guid key, CancellationToken ct)
    {
        if(!Owner(a)) return Fail<LeaveDraftView>(403,"forbidden");
        if(key==Guid.Empty) return Fail<LeaveDraftView>(400,"invalid_input");
        var hash=Fingerprint(input);
        var existing=await db.LeaveDrafts.AsNoTracking().SingleOrDefaultAsync(x=>x.OfficerId==a.Id && x.CreationKey==key,ct);
        if(existing is not null) return Replay(existing,a,hash);
        if(!Valid(input.StartsAt,input.EndsAt,input.Reason)) return Fail<LeaveDraftView>(400,"invalid_input");
        var template=Template(a);
        if(template.Error is {} error) return Error<LeaveDraftView>(error);
        if(template.Value!.VersionId!=input.TemplateVersionId) return Fail<LeaveDraftView>(409,"version_conflict");
        var file=await Fetch(TemplateKey(input.TemplateVersionId),template.Value.Sha256,ct);
        if(file.Error is {} fileError) return Error<LeaveDraftView>(fileError);
        var draft=new LeaveDraft { OfficerId=a.Id,WardCode=a.WardCode!,StartsAtUtc=input.StartsAt.UtcDateTime,EndsAtUtc=input.EndsAt.UtcDateTime,
            Reason=input.Reason.Trim(),TemplateVersionId=input.TemplateVersionId,TemplateHash=template.Value.Sha256,CreationKey=key,CreationHash=hash,UpdatedAtUtc=Now };
        var initial=new LeaveDocumentRevision {DraftId=draft.Id,MetadataVersion=0,Sha256=draft.TemplateHash,
            SizeBytes=file.Value!.Length,SavedAtUtc=Now};
        using var copy=new MemoryStream(file.Value,false);
        await storage!.UploadAsync(copy,RevisionKey(draft.Id,initial.Id),"application/vnd.openxmlformats-officedocument.wordprocessingml.document",Container,ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db,a.WardCode!,ct);
        existing=await db.LeaveDrafts.SingleOrDefaultAsync(x=>x.OfficerId==a.Id && x.CreationKey==key,ct);
        if(existing is not null) return Replay(existing,a,hash);
        db.LeaveDrafts.Add(draft); await db.SaveChangesAsync(ct);
        db.LeaveRevisions.Add(initial);draft.CurrentRevisionId=initial.Id;Audit(a,draft,"CREATE");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return WorkflowResult<LeaveDraftView>.Ok(View(draft));
    }
    private static WorkflowResult<LeaveDraftView> Replay(LeaveDraft draft,ReviewActor actor,string hash) =>
        draft.WardCode==actor.WardCode && draft.DeletedAtUtc==null && draft.CreationHash==hash
        ? WorkflowResult<LeaveDraftView>.Ok(View(draft)) : Fail<LeaveDraftView>(409,"key_conflict");
    public async Task<WorkflowResult<LeavePage>> List(ReviewActor a,int page,int pageSize,CancellationToken ct)
    {
        if(!Owner(a)) return Fail<LeavePage>(403,"forbidden");
        if(page is <1 or >100000 || pageSize is <1 or >100) return Fail<LeavePage>(400,"invalid_input");
        var q=Own(a).AsNoTracking().Where(x=>x.SubmittedLeaveId==null);
        var total=await q.CountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.UpdatedAtUtc).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        return WorkflowResult<LeavePage>.Ok(new(rows.Select(View).ToArray(),page,pageSize,total));
    }
    public async Task<WorkflowResult<LeaveDraftView>> Get(ReviewActor a,Guid id,CancellationToken ct)
    {
        if(!Owner(a)) return Fail<LeaveDraftView>(403,"forbidden");
        var d=await Own(a).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        return d is null ? Fail<LeaveDraftView>(404,"not_found") : WorkflowResult<LeaveDraftView>.Ok(View(d));
    }
    private async Task<WorkflowResult<LeaveDraftView>> Change(ReviewActor a,Guid id,int version,string action,Action<LeaveDraft> change,CancellationToken ct)
    {
        if(!Owner(a)) return Fail<LeaveDraftView>(403,"forbidden");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db,a.WardCode!,ct);
        var d=await Own(a).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(d is null) return Fail<LeaveDraftView>(404,"not_found");
        if(d.Version!=version || d.SubmittedLeaveId.HasValue) return Fail<LeaveDraftView>(409,"version_conflict");
        change(d); d.UpdatedAtUtc=Now; d.Version++;Audit(a,d,action);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return WorkflowResult<LeaveDraftView>.Ok(View(d));
    }
    public Task<WorkflowResult<LeaveDraftView>> Edit(ReviewActor a,Guid id,LeaveDraftEdit input,CancellationToken ct) =>
        !Valid(input.StartsAt,input.EndsAt,input.Reason) ? Task.FromResult(Fail<LeaveDraftView>(400,"invalid_input"))
        : Change(a,id,input.ExpectedVersion,"EDIT",d=>{ d.StartsAtUtc=input.StartsAt.UtcDateTime; d.EndsAtUtc=input.EndsAt.UtcDateTime; d.Reason=input.Reason.Trim(); d.MetadataVersion++; },ct);
    public Task<WorkflowResult<LeaveDraftView>> Delete(ReviewActor a,Guid id,int version,CancellationToken ct) =>
        Change(a,id,version,"DELETE",d=>d.DeletedAtUtc=Now,ct);
    public async Task<WorkflowResult<byte[]>> Read(ReviewActor a,Guid id,bool submitted,CancellationToken ct)
    {
        if(!Owner(a) && !(submitted && a.IsManager && !string.IsNullOrWhiteSpace(a.WardCode))) return Fail<byte[]>(403,"forbidden");
        var d=await db.LeaveDrafts.AsNoTracking().SingleOrDefaultAsync(x=>x.WardCode==a.WardCode && x.DeletedAtUtc==null &&
            (x.OfficerId==a.Id || submitted && a.IsManager) && (submitted ? x.SubmittedLeaveId==id : x.Id==id),ct);
        if(d is null) return Fail<byte[]>(404,"not_found");
        if(d.CurrentRevisionId is not {} revId) return await Fetch(TemplateKey(d.TemplateVersionId),d.TemplateHash,ct);
        var rev=await db.LeaveRevisions.AsNoTracking().SingleAsync(x=>x.Id==revId && x.DraftId==d.Id,ct);
        return await Fetch(RevisionKey(d.Id,revId),rev.Sha256,ct);
    }
    public async Task<WorkflowResult<LeaveDraftView>> Upload(ReviewActor a,Guid id,int version,int metadataVersion,Stream data,string filename,CancellationToken ct)
    {
        var current=await Get(a,id,ct);
        if(current.Error is not null) return current;
        if(current.Value!.Version!=version || current.Value.MetadataVersion!=metadataVersion || current.Value.SubmittedLeaveId.HasValue)
            return Fail<LeaveDraftView>(409,"version_conflict");
        if(storage is null || blobService is not null && !await LeaveBlobMaintenance.Private(blobService,ct)) return Fail<LeaveDraftView>(503,"storage_unavailable");
        var bytes=await Bounded(data,ct);
        if(bytes is null) return Fail<LeaveDraftView>(413,"too_large");
        if(!string.Equals(Path.GetExtension(filename),".docx",StringComparison.OrdinalIgnoreCase) || !Docx(bytes)) return Fail<LeaveDraftView>(415,"invalid_docx");
        var rev=new LeaveDocumentRevision { DraftId=id,MetadataVersion=metadataVersion,Sha256=Digest(bytes),SizeBytes=bytes.Length,SavedAtUtc=Now };
        var key=RevisionKey(id,rev.Id);
        using var stream=new MemoryStream(bytes,false);
        await storage.UploadAsync(stream,key,"application/vnd.openxmlformats-officedocument.wordprocessingml.document",Container,ct);
        var result=await Change(a,id,version,"SAVE_WORD",d=>{ db.LeaveRevisions.Add(rev); d.CurrentRevisionId=rev.Id; },ct);
        if(result.Error is not null) await storage.DeleteAsync(key,Container,ct);
        return result;
    }
    public async Task<WorkflowResult<LeaveSubmitReceipt>> Submit(ReviewActor a,Guid id,LeaveDraftSubmit input,Guid key,CancellationToken ct)
    {
        if(!Owner(a)) return Fail<LeaveSubmitReceipt>(403,"forbidden");
        if(key==Guid.Empty) return Fail<LeaveSubmitReceipt>(400,"invalid_input");
        var hash=Fingerprint(new { id,input });
        var prior=await Own(a).AsNoTracking().SingleOrDefaultAsync(x=>x.SubmissionKey==key,ct);
        if(prior!=null) return Receipt(prior,hash);
        var rev=await (from r in db.LeaveRevisions.AsNoTracking() join d in Own(a).AsNoTracking() on r.DraftId equals d.Id
            where d.Id==id && d.CurrentRevisionId==input.DocumentRevisionId && r.Id==input.DocumentRevisionId &&
                r.MetadataVersion==input.MetadataVersion select r).SingleOrDefaultAsync(ct);
        if(rev==null) return Fail<LeaveSubmitReceipt>(409,"revision_required");
        var file=await Fetch(RevisionKey(id,rev.Id),rev.Sha256,ct);
        if(file.Error is {} error) return Error<LeaveSubmitReceipt>(error);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db,a.WardCode!,ct);
        prior=await Own(a).SingleOrDefaultAsync(x=>x.SubmissionKey==key,ct);
        if(prior!=null) return Receipt(prior,hash);
        var draft=await Own(a).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(draft==null) return Fail<LeaveSubmitReceipt>(404,"not_found");
        if(draft.SubmittedLeaveId.HasValue || draft.Version!=input.ExpectedVersion ||
            draft.MetadataVersion!=input.MetadataVersion || draft.CurrentRevisionId!=rev.Id) return Fail<LeaveSubmitReceipt>(409,"version_conflict");
        if(draft.EndsAtUtc<=Now) return Fail<LeaveSubmitReceipt>(400,"invalid_input");
        if(await db.Leaves.AnyAsync(x=>x.WardCode==a.WardCode && x.OfficerId==a.Id && (x.Status=="PENDING" || x.Status=="APPROVED") &&
            x.StartsAtUtc<draft.EndsAtUtc && x.EndsAtUtc>draft.StartsAtUtc,ct)) return Fail<LeaveSubmitReceipt>(409,"overlap");
        var leave=new OfficerLeave { OfficerId=a.Id,WardCode=a.WardCode!,StartsAtUtc=draft.StartsAtUtc,EndsAtUtc=draft.EndsAtUtc,Reason=draft.Reason,CreatedAtUtc=Now };
        db.Leaves.Add(leave); draft.SubmittedLeaveId=leave.Id; draft.SubmissionKey=key; draft.SubmissionHash=hash;
        draft.Version++; draft.UpdatedAtUtc=leave.CreatedAtUtc;draft.SubmittedOfficerName=a.FullName;Audit(a,draft,"SUBMIT");
        WorkflowOutboxCapture.Staffing(db,leave.WardCode,leave.Id,leave.OfficerId,"LEAVE_SUBMITTED",true,leave.CreatedAtUtc);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Receipt(draft,hash);
    }
    private static WorkflowResult<LeaveSubmitReceipt> Receipt(LeaveDraft d,string hash) =>
        d.SubmissionHash==hash && d.SubmittedLeaveId is {} id
            ? WorkflowResult<LeaveSubmitReceipt>.Ok(new(id,"PENDING",0,d.UpdatedAtUtc)) : Fail<LeaveSubmitReceipt>(409,"key_conflict");
}
