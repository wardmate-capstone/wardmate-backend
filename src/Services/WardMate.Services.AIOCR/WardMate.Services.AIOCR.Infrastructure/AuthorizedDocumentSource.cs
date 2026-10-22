using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed class AuthorizedDocumentSource(HttpClient http, IConfiguration config) : IAuthorizedDocumentSource
{
    public async Task<ChecklistDocument> Download(Guid applicationId, Guid documentId, string bearer, CancellationToken ct)
    {
        if (!Uri.TryCreate(config["Services:DocumentForm:BaseUrl"], UriKind.Absolute, out var root)
            || root.Scheme is not ("https" or "http") || root.UserInfo.Length != 0 || root.Query.Length != 0 || root.Fragment.Length != 0)
            throw new AiFailure(503, "ai.document_service_not_configured", "Chưa cấu hình DocumentForm.");
        // Use configured service only; no client URLs or privileged service token.
        using var list = Request(root, $"api/v1/applications/{applicationId}/supporting-documents", bearer);
        using var listed = await http.SendAsync(list, ct); Check(listed);
        var documents = await listed.Content.ReadFromJsonAsync<List<DocumentLink>>(cancellationToken: ct);
        if (documents is null || !documents.Any(x => x.Id == documentId && x.ApplicationId == applicationId))
            throw new AiFailure(404, "ai.document_not_found", "Giấy tờ không thuộc hồ sơ được phép truy cập.");
        var link = documents.Single(x => x.Id == documentId);
        if (link.ChecklistId is not Guid checklistId || checklistId == Guid.Empty)
            throw new AiFailure(409, "ai.checklist_required", "Giấy tờ chưa liên kết mục checklist. Hãy tải lên lại đúng mục.");
        if (!Uri.TryCreate(config["Services:ApplicationWorkflow:BaseUrl"], UriKind.Absolute, out var workflow)
            || workflow.Scheme is not ("https" or "http") || workflow.UserInfo.Length != 0 || workflow.Query.Length != 0 || workflow.Fragment.Length != 0)
            throw new AiFailure(503, "ai.workflow_not_configured", "Chưa cấu hình ApplicationWorkflow.");
        using var owned = Request(workflow, $"api/v1/applications/{applicationId}", bearer);
        using var ownResponse = await http.SendAsync(owned, ct);
        WorkflowDocument? application;
        if (ownResponse.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
        {
            using var officer = Request(workflow, $"api/v1/officer/applications/{applicationId}", bearer);
            using var officerResponse = await http.SendAsync(officer, ct); Check(officerResponse);
            application = await officerResponse.Content.ReadFromJsonAsync<WorkflowDocument>(cancellationToken: ct);
        }
        else
        {
            Check(ownResponse);
            application = await ownResponse.Content.ReadFromJsonAsync<WorkflowDocument>(cancellationToken: ct);
        }
        if (application is null || application.Id != applicationId || application.Checklists is null)
            throw new AiFailure(503, "ai.checklist_source_invalid", "Workflow trả dữ liệu checklist không hợp lệ.");
        var matching = application.Checklists.Where(x => x.Id == checklistId).ToArray();
        if (matching.Length != 1 || string.IsNullOrWhiteSpace(matching[0].Title))
            throw new AiFailure(409, "ai.checklist_mismatch", "Không tìm thấy yêu cầu giấy tờ trong checklist hồ sơ. Hãy đối soát lại.");
        var expectedType = matching[0].Title.Trim();
        using var download = Request(root, $"api/v1/supporting-documents/{documentId}/download", bearer);
        using var response = await http.SendAsync(download, HttpCompletionOption.ResponseHeadersRead, ct); Check(response);
        if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) throw new AiFailure(413, "ai.file_too_large", "File tối đa 8 MiB.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return new(await GeminiAssistant.ReadBounded(stream, ct), checklistId, expectedType);
    }
    private static HttpRequestMessage Request(Uri root, string path, string bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, root.AbsoluteUri.TrimEnd('/') + "/" + path);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(bearer);
        return request;
    }
    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var status = (int)response.StatusCode;
        throw new AiFailure(status is 401 or 403 or 404 ? status : 503, "ai.document_access_failed", "Không lấy được giấy tờ theo quyền của người đăng nhập.");
    }
    private sealed record DocumentLink(Guid Id, Guid ApplicationId, Guid? ChecklistId);
    private sealed record WorkflowDocument(Guid Id, ChecklistItem[]? Checklists);
    private sealed record ChecklistItem(Guid Id, string Title);
}
