using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetApprovedQrImageQuery(Guid ApplicationId) : IQuery<byte[]>;

public sealed class GetApprovedQrImageQueryHandler : IQueryHandler<GetApprovedQrImageQuery, byte[]>
{
    private readonly IDocumentDbContext _dbContext;

    public GetApprovedQrImageQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<byte[]>> Handle(GetApprovedQrImageQuery request, CancellationToken cancellationToken)
    {
        var qr = await _dbContext.ApprovedQrs.AsNoTracking()
            .FirstOrDefaultAsync(q => q.ApplicationId == request.ApplicationId, cancellationToken);

        if (qr is null)
            return DocumentFormErrors.QrNotFound(request.ApplicationId);

        return qr.QrImagePng;
    }
}
