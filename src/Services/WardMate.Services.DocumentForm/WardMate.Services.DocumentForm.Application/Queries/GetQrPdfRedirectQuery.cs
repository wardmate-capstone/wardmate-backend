using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

/// <summary>
/// Kết quả redirect: SAS URL ngắn hạn trỏ thẳng đến file PDF mới nhất của hồ sơ.
/// </summary>
public sealed record QrPdfRedirectResult(string SasUrl, string FileName);

/// <summary>
/// Query tra cứu PDF mới nhất của hồ sơ qua mã QR — không yêu cầu xác thực.
/// Trả về SAS URL 15 phút để controller redirect 302 đến file PDF trên Azure Blob.
/// </summary>
public sealed record GetQrPdfRedirectQuery(Guid ApplicationId) : IQuery<QrPdfRedirectResult>;

public sealed class GetQrPdfRedirectQueryHandler(
    IDocumentDbContext dbContext,
    IBlobStorageClient blobClient)
    : IQueryHandler<GetQrPdfRedirectQuery, QrPdfRedirectResult>
{
    // SAS URL hiệu lực 15 phút — đủ để mở trình duyệt/viewer, không quá dài.
    private static readonly TimeSpan SasValidity = TimeSpan.FromMinutes(15);

    public async Task<Result<QrPdfRedirectResult>> Handle(
        GetQrPdfRedirectQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ApplicationId == Guid.Empty)
            return Error.Validation("qr.invalid_application_id", "ApplicationId không hợp lệ.");

        // 1. Xác nhận QR bản ghi tồn tại (chỉ hồ sơ đã được generate PDF mới có QR)
        var qrExists = await dbContext.ApprovedQrs.AsNoTracking()
            .AnyAsync(q => q.ApplicationId == request.ApplicationId, cancellationToken);

        if (!qrExists)
            return Error.NotFound("qr.not_found",
                "Không tìm thấy mã QR cho hồ sơ này. Hồ sơ chưa được xuất giấy tờ.");

        // 2. Lấy file PDF mới nhất (sắp xếp CreatedAtUtc giảm dần)
        var latestDoc = await dbContext.GeneratedDocuments.AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestDoc is null)
            return Error.NotFound("qr.pdf_not_found",
                "Hồ sơ chưa có file PDF được xuất. Vui lòng liên hệ cơ quan tiếp nhận.");

        // 3. Trích tên blob từ URL lưu trong DB
        var blobName = ExtractBlobName(latestDoc.PdfBlobUrl);

        // 4. Kiểm tra blob còn tồn tại trước khi tạo SAS
        if (!await blobClient.ExistsAsync(blobName, ct: cancellationToken))
            return Error.NotFound("qr.pdf_blob_missing",
                "File PDF không còn trên hệ thống lưu trữ. Vui lòng liên hệ cơ quan tiếp nhận.");

        // 5. Sinh SAS URL có hạn 15 phút
        string sasUrl;
        try
        {
            sasUrl = await blobClient.GenerateSasUrlAsync(blobName, SasValidity, ct: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // BlobServiceClient không có StorageSharedKeyCredential → fallback download qua backend
            throw new InvalidOperationException(
                "Cấu hình Azure Blob Storage không hỗ trợ SAS. " +
                "Hãy dùng StorageSharedKeyCredential thay vì DefaultAzureCredential.", ex);
        }

        var fileName = $"ho-so-{request.ApplicationId:N}.pdf";
        return new QrPdfRedirectResult(sasUrl, fileName);
    }

    private static string ExtractBlobName(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var path = uri.AbsolutePath.TrimStart('/');
        if (path.StartsWith("devstoreaccount1/", StringComparison.OrdinalIgnoreCase))
            path = path["devstoreaccount1/".Length..];
        var firstSlash = path.IndexOf('/');
        return Uri.UnescapeDataString(firstSlash >= 0 ? path[(firstSlash + 1)..] : path);
    }
}
