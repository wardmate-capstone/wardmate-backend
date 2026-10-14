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
    bool IsOfficer = false)
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

                if (submission.Status != SubmissionStatus.Approved)
                {
                    return DocumentFormErrors.SubmissionNotApproved(submission.Id, submission.Status.ToString());
                }
            }
        }

        var list = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new GeneratedDocumentDto
            {
                Id = d.Id,
                ApplicationId = d.ApplicationId,
                DocumentType = d.DocumentType,
                PdfBlobUrl = d.PdfBlobUrl,
                CreatedAtUtc = d.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<GeneratedDocumentDto>>(list);
    }
}
