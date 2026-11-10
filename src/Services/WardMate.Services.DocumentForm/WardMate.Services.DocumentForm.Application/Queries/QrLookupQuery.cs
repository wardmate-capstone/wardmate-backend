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
    Guid? SubmissionId,
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
        if (raw.Length > MaxTokenLength)
            return Error.Validation("qr.token_too_long", "Mã QR không hợp lệ.");

        // 1. Thử parse GUID trực tiếp
        Guid? targetGuid = Guid.TryParse(raw, out var parsed) ? parsed : null;

        // 2. Nếu là URL (QR mới .../applications/{id}/pdf-redirect hoặc QR cũ ...?applicationId={id}),
        //    lấy GUID cuối cùng trong chuỗi.
        if (targetGuid is null)
        {
            var segments = raw.Split(new[] { '/', '\\', '?', '&', '=', '#' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments.Reverse())
            {
                if (Guid.TryParse(seg, out var g))
                {
                    targetGuid = g;
                    break;
                }
            }
        }

        // 3. Tìm bản ghi ApprovedQr — CHỈ khớp chính xác (DOC-QR-06a).
        //    Không tìm theo chuỗi con: trước 10/11/2026 tìm kiểu Contains cho phép gõ "https" để lộ mã hồ sơ bất kỳ.
        ApprovedQr? qr = null;

        if (targetGuid.HasValue && targetGuid.Value != Guid.Empty)
        {
            qr = await dbContext.ApprovedQrs.AsNoTracking().FirstOrDefaultAsync(q =>
                q.ApplicationId == targetGuid.Value ||
                (q.SubmissionId != null && q.SubmissionId == targetGuid.Value), cancellationToken);
        }

        qr ??= await dbContext.ApprovedQrs.AsNoTracking().FirstOrDefaultAsync(q => q.QrCodeValue == raw, cancellationToken);

        if (qr is null)
            return Error.NotFound("qr.not_found", "Không tìm thấy hồ sơ tương ứng với mã QR này. Vui lòng kiểm tra lại.");

        // 4. Lấy thêm thông tin biểu mẫu / tên hồ sơ từ UserSubmissions và FormTemplates
        string? docName = null;
        // QR chỉ được cấp khi hồ sơ DOCUMENTS_READY/OFFICIALLY_RECEIVED; đây không phải phê duyệt hành chính.
        string status = "DOCUMENTS_READY";

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
            Message: QrValidMessage);
    }

    private const int MaxTokenLength = 2048;

    // DOC-QR-06b: QR xác nhận giấy tờ do WardMate cấp và đã được cán bộ kiểm tra đủ,
    // KHÔNG phải văn bản phê duyệt hay kết quả giải quyết thủ tục.
    public const string QrValidMessage =
        "Mã QR hợp lệ: giấy tờ của hồ sơ này do WardMate cấp và đã được cán bộ kiểm tra đầy đủ. " +
        "Đây không phải văn bản phê duyệt hay kết quả giải quyết thủ tục hành chính.";
}
