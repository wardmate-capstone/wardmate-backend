using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using WardMate.Services.AIOCR.Application.Extraction;

namespace WardMate.Services.AIOCR.Infrastructure.Extraction;

public sealed class TextFirstProcedureExtractor(AzureProcedureDocumentExtractor azure, IConfiguration configuration)
    : IProcedureDocumentExtractor
{
    public async Task<ProcedureExtraction> Extract(Stream pdf, CancellationToken ct)
    {
        // Bound the input even when called outside the HTTP controller.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await pdf.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > 20 * 1024 * 1024) throw new InvalidDataException("PDF tối đa 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var text = new StringBuilder();
        var incompletePages = new List<int>();
        var readableCharacters = 0;
        var warnings = new List<string>();
        try
        {
            using var document = PdfDocument.Open(buffer.ToArray());
            if (document.NumberOfPages is < 1 or > 100) throw new InvalidDataException("PDF phải có từ 1 đến 100 trang; hãy tách file lớn.");
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var content = ContentOrderTextExtractor.GetText(page).Normalize(NormalizationForm.FormC);
                readableCharacters += content.Count(char.IsLetterOrDigit);
                // Conservative heuristic, not a guarantee that all visually displayed text was recovered.
                if (content.Count(char.IsLetterOrDigit) < 20 || content.Contains('\uFFFD') ||
                    content.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)))
                    incompletePages.Add(page.Number);
                if (page.NumberOfImages > 0)
                    warnings.Add($"Trang {page.Number} có ảnh: nội dung chữ trong ảnh chưa được đọc trực tiếp; cần kiểm tra PDF gốc.");
                text.AppendLine($"--- Trang {page.Number} ---").AppendLine(content).AppendLine();
                if (text.Length > 100_000) throw new InvalidDataException("Văn bản vượt quá 100.000 ký tự; hãy tách PDF, không cắt bỏ nội dung.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidDataException) { throw; }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidDataException("Không đọc được PDF. File có thể hỏng, được mã hóa hoặc không được hỗ trợ; vui lòng xuất lại PDF không khóa.", e);
        }
        var extracted = text.ToString();
        if (incompletePages.Count > 0)
        {
            if (configuration.GetValue<bool>("Extraction:OcrFallbackEnabled"))
            {
                buffer.Position = 0;
                return await azure.Extract(buffer, ct);
            }
            warnings.Add($"Các trang {string.Join(", ", incompletePages)} không có đủ văn bản đọc được. Cần đối soát thủ công hoặc OCR; không tự suy đoán dữ liệu từ phần còn thiếu.");
            if (readableCharacters < 20) return Manual(extracted, warnings.ToArray());
        }
        warnings.Add("Đã đọc lớp văn bản PDF trực tiếp, không dùng OCR. Kiểm tra thứ tự dòng và bảng biểu với bản gốc.");
        if (!configuration.GetValue<bool>("Extraction:UseAI"))
            return Manual(extracted, warnings.Append("Chưa bật AI: văn bản đã được trích xuất, dữ liệu thủ tục cần nhập và đối soát thủ công.").ToArray());
        try
        {
            var mapped = await azure.MapText(extracted, ct);
            return mapped with { Warnings = mapped.Warnings.Concat(warnings).ToArray() };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or InvalidDataException or JsonException or KeyNotFoundException or OperationCanceledException)
        {
            var reason = e switch
            {
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.ServiceUnavailable } => "Dịch vụ AI trả HTTP 503 sau khi thử lại; hãy thử lại sau hoặc đối soát thủ công.",
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "AI hết hạn mức hoặc đang giới hạn lượt gọi; hãy thử lại sau, không cần bật thanh toán.",
                HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } => "Kiểm tra API key và quyền truy cập model.",
                OperationCanceledException => "AI quá thời gian xử lý; hãy thử lại hoặc dùng tài liệu ngắn hơn.",
                HttpRequestException => "Không kết nối được AI hoặc nhà cung cấp trả lỗi; kiểm tra model và dịch vụ.",
                InvalidDataException invalid => invalid.Message,
                JsonException => "AI trả JSON không hợp lệ.",
                KeyNotFoundException => "Phản hồi AI thiếu trường bắt buộc: choices, message hoặc content.",
                _ => "Cấu hình AI chưa đủ hoặc kiểu dữ liệu trong phản hồi AI không đúng hợp đồng."
            };
            return Manual(extracted, warnings.Append("Không thể chuyển văn bản thành dữ liệu bằng AI. " + reason + " Văn bản được giữ lại để đối soát thủ công.").ToArray());
        }
    }

    internal static ProcedureExtraction Manual(string text, string[] warnings) => new(
        JsonSerializer.Deserialize<JsonElement>("""
        {"categoryId":0,"procedureCode":"","title":"","levelOfImplementation":"","targetAudience":"",
         "feeSummary":"","processingTimeSummary":"","contentPayload":{"decisionNumber":"","receivingAddress":"",
         "cases":[],"submissionMethods":[],"legalReferences":[],"results":[]},"checklistSchema":[],"formDefinitions":[]}
        """), text, warnings);
}
