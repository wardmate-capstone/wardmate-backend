using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.Services.DocumentForm.Application.Services;

namespace WardMate.UnitTests.DocumentForm;

// DOC-QR-07: quét QR của hồ sơ không có PDF trả trang HTML tiếng Việt thay vì JSON 404.
public sealed class QrVerificationPageTests
{
    [Fact]
    public void Valid_page_shows_reference_time_in_vietnam_and_missing_pdf_note()
    {
        var id = Guid.NewGuid();
        var html = QrVerificationPage.Valid(new QrLookupResultDto(id, null, "https://x/" + id, new DateTime(2026, 11, 18, 15, 5, 0, DateTimeKind.Utc),
            "DOCUMENTS_READY", null, true, QrLookupQueryHandler.QrValidMessage), pdfMissing: true);

        Assert.StartsWith("<!doctype html>", html.TrimStart());
        Assert.Contains("Mã QR hợp lệ", html);
        Assert.Contains(id.ToString(), html);
        Assert.Contains("18/11/2026 22:05", html);
        Assert.Contains("không kèm bản PDF", html);
        Assert.Contains("không phải văn bản phê duyệt", html);
    }

    [Fact]
    public void Not_found_page_has_no_reference_rows()
    {
        var html = QrVerificationPage.NotFound();
        Assert.Contains("Không tìm thấy hồ sơ", html);
        Assert.DoesNotContain("<tr>", html);
    }
}
