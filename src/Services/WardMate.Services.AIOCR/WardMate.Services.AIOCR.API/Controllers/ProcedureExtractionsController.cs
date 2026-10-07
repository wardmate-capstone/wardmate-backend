using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AIOCR.Application.Extraction;

namespace WardMate.Services.AIOCR.API.Controllers;

[ApiController, Route("internal/v1/procedure-extractions")]
public sealed class ProcedureExtractionsController(IConfiguration configuration, IProcedureDocumentExtractor extractor) : ControllerBase
{
    [HttpPost, RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Extract(CancellationToken ct)
    {
        var key = configuration["ServiceAuthentication:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            return Failure(503, "aiocr.not_configured", "Chưa cấu hình dịch vụ AI/OCR.");
        var supplied = Request.Headers["X-Service-Key"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Failure(401, "auth.unauthorized", "Không có quyền gọi dịch vụ nội bộ.");
        if (!configuration.GetValue<bool>("Extraction:Enabled"))
            return Failure(503, "aiocr.not_configured", "Chưa bật dịch vụ AI/OCR.");
        using var pdf = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (pdf.Length + count > 20 * 1024 * 1024) return Failure(413, "aiocr.file_too_large", "PDF tối đa 20 MB.");
            await pdf.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (pdf.Length < 5 || !pdf.GetBuffer().AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return Failure(400, "aiocr.invalid_pdf", "File không có định dạng PDF.");
        pdf.Position = 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(8));
        try { return Ok(await extractor.Extract(pdf, deadline.Token)); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failure(504, "aiocr.timeout", "Bóc tách quá thời gian cho phép."); }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
        { return Failure(502, "aiocr.extraction_failed", "Không thể bóc tách tài liệu. Kiểm tra cấu hình hoặc đối soát thủ công."); }
    }
    private ObjectResult Failure(int status, string code, string title) => new(new ProblemDetails
    { Status = status, Title = title, Instance = Request.Path, Extensions = { ["code"] = code, ["traceId"] = HttpContext.TraceIdentifier } })
    { StatusCode = status, ContentTypes = { "application/problem+json" } };
}
