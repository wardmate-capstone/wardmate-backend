using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetApprovedQrQuery(Guid ApplicationId) : IQuery<ApprovedQrDto>;

public sealed class GetApprovedQrQueryHandler : IQueryHandler<GetApprovedQrQuery, ApprovedQrDto>
{
    private readonly IDocumentDbContext _dbContext;

    public GetApprovedQrQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ApprovedQrDto>> Handle(GetApprovedQrQuery request, CancellationToken cancellationToken)
    {
        var qr = await _dbContext.ApprovedQrs.AsNoTracking()
            .FirstOrDefaultAsync(q => q.ApplicationId == request.ApplicationId, cancellationToken);

        if (qr is null)
            return DocumentFormErrors.QrNotFound(request.ApplicationId);

        return new ApprovedQrDto
        {
            Id = qr.Id,
            ApplicationId = qr.ApplicationId,
            SubmissionId = qr.SubmissionId,
            QrCodeValue = qr.QrCodeValue,
            GeneratedAt = qr.GeneratedAt
        };
    }
}
