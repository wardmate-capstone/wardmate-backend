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

    /// <summary>Trạng thái lịch sử của luồng cũ; không được sử dụng cho xác nhận mới.</summary>
    Approved = 3, // Historical only; no new transitions to this value.

    /// <summary>Giấy tờ đã được kiểm tra, sẵn sàng tải và mang đến phường.</summary>
    ReadyForDownload = 4
}
