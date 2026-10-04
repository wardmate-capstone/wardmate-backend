using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Drafts;

public sealed class AiOcrClient(HttpClient http, IConfiguration configuration) : IProcedureExtractor
{
    public async Task<ExtractionResult> Extract(Stream pdf, CancellationToken ct)
    {
        var endpoint = configuration["ProcedureDrafts:AiOcrUrl"] ?? throw new InvalidOperationException("Chưa cấu hình AIOCR.");
        var key = configuration["ProcedureDrafts:ServiceKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Chưa cấu hình khóa AIOCR.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TrimEnd('/') + "/internal/v1/procedure-extractions");
        request.Headers.Add("X-Service-Key", key);
        request.Content = new StreamContent(pdf);
        request.Content.Headers.ContentType = new("application/pdf");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExtractionResult>(cancellationToken: ct)
            ?? throw new InvalidDataException("AIOCR không trả kết quả.");
    }
}

public sealed class DraftExtractionWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<DraftExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("ProcedureDrafts:ExtractionEnabled")) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await ProcessNext(scope.ServiceProvider, stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Không thể xử lý hàng đợi PDF: {ExceptionType}", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public static async Task<bool> ProcessNext(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<ProcedureDbContext>();
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = (await db.ProcedureDrafts.FromSqlInterpolated($"SELECT * FROM procedure_drafts WHERE status = 'Queued' OR (status = 'Processing' AND lease_until < {now}) ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct)).SingleOrDefault();
        if (d is null) return false;
        if (d.Attempts >= 3)
        {
            d.Status = "Failed"; d.FailureCode = "draft.retry_exhausted";
            d.Revision = Guid.NewGuid(); d.UpdatedAt = now;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        var lease = Guid.NewGuid();
        d.Status = "Processing"; d.LeaseId = lease; d.LeaseUntil = now.AddMinutes(12);
        d.Attempts++; d.Revision = Guid.NewGuid(); d.UpdatedAt = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var id = d.Id; var blobName = d.BlobName;
        db.ChangeTracker.Clear();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            await using var pdf = await services.GetRequiredService<IBlobStorageClient>().DownloadAsync(blobName, DraftFileStorage.Container, timeout.Token)
                ?? throw new InvalidDataException("Không tìm thấy PDF gốc.");
            var result = await services.GetRequiredService<IProcedureExtractor>().Extract(pdf, timeout.Token);
            if (result.Payload.ValueKind != System.Text.Json.JsonValueKind.Object || result.Payload.GetRawText().Length > 500_000 ||
                result.ExtractedText.Length > 500_000 || result.Warnings is null)
                throw new InvalidDataException("Dữ liệu bóc tách không hợp lệ hoặc quá lớn.");
            var warnings = result.Warnings.Append("Kết quả AI chưa được xác nhận. Hãy đối soát tất cả dữ liệu với PDF gốc.").ToArray();
            var payload = result.Payload.GetRawText();
            var warningsJson = System.Text.Json.JsonSerializer.Serialize(warnings);
            await db.ProcedureDrafts.Where(x => x.Id == id && x.LeaseId == lease && x.Status == "Processing")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PayloadJson, payload)
                    .SetProperty(x => x.WarningsJson, warningsJson).SetProperty(x => x.ExtractedText, result.ExtractedText)
                    .SetProperty(x => x.Status, "NeedsReview").SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.FailureCode, (string?)null).SetProperty(x => x.Revision, Guid.NewGuid())
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            services.GetRequiredService<ILogger<DraftExtractionWorker>>().LogWarning("Bóc tách bản nháp {DraftId} thất bại: {ExceptionType}", id, e.GetType().Name);
            await db.ProcedureDrafts.Where(x => x.Id == id && x.LeaseId == lease && x.Status == "Processing")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Failed").SetProperty(x => x.FailureCode, "draft.extraction_failed")
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.Revision, Guid.NewGuid())
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        }
        return true;
    }
}
