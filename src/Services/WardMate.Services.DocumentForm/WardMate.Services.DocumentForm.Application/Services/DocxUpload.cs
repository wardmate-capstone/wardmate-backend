using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Application.Services;

public static class DocxUpload
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public static async Task<Result<byte[]>> ReadAsync(Stream source, string name, long length, CancellationToken ct)
    {
        if (length <= 0) return DocumentFormErrors.EmptyFile;
        if (length > MaxBytes) return DocumentFormErrors.FileTooLarge(MaxBytes);
        if (!string.Equals(Path.GetExtension(name), ".docx", StringComparison.OrdinalIgnoreCase))
            return DocumentFormErrors.InvalidDocxFile("Only .docx files are accepted.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) return DocumentFormErrors.FileTooLarge(MaxBytes);
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length == 0) return DocumentFormErrors.EmptyFile;
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
            if (zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 100L * 1024 * 1024)
                return DocumentFormErrors.InvalidDocxFile("Expanded DOCX package is too large.");
            using var doc = WordprocessingDocument.Open(new MemoryStream(bytes, false), false);
            if (doc.DocumentType != DocumentFormat.OpenXml.WordprocessingDocumentType.Document
                || doc.MainDocumentPart?.Document.Body is null || doc.MainDocumentPart.VbaProjectPart is not null)
                return DocumentFormErrors.InvalidDocxFile("Expected a DOCX document without macros.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.InvalidDocxFile("Invalid or unsupported DOCX."); }
        return bytes;
    }
}
