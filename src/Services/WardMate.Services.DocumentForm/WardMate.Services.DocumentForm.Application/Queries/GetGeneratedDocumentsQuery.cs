using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetGeneratedDocumentsQuery(
    Guid ApplicationId,
    Guid? CallerUserId = null,
    bool IsOfficer = false,
    Guid? SubmissionId = null)
    : IQuery<IReadOnlyList<GeneratedDocumentDto>>;

public sealed class GetGeneratedDocumentsQueryHandler
    : IQueryHandler<GetGeneratedDocumentsQuery, IReadOnlyList<GeneratedDocumentDto>>
{
    private readonly IDocumentDbContext _dbContext;

    public GetGeneratedDocumentsQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<GeneratedDocumentDto>>> Handle(
        GetGeneratedDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.IsOfficer && request.CallerUserId.HasValue && request.CallerUserId.Value != Guid.Empty)
        {
            var submission = await _dbContext.UserSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ApplicationId == request.ApplicationId && !s.IsDeleted, cancellationToken);

            if (submission is not null)
            {
                if (submission.ApplicantId != request.CallerUserId.Value)
                {
                    return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.CallerUserId.Value);
                }

                if (submission.Status != SubmissionStatus.ReadyForDownload)
                {
                    return DocumentFormErrors.SubmissionNotReady(submission.Id, submission.Status.ToString());
                }
            }
        }

        var query = _dbContext.GeneratedDocuments
            .AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted);

        if (request.SubmissionId.HasValue && request.SubmissionId.Value != Guid.Empty)
        {
            query = query.Where(d => d.SubmissionId == request.SubmissionId.Value);
        }

        var list = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new GeneratedDocumentDto
            {
                Id = d.Id,
                ApplicationId = d.ApplicationId,
                SubmissionId = d.SubmissionId,
                DocumentType = d.DocumentType,
                PdfBlobUrl = d.PdfBlobUrl,
                CreatedAtUtc = d.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GeneratedDocumentDto>>(list);
    }
}
