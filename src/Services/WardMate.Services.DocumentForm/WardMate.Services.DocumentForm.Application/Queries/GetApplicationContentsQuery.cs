using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;
using WardMate.SharedKernel.Blob;
using WardMate.Services.DocumentForm.Application.Services;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record ApplicationContentItem(Guid Id, Guid? ChecklistId, string FileName, string Status, Guid? TemplateId = null,
    bool FileExists = false, string? SubmissionType = null, string? DocumentCopyType = null, int Quantity = 1);
public sealed record ApplicationContentsDto(Guid ApplicationId, IReadOnlyList<ApplicationContentItem> Submissions,
    IReadOnlyList<ApplicationContentItem> SupportingDocuments);
public sealed record GetApplicationContentsQuery(Guid ApplicationId) : IQuery<ApplicationContentsDto>;
public sealed class GetApplicationContentsHandler(IDocumentDbContext db, IBlobStorageClient blobs) : IQueryHandler<GetApplicationContentsQuery, ApplicationContentsDto>
{
    public async Task<Result<ApplicationContentsDto>> Handle(GetApplicationContentsQuery request, CancellationToken ct)
    {
        var submissions = await db.UserSubmissions.AsNoTracking()
            .Where(s => s.ApplicationId == request.ApplicationId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        var documents = await db.SupportingDocuments.AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        var forms = new List<ApplicationContentItem>();
        var evidence = new List<ApplicationContentItem>();
        try
        {
            foreach (var s in submissions)
                forms.Add(new(s.Id, s.ChecklistId, s.FileName, s.Status.ToString(), s.TemplateId,
                    await blobs.ExistsAsync(OnlineFormSupport.BlobName(s.BlobUrl), ct: ct)));
            foreach (var d in documents)
                evidence.Add(new(d.Id, d.ChecklistId, d.FileName, "Uploaded", null,
                    await blobs.ExistsAsync(OnlineFormSupport.BlobName(d.BlobUrl), ct: ct),
                    d.SubmissionType, d.DocumentCopyType, d.Quantity));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Error.Failure("document.storage_unavailable", "Chưa thể xác minh file trong kho lưu trữ. Vui lòng thử lại.");
        }
        return new ApplicationContentsDto(request.ApplicationId, forms, evidence);
    }
}
