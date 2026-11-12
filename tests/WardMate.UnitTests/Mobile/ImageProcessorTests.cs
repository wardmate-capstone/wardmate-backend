using ImageMagick;
using WardMate.Imaging;
using WardMate.Services.DocumentForm.Application.Commands;

namespace WardMate.UnitTests.Mobile;

// PROFILE-AVATAR-01 / DOC-HEIC-01: chuẩn hóa ảnh (định dạng, kích thước, xóa metadata) dùng chung cho ảnh đại diện và giấy tờ.
public sealed class ImageProcessorTests
{
    private static readonly MagickImageProcessor Processor = new();

    private static byte[] Image(MagickFormat format, uint width, uint height, bool alpha = false)
    {
        using var image = new MagickImage(alpha ? MagickColors.Transparent : MagickColors.SkyBlue, width, height);
        image.Comment = "GPS 21.0285,105.8542"; // metadata phải bị xóa
        image.Format = format;
        return image.ToByteArray();
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ImageKind.Jpeg)]
    [InlineData(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, ImageKind.Png)]
    [InlineData(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c' }, ImageKind.Heif)]
    [InlineData(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'i', (byte)'f', (byte)'1' }, ImageKind.Heif)]
    [InlineData(new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-' }, ImageKind.Unknown)]
    public void Signature_detects_format_from_bytes(byte[] head, ImageKind expected) =>
        Assert.Equal(expected, ImageSignature.Detect(head));

    [Fact]
    public void Avatar_is_square_512_jpeg_without_metadata()
    {
        var result = Processor.ToJpeg(Image(MagickFormat.Png, 1200, 800, alpha: true), 512, square: true);
        Assert.Equal((512, 512), (result.Width, result.Height));
        Assert.Equal(ImageKind.Jpeg, ImageSignature.Detect(result.Content));
        using var output = new MagickImage(result.Content);
        Assert.True(string.IsNullOrEmpty(output.Comment));
        Assert.Null(output.GetExifProfile());
    }

    [Fact]
    public void Small_image_is_not_upscaled()
    {
        var result = Processor.ToJpeg(Image(MagickFormat.Jpeg, 300, 200), 3000);
        Assert.Equal((300, 200), (result.Width, result.Height));
    }

    [Fact]
    public void Unknown_or_broken_bytes_are_rejected_with_vietnamese_message()
    {
        var unknown = Assert.Throws<ImageProcessingException>(() => Processor.ToJpeg("hello world, not an image"u8.ToArray(), 512));
        Assert.Equal("image.unsupported", unknown.Code);
        var broken = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        Assert.Equal("image.unreadable", Assert.Throws<ImageProcessingException>(() => Processor.ToJpeg(broken, 512)).Code);
    }

    [Fact]
    public async Task Supporting_document_webp_is_converted_to_jpg()
    {
        using var input = new MemoryStream(Image(MagickFormat.WebP, 4000, 1000));
        var result = await UploadSupportingDocumentCommandHandler.NormalizeImage(Processor, input, "giay-to.webp", "image/webp", input.Length, default);
        Assert.Null(result.Error);
        Assert.Equal("giay-to.jpg", result.FileName);
        Assert.Equal("image/jpeg", result.ContentType);
        using var output = new MagickImage(result.Stream);
        Assert.Equal(3000u, output.Width);
    }

    [Fact]
    public async Task Supporting_document_pdf_is_passed_through_and_bad_heic_is_rejected()
    {
        using var pdf = new MemoryStream("%PDF-1.7"u8.ToArray());
        var passed = await UploadSupportingDocumentCommandHandler.NormalizeImage(Processor, pdf, "a.pdf", "application/pdf", pdf.Length, default);
        Assert.Same(pdf, passed.Stream);
        Assert.Null(passed.Error);

        using var heic = new MemoryStream("not really heic"u8.ToArray());
        var rejected = await UploadSupportingDocumentCommandHandler.NormalizeImage(Processor, heic, "anh.heic", "image/heic", heic.Length, default);
        Assert.Equal("document.unsupported_file_type", rejected.Error!.Code);
    }
}
