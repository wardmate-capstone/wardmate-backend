namespace WardMate.Services.DocumentForm.Application.Services;

/// <summary>
/// Quyền truy cập tài liệu hồ sơ dựa trên dữ liệu Workflow trả về.
/// Workflow chỉ trả hồ sơ cùng phường cho cán bộ nên phường không cần kiểm lại ở đây.
/// </summary>
public static class ApplicationReadRules
{
    /// <summary>
    /// Chủ hồ sơ; cán bộ thụ lý (không phải người nộp); hoặc — chỉ đọc (DOC-READ-WARD-01) — cán bộ cùng phường
    /// với hồ sơ đã gửi (không phải bản nháp, không phải hồ sơ của chính mình).
    /// </summary>
    public static bool Allowed(Guid ownerId, Guid? assignedOfficerId, string status, Guid userId, bool officer, bool wardRead)
    {
        if (!officer) return ownerId == userId;
        if (ownerId == userId) return false;
        if (assignedOfficerId == userId) return true;
        return wardRead && status != "DRAFT";
    }
}
