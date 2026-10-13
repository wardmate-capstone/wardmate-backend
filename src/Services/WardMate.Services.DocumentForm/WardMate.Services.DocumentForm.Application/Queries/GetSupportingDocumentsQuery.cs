using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetSupportingDocumentsQuery(Guid ApplicationId)
    : IQuery<IReadOnlyList<SupportingDocumentDto>>;

public sealed class GetSupportingDocumentsQueryHandler
    : IQueryHandler<GetSupportingDocumentsQuery, IReadOnlyList<SupportingDocumentDto>>
{
    private readonly IDocumentDbContext _dbContext;

    public GetSupportingDocumentsQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<SupportingDocumentDto>>> Handle(
        GetSupportingDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var list = await _dbContext.SupportingDocuments
            .AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new SupportingDocumentDto
            {
                Id = d.Id,
                ApplicationId = d.ApplicationId,
                ChecklistId = d.ChecklistId,
                FileName = d.FileName,
                BlobUrl = d.BlobUrl,
                FileSizeBytes = d.FileSizeBytes,
                ContentType = d.ContentType,
                CreatedAtUtc = d.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupportingDocumentDto>>(list);
    }
}
