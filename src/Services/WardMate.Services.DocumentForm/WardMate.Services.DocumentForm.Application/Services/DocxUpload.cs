using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Application.Services;

public static class DocxUpload
{
    public const long MaxBytes = 20 * 1024 * 1024;
    private static readonly byte[] Ole2Header = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] WordDocumentStreamUtf16 = [0x57, 0x00, 0x6F, 0x00, 0x72, 0x00, 0x64, 0x00, 0x44, 0x00, 0x6F, 0x00, 0x63, 0x00, 0x75, 0x00, 0x6D, 0x00, 0x65, 0x00, 0x6E, 0x00, 0x74, 0x00];
    private static readonly byte[] WordDocumentStreamAscii = "WordDocument"u8.ToArray();
    private static readonly byte[] VbaProjectUtf16 = [0x5F, 0x00, 0x56, 0x00, 0x42, 0x00, 0x41, 0x00, 0x5F, 0x00, 0x50, 0x00, 0x52, 0x00, 0x4F, 0x00, 0x4A, 0x00, 0x45, 0x00, 0x43, 0x00, 0x54, 0x00];
    private static readonly byte[] VbaProjectAscii = "_VBA_PROJECT"u8.ToArray();

    public static async Task<Result<byte[]>> ReadAsync(Stream source, string name, long length, CancellationToken ct)
    {
        if (length <= 0) return DocumentFormErrors.EmptyFile;
        if (length > MaxBytes) return DocumentFormErrors.FileTooLarge(MaxBytes);
        var ext = Path.GetExtension(name);
        if (!string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase))
            return DocumentFormErrors.InvalidDocxFile("Chỉ chấp nhận tệp Word có định dạng .docx hoặc .doc.");

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

        // OLE2 binary Word format (.doc)
        if (bytes.Length >= 8 && bytes.AsSpan().StartsWith(Ole2Header))
        {
            if (!IsValidOle2Doc(bytes))
                return DocumentFormErrors.InvalidDocxFile("Tài liệu Word (.doc) không hợp lệ hoặc chứa macro.");
            return bytes;
        }

        // OpenXML Word format (.docx or docx content with .doc extension)
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
            if (zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 100L * 1024 * 1024)
                return DocumentFormErrors.InvalidDocxFile("Gói tài liệu Word giải nén vượt quá kích thước cho phép.");
            using var doc = WordprocessingDocument.Open(new MemoryStream(bytes, false), false);
            if (doc.DocumentType != DocumentFormat.OpenXml.WordprocessingDocumentType.Document
                || doc.MainDocumentPart?.Document.Body is null || doc.MainDocumentPart.VbaProjectPart is not null)
                return DocumentFormErrors.InvalidDocxFile("Tài liệu Word không hợp lệ hoặc chứa macro.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DocumentFormErrors.InvalidDocxFile("Tệp Word không hợp lệ hoặc không được hỗ trợ.");
        }
        return bytes;
    }

    public static bool IsValidOle2Doc(byte[] bytes)
    {
        if (bytes.Length < 512) return false;
        var span = bytes.AsSpan();
        if (!span.StartsWith(Ole2Header)) return false;

        bool hasWordDocument = span.IndexOf(WordDocumentStreamUtf16) >= 0 || span.IndexOf(WordDocumentStreamAscii) >= 0;
        if (!hasWordDocument) return false;

        bool hasVba = span.IndexOf(VbaProjectUtf16) >= 0 || span.IndexOf(VbaProjectAscii) >= 0;
        if (hasVba) return false;

        return true;
    }
}
