namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed class StaffingOptions
{
    public const string SectionName = "Staffing";

    /// <summary>
    /// Chế độ thử nghiệm: bỏ qua điều kiện kiểm tra ca làm hiện tại (NO_ACTIVE_SHIFT).
    /// Mặc định: false. Chỉ bật qua biến môi trường Staffing__SkipShiftCheck=true trên môi trường test.
    /// Vẫn giữ nguyên kiểm tra nghỉ phép đã duyệt (APPROVED_LEAVE).
    /// </summary>
    public bool SkipShiftCheck { get; set; } = false;
}
