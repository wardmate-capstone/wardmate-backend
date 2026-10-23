using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

/// <summary>
/// Kết quả redirect: SAS URL ngắn hạn trỏ thẳng đến file PDF mới nhất hoặc bản gộp của hồ sơ.
/// </summary>
public sealed record QrPdfRedirectResult(string SasUrl, string FileName);

/// <summary>
/// Query tra cứu PDF của hồ sơ qua mã QR — không yêu cầu xác thực.
/// Nếu hồ sơ có từ 2 đơn trở lên, tự động gộp tất cả các đơn thành một file PDF duy nhất.
/// Trả về SAS URL 15 phút để controller redirect 302 đến file PDF trên Azure Blob.
/// </summary>
public sealed record GetQrPdfRedirectQuery(Guid ApplicationId) : IQuery<QrPdfRedirectResult>;

public sealed class GetQrPdfRedirectQueryHandler(
    IDocumentDbContext dbContext,
    IBlobStorageClient blobClient,
    IPdfRenderEngine pdfRenderEngine,
    ILogger<GetQrPdfRedirectQueryHandler> logger)
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

        // 2. Lấy danh sách toàn bộ các file PDF đã kết xuất của hồ sơ
        var allDocs = await dbContext.GeneratedDocuments
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted)
            .ToListAsync(cancellationToken);

        var individualDocs = allDocs
            .Where(d => d.DocumentType != "COMBINED_APPLICATION")
            .OrderBy(d => d.CreatedAtUtc)
            .ToList();

        if (individualDocs.Count == 0)
            return Error.NotFound("qr.pdf_not_found",
                "Hồ sơ chưa có file PDF được xuất. Vui lòng liên hệ cơ quan tiếp nhận.");

        GeneratedDocument targetDoc;

        // Nếu hồ sơ chỉ có đúng 1 đơn -> mở thẳng file đó
        if (individualDocs.Count == 1)
        {
            targetDoc = individualDocs[0];
        }
        else
        {
            // Hồ sơ có từ 2 đơn trở lên -> kiểm tra đã có bản gộp COMBINED_APPLICATION mới nhất chưa
            var latestIndividualDate = individualDocs.Max(d => d.CreatedAtUtc);
            var existingCombined = allDocs
                .Where(d => d.DocumentType == "COMBINED_APPLICATION")
                .OrderByDescending(d => d.CreatedAtUtc)
                .FirstOrDefault();

            if (existingCombined is not null && existingCombined.CreatedAtUtc >= latestIndividualDate)
            {
                targetDoc = existingCombined;
            }
            else
            {
                // Cần gộp tất cả các đơn đơn lẻ lại thành 1 file PDF trọn bộ
                targetDoc = await MergeIndividualPdfsAsync(request.ApplicationId, individualDocs, cancellationToken)
                    ?? individualDocs[^1];
            }
        }

        // 3. Trích tên blob từ URL lưu trong DB
        var blobName = ExtractBlobName(targetDoc.PdfBlobUrl);

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
            throw new InvalidOperationException(
                "Cấu hình Azure Blob Storage không hỗ trợ SAS. " +
                "Hãy dùng StorageSharedKeyCredential thay vì DefaultAzureCredential.", ex);
        }

        var fileName = $"ho-so-{request.ApplicationId:N}.pdf";
        return new QrPdfRedirectResult(sasUrl, fileName);
    }

    private async Task<GeneratedDocument?> MergeIndividualPdfsAsync(
        Guid applicationId,
        IReadOnlyList<GeneratedDocument> individualDocs,
        CancellationToken cancellationToken)
    {
        var downloadedStreams = new List<(Stream Stream, string FileName)>();
        try
        {
            for (int i = 0; i < individualDocs.Count; i++)
            {
                var doc = individualDocs[i];
                var blobName = ExtractBlobName(doc.PdfBlobUrl);
                var stream = await blobClient.DownloadAsync(blobName, ct: cancellationToken);
                if (stream is null)
                {
                    logger.LogWarning("Không thể tải blob {BlobName} của document {DocId} để gộp.", blobName, doc.Id);
                    return null;
                }
                downloadedStreams.Add((stream, $"doc_{i + 1:D2}.pdf"));
            }

            using var mergedPdf = await pdfRenderEngine.MergePdfsAsync(downloadedStreams, cancellationToken);
            var combinedBlobPath = $"generated-documents/{applicationId}/combined_application_{Guid.NewGuid():N}.pdf";

            var mergedBlobUrl = await blobClient.UploadAsync(
                mergedPdf,
                combinedBlobPath,
                "application/pdf",
                ct: cancellationToken);

            var combinedDoc = new GeneratedDocument(
                applicationId,
                "COMBINED_APPLICATION",
                mergedBlobUrl,
                createdBy: "SYSTEM_MERGE",
                submissionId: null);

            dbContext.GeneratedDocuments.Add(combinedDoc);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Đã gộp thành công {Count} đơn của ApplicationId {ApplicationId} thành bản PDF trọn bộ.",
                individualDocs.Count, applicationId);

            return combinedDoc;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi gộp các file PDF cho ApplicationId: {ApplicationId}", applicationId);
            return null;
        }
        finally
        {
            foreach (var (stream, _) in downloadedStreams)
            {
                await stream.DisposeAsync();
            }
        }
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
