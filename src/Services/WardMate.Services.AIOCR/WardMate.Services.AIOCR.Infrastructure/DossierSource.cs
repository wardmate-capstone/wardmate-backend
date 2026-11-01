using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure;

// Collects one dossier for AI-OFFICER-ASSIST-01 with the officer's own token. Workflow says whether the caller is the
// assigned officer; DocumentForm checks it again on every list and download. No service key, no client-supplied URLs.
public sealed class DossierSource(HttpClient http, IConfiguration config) : IDossierSource
{
    public const int MaxFiles = 8;
    public const int MaxBinaryFiles = 5;
    public const long MaxBinaryBytes = 12L * 1024 * 1024; // Gemini inline requests are limited to 20 MB after base64.
    public const int MaxTotalTextChars = 60000;
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public async Task<DossierBundle> Load(Guid applicationId, Guid officerId, DossierAssistOptions options, string bearer, CancellationToken ct)
    {
        var workflow = Root("Services:ApplicationWorkflow:BaseUrl", "ai.workflow_not_configured", "Chưa cấu hình ApplicationWorkflow.");
        var documents = Root("Services:DocumentForm:BaseUrl", "ai.document_service_not_configured", "Chưa cấu hình DocumentForm.");
        var application = await Get<WorkflowApplication>(workflow, $"api/v1/officer/applications/{applicationId}", bearer, ct);
        if (application is null || application.Id != applicationId || application.Checklists is null)
            throw new AiFailure(503, "ai.checklist_source_invalid", "Workflow trả dữ liệu hồ sơ không hợp lệ.");
        if (application.AssignedOfficerId != officerId || application.UserId == officerId)
            throw new AiFailure(403, "ai.not_assigned_officer", "Chỉ cán bộ đang được phân công hồ sơ mới dùng được trợ lý thẩm định.");
        if (application.Status != "UNDER_REVIEW")
            throw new AiFailure(409, "ai.application_not_under_review", "Chỉ hỗ trợ khi hồ sơ đang ở trạng thái UNDER_REVIEW.");
        var checklist = application.Checklists.Select(c => new DossierChecklistItem(c.Id, c.Title ?? "", c.IsRequired)).ToArray();
        string? Title(Guid? id) => checklist.FirstOrDefault(c => c.Id == id)?.Title;

        var files = new List<DossierFile>();
        long binaryBytes = 0; int binaryCount = 0, textChars = 0;
        async Task Add(string kind, Guid id, Guid? checklistId, string fileName, string downloadPath)
        {
            if (files.Count(f => f.SkipReason is null) >= MaxFiles) { files.Add(Skip("Vượt quá " + MaxFiles + " tệp mỗi lần phân tích.")); return; }
            var bytes = await Download(documents, downloadPath, bearer, ct);
            if (bytes is null) { files.Add(Skip("Tệp lớn hơn 8 MiB.")); return; }
            var text = DocxText.Extract(bytes, out var truncated);
            if (text is not null)
            {
                if (text.Length == 0) { files.Add(Skip("Tệp Word không có nội dung chữ.")); return; }
                if (textChars + text.Length > MaxTotalTextChars) { files.Add(Skip("Tổng nội dung chữ vượt giới hạn phân tích.")); return; }
                textChars += text.Length;
                files.Add(new(kind, id, checklistId, Title(checklistId), fileName, truncated ? text + "\n[...đã cắt bớt]" : text, null, null));
                return;
            }
            if (!IsMedia(bytes)) { files.Add(Skip("Định dạng chưa hỗ trợ (chỉ DOCX, PDF, PNG, JPEG).")); return; }
            if (binaryCount >= MaxBinaryFiles || binaryBytes + bytes.Length > MaxBinaryBytes)
            { files.Add(Skip("Vượt giới hạn " + MaxBinaryFiles + " tệp ảnh/PDF hoặc 12 MiB mỗi lần phân tích.")); return; }
            binaryCount++; binaryBytes += bytes.Length;
            files.Add(new(kind, id, checklistId, Title(checklistId), fileName, null, bytes, null));
            DossierFile Skip(string reason) => new(kind, id, checklistId, Title(checklistId), fileName, null, null, reason);
        }

        if (options.IncludeSubmissions)
        {
            var submissions = await Get<List<SubmissionLink>>(documents, $"api/v1/officer/submissions/by-application/{applicationId}", bearer, ct) ?? [];
            foreach (var s in submissions.Where(s => s.ApplicationId == applicationId).OrderBy(s => s.SubmittedAt))
                await Add("SUBMISSION", s.Id, s.ChecklistId, s.FileName ?? "tờ khai.docx", $"api/v1/officer/submissions/{s.Id}/download-docx");
        }
        if (options.IncludeSupportingDocuments)
        {
            var supporting = await Get<List<SupportingLink>>(documents, $"api/v1/applications/{applicationId}/supporting-documents", bearer, ct) ?? [];
            foreach (var d in supporting.Where(d => d.ApplicationId == applicationId).OrderBy(d => d.CreatedAtUtc))
            {
                if (d.ContentType is { } type && type != Docx && type != "application/pdf" && !type.StartsWith("image/", StringComparison.Ordinal))
                { files.Add(new("SUPPORTING_DOCUMENT", d.Id, d.ChecklistId, Title(d.ChecklistId), d.FileName ?? "", null, null, "Định dạng chưa hỗ trợ (chỉ DOCX, PDF, PNG, JPEG).")); continue; }
                await Add("SUPPORTING_DOCUMENT", d.Id, d.ChecklistId, d.FileName ?? "", $"api/v1/supporting-documents/{d.Id}/download");
            }
        }
        return new(applicationId, application.Status, application.ProcedureTitle ?? "", checklist, files);
    }

