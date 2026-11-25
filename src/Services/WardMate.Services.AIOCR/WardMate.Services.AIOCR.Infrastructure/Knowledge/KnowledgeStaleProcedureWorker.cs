using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

/// <summary>
/// KB-STALE-01: định kỳ tắt nguồn "Thủ tục (đồng bộ)" của thủ tục đã ngừng công khai trong Catalog, để chatbot
/// không trả lời theo thủ tục đã ẩn khi chưa ai bấm "Đồng bộ tất cả". Không tạo hay cập nhật nguồn, không gọi AI;
/// không bật lại nguồn (bật lại vẫn là quyết định của Quản lý thủ tục). Knowledge:StaleCheckMinutes = 0 để tắt.
/// </summary>
public sealed class KnowledgeStaleProcedureWorker(IServiceScopeFactory scopes, KnowledgeSettings settings, IConfiguration config,
    ILogger<KnowledgeStaleProcedureWorker> logger) : BackgroundService
{
    // Người thực hiện hệ thống (Guid rỗng) — nhật ký nguồn ghi UpdatedBy rỗng cho thay đổi tự động.
    private static readonly Guid SystemActor = Guid.Empty;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            var interval = settings.StaleCheckInterval;
            if (interval <= TimeSpan.Zero) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(config.GetConnectionString("AiOcrDatabase")))
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var kb = scope.ServiceProvider.GetRequiredService<IKnowledgeBase>();
                    var count = await kb.DeactivateUnpublishedProcedures(SystemActor, ct);
                    if (count > 0) logger.LogInformation("Knowledge: deactivated {Count} source(s) of unpublished procedures.", count);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Knowledge stale-procedure check failed ({Type}); retrying later.", e.GetType().Name); }
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
        }
    }
}
