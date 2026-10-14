using System.Net.Http.Json;
using WardMate.Services.ApplicationWorkflow.Application;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WardMate.Services.ApplicationWorkflow.API;

// Verify checklist evidence through DocumentForm HTTP API, never its database.
public interface IDocumentContentsClient
{
    Task<ObjectResult?> Validate(Guid applicationId, IEnumerable<ChecklistDto> requiredChecklists, CancellationToken ct);
}

public sealed class DocumentContentsClient(HttpClient http, IConfiguration config, IHttpContextAccessor context) : IDocumentContentsClient
{
    public async Task<ObjectResult?> Validate(Guid applicationId, IEnumerable<ChecklistDto> requiredChecklists, CancellationToken ct)
    {
        var items = requiredChecklists.ToArray();
        if (items.Length == 0) return null;
        var root = config["Services:DocumentForm:BaseUrl"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return Problem(503, "application.documents_unavailable", "Chưa cấu hình DocumentForm.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri.AbsoluteUri.TrimEnd('/') + $"/api/v1/applications/{applicationId}/contents");
            request.Headers.TryAddWithoutValidation("Authorization", context.HttpContext!.Request.Headers.Authorization.ToString());
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return Problem(503, "application.documents_unavailable", "Không xác minh được giấy tờ trong DocumentForm.");
            var contents = await response.Content.ReadFromJsonAsync<Contents>(cancellationToken: ct);
            if (contents is null || contents.ApplicationId != applicationId || contents.Submissions is null || contents.SupportingDocuments is null)
                return Problem(503, "application.documents_unavailable", "DocumentForm trả dữ liệu không hợp lệ.");
            var missing = items.Where(item =>
                !contents.Submissions.Any(s => s.ChecklistId == item.Id && s.Status is ("Submitted" or "Approved")
                    && (!item.FormTemplateId.HasValue || s.TemplateId == item.FormTemplateId))
                && (item.FormTemplateId.HasValue || !contents.SupportingDocuments.Any(d => d.ChecklistId == item.Id)))
                .Select(item => item.Id).ToArray();
            if (missing.Length == 0) return null;
            var problem = Problem(422, "application.documents_incomplete", "Cần lưu giấy tờ hoặc hoàn thành đơn tương ứng trước khi đánh dấu/nộp hồ sơ.");
            ((ProblemDetails)problem.Value!).Extensions["missingChecklistIds"] = missing;
            return problem;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return Problem(503, "application.documents_unavailable", "Không kết nối được DocumentForm.");
        }
    }
    private ObjectResult Problem(int status, string code, string title) => new(new ProblemDetails
    { Status = status, Title = title, Extensions = { ["code"] = code, ["traceId"] = context.HttpContext!.TraceIdentifier } })
    { StatusCode = status, ContentTypes = { "application/problem+json" } };
    private sealed record Contents(Guid ApplicationId, Item[] Submissions, Item[] SupportingDocuments);
    private sealed record Item(Guid Id, Guid? ChecklistId, string Status, Guid? TemplateId);
}
