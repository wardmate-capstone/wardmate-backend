namespace WardMate.UnitTests.Support;

/// <summary>Đồng hồ giả để kiểm tra logic theo thời gian (cửa sổ khóa đăng nhập...).</summary>
public sealed class MutableTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}