    private static bool IsMedia(byte[] b) =>
        b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
        || b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255
        || b.AsSpan().StartsWith("%PDF-"u8);

    private Uri Root(string key, string code, string message) =>
        Uri.TryCreate(config[key], UriKind.Absolute, out var root) && root.Scheme is "https" or "http"
            && root.UserInfo.Length == 0 && root.Query.Length == 0 && root.Fragment.Length == 0
            ? root : throw new AiFailure(503, code, message);

    private async Task<T?> Get<T>(Uri root, string path, string bearer, CancellationToken ct)
    {
        using var request = Request(root, path, bearer);
        using var response = await http.SendAsync(request, ct);
        Check(response);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    // Null when the file is larger than the per-file limit.
    private async Task<byte[]?> Download(Uri root, string path, string bearer, CancellationToken ct)
    {
        using var request = Request(root, path, bearer);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        Check(response);
        if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        try { return await GeminiAssistant.ReadBounded(stream, ct); }
        catch (AiFailure e) when (e.Code == "ai.file_too_large") { return null; }
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
        var status = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => 401,
            HttpStatusCode.Forbidden => 403,
            HttpStatusCode.NotFound => 404,
            HttpStatusCode.Conflict => 409,
            _ => 503
        };
        throw new AiFailure(status, "ai.document_access_failed", "Không lấy được hồ sơ hoặc giấy tờ theo quyền của người đăng nhập.");
    }

    private sealed record WorkflowApplication(Guid Id, Guid UserId, string Status, string? ProcedureTitle, Guid? AssignedOfficerId, ChecklistItem[]? Checklists);
    private sealed record ChecklistItem(Guid Id, string? Title, bool IsRequired);
    private sealed record SubmissionLink(Guid Id, Guid? ApplicationId, Guid? ChecklistId, string? FileName, DateTime? SubmittedAt);
    private sealed record SupportingLink(Guid Id, Guid ApplicationId, Guid? ChecklistId, string? FileName, string? ContentType, DateTime CreatedAtUtc);
}
