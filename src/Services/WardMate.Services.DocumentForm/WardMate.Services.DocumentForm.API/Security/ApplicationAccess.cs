using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;

namespace WardMate.Services.DocumentForm.API.Security;

// HTTP integration only: Workflow owns applications, their owners and checklist IDs.
public sealed class ApplicationAccess(HttpClient http, IConfiguration config, IHttpContextAccessor context,
    IDocumentDbContext db)
{
    public async Task<ObjectResult?> Check(Guid applicationId, Guid? checklistId, bool write, CancellationToken ct,
        Guid? templateId = null, bool supporting = false)
    {
        if (applicationId == Guid.Empty || checklistId == Guid.Empty)
            return Problem(400, "document.invalid_application", "ApplicationId/checklistId không hợp lệ.");
        var root = config["Services:ApplicationWorkflow:BaseUrl"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != "http" && baseUri.Scheme != "https"))
            return Problem(503, "document.workflow_unavailable", "Chưa cấu hình API ApplicationWorkflow.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(baseUri.AbsoluteUri.TrimEnd('/') + $"/api/v1/applications/{applicationId}"));
            request.Headers.TryAddWithoutValidation("Authorization", context.HttpContext!.Request.Headers.Authorization.ToString());
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                return Problem(404, "document.application_not_found", "Không tìm thấy hồ sơ của bạn.");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Problem(401, "auth.unauthorized", "Token không được ApplicationWorkflow chấp nhận.");
            if (!response.IsSuccessStatusCode)
                return Problem(503, "document.workflow_unavailable", "Không xác minh được hồ sơ. Vui lòng thử lại.");
            var application = await response.Content.ReadFromJsonAsync<WorkflowApplication>(cancellationToken: ct);
            if (application is null || application.Id != applicationId || !Guid.TryParse(context.HttpContext.User.FindFirst("sub")?.Value, out var userId) || application.UserId != userId)
                return Problem(403, "document.application_forbidden", "Hồ sơ không thuộc người đăng nhập.");
            if (application.Checklists is null)
                return Problem(503, "document.workflow_unavailable", "ApplicationWorkflow trả checklist không hợp lệ.");
            if (checklistId.HasValue && !application.Checklists.Any(x => x.Id == checklistId))
                return Problem(400, "document.checklist_mismatch", "ChecklistId không thuộc hồ sơ này.");
            var item = application.Checklists.FirstOrDefault(x => x.Id == checklistId);
            if (item?.FormTemplateId is Guid requiredTemplate && (supporting || (templateId.HasValue && templateId != requiredTemplate)))
                return Problem(400, "document.template_mismatch", "Mục này cần đơn DOCX đúng FormTemplateId của hồ sơ, không dùng giấy tờ thay thế.");
            if (write && application.Status is not ("DRAFT" or "NEED_REVISION"))
                return Problem(409, "document.application_locked", "Chỉ được sửa tài liệu khi hồ sơ DRAFT hoặc NEED_REVISION.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return Problem(503, "document.workflow_unavailable", "Không kết nối được ApplicationWorkflow.");
        }
    }

    public async Task<(Guid ApplicationId, Guid ChecklistId, ObjectResult? Error)> Submission(Guid id, bool write, CancellationToken ct)
    {
        var s = await db.UserSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (s is null) return (default, default, Problem(404, "document.submission_not_found", "Không tìm thấy đơn."));
        if (!Guid.TryParse(context.HttpContext!.User.FindFirst("sub")?.Value, out var actor) || s.ApplicantId != actor)
            return (default, default, Problem(403, "document.not_owner", "Đơn không thuộc người đăng nhập."));
        if (!s.ApplicationId.HasValue || !s.ChecklistId.HasValue)
            return (default, default, Problem(409, "document.legacy_unlinked", "Đơn cũ chưa liên kết hồ sơ; không tự động gán ID."));
        return (s.ApplicationId.Value, s.ChecklistId.Value, await Check(s.ApplicationId.Value, s.ChecklistId, write, ct, s.TemplateId));
    }

    public async Task<ObjectResult?> Supporting(Guid id, bool write, CancellationToken ct)
    {
        var d = await db.SupportingDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return d is null ? Problem(404, "document.not_found", "Không tìm thấy giấy tờ.") : await Check(d.ApplicationId, d.ChecklistId, write, ct);
    }

    public async Task<ObjectResult?> Generated(Guid id, CancellationToken ct)
    {
        var d = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return d is null ? Problem(404, "document.not_found", "Không tìm thấy PDF.") : await Check(d.ApplicationId, null, false, ct);
    }

    private ObjectResult Problem(int status, string code, string message) => new(new ProblemDetails
    {
        Status = status, Title = message, Extensions = { ["code"] = code, ["traceId"] = context.HttpContext!.TraceIdentifier }
    }) { StatusCode = status, ContentTypes = { "application/problem+json" } };

    private sealed record WorkflowApplication(Guid Id, Guid UserId, string Status, WorkflowChecklist[] Checklists);
    private sealed record WorkflowChecklist(Guid Id, Guid? FormTemplateId);
}
