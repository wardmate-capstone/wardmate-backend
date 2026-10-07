using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Infrastructure;

public sealed class DocumentFormsClient(HttpClient http, IConfiguration configuration) : IDocumentFormsClient
{
    private sealed record Template(Guid Id, string Code, string Title, string? FileDocxUrl, bool IsActive);
    private sealed record Page(Template[]? Items);
    public async Task<ProcedureResult<DocumentFormOptionDto[]>> List(DocumentFormsQuery query, CancellationToken ct)
    {
        var baseUrl = configuration["DocumentForm:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            return ProcedureResult<DocumentFormOptionDto[]>.Fail("document_form.not_configured", "Chưa cấu hình địa chỉ DocumentForm hợp lệ.", 503);
        try
        {
            var url = new Uri(new Uri(address.AbsoluteUri.TrimEnd('/') + "/"),
                $"api/v1/form-templates?page={query.Page}&pageSize={query.PageSize}&isActive=true&searchCode={Uri.EscapeDataString(query.SearchCode ?? string.Empty)}");
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Unavailable();
            var page = await response.Content.ReadFromJsonAsync<Page>(cancellationToken: ct);
            if (page?.Items is null || page.Items.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Title)))
                return ProcedureResult<DocumentFormOptionDto[]>.Fail("document_form.invalid_response", "DocumentForm trả dữ liệu không hợp lệ.", 502);
            // Current DocumentForm contract represents DOCX templates; do not invent ONLINE_INTERACTIVE capability.
            return ProcedureResult<DocumentFormOptionDto[]>.Ok(page.Items.Where(x => x.IsActive)
                .Select(x => new DocumentFormOptionDto(x.Id, x.Code, x.Title, "DOCX_TEMPLATE")).ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return ProcedureResult<DocumentFormOptionDto[]>.Fail("document_form.timeout", "DocumentForm phản hồi quá thời gian cho phép.", 504); }
        catch (JsonException) { return ProcedureResult<DocumentFormOptionDto[]>.Fail("document_form.invalid_response", "DocumentForm trả dữ liệu không hợp lệ.", 502); }
        catch (HttpRequestException) { return Unavailable(); }
    }
    private static ProcedureResult<DocumentFormOptionDto[]> Unavailable() =>
        ProcedureResult<DocumentFormOptionDto[]>.Fail("document_form.unavailable", "Không thể lấy danh sách biểu mẫu. Vui lòng thử lại sau.", 503);
}
