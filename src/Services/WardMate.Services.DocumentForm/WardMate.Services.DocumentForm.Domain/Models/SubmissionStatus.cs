namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Trạng thái vòng đời của hồ sơ người dùng (UserSubmission).
/// </summary>
public enum SubmissionStatus
{
    /// <summary>Đang lưu nháp — người dùng chưa nộp chính thức.</summary>
    Draft = 0,

    /// <summary>Đã nộp — đang chờ cán bộ xem xét.</summary>
    Submitted = 1,

    /// <summary>Cán bộ yêu cầu sửa lại — hồ sơ bị trả về kèm comment.</summary>
    RevisionRequested = 2,

    /// <summary>Đã được cán bộ duyệt — hồ sơ hợp lệ, có thể in và nộp ra phường.</summary>
    Approved = 3
}
