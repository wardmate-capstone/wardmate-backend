using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

/// <summary>
/// Kết quả tra cứu hồ sơ qua mã QR — hiển thị công khai khi quét mã.
/// </summary>
public sealed record QrLookupResultDto(
    Guid ApplicationId,
    Guid SubmissionId,
    string QrCodeValue,
    DateTime GeneratedAt,
    string Status,
    string? DocumentName,
    bool IsValid,
    string Message);

/// <summary>
/// Query tra cứu hồ sơ bằng giá trị mã QR (token hoặc URL quét từ camera).
/// Endpoint này KHÔNG yêu cầu xác thực.
/// </summary>
public sealed record QrLookupQuery(string Token) : IQuery<QrLookupResultDto>;

public sealed class QrLookupQueryHandler(IDocumentDbContext dbContext)
    : IQueryHandler<QrLookupQuery, QrLookupResultDto>
{
    public async Task<Result<QrLookupResultDto>> Handle(QrLookupQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Error.Validation("qr.token_required", "Token tra cứu không được để trống.");

        var raw = request.Token.Trim();

        // 1. Thử parse GUID trực tiếp
        Guid? targetGuid = Guid.TryParse(raw, out var parsed) ? parsed : null;

        // 2. Nếu là URL, trích xuất đoạn cuối cùng (thường là ApplicationId GUID)
        if (targetGuid is null && (raw.Contains('/') || raw.Contains('\\')))
        {
            var segments = raw.Split(new[] { '/', '\\', '?', '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments.Reverse())
            {
                if (Guid.TryParse(seg, out var g))
                {
                    targetGuid = g;
                    break;
                }
            }
        }

        // 3. Tìm bản ghi ApprovedQr
        ApprovedQr? qr = null;

        if (targetGuid.HasValue)
        {
            qr = await dbContext.ApprovedQrs.AsNoTracking().FirstOrDefaultAsync(q =>
                q.ApplicationId == targetGuid.Value ||
                q.SubmissionId == targetGuid.Value ||
                q.QrCodeValue == raw, cancellationToken);
        }

        qr ??= await dbContext.ApprovedQrs.AsNoTracking().FirstOrDefaultAsync(q =>
            q.QrCodeValue == raw ||
            q.QrCodeValue.Contains(raw), cancellationToken);

        if (qr is null)
            return Error.NotFound("qr.not_found", "Không tìm thấy hồ sơ tương ứng với mã QR này. Vui lòng kiểm tra lại.");

        // 4. Lấy thêm thông tin biểu mẫu / tên hồ sơ từ UserSubmissions và FormTemplates
        string? docName = null;
        string status = "APPROVED";

        var submission = await dbContext.UserSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == qr.SubmissionId, cancellationToken);

        if (submission is not null)
        {
            status = submission.Status.ToString().ToUpperInvariant();
            var template = await dbContext.FormTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == submission.TemplateId, cancellationToken);
            docName = template?.Title ?? submission.FileName;
        }

        return new QrLookupResultDto(
            qr.ApplicationId,
            qr.SubmissionId,
            qr.QrCodeValue,
            qr.GeneratedAt,
            status,
            docName,
            IsValid: true,
            Message: "Hồ sơ điện tử hợp lệ, đã được cơ quan có thẩm quyền phê duyệt và đóng dấu xác thực.");
    }
}
