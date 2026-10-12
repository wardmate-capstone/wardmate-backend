using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetGeneratedDocumentsQuery(Guid ApplicationId)
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
