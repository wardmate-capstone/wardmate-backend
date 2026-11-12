namespace WardMate.Services.IAM.Domain.Entities;

/// <summary>PROFILE-AVATAR-01: ảnh đại diện đã chuẩn hóa (JPEG vuông ≤ 512px, không metadata), lưu riêng khỏi bảng users.</summary>
public sealed class UserAvatar
{
    public Guid UserId { get; set; }
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "image/jpeg";
    public string Sha256 { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
