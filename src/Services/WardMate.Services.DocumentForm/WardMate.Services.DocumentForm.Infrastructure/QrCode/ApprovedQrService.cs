using Microsoft.Extensions.Configuration;
using QRCoder;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.QrCode;

/// <summary>
/// Sinh mã QR PNG và đóng gói thành entity <see cref="ApprovedQr"/> sẵn sàng để lưu DB.
/// </summary>
public sealed class ApprovedQrService : IApprovedQrService
{
    private readonly string _verificationBaseUrl;

    public ApprovedQrService(IConfiguration configuration)
    {
        // Fallback về endpoint tra cứu công khai nếu chưa cấu hình
        _verificationBaseUrl = (configuration["QrCode:VerificationBaseUrl"]
            ?? "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/public/qr-lookup?applicationId=")
            .TrimEnd('/');
    }

    public ApprovedQr Generate(Guid applicationId, Guid submissionId)
    {
        var verificationUrl = _verificationBaseUrl.Contains('?')
            ? $"{_verificationBaseUrl}{applicationId}"
            : $"{_verificationBaseUrl}/{applicationId}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        // 10 px mỗi ô → QR ~330×330 px cho ECCLevel.Q trung bình
        var png = qrCode.GetGraphic(pixelsPerModule: 10);

        return new ApprovedQr(applicationId, submissionId, verificationUrl, png);
    }
}
