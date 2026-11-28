using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.UnitTests.DocumentForm;

// DOC-QR-08 (L-33): trang "Mã QR hợp lệ" hiện mã hồ sơ HS-… để cán bộ đối chiếu.
public sealed class QrApplicationCodeTests
{
    private static ApprovedQr Qr() => new(Guid.NewGuid(), null, "https://x/qr", [1, 2, 3]);

    private static string Page(string? code) => QrVerificationPage.Valid(new QrLookupResultDto(Guid.NewGuid(), null, "https://x",
        new DateTime(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc), "DOCUMENTS_READY", null, true, QrLookupQueryHandler.QrValidMessage, code), pdfMissing: true);

    [Fact]
    public void Page_shows_application_code_when_known()
    {
        var html = Page("HS-20261118-00000062");
        Assert.Contains("Mã hồ sơ", html);
        Assert.Contains("HS-20261118-00000062", html);
    }

    [Fact]
    public void Page_says_code_is_missing_for_old_qr()
    {
        Assert.Contains("Chưa có (tra theo mã tham chiếu bên dưới)", Page(null));
    }

    [Fact]
    public void Code_is_set_once_trimmed_and_never_overwritten()
    {
        var qr = Qr();
        Assert.False(qr.SetApplicationCode("  "));
        Assert.Null(qr.ApplicationCode);
        Assert.True(qr.SetApplicationCode(" HS-1 "));
        Assert.Equal("HS-1", qr.ApplicationCode);
        Assert.False(qr.SetApplicationCode("HS-2"));
        Assert.Equal("HS-1", qr.ApplicationCode);
    }

    [Fact]
    public void Code_is_capped_to_column_length()
    {
        var qr = Qr();
        qr.SetApplicationCode(new string('A', 100));
        Assert.Equal(64, qr.ApplicationCode!.Length);
    }
}
