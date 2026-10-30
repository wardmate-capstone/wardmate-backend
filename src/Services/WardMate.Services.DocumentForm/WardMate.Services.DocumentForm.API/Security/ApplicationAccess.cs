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
    private readonly Dictionary<Guid, string> verifiedStates = new();
    // A staff account may also submit its own citizen application. Ownership takes
    // precedence on shared read routes; officer-only review routes still use Check directly.
    public async Task<ObjectResult?> Read(Guid applicationId, Guid? checklistId, CancellationToken ct, bool requireReady = false)
    {
        var result = await Check(applicationId, checklistId, false, ct, requireReady: requireReady);
        if (result?.StatusCode is 404 or 403 && context.HttpContext!.User.IsInRole("FRONT_DESK_OFFICER"))
        {
            var officerResult = await Check(applicationId, checklistId, false, ct, officer: true, requireReady: requireReady);
            if (officerResult is null) return null;
            if (officerResult.StatusCode is 404 or 403)
            {
                var root = config["Services:ApplicationWorkflow:BaseUrl"];
                if (Uri.TryCreate(root, UriKind.Absolute, out var baseUri) && await CheckConsultationAccess(applicationId, baseUri, ct))
                    return null;
            }
            return officerResult;
        }
        return result;
    }

    private async Task<bool> CheckConsultationAccess(Guid applicationId, Uri baseUri, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(baseUri.AbsoluteUri.TrimEnd('/') + $"/api/v1/applications/{applicationId}/consultations/document-access"));
            request.Headers.TryAddWithoutValidation("Authorization", context.HttpContext!.Request.Headers.Authorization.ToString());
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            var doc = await response.Content.ReadFromJsonAsync<ConsultationDocAccess>(cancellationToken: ct);
            return doc?.Allowed == true;
        }
        catch { return false; }
    }

    public async Task<ObjectResult?> Check(Guid applicationId, Guid? checklistId, bool write, CancellationToken ct,
        Guid? templateId = null, bool supporting = false, bool officer = false, bool requireReady = false)
    {
        if (applicationId == Guid.Empty || checklistId == Guid.Empty)
            return Problem(400, "document.invalid_application", "ApplicationId/checklistId không hợp lệ.");
        var root = config["Services:ApplicationWorkflow:BaseUrl"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != "http" && baseUri.Scheme != "https"))
            return Problem(503, "document.workflow_unavailable", "Chưa cấu hình API ApplicationWorkflow.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(baseUri.AbsoluteUri.TrimEnd('/') + (officer ? "/api/v1/officer/applications/" : "/api/v1/applications/") + applicationId));
            request.Headers.TryAddWithoutValidation("Authorization", context.HttpContext!.Request.Headers.Authorization.ToString());
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                return Problem(404, "document.application_not_found", "Không tìm thấy hồ sơ của bạn.");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return Problem(401, "auth.unauthorized", "Token không được ApplicationWorkflow chấp nhận.");
            if (!response.IsSuccessStatusCode)
                return Problem(503, "document.workflow_unavailable", "Không xác minh được hồ sơ. Vui lòng thử lại.");
            var application = await response.Content.ReadFromJsonAsync<WorkflowApplication>(cancellationToken: ct);
            if (application is null || application.Id != applicationId || !Guid.TryParse(context.HttpContext.User.FindFirst("sub")?.Value, out var userId)
                || (officer ? application.AssignedOfficerId != userId || application.UserId == userId : application.UserId != userId))
                return Problem(403, "document.application_forbidden", "Hồ sơ không thuộc người đăng nhập.");
            if (application.Checklists is null)
                return Problem(503, "document.workflow_unavailable", "ApplicationWorkflow trả checklist không hợp lệ.");
            if (checklistId.HasValue && !application.Checklists.Any(x => x.Id == checklistId))
                return Problem(400, "document.checklist_mismatch", "ChecklistId không thuộc hồ sơ này.");
            var item = application.Checklists.FirstOrDefault(x => x.Id == checklistId);
            if (item?.FormTemplateId is Guid requiredTemplate && (supporting || (templateId.HasValue && templateId != requiredTemplate)))
                return Problem(400, "document.template_mismatch", "Mục này cần đơn DOCX đúng FormTemplateId của hồ sơ, không dùng giấy tờ thay thế.");
            if (write && (officer ? application.Status != "UNDER_REVIEW" : application.Status is not ("DRAFT" or "NEED_REVISION")))
                return Problem(409, "document.application_locked", "Trạng thái hồ sơ không cho phép sửa tài liệu.");
            if (requireReady && application.Status is not ("DOCUMENTS_READY" or "OFFICIALLY_RECEIVED"))
                return Problem(409, "document.application_not_ready", "Hồ sơ phải được xác nhận giấy tờ sẵn sàng tại Workflow trước khi xuất hoặc tải PDF giấy tờ chuẩn bị.");
            verifiedStates[applicationId] = application.Status;
            if (application.Status is "DOCUMENTS_READY" or "OFFICIALLY_RECEIVED")
            {
                if (application.AssignedOfficerId is not Guid reviewer || reviewer == Guid.Empty)
                    return Problem(409, "document.readiness_invalid", "Hồ sơ thiếu cán bộ xác nhận giấy tờ sẵn sàng. Vui lòng kiểm tra Workflow.");
                // Workflow is authoritative. Retry on the next access if local persistence fails.
                var submissions = await db.UserSubmissions.Where(x => x.ApplicationId == applicationId && !x.IsDeleted
                    && x.Status == WardMate.Services.DocumentForm.Domain.Models.SubmissionStatus.Submitted).ToListAsync(ct);
                foreach (var s in submissions) s.ConfirmReadiness(reviewer, "workflow-readiness");
                if (submissions.Count > 0) await db.SaveChangesAsync(ct);
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return Problem(503, "document.workflow_unavailable", "Không kết nối được ApplicationWorkflow.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(409, "document.concurrent_update", "Đơn đã thay đổi trong lúc đồng bộ. Vui lòng tải lại và thử lại.");
        }
        catch (DbUpdateException)
        {
            return Problem(503, "document.sync_unavailable", "Chưa lưu được trạng thái sẵn sàng từ Workflow. Vui lòng thử lại.");
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
        return d is null ? Problem(404, "document.not_found", "Không tìm thấy giấy tờ.")
            : write ? await Check(d.ApplicationId, d.ChecklistId, true, ct)
            : await Read(d.ApplicationId, d.ChecklistId, ct);
    }

    public async Task<ObjectResult?> Generated(Guid id, CancellationToken ct)
    {
        var d = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        return d is null ? Problem(404, "document.not_found", "Không tìm thấy PDF.")
            : await Read(d.ApplicationId, null, ct, requireReady: true);
    }

    public async Task<(Guid ApplicationId, ObjectResult? Error)> OfficerSubmission(Guid id, bool write, CancellationToken ct)
    {
        var s = await db.UserSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (s is null) return (default, Problem(404, "document.not_found", "Không tìm thấy đơn."));
        if (s.ApplicationId is not Guid applicationId || s.ChecklistId is null)
            return (default, Problem(409, "document.legacy_unlinked", "Đơn chưa liên kết hồ sơ Workflow."));
        return (applicationId, await Check(applicationId, s.ChecklistId, write, ct, s.TemplateId, officer: true));
    }

    public async Task<ObjectResult?> Decide(Guid id, bool confirmReadiness, string? reason, CancellationToken ct)
    {
        var link = await OfficerSubmission(id, false, ct);
        if (link.Error is not null) return link.Error;
        if (verifiedStates.TryGetValue(link.ApplicationId, out var state)
            && state == (confirmReadiness ? "DOCUMENTS_READY" : "NEED_REVISION")) return null;
        var root = config["Services:ApplicationWorkflow:BaseUrl"]!.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, root + $"/api/v1/officer/applications/{link.ApplicationId}/" + (confirmReadiness ? "confirm-readiness" : "request-revision"));
        request.Headers.TryAddWithoutValidation("Authorization", context.HttpContext!.Request.Headers.Authorization.ToString());
        if (!confirmReadiness) request.Content = JsonContent.Create(new { reason });
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return Problem((int)response.StatusCode, "document.workflow_review_failed", "Workflow chưa chấp nhận yêu cầu kiểm tra giấy tờ. Kiểm tra trạng thái, quyền thụ lý và các lỗi còn mở.");
            return await Check(link.ApplicationId, null, false, ct, officer: true, requireReady: confirmReadiness);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { return Problem(503, "document.workflow_unavailable", "Chưa xác nhận được yêu cầu kiểm tra giấy tờ. Tải lại trạng thái hồ sơ trước khi thử lại."); }
    }

    private ObjectResult Problem(int status, string code, string message) => new(new ProblemDetails
    {
        Status = status, Title = message, Extensions = { ["code"] = code, ["traceId"] = context.HttpContext!.TraceIdentifier }
    }) { StatusCode = status, ContentTypes = { "application/problem+json" } };

    private sealed record WorkflowApplication(Guid Id, Guid UserId, string Status, WorkflowChecklist[] Checklists, Guid? AssignedOfficerId);
    private sealed record WorkflowChecklist(Guid Id, Guid? FormTemplateId);
    private sealed record ConsultationDocAccess(bool Allowed, Guid ApplicationId, int? Version);
}
