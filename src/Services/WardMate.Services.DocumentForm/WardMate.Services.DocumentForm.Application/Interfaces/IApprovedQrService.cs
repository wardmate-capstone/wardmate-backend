using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Sinh và trả về entity <see cref="ApprovedQr"/> chứa URL tra cứu và hình PNG.
/// </summary>
public interface IApprovedQrService
{
    /// <summary>
    /// Tạo mới ApprovedQr với URL tra cứu được mã hoá thành ảnh PNG.
    /// </summary>
    ApprovedQr Generate(Guid applicationId, Guid submissionId);
}
