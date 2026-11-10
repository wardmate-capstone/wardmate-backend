namespace WardMate.Services.ProcedureCatalog.Application.Management;

/// <summary>
/// BE-H (A.2, A.3): giới hạn độ dài thủ tục. Văn bản mô tả không giới hạn theo nghiệp vụ, chỉ có trần
/// chống dán nhầm/payload quá lớn. Đổi số ở đây phải đổi cột DB (ProcedureConfiguration) và báo FE.
/// </summary>
public static class ProcedureLimits
{
    public const int Title = 1_000;
    public const int ProcedureCode = 100;
    public const int Level = 255;
    public const int DecisionNumber = 255;
    /// <summary>Trường văn bản một dòng: tên giấy tờ/biểu mẫu, đối tượng, cơ quan, tóm tắt, phần tử danh sách.</summary>
    public const int Line = 10_000;
    /// <summary>Tổng nội dung (contentPayload + checklistSchema + formDefinitions) khi serialize JSON.</summary>
    public const int Payload = 500_000;
    public const int SectionTitle = 255;
    public const int Sections = 50;
    public const int ListItems = 100;
    public const int FormCodesPerItem = 20;
    public const int FeesPerMethod = 100;
}
