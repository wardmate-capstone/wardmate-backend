using Microsoft.Extensions.Configuration;
using QRCoder;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.QrCode;

/// <summary>
/// Sinh mã QR PNG và đóng gói thành entity <see cref="ApprovedQr"/> sẵn sàng để lưu DB.
/// URL được mã hoá trong QR trỏ đến endpoint pdf-redirect — khi quét sẽ redirect thẳng
/// đến file PDF mới nhất của hồ sơ (SAS URL ngắn hạn, không cần đăng nhập).
/// </summary>
public sealed class ApprovedQrService : IApprovedQrService
{
    private readonly string _verificationBaseUrl;

    public ApprovedQrService(IConfiguration configuration)
    {
        // Fallback về endpoint pdf-redirect nếu chưa cấu hình
        _verificationBaseUrl = (configuration["QrCode:VerificationBaseUrl"]
            ?? "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/public/applications/")
            .TrimEnd('/');
    }

    public ApprovedQr Generate(Guid applicationId, Guid submissionId)
    {
        // URL trong QR: /api/v1/public/applications/{applicationId}/pdf-redirect
        // Khi quét → backend tìm PDF mới nhất → redirect 302 → Azure Blob SAS URL
        var redirectUrl = _verificationBaseUrl.EndsWith("/applications", StringComparison.OrdinalIgnoreCase)
            || _verificationBaseUrl.EndsWith("/applications/", StringComparison.OrdinalIgnoreCase)
            ? $"{_verificationBaseUrl.TrimEnd('/')}/{applicationId}/pdf-redirect"
            : $"{_verificationBaseUrl}/{applicationId}/pdf-redirect";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(redirectUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        // 10 px mỗi ô → QR ~330×330 px cho ECCLevel.Q trung bình
        var png = qrCode.GetGraphic(pixelsPerModule: 10);

        return new ApprovedQr(applicationId, submissionId, redirectUrl, png);
    }
}
