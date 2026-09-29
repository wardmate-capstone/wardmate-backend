using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record ExtractDocxPlaceholdersQuery(
    Stream DocxStream,
    string FileName) : IQuery<IReadOnlyList<DocxPlaceholder>>;

public sealed class ExtractDocxPlaceholdersQueryHandler : IQueryHandler<ExtractDocxPlaceholdersQuery, IReadOnlyList<DocxPlaceholder>>
{
    private readonly IDocxPlaceholderEngine _placeholderEngine;

    public ExtractDocxPlaceholdersQueryHandler(IDocxPlaceholderEngine placeholderEngine)
    {
        _placeholderEngine = placeholderEngine;
    }

    public async Task<Result<IReadOnlyList<DocxPlaceholder>>> Handle(ExtractDocxPlaceholdersQuery request, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(request.FileName);
        if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<DocxPlaceholder>>.Failure(
                DocumentFormErrors.InvalidDocxFile("Only .docx files are supported for placeholder extraction."));
        }

        using var ms = new MemoryStream();
        await request.DocxStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
        {
            return Result<IReadOnlyList<DocxPlaceholder>>.Failure(DocumentFormErrors.EmptyFile);
        }

        return _placeholderEngine.ExtractPlaceholders(bytes);
    }
}
