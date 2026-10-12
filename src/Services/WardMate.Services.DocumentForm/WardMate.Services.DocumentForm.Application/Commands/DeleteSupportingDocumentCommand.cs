using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record DeleteSupportingDocumentCommand(
    Guid DocumentId,
    string? UpdatedBy = null) : ICommand;

public sealed class DeleteSupportingDocumentCommandHandler
    : ICommandHandler<DeleteSupportingDocumentCommand>
{
    private readonly IDocumentDbContext _dbContext;

    public DeleteSupportingDocumentCommandHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        DeleteSupportingDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var doc = await _dbContext.SupportingDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && !d.IsDeleted, cancellationToken);

        if (doc is null)
        {
            return DocumentFormErrors.SupportingDocumentNotFound(request.DocumentId);
        }

        doc.SoftDelete(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
