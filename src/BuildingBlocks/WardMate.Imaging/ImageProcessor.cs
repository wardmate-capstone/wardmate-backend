using ImageMagick;
using Microsoft.Extensions.DependencyInjection;

namespace WardMate.Imaging;

/// <summary>Định dạng ảnh nhận diện bằng chữ ký đầu tệp, không tin phần mở rộng hay Content-Type của client.</summary>
public enum ImageKind { Unknown, Jpeg, Png, WebP, Heif }

public sealed record ProcessedImage(byte[] Content, int Width, int Height, string ContentType = "image/jpeg");

/// <summary>Ảnh hỏng, quá lớn hoặc không giải mã được. Message là tiếng Việt, trả thẳng cho người dùng.</summary>
public sealed class ImageProcessingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IImageProcessor
{
    /// <summary>
    /// Giải mã (JPEG/PNG/WebP/HEIC/HEIF), xoay theo EXIF, xóa toàn bộ metadata (GPS, máy chụp), thu nhỏ để cạnh dài nhất
    /// không quá <paramref name="maxEdge"/> (không phóng to), nền trong suốt thành trắng, xuất JPEG.
    /// <paramref name="square"/> cắt giữa thành hình vuông cạnh <paramref name="maxEdge"/> (ảnh đại diện).
    /// </summary>
    ProcessedImage ToJpeg(byte[] input, int maxEdge, int quality = 85, bool square = false);
}

public static class ImageSignature
{
    public static ImageKind Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return ImageKind.Jpeg;
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ImageKind.Png;
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return ImageKind.WebP;
        // ISO-BMFF: "....ftyp<brand>"; iPhone dùng heic/heix/mif1/msf1, đôi khi hevc/hevx.
        if (head.Length >= 12 && head[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(head[8..12]);
            if (brand is "heic" or "heix" or "heim" or "heis" or "hevc" or "hevx" or "mif1" or "msf1") return ImageKind.Heif;
        }
        return ImageKind.Unknown;
    }
}

public sealed class MagickImageProcessor : IImageProcessor
{
    // Chặn ảnh "bom giải nén": 1 tệp nhỏ nhưng khai báo kích thước khổng lồ.
    private const uint MaxDimension = 12_000;
    private const ulong MaxPixels = 80_000_000;

    static MagickImageProcessor()
    {
        ResourceLimits.Width = MaxDimension;
        ResourceLimits.Height = MaxDimension;
        ResourceLimits.Area = MaxPixels;
    }

    public ProcessedImage ToJpeg(byte[] input, int maxEdge, int quality = 85, bool square = false)
    {
        if (maxEdge is < 16 or > 8000) throw new ArgumentOutOfRangeException(nameof(maxEdge));
        var kind = ImageSignature.Detect(input);
        if (kind == ImageKind.Unknown)
            throw new ImageProcessingException("image.unsupported", "Chỉ nhận ảnh JPEG, PNG, WebP hoặc HEIC/HEIF.");
        try
        {
            var settings = new MagickReadSettings
            {
                Format = kind switch { ImageKind.Jpeg => MagickFormat.Jpeg, ImageKind.Png => MagickFormat.Png, ImageKind.WebP => MagickFormat.WebP, _ => MagickFormat.Heic }
            };
            using var image = new MagickImage(input, settings);
            image.AutoOrient();
            if (image.HasAlpha)
            {
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
            }
            if (square)
            {
                var side = Math.Min(image.Width, image.Height);
                image.Crop(new MagickGeometry(side, side) { }, Gravity.Center);
                image.ResetPage();
            }
            var edge = (uint)maxEdge;
            if (image.Width > edge || image.Height > edge)
                image.Resize(new MagickGeometry(edge, edge) { IgnoreAspectRatio = false });
            image.Strip();
            image.ColorSpace = ColorSpace.sRGB;
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)Math.Clamp(quality, 40, 95);
            return new ProcessedImage(image.ToByteArray(), (int)image.Width, (int)image.Height);
        }
        catch (MagickException)
        {
            throw new ImageProcessingException("image.unreadable", "Không đọc được ảnh (tệp hỏng hoặc kích thước quá lớn).");
        }
    }
}

public static class ImagingServiceExtensions
{
    public static IServiceCollection AddWardMateImaging(this IServiceCollection services) =>
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
}
