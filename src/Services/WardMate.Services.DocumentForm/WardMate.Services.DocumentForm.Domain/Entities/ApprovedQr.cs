namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Mã QR tham chiếu giấy tờ chuẩn bị, sinh khi kết xuất PDF sau xác nhận sẵn sàng; tên entity được giữ để tương thích.
/// Mỗi applicationId chỉ có một bản ghi QR duy nhất (unique index).
/// Bảng: document.approved_qrs
/// </summary>
public sealed class ApprovedQr
{
    // EF Core private constructor
    private ApprovedQr() { }

    public ApprovedQr(Guid applicationId, Guid submissionId, string qrCodeValue, byte[] qrImagePng)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId không được để trống.", nameof(applicationId));
        if (submissionId == Guid.Empty)
            throw new ArgumentException("SubmissionId không được để trống.", nameof(submissionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCodeValue);
        if (qrImagePng is null || qrImagePng.Length == 0)
            throw new ArgumentException("Dữ liệu hình ảnh QR không được rỗng.", nameof(qrImagePng));

        ApplicationId = applicationId;
        SubmissionId = submissionId;
        QrCodeValue = qrCodeValue;
        QrImagePng = qrImagePng;
        GeneratedAt = DateTime.UtcNow;
    }

    /// <summary>Khoá chính.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>ID hồ sơ hành chính (ApplicationWorkflow).</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>ID tờ khai nộp (DocumentForm UserSubmission).</summary>
    public Guid SubmissionId { get; private set; }

    /// <summary>URL tra cứu được mã hoá trong QR (VD: https://gateway/.../applications/{id}).</summary>
    public string QrCodeValue { get; private set; } = string.Empty;

    /// <summary>Hình ảnh QR dạng PNG byte[], lưu cột bytea trong PostgreSQL.</summary>
    public byte[] QrImagePng { get; private set; } = [];

    /// <summary>Thời điểm sinh mã QR (UTC).</summary>
    public DateTime GeneratedAt { get; private set; }

    /// <summary>Cập nhật mã QR khi URL tra cứu hoặc nội dung thay đổi.</summary>
    public void Update(string qrCodeValue, byte[] qrImagePng)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCodeValue);
        if (qrImagePng is null || qrImagePng.Length == 0)
            throw new ArgumentException("Dữ liệu hình ảnh QR không được rỗng.", nameof(qrImagePng));

        QrCodeValue = qrCodeValue;
        QrImagePng = qrImagePng;
        GeneratedAt = DateTime.UtcNow;
    }
}

