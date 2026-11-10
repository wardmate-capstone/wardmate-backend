namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>BE-H (Phần C): mục mở rộng tự do cho nội dung chưa có trường riêng ("Lưu ý", "Thông tin liên hệ"...).</summary>
public sealed class AdditionalSection
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
}
