namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Chèn hình ảnh QR code vào cuối tài liệu DOCX trước khi render PDF.
/// </summary>
public interface IDocxQrStamper
{
    /// <summary>
    /// Đọc <paramref name="docxStream"/>, chèn QR vào cuối trang, trả về MemoryStream mới.
    /// Stream gốc không bị dispose.
    /// </summary>
    Stream StampQr(Stream docxStream, byte[] qrImagePng);
}
