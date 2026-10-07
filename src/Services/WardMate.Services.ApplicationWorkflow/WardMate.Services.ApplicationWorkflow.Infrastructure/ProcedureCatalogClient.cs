using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ProcedureCatalogClient(HttpClient http) : IProcedureCatalogClient
{
    public async Task<WorkflowResult<ProcedureSnapshot>> Get(Guid procedureId, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/procedures/{procedureId}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return WorkflowResult<ProcedureSnapshot>.Fail(404, "application.procedure_not_found", "Không tìm thấy thủ tục đang hoạt động.");
            if (!response.IsSuccessStatusCode)
                return WorkflowResult<ProcedureSnapshot>.Fail(503, "application.catalog_unavailable", "Chưa thể lấy thông tin thủ tục. Vui lòng thử lại sau.");
            var value = await response.Content.ReadFromJsonAsync<ProcedureSnapshot>(cancellationToken: ct);
            if (value is null || value.Id != procedureId || string.IsNullOrWhiteSpace(value.Title) || value.Title.Length > 500
                || (value.ContentPayload?.Cases?.Any(x => x is null || string.IsNullOrWhiteSpace(x.CaseCode) || x.CaseCode.Length > 100) ?? false))
                return WorkflowResult<ProcedureSnapshot>.Fail(502, "application.invalid_schema", "Dữ liệu thủ tục trả về không hợp lệ.");
            return WorkflowResult<ProcedureSnapshot>.Ok(value);
        }
        catch (JsonException) { return WorkflowResult<ProcedureSnapshot>.Fail(502, "application.invalid_schema", "Dữ liệu thủ tục không đúng định dạng JSON."); }
        catch (HttpRequestException) { return WorkflowResult<ProcedureSnapshot>.Fail(503, "application.catalog_unavailable", "Không kết nối được dịch vụ thủ tục."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return WorkflowResult<ProcedureSnapshot>.Fail(504, "application.catalog_timeout", "Dịch vụ thủ tục phản hồi quá thời gian cho phép."); }
    }
}

public static class WorkflowInfrastructureRegistration
{
    public static IServiceCollection AddWorkflowInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<WorkflowDbContext>(o => o.UseNpgsql(config.GetConnectionString("WorkflowDatabase")
            ?? throw new InvalidOperationException("Cần cấu hình ConnectionStrings:WorkflowDatabase.")).UseSnakeCaseNamingConvention());
        services.AddScoped<IApplicationStore, ApplicationStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IProcedureCatalogClient, ProcedureCatalogClient>(http =>
        {
            var endpoint = config["ProcedureCatalog:BaseUrl"] ?? "http://localhost:5002/";
            if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Địa chỉ Procedure Catalog không hợp lệ.");
            http.BaseAddress = uri;
            http.Timeout = TimeSpan.FromSeconds(15);
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;
        });
        return services;
    }
}
