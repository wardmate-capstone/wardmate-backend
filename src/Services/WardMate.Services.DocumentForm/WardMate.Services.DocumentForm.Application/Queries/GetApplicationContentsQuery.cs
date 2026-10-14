using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record ApplicationContentItem(Guid Id, Guid? ChecklistId, string FileName, string Status, Guid? TemplateId = null);
public sealed record ApplicationContentsDto(Guid ApplicationId, IReadOnlyList<ApplicationContentItem> Submissions,
    IReadOnlyList<ApplicationContentItem> SupportingDocuments);
public sealed record GetApplicationContentsQuery(Guid ApplicationId, Guid ApplicantId) : IQuery<ApplicationContentsDto>;
public sealed class GetApplicationContentsHandler(IDocumentDbContext db) : IQueryHandler<GetApplicationContentsQuery, ApplicationContentsDto>
{
    public async Task<Result<ApplicationContentsDto>> Handle(GetApplicationContentsQuery request, CancellationToken ct)
    {
        var submissions = await db.UserSubmissions.AsNoTracking()
            .Where(s => s.ApplicationId == request.ApplicationId && s.ApplicantId == request.ApplicantId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => new ApplicationContentItem(s.Id, s.ChecklistId, s.FileName, s.Status.ToString(), s.TemplateId)).ToListAsync(ct);
        var documents = await db.SupportingDocuments.AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .OrderBy(d => d.CreatedAtUtc)
            .Select(d => new ApplicationContentItem(d.Id, d.ChecklistId, d.FileName, "Uploaded", null)).ToListAsync(ct);
        return new ApplicationContentsDto(request.ApplicationId, submissions, documents);
    }
}
