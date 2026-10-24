using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// Reads the embedded text layer only. Scanned (image-only) PDFs are rejected instead of silently indexed empty.
public sealed class KnowledgePdfReader : IKnowledgePdfReader
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private const int MaxPages = 100;

    public async Task<PdfTextResult> Read(Stream pdf, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await pdf.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new AiFailure(413, "knowledge.pdf_too_large", "PDF tối đa 10 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new AiFailure(415, "knowledge.pdf_invalid", "File không phải PDF.");
        var text = new StringBuilder();
        var empty = new List<int>();
        int pages;
        try
        {
            using var document = PdfDocument.Open(bytes);
            pages = document.NumberOfPages;
            if (pages is < 1 or > MaxPages) throw new AiFailure(422, "knowledge.pdf_page_limit", $"PDF phải có từ 1 đến {MaxPages} trang; hãy tách file.");
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var content = ContentOrderTextExtractor.GetText(page).Normalize(NormalizationForm.FormC).Trim();
                if (content.Count(char.IsLetterOrDigit) < 20) empty.Add(page.Number);
                if (content.Length > 0) text.Append(content).Append("\n\n");
                if (text.Length > KnowledgeChunker.MaxContentChars)
                    throw new AiFailure(422, "knowledge.content_too_long", "Nội dung PDF vượt quá 200.000 ký tự; hãy tách thành nhiều tài liệu.");
            }
        }
        catch (Exception e) when (e is not (AiFailure or OperationCanceledException or OutOfMemoryException))
        {
            throw new AiFailure(422, "knowledge.pdf_unreadable", "Không đọc được PDF. File có thể hỏng hoặc bị khóa; hãy xuất lại PDF không đặt mật khẩu.");
        }
        if (text.ToString().Count(char.IsLetterOrDigit) < 50)
            throw new AiFailure(422, "knowledge.pdf_no_text", "PDF không có lớp chữ đọc được (có thể là bản scan). Hãy dùng PDF có chữ hoặc nhập nội dung dạng văn bản.");
        return new(text.ToString(), pages, empty.ToArray());
    }
}
