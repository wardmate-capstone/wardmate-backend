using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed class AuthorizedDocumentSource(HttpClient http, IConfiguration config) : IAuthorizedDocumentSource
{
    public async Task<byte[]> Download(Guid applicationId, Guid documentId, string bearer, CancellationToken ct)
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
        using var download = Request(root, $"api/v1/supporting-documents/{documentId}/download", bearer);
        using var response = await http.SendAsync(download, HttpCompletionOption.ResponseHeadersRead, ct); Check(response);
        if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) throw new AiFailure(413, "ai.file_too_large", "File tối đa 8 MiB.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await GeminiAssistant.ReadBounded(stream, ct);
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
    private sealed record DocumentLink(Guid Id, Guid ApplicationId);
}
