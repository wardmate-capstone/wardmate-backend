


# WardMate — Nhật ký phát triển
**Trạng thái QR-BACKWARD-COMPATIBILITY-FIX: HOÀN THÀNH** — Giải quyết dứt điểm lỗi quét mã QR 401/403 khi quét bằng điện thoại/camera không có token: (1) Cập nhật `ApplicationsController` cho phép truy cập ẩn danh `[AllowAnonymous]` trên `GET /api/v1/applications/{id}`: nếu người gọi không có Bearer token (quét từ camera điện thoại đối với mã QR cũ đã in/tải trước đây), tự động HTTP 302 Redirect sang `/api/v1/public/qr-lookup?applicationId={id}` để xem kết quả hợp lệ công khai mà không bị chặn token. Nếu có Bearer token, vẫn trả về đầy đủ `ApplicationDto` cho chủ hồ sơ hoặc cán bộ. (2) Bổ sung phương thức `Update` trên entity `ApprovedQr` và cập nhật `GeneratePdfDocumentCommand` tự động cập nhật bản ghi QR cũ sang URL tra cứu công khai mới và sinh lại ảnh PNG khi xuất PDF mới. Release build: 0 errors, 0 warnings. Xác minh ngày 20/10/2026 lúc 02:40 (Asia/Saigon, UTC+07:00).

**Trạng thái TASK-OFFICER-LIST-QR-LOOKUP (Officer All Applications List, Public QR Lookup & Gotenberg Azure HTTPS Fix): HOÀN THÀNH** — (1) Bổ sung API `GET /api/v1/officer/applications` cho Cán bộ một cửa (`FRONT_DESK_OFFICER`): truy vấn toàn bộ hồ sơ trong phạm vi phường (kể cả đã duyệt / từ chối / cần sửa), loại trừ hồ sơ chính cán bộ nộp, hỗ trợ lọc theo trạng thái đơn lẻ hoặc nhiều trạng thái phân tách bằng dấu phẩy (ví dụ: `status=DOCUMENTS_READY,NEED_REVISION,READY_TO_SUBMIT`), theo `procedureId`, `applicationCode`, khoảng thời gian `submittedFrom`/`submittedTo`, và phân trang an toàn. (2) Bổ sung API `GET /api/v1/public/qr-lookup` và `GET /api/v1/public/qr-lookup/{**token}` trong `DocumentForm`: endpoint công khai (`AllowAnonymous`) phục vụ quét mã QR in trên tờ khai đã duyệt qua điện thoại di động/camera, tự động nhận diện GUID, URL đầy đủ hoặc token, truy vấn `ApprovedQr` + `UserSubmission` + `FormTemplate` để trả về xác thực tính hợp lệ của hồ sơ. (3) Sửa lỗi PDF 503 (`document.pdf_render_failed`): điều tra nguyên nhân do Container App `wardmate-gotenberg` trên Azure Express/Consumption Environment chỉ hỗ trợ HTTPS, trong khi `wardmate-documentform` cấu hình `Gotenberg__Url=http://wardmate-gotenberg:3000` (port 3000 không mở ra ingress proxy); đã cập nhật biến môi trường trên Azure thành `Gotenberg__Url=https://wardmate-gotenberg.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`, đồng thời bổ sung ILogger bắt và ghi chi tiết stack trace khi convert hoặc blob upload thất bại. (4) Cập nhật và đồng bộ YARP Gateway (`appsettings.json` và `appsettings.Production.json`): định tuyến route `GET /api/v1/public/{**catch-all}` về `document-form`, `GET /api/v1/officer/applications` về `application-workflow`. Solution-wide Release build: **0 errors, 0 warnings**. Xác minh ngày 19/10/2026 lúc 23:30 (Asia/Saigon, UTC+07:00).

**Trạng thái DOCUMENT-READINESS & NOTIFICATION-HARDEN-DEPLOY: HOÀN THÀNH** — Triển khai 3 dịch vụ lên Azure: (1) `ApplicationWorkflow`: Luồng xác nhận giấy tờ sẵn sàng (`DOCUMENTS_READY`, `documents_ready_at`, endpoint `POST /api/v1/officer/applications/{id}/confirm-readiness`). (2) `DocumentForm`: Đồng bộ trạng thái nộp hồ sơ sang `ReadyForDownload` (status 4) và kết xuất PDF sạch qua Gotenberg. (3) `AnalyticsSystem`: Gia cố Inbox worker rollback giao dịch khi lỗi, giới hạn retry tối đa 10 lần chuyển Dead, và kiểm tra quyền IAM/phường cho từng kết nối SignalR trước khi gửi thông báo realtime. Build Docker và push thành công lên ACR `acrwardmate2026.azurecr.io`: `wardmate-application-workflow:workflow-20261017-01` (sha256:74cf71f74f1111cf130298dcba0dae582f5015eeb3f3cb181f2a22896ad3a111), `wardmate-documentform:task25-readiness-20261017-01` (sha256:802784655ef943bdf3fe18981c47ad548182f6065711675bcbdd08cef787dcb6), `wardmate-analytics-system:harden-20261017-01` (sha256:bc63f963c0f016e86c294a84a7f0b7f612c8d200abbac9d0fcf42bca31e17aac). Toàn bộ solution Release build: 0 errors, 0 warnings. Đã đồng bộ nhánh `kha`, `main`, `deploy` tại commit `43479d6`. Xác minh ngày 17/10/2026 lúc 22:45 (Asia/Saigon, UTC+07:00).
**Trạng thái TASK-25 / GAP-FIX (DocumentForm Gaps 1 & 2 — Officer Access + SubmissionId — DEPLOY AZURE): HOÀN THÀNH** — Sửa hai điểm thiếu sót FE phản ánh trong phạm vi `DocumentForm`: (1) Gap 1: Đổi `ApplicationContentsController` sang policy `DocumentUser` + `access.Read` để cán bộ xem được nội dung hồ sơ; thêm endpoint `GET /api/v1/officer/submissions/by-application/{applicationId}` (query `GetSubmissionsByApplicationQuery`) trả danh sách `UserSubmissionSummaryDto[]` với submissionId để cán bộ thực hiện review/download/annotate/approve. (2) Gap 2: Thêm `SubmissionId (Guid?)` vào entity `GeneratedDocument` + EF config + index + migration `AddSubmissionIdToGeneratedDocuments`; truyền SubmissionId từ `GeneratePdfDocumentCommand`; map vào `GeneratedDocumentDto`; thêm `?submissionId` filter trong `GetGeneratedDocumentsQuery` và controller. Build Release: **0 errors, 0 warnings**. Đã push 11 commits lên nhánh `kha`. Docker image `acrwardmate2026.azurecr.io/wardmate-documentform:task25-gaps-20261016-01` build thành công, push lên ACR, cập nhật Container App `wardmate-documentform` trên Azure (`rg-wardmate-prod`) provisioningState=Succeeded (Healthy, 100% traffic). Kiểm tra trực tiếp qua Gateway: route `GET /api/v1/officer/submissions/by-application/{applicationId}` phản hồi 401 Unauthorized theo đúng chuẩn ProblemDetails (yêu cầu Bearer token). Xác minh ngày 16/10/2026 lúc 00:25 (Asia/Saigon, UTC+07:00).
**Trạng thái TASK-22 & TASK-23 (Approved QR Generation & PDF Stamping — DEPLOY AZURE): HOÀN THÀNH** — Triển khai quy trình sinh mã QR tra cứu khi hồ sơ được phê duyệt (APPROVED status -> sinh mã QR lưu bảng `document.approved_qrs` với URL tra cứu Gateway), đóng dấu mã QR chuẩn 3.5cm x 3.5cm căn lề phải vào cuối văn bản DOCX bằng OpenXML SDK trước khi Gotenberg render PDF chính thức. Thêm 2 endpoints mới: `GET /api/v1/applications/{id}/approved-qr` (lấy URL/metadata QR) và `GET /api/v1/applications/{id}/approved-qr/image` (tải ảnh PNG QR). Cài đặt thư viện QRCoder 1.4.3, tạo migration `AddApprovedQrTable`, bảo vệ quyền truy cập chính chủ và cán bộ thụ lý. Solution-wide Release build: 0 errors, 0 warnings. Triển khai Live lên Azure: build & push ACR `wardmate-documentform:task22-23-qr-20261015-01` và `wardmate-gateway:v8-approved-qr-20261015`. Cập nhật Container Apps `wardmate-documentform` và `wardmate-gateway` trên Azure (`rg-wardmate-prod`) provisioningState=Succeeded. Kiểm tra trực tiếp qua Gateway: Health 200 OK. Đã đồng bộ toàn bộ commits lên nhánh `kha`, `main`, `nghia`, `deploy`. Xác minh ngày 15/10/2026 lúc 23:40 (Asia/Saigon, UTC+07:00).

**Trạng thái FLOW-REVIEW-DEPLOY: HOÀN THÀNH** — Rà soát luồng liên service và hoàn thiện validation: kiểm tra checklist/case/form chặt chẽ trong ProcedureCatalog; chặn null payload và document evidence rỗng/trùng trong ApplicationWorkflow; bảo toàn quyền đọc chính chủ cho tài khoản kép (citizen + officer) trong DocumentForm; hoàn thiện sổ tay ràng buộc hệ thống docs/system-constraints.md. Release build: 0 errors, 0 warnings. Build Docker và push ACR thành công: `wardmate-procedure-catalog:v8-checklist-review-20261015`, `wardmate-application-workflow:workflow-20261015-01`, `wardmate-documentform:task18-review-20261015-01`. Xác minh ngày 15/10/2026 lúc 07:25 (Asia/Saigon, UTC+07:00).

**Trạng thái APPROVAL-PDF-FLOW-DEPLOY: HOÀN THÀNH** — Triển khai 5 hạng mục BE (phạm vi thụ lý cán bộ đúng phường, Workflow quyết định duyệt một lần & DocumentForm đồng bộ đọc, chuyển PDF thật qua Gotenberg loại bỏ Word comments & bỏ PDF mẫu giả, khóa đơn và annotation sau khi duyệt, compose Gotenberg). Đã tạo 4 commits functional trên nhánh `kha` (`a28ab08..dc5e9f3`), đồng bộ lên nhánh `main` và `deploy`. Build Docker và push thành công lên ACR `acrwardmate2026.azurecr.io`: `wardmate-procedure-catalog:v7-checklist-20261014`, `wardmate-application-workflow:workflow-20261014-02`, `wardmate-documentform:task18-gotenberg-20261014-01`. Xác minh ngày 14/10/2026 lúc 16:30 (Asia/Saigon, UTC+07:00).

**Trạng thái WORKFLOW-DEPLOY-AZURE: HOÀN THÀNH** — Triển khai dịch vụ `WardMate.Services.ApplicationWorkflow` dùng chung PostgreSQL database với `DocumentForm` (schema `public` độc lập hoàn toàn với schema `document`). Đã chạy toàn bộ 5 migrations EF Core thành công lên Supabase (`InitialApplicationWorkflow`, `ApplicationReviewVersions`, `WardScopedFinalDecisions`, `WorkflowNotificationOutbox`, `LinkChecklistFormTemplates`). Build Docker image `acrwardmate2026.azurecr.io/wardmate-application-workflow:workflow-20261014-01` (0 errors), push ACR, tạo mới và kích hoạt Container App `wardmate-application-workflow` trên Azure (`rg-wardmate-prod`) provisioningState=Succeeded. Cập nhật Gateway định tuyến `/api/v1/applications` sang Workflow service. Test suite Workflow: 39/39 passed; DocumentForm: 76/76 passed. Xác thực trực tiếp live: Health 200, Swagger 200, Gateway routing 401 Unauthorized (yêu cầu Bearer token). Xác minh ngày 14/10/2026 lúc 10:05 (Asia/Saigon, UTC+07:00).

**Trạng thái TASK-18 Officer Review & Annotated DOCX Flow (DF-OFFICER-ANNOTATE): HOÀN THÀNH** — Triển khai luồng Cán bộ xét duyệt hồ sơ online qua Word DOCX: Cán bộ tải DOCX về (`GET /api/v1/officer/submissions/{id}/download-docx`), thêm comment trực tiếp trên web editor FE, lưu lại file DOCX có comment về BE (`PUT /api/v1/officer/submissions/{id}/annotate-docx`), sau đó yêu cầu sửa (`POST /api/v1/officer/submissions/{id}/request-revision`). Người dân tải lại DOCX (`GET /api/v1/citizen/submissions/{id}/download-docx`) sẽ thấy đầy đủ comment của cán bộ ở lề tài liệu để chỉnh sửa. Khi hồ sơ được duyệt (`Approved`), người dân kết xuất PDF (`POST /api/v1/applications/{id}/generate-pdf`), hệ thống tự động bóc tách và loại bỏ sạch comment Word (`DocxCommentStripper`) trước khi render PDF để người dân in nộp bản sạch không có comment. Build Release: 0 errors, 0 warnings. Đã commit và push lên nhánh `nghia` (5 commits: 078902c..29531c6). Docker image `wardmate-documentform:task18-annotate-20261013-01` build 0 errors, push lên ACR `acrwardmate2026.azurecr.io`, update Container App `wardmate-documentform` trên Azure (`rg-wardmate-prod`) provisioningState=Succeeded. Swagger và Health endpoint xác nhận live: 200 Healthy. Đồng thời khởi động toàn bộ môi trường local docker compose (postgres, pgadmin, azurite, rabbitmq, redis). Xác minh ngày 13/10/2026 lúc 21:20 (Asia/Saigon, UTC+07:00).

**Trạng thái TASK-18 PDF Approval Validation (DF-PDF-APPROVAL): HOÀN THÀNH** — Thêm validation bắt buộc hồ sơ phải ở trạng thái Approved (cán bộ đã duyệt) trước khi người dân có thể xuất PDF. Thêm lại OfficerSubmissionsController + OfficerReviewCommands cho endpoint POST /api/v1/officer/submissions/{id}/approve và POST /api/v1/officer/submissions/{id}/request-revision. Build Release: 0 errors, 0 warnings. Test suite: DocumentForm 76/76 passed, ApplicationWorkflow 19/19 passed, ProcedureCatalog 96/96 passed. 7 commits granular trên nhánh nghia (d3a140d..52ade89), push lên origin/nghia. Docker image wardmate-documentform:task18-approved-20261012-01 build 0 errors, push ACR, update Container App wardmate-documentform provisioningState=Succeeded. Swagger xác nhận live: /api/v1/applications/{id}/generate-pdf và /api/v1/officer/submissions/{id}/approve. Xác minh ngày 12/10/2026 lúc 10:37 (Asia/Saigon, UTC+07:00).

**Tráº¡ng thÃ¡i TASK-17 & TASK-18 (Supporting Documents & Generated PDF Engine): HOÃ€N THÃ€NH** â€” Triá»ƒn khai toÃ n diá»‡n bá»™ API Giáº¥y tá» minh chá»©ng (document.supporting_documents: Upload multipart gáº¯n checklist, List, Soft-Delete, Download) vÃ  Engine káº¿t xuáº¥t DOCX sang PDF (document.generated_documents: Gotenberg REST API + Resilient RFC-3200 PDF Fallback, List, Download). Build Release: 0 errors, 0 warnings. ToÃ n bá»™ test suite vÆ°á»£t qua: DocumentForm 76/76 passed, ProcedureCatalog 96/96 passed, ApplicationWorkflow 19/19 passed. XÃ¡c minh ngÃ y 11/10/2026 lÃºc 22:55 (Asia/Saigon, UTC+07:00).


**Trạng thái DF-AZURE-ONLINE-GIT-PUSH: HOÀN THÀNH** — đã tạo 16 commits cho tính năng online drafts + preserved DOCX trên nhánh `nghia`, merge với `kha` (resolve conflict PROGRESS/api-guide/appsettings.json bằng ours), push lên `kha` (17 commits mới: `5b7667f..95c8a01`). Build Docker image `wardmate-documentform:kha-20261007-0817` (0 errors), push lên ACR `acrwardmate2026.azurecr.io`, update Container App `wardmate-documentform` provisioningState=Succeeded. Kiểm tra công khai qua Gateway: health 200, Swagger UI 200, form-templates 200, citizen submissions 200, Swagger JSON xác nhận online-config hiện diện và officer endpoints không còn. Xác minh ngày 07/10/2026 lúc 08:20 (Asia/Saigon).

**Trạng thái IAM Ingress CSRF (IAM-007): HOÀN THÀNH** — đã cấu hình ForwardedHeadersOptions xử lý X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host trong middleware pipeline IAM, hỗ trợ Swagger Azure và ingress proxy tin cậy. 27/27 HTTP checks passed, 131/131 solution tests passed. Xác minh ngày 02/10/2026 lúc 01:45 (Asia/Saigon).
**Trạng thái Procedure Catalog (TASK-09 điều chỉnh): HOÀN THÀNH** — đã triển khai xuất bản thủ tục đã đối soát kèm lịch sử phiên bản PDF, phân trang danh sách Public/Manager, tìm kiếm unaccent, hoàn thiện 11 integration tests, build Docker image và migrate database Local Dev. Xác minh ngày 01/10/2026 lúc 14:15 (Asia/Saigon).
**Trạng thái Fix AddVersion Concurrency: HOÀN THÀNH** — đã fix `DbUpdateConcurrencyException` trên endpoint `POST /api/v1/form-templates/{id}/versions`. Xác minh ngày 01/10/2026 lúc 11:30 (Asia/Saigon).
**Trạng thái Document & Form Service (TASK-10, TASK-11, TASK-12): HOÀN THÀNH** — đã triển khai thực thể Document, E-Forms dynamic schema engine, OpenXML DOCX placeholder extraction, Azure Blob upload, FormTemplates API và 22 unit tests. Xác minh ngày 29/09/2026 lúc 11:15 (Asia/Saigon).
**Trạng thái Core IAM: HOÀN THÀNH** — đã bàn giao lịch sử commit tách theo file lên `kha` và `deploy`; PostgreSQL Local Dev đã migrate và Swagger IAM đang chạy. Xác minh ngày 25/09/2026 lúc 10:13 (Asia/Saigon).
**Trạng thái Shared Kernel & Central Logging (TASK-05, TASK-06): HOÀN THÀNH** — đã triển khai đầy đủ Domain primitives, CQRS MediatR abstractions, Azure Blob Storage client wrapper, Serilog tập trung, RequestLoggingMiddleware và 39 unit tests cho SharedKernel. Xác minh ngày 26/09/2026 lúc 18:10 (Asia/Saigon).

**Trạng thái User Submissions API: HOÀN THÀNH** — đã thêm entity UserSubmission, enum SubmissionStatus, các command/query và 7 endpoints quản lý vòng đời hồ sơ người dân (draft, submit, request-revision, approve). Đã commit từng lớp và push lên `main`, `nghia`, `deploy`. Xác minh ngày 04/10/2026 lúc 21:30 (Asia/Saigon).
**Trạng thái DOC-003 Citizen/Officer API split + Dockerfile: HOÀN THÀNH** — đã tách UserSubmissionsController thành CitizenSubmissionsController (/api/v1/citizen/submissions) và OfficerSubmissionsController (/api/v1/officer/submissions), cập nhật Dockerfile Alpine + non-root user + HEALTHCHECK, thêm service document-form vào docker-compose.yml. Docker image `wardmate-document-form:local` 196MB, 0 errors. Xác minh ngày 05/10/2026 lúc 08:21 (Asia/Saigon).
**Trạng thái Fix SaveDraftSubmission 500: HOÀN THÀNH** — đã fix lỗi 500 trên `PUT /api/v1/citizen/submissions/{id}/draft` do `Azure.RequestFailedException` không được catch. Đã thêm try-catch quanh `UploadAsync`, thêm error `BlobUploadFailed`, và đổi thứ tự logic: kiểm tra DB trước rồi mới upload blob (tránh orphaned blobs). Build Release 0 errors 0 warnings. Xác minh ngày 07/10/2026 lúc 00:59 (Asia/Saigon).

---

## DOC-002 — User Submissions API (Hồ sơ người dân)

- Hoàn thành: **04/10/2026, 21:30 (Asia/Saigon, UTC+07:00)**
- Nhánh: `nghia`, `main`, `deploy`

### Chức năng hoàn thiện
- Thêm Entity `UserSubmission` và Enum `SubmissionStatus` (Draft, Submitted, RevisionRequested, Approved).
- Cấu hình EF Core `UserSubmissionConfiguration` với bảng `document.user_submissions`.
- Bổ sung Migration `AddUserSubmissions`.
- Application layer: `SaveDraftSubmissionCommand`, `SubmitSubmissionCommand`, `RequestRevisionCommand`, `ApproveSubmissionCommand`.
- Queries: `GetMySubmissionsQuery`, `GetSubmissionByIdQuery`, `DownloadSubmissionDocxQuery`.
- Controllers: `UserSubmissionsController` với 7 endpoints (GET list, GET detail, POST draft, PUT draft, POST submit, GET download-docx, POST request-revision, POST approve).

### Build & Test
- `dotnet build`: **0 errors, 0 warnings**

---

## FIX-001 — Fix DbUpdateConcurrencyException trên AddVersion endpoint

- Hoàn thành: **01/10/2026, 11:30 (Asia/Saigon, UTC+07:00)**
- Nhánh: `kha-feat-fix-addversion-concurrency` → merge vào `kha` → `main` → `deploy`

### Endpoint bị ảnh hưởng
- `POST /api/v1/form-templates/{templateId}/versions` → `201 Created`
- Request body: `{ "schemaDefinition": "<JSON string>" }`
- Response: `FormTemplateVersionDto` (id, templateId, versionNumber, schemaDefinition, createdAtUtc, updatedAtUtc)

### Root cause
EF Core với `.HasDefaultValue(true/false)` tự động đặt các property boolean (`IsActive`, `IsDeleted`) là `ValueGeneratedOnAdd`. Khi EF Core thực thi batch gồm UPDATE trên `FormTemplate` và INSERT trên `FormTemplateVersion`, nó chờ đọc lại giá trị từ database nhưng nhận 0 rows affected → `DbUpdateConcurrencyException`.

### Các thay đổi
| File | Thay đổi |
|------|---------|
| [`CreateFormTemplateVersionCommand.cs`](src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/CreateFormTemplateVersionCommand.cs) | Thay `template.AddVersion()` bằng 2 bước Save riêng: update FormTemplate → insert FormTemplateVersion qua DbSet trực tiếp |
| `FormTemplateConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsActive` và `IsDeleted` |
| `FormTemplateVersionConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `SupportingDocumentConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `ApplicationFormConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `GeneratedDocumentConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| Migrations/ | Thêm migration `AddSoftDeleteToDocumentForm` |

### Build & Test
- `dotnet build WardMate.sln -c Release`: **0 errors, 0 warnings**
- Manual test `POST .../versions` với schemaDefinition hợp lệ: **HTTP 201** ✅

---


## DOC-001 (TASK-10, TASK-11, TASK-12) — Thực thể Document, Engine E-Forms Schema & OpenXML Docx Placeholder

- Hoàn thành triển khai và kiểm thử: **29/09/2026, 11:15 (Asia/Saigon, UTC+07:00)**.
- Người phụ trách: **Nghĩa**.
- Nhánh tính năng: `nghia-feat-document-form`.
- Tổng số commit chi tiết: **35+ commit granular theo từng file/tính năng độc lập**.

### Chức năng hoàn thiện

1. **Thực thể Document & Form Service và Azure Storage (TASK-10)**:
   - Các thực thể Domain: `FormTemplate` (Aggregate Root), `FormTemplateVersion`, `ApplicationForm`, `SupportingDocument`, `GeneratedDocument` kế thừa `BaseEntity` chuẩn Clean Architecture.
   - Quản lý phiên bản schema `FormTemplateVersion` gắn với `FormTemplate`, hỗ trợ kích hoạt/vô hiệu hóa, cập nhật thông tin và URL phôi mẫu.
   - Upload file DOCX phôi mẫu lên Azure Blob Storage qua `IBlobStorageClient` (container `form-templates`) và lưu liên kết trong `form_templates`.
   - Cấu hình EF Core Fluent API đầy đủ ánh xạ tới schema `document` (`document.form_templates`, `document.form_template_versions`, `document.application_forms`, `document.supporting_documents`, `document.generated_documents`), cấu hình cột JSONB `schema_definition` và `form_data`.

2. **Engine Schema Định nghĩa Biểu mẫu động E-Forms (TASK-11)**:
   - Domain models: `FormSchemaDefinition`, `FormSection`, `FormField`, `FormFieldOption`, `FieldValidationRule`, `FormFieldType` (hỗ trợ 12 loại trường: Text, Number, Date, DateTime, Select, Radio, Checkbox, Textarea, NationalId/CCCD, PhoneNumber, Email, Currency).
   - `FormSchemaEngine`:
     - Phân tích và thẩm định cấu trúc Schema JSON: kiểm tra tính hợp lệ cú pháp, bắt buộc tiêu đề, section, chống trùng lặp `section_id` và `field_id`, kiểm tra quy tắc đặt tên trường `^[a-zA-Z0-9_]+$`, thẩm định trường select/radio phải có options không rỗng, kiểm tra biểu thức chính quy Regex tùy biến.
     - Kiểm tra dữ liệu biểu mẫu động nhập liệu (`ValidateFormData`): thẩm định bắt buộc (required), kiểm tra định dạng email chuẩn RFC, kiểm tra số điện thoại Việt Nam (đầu 0 hoặc +84), kiểm tra số định danh cá nhân / CCCD (9 hoặc 12 chữ số), kiểm tra giới hạn min/max độ dài chuỗi và giá trị số, kiểm tra giá trị chọn thuộc options cho phép, báo cáo danh sách lỗi chi tiết theo trường (`field_id`, `error_code`, `error_message`).

3. **Engine Bóc tách Placeholder từ File Word DOCX (TASK-12)**:
   - `DocxPlaceholderEngine` (sử dụng OpenXML SDK):
     - Bóc tách toàn bộ biến giữ chỗ dạng `{{ten_bien}}` từ Word document body, header và footer, xử lý triệt để hiện tượng phân mảnh văn bản XML (fragmented Run/Text elements) do kiểm tra chính tả/định dạng trong Word.
     - Chuẩn hóa tên placeholder (chữ thường, loại bỏ khoảng trắng), đếm số lần xuất hiện và xác định vị trí trong tài liệu.
     - So khớp danh sách placeholders bóc tách được với `FormSchemaDefinition` (`MatchPlaceholdersWithSchema`): tự động phát hiện các trường khớp hoàn toàn, các trường có trong DOCX nhưng thiếu trong Schema, và các trường có trong Schema nhưng không được sử dụng trong DOCX.
     - Tự động sinh cấu trúc Schema biểu mẫu dự thảo (`GenerateDraftSchema`): suy luận kiểu trường thông minh dựa trên tên biến (ví dụ `ngay_sinh` -> `Date`, `email` -> `Email`, `dien_thoai` -> `PhoneNumber`, `cccd`/`so_dinh_danh` -> `NationalId`, `so_tien`/`le_phi` -> `Currency`, `so_luong` -> `Number`) và tạo nhãn hiển thị trực quan.

4. **API Endpoints (Controllers & CQRS)**:
   - `GET /api/v1/form-templates`: Lấy danh sách biểu mẫu có phân trang (`page`, `pageSize`, `isActive`, `searchCode`).
   - `GET /api/v1/form-templates/{templateId}`: Xem chi tiết biểu mẫu kèm toàn bộ lịch sử các phiên bản schema.
   - `POST /api/v1/form-templates`: Tạo mới biểu mẫu (tùy chọn kèm schema phiên bản đầu tiên).
   - `POST /api/v1/form-templates/{templateId}/versions`: Tạo phiên bản schema mới cho biểu mẫu.
   - `POST /api/v1/form-templates/{templateId}/upload-docx`: Upload file phôi mẫu DOCX lên Azure Blob Storage, tự động bóc tách placeholder và so khớp với schema hiện tại.
   - `POST /api/v1/form-templates/extract-placeholders`: Bóc tách danh sách placeholder từ file DOCX tải lên mà không cần lưu.

5. **Tích hợp Infrastructure, DI & Swagger**:
   - Tích hợp `AddDocumentFormApplication()` và `AddDocumentFormInfrastructure()` vào `Program.cs`.
   - Tích hợp Serilog tập trung, RequestLoggingMiddleware, ExceptionHandling, OpenAPI SwaggerGen.
   - `appsettings.json` cấu hình kết nối PostgreSQL `wardmate_db` (schema `document`) và Azure Blob Storage.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Domain Models & Entities (TASK-10, 11) | `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/Entities/` (`FormTemplate.cs`, `FormTemplateVersion.cs`, `ApplicationForm.cs`, `SupportingDocument.cs`, `GeneratedDocument.cs`), `Models/` (`FormFieldType.cs`, `FormFieldOption.cs`, `FieldValidationRule.cs`, `FormField.cs`, `FormSection.cs`, `FormSchemaDefinition.cs`), `Errors/DocumentFormErrors.cs` |
| Application Layer (TASK-10, 11, 12) | `WardMate.Services.DocumentForm.Application/Commands/` (`CreateFormTemplateCommand.cs`, `CreateFormTemplateVersionCommand.cs`, `UploadFormTemplateDocxCommand.cs`), `Queries/` (`GetFormTemplatesQuery.cs`, `GetFormTemplateByIdQuery.cs`, `ExtractDocxPlaceholdersQuery.cs`), `DTOs/` (`FormTemplateDto.cs`, `FormTemplateDetailDto.cs`, `FormTemplateVersionDto.cs`), `Models/` (`DocxPlaceholder.cs`, `PlaceholderValidationResult.cs`, `FormDataValidationError.cs`), `Interfaces/` (`IDocumentDbContext.cs`, `IFormSchemaEngine.cs`, `IDocxPlaceholderEngine.cs`), `Services/FormSchemaEngine.cs`, `DependencyInjection.cs`, `.csproj` |
| Infrastructure Layer (TASK-10, 12) | `WardMate.Services.DocumentForm.Infrastructure/Persistence/` (`DocumentDbContext.cs`, `DocumentDbContextFactory.cs`, `Configurations/` cho 5 entities), `OpenXml/DocxPlaceholderEngine.cs`, `DependencyInjection.cs`, `.csproj` |
| API Layer | `WardMate.Services.DocumentForm.API/Controllers/FormTemplatesController.cs`, `Program.cs`, `appsettings.json`, `.csproj` |
| Unit Tests Mới | `tests/WardMate.Services.DocumentForm.Tests/` — `FormTemplateEntityTests.cs`, `FormSchemaEngineTests.cs`, `DocxPlaceholderEngineTests.cs`, `WardMate.Services.DocumentForm.Tests.csproj` |
| Solution | `WardMate.sln`, `PROGRESS.md` |

### Kết quả kiểm thử

- `dotnet build -c Release`: **0 errors, 0 warnings** trên toàn bộ 16 projects trong solution.
- **22 unit tests mới cho DocumentForm Service — 100% PASS**: kiểm thử đầy đủ Entity lifecycle & soft delete, schema validation, form data validation, regex constraints, OpenXML placeholder extraction, schema matching, và draft schema generation.
- **39 unit tests SharedKernel — 100% PASS**.
- **78 unit tests IAM — 100% PASS**.
- Tổng số unit tests pass: **139 passed, 0 failed, 0 skipped**.


## SHARED-001 (TASK-05 & TASK-06) — Xây dựng Shared Kernel, Azure Blob Client & Logging tập trung

- Hoàn thành triển khai và kiểm thử: **26/09/2026, 18:10 (Asia/Saigon, UTC+07:00)**.
- Người phụ trách: **Nghĩa**.
- Nhánh tính năng: `nghia-feat-shared-kernel-logging`.
- Tổng số commit chi tiết: **35+ commit theo từng file/chức năng độc lập**.

### Chức năng hoàn thiện

1. **Domain Primitives (TASK-05)**:
   - `IDomainEvent`: Marker interface cho domain events với `EventId` và `OccurredOnUtc`.
   - `BaseEntity`: Base class cho entity với UUID `Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted`, `DeletedAtUtc`, quản lý pending domain events (`RaiseDomainEvent`, `ClearDomainEvents`) và helper methods `SetCreated`, `SetUpdated`, `SoftDelete`.
   - `AggregateRoot`: Kế thừa `BaseEntity`, hỗ trợ optimistic concurrency qua thuộc tính `uint Version`.
   - `ValueObject`: Base class cho immutable value objects với structural equality (`GetEqualityComponents`, `Equals`, `GetHashCode`, `==`, `!=`).

2. **Common Railway-Oriented Programming & Pagination (TASK-05)**:
   - `Result` và `Result<TValue>`: Discriminated-union result type giúp tránh throw exception cho predictable business failures, hỗ trợ implicit conversion từ `Error` hoặc `TValue`.
   - `Error` & `ErrorType`: Structured error record gồm `Code`, `Description`, `ErrorType` (`Failure`, `NotFound`, `Validation`, `Conflict`, `Unauthorized`) và các static factory helper methods.
   - `PagedResult<T>`: Hỗ trợ pagination metadata (`Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`, `HasPreviousPage`) và factory `Create`, `Empty`.

3. **CQRS & MediatR Markers (TASK-05)**:
   - `ICommand` (trả về `Result`) & `ICommand<TResponse>` (trả về `Result<TResponse>`).
   - `ICommandHandler<TCommand>` & `ICommandHandler<TCommand, TResponse>`.
   - `IQuery<TResponse>` & `IQueryHandler<TQuery, TResponse>`.

4. **Azure Blob Storage Wrapper (TASK-05)**:
   - `BlobStorageOptions`: Cấu hình Azure Blob (`ConnectionString`, `ContainerName`, `MaxFileSizeBytes`).
   - `IBlobStorageClient`: Interface cho blob operations (`UploadAsync`, `DownloadAsync`, `DeleteAsync`, `ExistsAsync`, `GenerateSasUri`).
   - `AzureBlobStorageClient`: Triển khai với Azure.Storage.Blobs SDK, tự động khởi tạo container nếu chưa có, hỗ trợ content-type detection và SAS token generation.
   - `BlobServiceExtensions.AddAzureBlobStorage`: Extension method đăng ký `BlobServiceClient` (Singleton) và `IBlobStorageClient` (Scoped) vào DI container.

5. **Logging tập trung & Request Tracking Middleware (TASK-06)**:
   - `SerilogOptions`: Cấu hình Serilog từ appsettings (`MinimumLevel`, `EnableConsoleSink`, `EnableFileSink`, `LogFilePath`, `FileSizeLimitMb`, `RetainedFileCountLimit`, `ApplicationName`).
   - `SerilogExtensions`:
     - `AddWardMateLogging`: Bootstrap Serilog với Console sink (Compact JSON trong Production, readable console trong Development), Rolling File sink với 50MB limit và 7 ngày retention, tự động enrich `Application`, `Environment`, `ThreadId`, `ProcessId`.
     - `UseWardMateRequestLogging`: Tích hợp HTTP request logging của Serilog.
   - `RequestLoggingMiddleware`: Custom middleware ghi log chi tiết HTTP request/response (`Method`, `Path`, `QueryString`, `StatusCode`, `ElapsedMs`, `ClientIp`, `TraceId`), tự động phân loại LogLevel theo HTTP status (2xx/3xx -> Info, 4xx -> Warning, 5xx -> Error) và bỏ qua các đường dẫn nội bộ (`/health`, `/favicon.ico`).
   - Tích hợp Bootstrap Serilog và RequestLoggingMiddleware vào IAM Service `Program.cs` và cấu hình mẫu trong `appsettings.json`.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Domain Primitives | `src/BuildingBlocks/WardMate.SharedKernel/Domain/IDomainEvent.cs`, `BaseEntity.cs`, `AggregateRoot.cs`, `ValueObject.cs` |
| Common & CQRS | `src/BuildingBlocks/WardMate.SharedKernel/Common/Result.cs`, `Error.cs`, `PagedResult.cs`, `CQRS/ICommand.cs`, `ICommandHandler.cs`, `IQuery.cs`, `IQueryHandler.cs` |
| Azure Blob Storage | `src/BuildingBlocks/WardMate.SharedKernel/Blob/BlobStorageOptions.cs`, `IBlobStorageClient.cs`, `AzureBlobStorageClient.cs`, `BlobServiceExtensions.cs` |
| Central Logging | `src/BuildingBlocks/WardMate.SharedKernel/Logging/SerilogOptions.cs`, `SerilogExtensions.cs`, `RequestLoggingMiddleware.cs` |
| Project Configuration | `src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj`, `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`, `appsettings.json`, `global.json` |
| Unit Tests mới | `tests/WardMate.SharedKernel.Tests/` — `ResultTests.cs`, `ErrorTests.cs`, `PagedResultTests.cs`, `BaseEntityTests.cs`, `ValueObjectTests.cs`, `BlobStorageOptionsTests.cs`, `SerilogOptionsTests.cs`, `RequestLoggingMiddlewareTests.cs`, `AggregateRootTests.cs`, `CqrsMarkerTests.cs`, `BlobServiceExtensionsTests.cs` |

### Kết quả kiểm thử

- `dotnet build -c Release`: **0 errors, 0 warnings** trên toàn bộ 15 project trong solution.
- **39 unit tests mới cho SharedKernel — 100% PASS**: kiểm thử đầy đủ Result pattern, Error, PagedResult calculation, BaseEntity domain events & soft delete, ValueObject equality, Serilog options, Blob options, MediatR pipeline integration, DI registration, và TestServer middleware execution.
- **32 unit tests IAM hiện có — 100% PASS**.
- Tổng số unit test pass: **71 passed, 0 failed, 0 skipped**.


## IAM-001 — Core IAM Service & API xác thực

- Hoàn thành triển khai và kiểm thử: **25/09/2026, 09:57 (Asia/Saigon, UTC+07:00)**.
- Nhánh tính năng: `kha-feat-iam-auth`; trình tự bàn giao: feature → `kha` → `deploy`.
- Khởi tạo `kha` từ `origin/main` vì remote chưa có nhánh `kha`; nhánh `deploy` cũng chưa tồn tại khi bắt đầu task.

### Chức năng hoàn thiện

- 7 entity và Fluent API: User, UserProfile, Role, Permission, UserRole, RolePermission, RefreshToken; khóa ngoại chỉ trong `wardmate_iam_db`.
- Migration `InitialIam`, tự migrate khi API khởi động; seed 5 role yêu cầu, 2 permission mẫu và 6 role-permission. Không seed mật khẩu/tài khoản quản trị mặc định.
- BCrypt cost 12; JWT HS256 chứa `sub`, `email`, `role`, `permissions`; refresh token sinh từ 64 byte ngẫu nhiên, chỉ lưu SHA-256 digest trong database.
- MediatR CQRS, FluentValidation, Result Pattern cho register/login/refresh/revoke/me. Controller chỉ nhận request, lấy subject từ JWT và gọi Application.
- Chặn tài khoản inactive, trùng username/email, sai chủ sở hữu token, token hết hạn/đã thu hồi. Refresh rotation và insert token mới nằm trong một giao dịch; concurrent refresh thất bại không để lại token thay thế.
- Swagger OpenAPI, bearer security cho endpoint authorized, ProblemDetails và `traceId`; thêm route IAM versioned qua YARP.
- Lưu workflow dài hạn trong `AGENTS.md`; CI build/test bằng GitHub Actions cho feature, `kha`, `deploy`.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Workflow/tài liệu | `AGENTS.md`, `PROGRESS.md`, `README.md`, `docs/iam-auth.md`, `.github/workflows/iam-ci.yml` |
| Solution/tooling | `WardMate.sln`, `.config/dotnet-tools.json` |
| PostgreSQL local | `docker/docker-compose.yml`, `docker/.env.example` |
| Gateway | `src/Gateways/WardMate.YarpGateway/appsettings.json` |
| Domain mới | `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/Entities/` (7 entity), `RoleNames.cs` |
| Application mới | `WardMate.Services.IAM.Application/Commands/`, `Queries/`, `DTOs/`, `Interfaces/`, `Common/`, `Validation/`, `DependencyInjection.cs`; cập nhật `.csproj` |
| Infrastructure mới | `WardMate.Services.IAM.Infrastructure/Persistence/` (context, factory, store, configurations, migration/snapshot), `Security/` (BCrypt/JWT/options), `DependencyInjection.cs`; cập nhật `.csproj` |
| API | `WardMate.Services.IAM.API/Controllers/`, `Errors/`, `OpenApi/`; cập nhật `Program.cs`, `appsettings.json`, `.csproj` |
| Unit tests mới | `tests/WardMate.Services.IAM.Tests/` — `AuthFlowTests.cs`, `SecurityTests.cs`, `TestIdentityStore.cs`, `.csproj` |
| Integration tests mới | `tests/WardMate.Services.IAM.IntegrationTests/` — `IamApiTests.cs`, `IamFixture.cs`, `.csproj` |

Các đường dẫn `WardMate.Services.IAM.*` trong bảng nằm dưới `src/Services/WardMate.Services.IAM/`. Không triển khai nghiệp vụ cho năm service khác.

### API mới

Base URL local: gateway `http://localhost:5000`, IAM trực tiếp `http://localhost:5001`.

| Method | Route | Request body | Response codes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | `{"username":"citizen","email":"citizen@example.test","password":"StrongPassword123!","fullName":"Nguyen Van A"}` | 201 CurrentUserDto; 400 validation; 409 duplicate |
| POST | `/api/v1/auth/login` | `{"usernameOrEmail":"citizen","password":"StrongPassword123!"}` | 200 AuthResponseDto; 400 validation; 401 invalid/inactive |
| POST | `/api/v1/auth/refresh-token` | `{"accessToken":"...","refreshToken":"..."}` | 200 AuthResponseDto; 400 validation; 401 invalid/expired/revoked |
| POST | `/api/v1/auth/revoke-token` | `{"refreshToken":"..."}` + Bearer token | 204; 400 validation; 401 invalid/not owned/unauthenticated |
| GET | `/api/v1/users/me` | Không có body; Bearer token | 200 CurrentUserDto; 401 unavailable/unauthenticated |

Lỗi bất ngờ trả 500 ProblemDetails đã loại bỏ thông tin nội bộ.

### Kết quả kiểm thử

- `dotnet build WardMate.sln -c Release --no-restore`: **0 errors, 0 warnings**.
- **32 unit test cases mới — PASS**: hash/salt/verify, giới hạn UTF-8 BCrypt, JWT claims/signature/audience, đăng ký/validation/trùng dữ liệu, đăng nhập, inactive account, refresh/rotation/reuse/ownership/concurrency outcome, logout và me.
- **8 integration tests mới — PASS** trên PostgreSQL 16 dùng Testcontainers: tự migrate/seed và chạy lại idempotent; lifecycle/ownership; validation/duplicate; giao dịch refresh cạnh tranh và rollback; expired access/refresh; đăng ký đồng thời; inactive account; Swagger endpoints/bearer metadata.
- **3 test cases SharedKernel hiện có — PASS**.
- Toàn bộ solution: **43 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations has-pending-model-changes`: không có thay đổi model chưa được migration hóa.
- Lần thử integration ban đầu chưa chạy được vì Docker tắt; đã khởi động Docker và chạy lại đầy đủ thành công. Không bỏ qua test để merge.
- Container/database kiểm thử tạm đã được Testcontainers dọn sau khi chạy; không dùng database dev của người dùng.

### Hướng dẫn Frontend

- Contract chi tiết, ví dụ DTO và cấu hình: [docs/iam-auth.md](docs/iam-auth.md).
- JSON camelCase. Register trả CurrentUserDto và chưa đăng nhập; gọi login để nhận AuthResponseDto.
- AuthResponseDto gồm `accessToken`, `accessTokenExpiresAt`, `refreshToken`, `refreshTokenExpiresAt`, `tokenType`. Hạn mặc định: access 15 phút, refresh 7 ngày; thời gian UTC.
- Gửi `Authorization: Bearer <accessToken>` cho me/revoke. Sau refresh phải thay cả hai token, chỉ cho phép một refresh đang xử lý trong một phiên. Access token đã hết hạn chỉ được sử dụng ở request refresh cùng refresh token còn hợp lệ.
- Logout thu hồi refresh token được gửi, không blacklist ngay access token; access token cũ hết hiệu lực tự nhiên theo thời hạn. Refresh 401 → xóa phiên, yêu cầu đăng nhập lại.
- ProblemDetails có `code`, `traceId`; validation có `errors` theo field. Mã: `validation_failed`, `iam.duplicate_account`, `iam.invalid_credentials`, `iam.invalid_token`, `iam.user_unavailable`, `iam.unauthorized`. Không dựa vào chuỗi tiếng Anh để điều khiển UI.
- Chỉ cấp `REGISTERED_CITIZEN` khi đăng ký. `roles`/`permissions` trong DTO và JWT là mảng; `/me` đọc quyền hiện tại từ database.
- Frontend dùng same-origin proxy hoặc cấu hình CORS theo môi trường triển khai. Task này chưa thêm CORS/cookie authentication.
- Push `deploy` là bước bàn giao Git được yêu cầu. Workflow mới xác minh backend build/tests; không đồng nghĩa đã xác minh một pipeline deploy frontend bên ngoài repository.

## GIT-001 — Tách commit IAM theo từng file và lưu quy ước mới

- Hoàn thành tách lịch sử local: **25/09/2026, 10:03 (Asia/Saigon, UTC+07:00)**.
- Đã xác nhận commit tổng `25b1e72` được push lên `origin/kha` trước khi nhận yêu cầu tách commit. Chưa merge/push sang `deploy`.
- Trên `kha-feat-iam-auth`, thay commit tổng bằng **59 commit**, mỗi commit thay đổi đúng một file. Đối chiếu Git tree sau khi tách: giống hoàn toàn Git tree của `25b1e72`.
- Thay đổi bổ sung: `AGENTS.md` lưu quy ước commit mới; `PROGRESS.md` ghi nhật ký lần tách này. Mỗi cập nhật tài liệu được commit riêng.
- Quy ước từ nay: mỗi file tạo mới một commit; mỗi cập nhật chức năng một commit riêng; không squash các commit khi merge.
- API mới: không có. DTO, mã lỗi và cách xử lý token không thay đổi so với IAM-001.
- Test mới: 0. Giữ nguyên kết quả của đúng nội dung mã nguồn đã kiểm chứng: **43 passed, 0 failed**, build **0 errors, 0 warnings**. Chỉ tách lịch sử và sửa tài liệu nên không chạy lại suite.
- Bàn giao: nhánh tính năng chứa lịch sử đã tách; việc thay lịch sử đã công bố trên `kha` và tiếp tục sang `deploy` đang chờ quyết định của người dùng. Chưa force push lên nhánh chung.

## IAM-001 / LOCAL-001 — Hoàn tất bàn giao và khởi động Local Dev

- Hoàn thành xác minh: **25/09/2026, 10:13 (Asia/Saigon, UTC+07:00)**.
- Người dùng đã chấp thuận rõ ràng việc dùng `--force-with-lease` cho lần thay lịch sử `kha` này. Đã cập nhật `origin/kha` từ `25b1e72` sang lịch sử đã tách tại `43c57ac`, với lease kiểm tra đúng hash cũ. Đã merge fast-forward `kha` vào `deploy` và push `origin/deploy` thành công. Phần chờ quyết định tại GIT-001 đã được giải quyết.
- Cập nhật file được commit riêng: `PROGRESS.md`. Không thay đổi source code, DTO hoặc endpoint. File `docker/.env` được tạo riêng cho máy local với mật khẩu database và JWT key ngẫu nhiên; file này bị gitignore và không được commit.
- `docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --wait iam-db` đã chạy thành công. Container `wardmate-iam-db-1` healthy, PostgreSQL 16 tại `127.0.0.1:5433`, database `wardmate_iam_db`, dữ liệu lưu trong volume `wardmate_iam-data`.
- Đã khởi động IAM API ở cổng **5001** và YARP Gateway ở cổng **5000** trong môi trường Development, giữ các tiến trình chạy nền để người dùng test.
- Đã kiểm tra trực tiếp database: migration **`20260925024711_InitialIam`** có trong `__EFMigrationsHistory`; đủ **7 bảng nghiệp vụ**, **5 roles**, **2 permissions**, **6 role-permission assignments**.
- **Swagger UI trực tiếp IAM Service:** <http://localhost:5001/swagger/index.html> — đã kiểm tra HTTP 200. OpenAPI JSON: <http://localhost:5001/swagger/v1/swagger.json>. Gateway hiện không có route Swagger UI riêng.
- **API qua Gateway:** `http://localhost:5000/api/v1/auth/register`, `http://localhost:5000/api/v1/auth/login` và các endpoint IAM còn lại giữ nguyên đường dẫn. Swagger tại cổng 5001 gọi trực tiếp IAM Service.
- Smoke checks local đã pass: `/api/iam/health` qua gateway trả Healthy; `/api/v1/users/me` không token trả 401; register dữ liệu sai trả 400; login tài khoản không tồn tại trả 401. Không tạo tài khoản mẫu trong database dev.
- Tests mới: 0; giữ kết quả của source code không đổi: **43 tests passed**, build **0 errors, 0 warnings**. Các kiểm tra local bổ sung đã xác nhận Docker, migration và HTTP runtime.
- Log của các host local được ghi trong thư mục `%TEMP%/wardmate-local-dev`. Secrets trong `.env` chỉ được nạp vào môi trường của các tiến trình local; không ghi vào source hoặc nhật ký.
- Frontend có thể test đăng ký/đăng nhập ngay bằng Swagger cổng 5001 hoặc gọi API qua gateway cổng 5000. Đăng ký thành công trả 201; tiếp tục login để lấy access/refresh token; nhấn Authorize trên Swagger với access token để thử `/users/me`.
- Trạng thái bàn giao được xác nhận ở cấp Git và Local Dev. Không suy diễn việc push `deploy` thành công thành xác nhận một hệ thống triển khai frontend bên ngoài đã chạy thành công.

## DEPLOY-001 — Chuẩn bị triển khai Azure Portal, cập nhật chuỗi merge và đóng gói Docker

- Hoàn thành xác minh: **25/09/2026, 15:50 (Asia/Saigon, UTC+07:00)**.
- Đã xóa nhánh remote mồ côi `develop` trên GitHub; giữ lại nhánh tính năng `kha-feat-iam-auth` theo yêu cầu người dùng để phục vụ kiểm thử thủ công.
- Cập nhật quy tắc workflow trong `AGENTS.md`: trình tự merge bắt buộc qua `main` trước khi sang nhánh `deploy` (`feature` → `kha` → `main` → `deploy`), phục vụ Azure Continuous Deployment cho Frontend kết nối API trực tiếp.
- Bổ sung Dockerfile đa tầng (.NET 8) cho `WardMate.YarpGateway` và `WardMate.Services.IAM.API`, bổ sung `.dockerignore` ở root. Đã xác minh `docker build` thành công cả 2 image với 0 lỗi, 0 cảnh báo.
- Bảo mật cấu hình pgAdmin trong `docker/docker-compose.yml` và `docker/.env.example` qua biến môi trường, không để lộ plain credentials.
- Biên soạn tài liệu chi tiết [docs/azure-deployment.md](docs/azure-deployment.md) hướng dẫn toàn bộ quy trình thiết lập Azure Resource Group, ACR, Azure PostgreSQL Flexible Server, Azure Container Apps (Internal IAM + External Gateway) và tích hợp GitHub Actions Continuous Deployment từ nhánh `deploy`.


## IAM-002 — Omit null response fields and enforce registration password strength

- Completed: **2026-09-25 17:13 (Asia/Saigon, UTC+07:00)**.
- Controllers use `JsonIgnoreCondition.WhenWritingNull`. Optional profile fields are omitted when null; populated fields and empty strings remain present.
- Registration requires at least 8 characters, one uppercase letter and one special character (Unicode punctuation or symbol; whitespace does not count). Retained the BCrypt limit of 72 UTF-8 bytes. Login validation remains compatible with existing passwords.
- Changed files:
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs`
  - `tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs`
  - `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
  - `docs/iam-auth.md`
  - `PROGRESS.md`
- No new endpoints. Affected contracts:
  - `POST /api/v1/auth/register`: body `{ "username": "citizen", "email": "citizen@example.test", "password": "Abcdefg!", "fullName": "Nguyen Van A" }`; 201 CurrentUserDto with null fields omitted; 400 validation ProblemDetails; 409 duplicate account.
  - `GET /api/v1/users/me`: no request body, Bearer token required; 200 CurrentUserDto with null fields omitted; 401 unauthorized.
- Validation: solution Release build succeeded with **0 errors, 0 warnings**. Full test suite **57/57 passed, 0 failed, 0 skipped**: 41 IAM unit tests, 3 SharedKernel tests, 13 PostgreSQL integration tests. Added 9 unit cases and 5 integration cases; existing registration helper also checks raw JSON omission. Initial integration run exposed a double-read of the response stream in the new test helper; fixed before the successful complete rerun.
- Frontend: optional DTO properties may be absent; apply the same registration password rules client-side. Invalid passwords return HTTP 400, `code: validation_failed`, and `errors.password`. Token handling and endpoint routes are unchanged.
- Handoff: implementation remains uncommitted in the existing `deploy` working tree. No checkout, merge, commit or push performed; Antigravity handles Git publishing per the user's explicit instruction. Deployment/runtime restart has not been performed by this task.

## IAM-003 — Việt hóa thông báo validation và lỗi API

- Hoàn thành: **2026-09-25 17:26 (Asia/Saigon, UTC+07:00)**.
- Việt hóa toàn bộ quy tắc FluentValidation hiện có: đăng ký, đăng nhập, làm mới và thu hồi token. Giữ nguyên điều kiện kiểm tra dữ liệu.
- Việt hóa tiêu đề validation, lỗi nghiệp vụ IAM, lỗi body thiếu/sai định dạng, thông báo HTTP mặc định của IAM và lỗi hệ thống trong SharedKernel. Log kỹ thuật nội bộ không thuộc thông báo giao diện người dùng.
- File thay đổi:
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Common/Result.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Errors/ValidationExceptionHandler.cs`
  - `src/BuildingBlocks/WardMate.SharedKernel/Web/GlobalExceptionHandler.cs`
  - `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
  - `docs/iam-auth.md`
  - `PROGRESS.md`
- Không thêm endpoint. Các route và body giữ nguyên: POST `/api/v1/auth/register` (username, email, password, fullName; 201/400/409), POST `/api/v1/auth/login` (usernameOrEmail, password; 200/400/401), POST `/api/v1/auth/refresh-token` (accessToken, refreshToken; 200/400/401), POST `/api/v1/auth/revoke-token` (refreshToken; 204/400/401), GET `/api/v1/users/me` (không body; 200/401).
- Kiểm thử: Release solution build **0 errors, 0 warnings**; **61/61 tests PASS**, 0 failed, 0 skipped (41 IAM unit, 3 SharedKernel, 17 integration PostgreSQL). Thêm 4 integration cases kiểm tra body lỗi và thông báo xác thực tiếng Việt; bổ sung assertion tiếng Việt vào test validation hiện có. Không thêm unit case mới.
- Frontend: hiển thị `title` và `errors` tiếng Việt; vẫn phân nhánh theo HTTP status và `code`, không so sánh nội dung thông báo. Tên trường DTO, token handling và cơ chế bỏ trường null giữ nguyên.
- Bàn giao: các thay đổi đã được commit riêng từng file theo quy ước, kiểm thử toàn bộ giải pháp 61/61 tests PASS, merge theo chuỗi kha-feat-iam-vietnamese-messages → kha → main → deploy và push lên origin.

## IAM-004 — JWT, refresh token, CRUD hồ sơ và RBAC/quản lý tài khoản

- Hoàn thành: **2026-09-26 08:56 (Asia/Saigon, UTC+07:00)**.
- REGISTERED_CITIZEN CRUD hồ sơ của mình; IT_ADMIN CRUD mọi hồ sơ, xem danh sách/chi tiết tài khoản và khóa/mở khóa tài khoản. Xóa hồ sơ không xóa tài khoản. PUT thay toàn bộ hồ sơ; POST tạo lại sau khi xóa.
- Authorization middleware dùng policy và quyền hiện tại trong database. Từ chối JWT của tài khoản bị khóa; thu hồi refresh token trong cùng transaction khóa tài khoản. Cấp token được tuần tự hóa bằng khóa hàng user để tránh race với thao tác khóa. Không thêm API công khai sửa/đọc trực tiếp refresh_tokens.
- Migration mới `20260926014941_ProfileWritePermission` thêm quyền ghi hồ sơ và seed cho 5 vai trò, tổng cộng 3 permissions/11 role-permission assignments. Đã chạy migration thật trên PostgreSQL tạm khi integration tests khởi động; chưa áp dụng vào Local Dev database hoặc Azure trong task này.
- Endpoint mới (đều yêu cầu Bearer):
  - GET/POST/PUT/DELETE `/api/v1/users/me/profile`: quyền đọc/ghi hồ sơ cá nhân; GET 200/401/403/404; POST 201/400/401/403/404/409; PUT 200/400/401/403/404/409; DELETE 204/401/403/404/409.
  - GET/POST/PUT/DELETE `/api/v1/users/{userId}/profile`: `iam.manage`; body/status tương tự route cá nhân.
  - POST/PUT hồ sơ nhận `{ fullName, identityNumber?, phoneNumber?, dateOfBirth?, gender?, permanentAddress?, temporaryAddress? }`; GET/DELETE không body.
  - GET `/api/v1/accounts?page=1&pageSize=20`: `iam.manage`, không body; 200/400/401/403; phân trang tối đa 100 items.
  - GET `/api/v1/accounts/{userId}`: `iam.manage`, không body; 200/401/403/404.
  - PUT `/api/v1/accounts/{userId}/status`: `iam.manage`, body `{ "isActive": false }` khóa, true mở; 204/400/401/403/404/409. Không cho tự khóa tài khoản quản trị đang dùng.
- Kiểm thử cuối: Release solution build **0 errors, 0 warnings**; **81/81 tests PASS**, 0 failed/skipped: 54 IAM unit, 3 SharedKernel, 24 integration PostgreSQL. Thêm 13 unit cases và 7 integration cases (CRUD, IDOR, quản trị, quyền thay đổi, validation/trùng số định danh, race khóa/cấp token, Swagger); cập nhật test migration hiện có. Build trung gian phát hiện nullable assertion/overload test và đã sửa trước lượt kiểm tra cuối.
- Frontend: thông báo tiếng Việt; giữ error codes cũ, bổ sung `iam.forbidden`, `iam.profile_not_found`, `iam.user_not_found`, `iam.profile_exists`, `iam.profile_conflict`, `iam.profile_changed`, `iam.self_disable`. Sau xóa hồ sơ, `/users/me` không có thuộc tính profile; 401 yêu cầu đăng nhập lại, 403 thiếu quyền. Refresh token bị thu hồi không phục hồi khi mở khóa; JWT còn hạn có thể hoạt động trở lại khi tài khoản được mở khóa. Xem `docs/iam-profiles-rbac.md` cho DTO, validation, migration và ví dụ đầy đủ.
- Gateway thêm route accounts; đường dẫn users hiện có bao phủ hồ sơ. Chưa kiểm thử runtime Gateway hoặc xác nhận triển khai Azure; test API dùng WebApplicationFactory và PostgreSQL thật.
- Bàn giao: đã commit tách riêng 29 commit cho từng file theo quy ước, kiểm thử toàn bộ giải pháp 81/81 tests PASS, merge theo chuỗi kha-feat-iam-profiles-rbac → kha → main → deploy và push lên origin.
- Các file tạo mới/thay đổi:
- `docs/iam-auth.md`
- `docs/iam-profiles-rbac.md`
- `PROGRESS.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/PermissionAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AccountsController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AdminProfilesController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/ProfilesController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/UsersController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Accounts/AccountRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/DTOs/IdentityDtos.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Interfaces/IProfileStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileValidators.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/PermissionCodes.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/AccountStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Configurations/IdentityConfigurations.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/IdentityStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926014941_ProfileWritePermission.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926014941_ProfileWritePermission.Designer.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/IamDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/ProfileStore.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs`
- `tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs`
- `tests/WardMate.Services.IAM.Tests/ProfileValidationTests.cs`

## DOCKER-002 — Cấu hình Local Dev để người dùng tự build

- Hoàn thành cấu hình: **2026-09-26 09:59 (Asia/Saigon, UTC+07:00)**.
- Sửa `docker/docker-compose.yml`: thêm build/service IAM và Gateway; kết nối nội bộ iam-db:5432 và iam-api:8080; bật Development/Swagger và AutoMigrate cho IAM; mở cổng localhost 5001/5000; pgAdmin dùng mật khẩu bắt buộc từ môi trường và bind localhost 5050.
- Sửa `docker/.env.example`: thêm JWT_KEY. Cập nhật `docker/.env` local (gitignored), giữ mật khẩu database/JWT đã có, tạo ngẫu nhiên mật khẩu pgAdmin còn thiếu; không ghi giá trị secrets vào tài liệu.
- Tạo `docs/docker-local.md`; cập nhật `PROGRESS.md`.
- Không thêm/thay đổi API endpoint hoặc DTO; token/ProblemDetails giữ nguyên. Frontend gọi qua localhost:5000; Swagger trực tiếp localhost:5001/swagger.
- Kiểm tra `docker compose --env-file docker/.env -f docker/docker-compose.yml config --quiet`: PASS. Không thêm unit tests. Không chạy build, test suite, migration, tải image hoặc khởi động container theo yêu cầu người dùng; runtime chưa được xác minh.
- Không commit/push; người dùng tự chạy lệnh Compose trong tài liệu, Antigravity xử lý Git.

## IAM-005 — Hoàn thiện API quản trị RBAC

- Hoàn thành: **2026-09-26 10:28 (Asia/Saigon, UTC+07:00)**.
- Thêm nhóm Swagger Rbac gồm 13 operations: CRUD vai trò tùy chỉnh, danh sách quyền, xem/gán/thu hồi quyền vai trò, xem/gán/thu hồi vai trò người dùng, đọc audit phân trang.
- Giữ tên và bảo vệ xóa 5 vai trò hệ thống; vai trò đang được gán phải thu hồi trước khi xóa. Không cho thu hồi iam.manage khỏi IT_ADMIN. Chỉ IT_ADMIN đang hoạt động có iam.manage được quản trị RBAC, dựa vào database hiện tại chứ không dùng role claim cũ.
- Bảo vệ admin cuối cùng trong cả thu hồi vai trò và khóa tài khoản. Các mutation dùng chung PostgreSQL transaction advisory lock và kiểm tra lại actor sau khi lấy khóa, bảo vệ giữa nhiều instance và các request đồng thời. Audit được ghi cùng transaction; lỗi rollback toàn bộ. PUT/DELETE liên kết idempotent, không ghi audit lặp cho no-op.
- Migration `20260926032128_RbacAdministration`: bảng rbac_audit_logs (schema hiện có 8 entity), index thời gian/ID và đồng bộ role ID sequence. Đã áp dụng vào PostgreSQL tạm qua test startup; chưa áp dụng trực tiếp vào Local Dev/Azure. Không build image hoặc khởi động stack ứng dụng.
- Endpoints mới, prefix `/api/v1/rbac`, tất cả yêu cầu Bearer + policy IT_ADMIN:
  - GET `/roles?page=1&pageSize=20`: không body; 200 RbacPage<RoleDto>, 400/401/403.
  - GET `/roles/{roleId}`: không body; 200 RoleDto, 401/403/404.
  - POST `/roles`: `{ "roleName": "DOCUMENT_REVIEWER", "description": "Nhân viên kiểm tra hồ sơ" }`; 201 RoleDto + Location, 400/401/403/409.
  - PUT `/roles/{roleId}`: cùng body POST; 200 RoleDto, 400/401/403/404/409.
  - DELETE `/roles/{roleId}`: không body; 204, 400/401/403/404/409.
  - GET `/permissions`: không body; 200 PermissionDto[], 401/403.
  - GET `/roles/{roleId}/permissions`: không body; 200 PermissionDto[], 401/403/404.
  - PUT/DELETE `/roles/{roleId}/permissions/{permissionId}`: không body; 204, 400/401/403/404/409.
  - GET `/users/{userId}/roles`: không body; 200 RoleDto[], 401/403/404.
  - PUT/DELETE `/users/{userId}/roles/{roleId}`: không body; 204, 400/401/403/404/409.
  - GET `/audit-logs?page=1&pageSize=20`: không body; 200 RbacPage<AuditDto>, 400/401/403.
- Endpoint cũ PUT `/api/v1/accounts/{userId}/status` bổ sung lỗi 409 iam.last_admin; actor hết quyền trong lúc chờ transaction trả 403. Gateway có route mới `/api/v1/rbac/{**catch-all}`.
- Kiểm thử: **115/115 PASS**, 0 failed/skipped: 78 IAM unit, 3 SharedKernel, 34 integration PostgreSQL. Thêm 24 unit cases và 10 integration cases; cập nhật kiểm tra schema/migration và account store. Release toàn solution **0 errors, 0 warnings**. Kiểm tra schema Swagger có đủ 13 operations và Bearer; test concurrent revoke/disable giữ ít nhất một admin; test negative không tạo audit hoặc ghi dữ liệu dở dang.
- Frontend: tên role chuẩn hóa IN HOA; RoleDto có isSystem và permissions. PageSize 1–100. Danh mục quyền được mở rộng cùng chức năng backend; không có CRUD tùy ý permission code. Thông báo tiếng Việt, mã lỗi/DTO chi tiết trong `docs/iam-rbac-admin.md`. Audit details là chuỗi JSON. Claim JWT có thể cũ nhưng policy đọc quyền hiện tại; login/refresh phát token với quyền mới. Swagger mới xuất hiện sau khi người dùng tự build/chạy lại image IAM.
- Bàn giao: đã commit tách riêng 33 commit cho từng file theo quy ước, kiểm thử toàn bộ giải pháp 115/115 tests PASS, merge theo chuỗi kha-feat-iam-rbac-admin → kha → main → deploy và push lên origin.
- File tạo mới/thay đổi của IAM-005:
- `docs/iam-auth.md`
- `docs/iam-profiles-rbac.md`
- `docs/iam-rbac-admin.md`
- `PROGRESS.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/PermissionAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/RbacAdministratorAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/RbacController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Errors/ResultExtensions.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Accounts/AccountRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Common/Result.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/IRbacStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacCommandHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacModels.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacQueryHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacValidators.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/Entities/RbacAuditLog.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/AccountStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Configurations/RbacAuditConfiguration.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/IamDbContext.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926032128_RbacAdministration.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926032128_RbacAdministration.Designer.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/IamDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/RbacStore.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/RbacAdministrationTests.cs`
- `tests/WardMate.Services.IAM.Tests/RbacValidationTests.cs`

## CLEANUP-001 — Gỡ mã nguồn và dữ liệu kiểm thử theo yêu cầu chủ dự án

- Hoàn thành: **2026-09-30 15:05 (Asia/Saigon, UTC+07:00)**.
- Xóa toàn bộ `tests/` gồm 3 project (IAM unit, IAM integration, SharedKernel unit) và 13 file được Git theo dõi; xóa cả build outputs bên trong tests và thư mục kết quả `TestResults/` local.
- Gỡ 3 project và solution folder tests khỏi `WardMate.sln`; solution còn 27 project ứng dụng/building blocks/gateway.
- Sửa `.github/workflows/iam-ci.yml`: chỉ restore và build Release với warnings-as-errors, bỏ bước chạy test; job hiện tên `build`.
- Cập nhật `AGENTS.md`, `README.md`, `docs/iam-auth.md` để phản ánh việc không còn test suite. Giữ tài liệu vận hành, migration, cấu hình build/triển khai và các kết quả lịch sử trong PROGRESS.md. Giữ ignore rules cho TestResults để tránh commit nhầm artifacts nếu phát sinh sau này.
- Code trong src, database, endpoints, DTO, ProblemDetails và token handling không thay đổi. Không có endpoint mới.
- Kiểm tra: `dotnet build WardMate.sln -c Release --no-restore --verbosity quiet` PASS, **0 errors, 0 warnings**; `git diff --check` PASS; solution và hướng dẫn chạy hiện hành không còn tham chiếu project/lệnh test đã xóa. Không chạy unit/integration tests vì suite đã được gỡ; không coi build pass là kiểm thử hành vi API.
- Không build Docker, không chạy container hoặc migration; không commit/push. Antigravity sẽ gom toàn bộ hành động dọn dẹp này vào **một commit duy nhất** theo chỉ định riêng của người dùng (ngoại lệ với quy tắc mỗi file một commit).
- Danh sách file thay đổi/xóa được Git theo dõi:
- M	.github/workflows/iam-ci.yml
- M	AGENTS.md
- M	PROGRESS.md
- M	README.md
- M	WardMate.sln
- M	docs/iam-auth.md
- D	tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/IamFixture.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/RbacAdministrationTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/WardMate.Services.IAM.IntegrationTests.csproj
- D	tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs
- D	tests/WardMate.Services.IAM.Tests/ProfileValidationTests.cs
- D	tests/WardMate.Services.IAM.Tests/RbacValidationTests.cs
- D	tests/WardMate.Services.IAM.Tests/SecurityTests.cs
- D	tests/WardMate.Services.IAM.Tests/TestIdentityStore.cs
- D	tests/WardMate.Services.IAM.Tests/WardMate.Services.IAM.Tests.csproj
- D	tests/WardMate.SharedKernel.Tests/GlobalExceptionHandlerTests.cs
- D	tests/WardMate.SharedKernel.Tests/WardMate.SharedKernel.Tests.csproj

## SYNC-001 — Đồng bộ nhánh main (DocumentForm Service & Serilog SharedKernel) và dọn dẹp kiểm thử

- Hoàn thành: **2026-09-30 15:20 (Asia/Saigon, UTC+07:00)**.
- Kiểm tra nhánh `main` và tích hợp toàn bộ code mới từ `origin/main` (commit `12f5e84`):
  - Service mới: `WardMate.Services.DocumentForm` (API, Application, Domain, Infrastructure).
  - BuildingBlocks mới: `WardMate.SharedKernel` (Domain primitives, CQRS MediatR markers, Azure Blob Storage client, Serilog centralized logging).
  - Khởi tạo database: `docker/init-db/01-init-schemas-and-tables.sql` và cấu hình `docker-compose.yml`.
- Xử lý xung đột và đồng bộ dọn dẹp kiểm thử (CLEANUP-001):
  - Gỡ bỏ toàn bộ test projects và files vừa được kéo về từ `main` (`WardMate.Services.DocumentForm.Tests`, `WardMate.SharedKernel.Tests`), đảm bảo tuân thủ nghiêm ngặt quy định của chủ dự án (xóa bỏ test suites, solution có 0 test projects).
  - Cập nhật `WardMate.sln`: chứa đầy đủ 27 project ứng dụng và 0 project test.
  - Cập nhật `WardMate.Services.IAM.API/Program.cs`: giữ nguyên cấu hình Serilog tập trung, đồng thời khôi phục `PermissionAuthorization.AddPermissionAuthorization()`, `JsonIgnoreCondition.WhenWritingNull`, và định dạng ProblemDetails tiếng Việt.
- Kiểm tra giải pháp:
  - `dotnet restore WardMate.sln --disable-parallel`: thành công (exit code 0).
  - `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **0 Warning(s), 0 Error(s)** trên toàn bộ 27 projects.
- Chuỗi merge và triển khai Git:
  - Hoàn tất merge vào `kha` và push `origin kha`.
  - Merge `kha` vào `main` và push `origin main`.
  - Merge `main` vào `deploy` và push `origin deploy` để kích hoạt Azure CI/CD.

## TASK-07 — Procedure Catalog entities và ánh xạ EF Core JSONB

- Hoàn thành triển khai/xác minh: **2026-09-30 16:18 (Asia/Saigon, UTC+07:00)**.
- Nhánh feature: `kha-feat-procedure-catalog-entities`, tạo sau fetch origin và pull --ff-only nhánh kha sạch.
- Thêm PostgreSQL 16 độc lập: service procedure-db, database wardmate_procedure_db, user wardmate_procedure, localhost:5434, volume procedure-data, password riêng qua PROCEDURE_DB_PASSWORD. Không dùng/chỉnh sửa script database dùng chung docker/init-db. Mật khẩu local được thêm vào docker/.env ignored, không commit.
- Domain: ProcedureCategory, Procedure, ProcedureVersion và đầy đủ strongly typed JSON models cho content, cases/steps, submission methods, legal references, checklist và form definitions. FormTemplateId là external ID, không có FK xuyên service.
- Infrastructure: ProcedureDbContext, Fluent API đầy đủ kiểu/độ dài/default/nullability, B-tree unique code, composite unique procedure/version, GIN content/checklist. ValueConverter JSONB + ValueComparer clone sâu theo dõi sửa POCO lồng nhau. UTC timestamps, DateOnly date, UUID default gen_random_uuid().
- Migration `20260930091232_Initial_Procedure_Catalog_Schema`: 3 bảng nghiệp vụ, 4 cột JSONB; seed 3 danh mục và 1 thủ tục minh họa Đăng ký kết hôn (DEMO-KET-HON), đầy đủ content/checklist/forms. Snapshot version serialize độc lập, không bị sửa theo Procedure đang hoạt động.
- Application: IProcedureRepository, implementation EF, GetProcedureByIdQuery (MediatR), ProcedureDetailDto. Bổ sung Swagger API công khai GET `/api/v1/procedures/{id}`, không request body; 200 DTO đầy đủ scalar/category/JSON; 404 ProblemDetails tiếng Việt với code procedure.not_found khi không tồn tại/inactive. Gateway chuyển nguyên route này. Swagger trực tiếp cổng 5002.
- Frontend: JSON camelCase, null properties bị bỏ, mảng rỗng giữ []; DateOnly YYYY-MM-DD, UTC datetime, decimal cho phí/số ngày; không cần Bearer cho query này. Seed là dữ liệu giả để kiểm thử, không phải nội dung pháp lý chính thức. Giới hạn/giá trị nghiệp vụ bên trong JSON cần validation ở command ghi khi triển khai CRUD; không có API ghi trong task này.
- Kiểm thử theo yêu cầu mới (không khôi phục test IAM/SharedKernel đã xóa): **11/11 integration cases PASS**, 0 failed/skipped; kiểm tra migration/schema/index/seed, complex JSON roundtrip repository/MediatR/HTTP, nested mutation, null vs empty, constraints code/version, snapshot frozen, defaults/identity và inactive/missing. Testcontainers dùng PostgreSQL tạm, không chạm Local Dev database.
- Release toàn solution: **0 errors, 0 warnings**. `dotnet ef migrations has-pending-model-changes --configuration Release --no-build`: không có pending model changes. Compose config --quiet PASS; git diff --check PASS. Restore NuGet cần chạy ngoài sandbox vì hạn chế network, sau đó thành công.
- Cập nhật CI chạy test suite Procedure Catalog; README và docs/procedure-catalog.md có hướng dẫn chạy, DTO, migration và giới hạn JSON query converter.
- Chưa build Docker image/khởi động stack ứng dụng hoặc migrate database Local Dev/Azure. Migration được kiểm thử thực tế trên PostgreSQL tạm.
- Git cho riêng TASK-07 theo yêu cầu mới: commit từng file, không squash; merge/push feature → kha → deploy, không thay đổi main. Kết quả push được xác nhận trong báo cáo bàn giao cuối, không suy diễn push thành triển khai Azure thành công.
- File/thư mục đã tạo hoặc thay đổi:
- `.github/workflows/iam-ci.yml`
- `.gitignore`
- `docker/.env.example`
- `docker/docker-compose.yml`
- `docs/procedure-catalog.md`
- `PROGRESS.md`
- `README.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/appsettings.Development.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureDetailDto.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProcedureByIdQuery.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/WardMate.Services.ProcedureCatalog.Application.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureCategory.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ChecklistItemSchema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/FormDefinitionSchema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/LegalReferenceDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureCaseDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureContentPayload.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureStepDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/SubmissionMethodDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureCategoryConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureVersionConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/JsonbMapping.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20260930091232_Initial_Procedure_Catalog_Schema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20260930091232_Initial_Procedure_Catalog_Schema.Designer.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContext.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContextFactory.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureSeed.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/WardMate.Services.ProcedureCatalog.IntegrationTests.csproj`
- `WardMate.sln`

## TASK-08 — API quản lý thủ tục, versioning tự động và trạng thái

- Hoàn thành: **2026-10-01 08:22 (Asia/Saigon, UTC+07:00)**.
- Trạng thái: **Hoàn thành code và xác minh local**. Không chạy bất kỳ lệnh Git nào; Antigravity chịu trách nhiệm commit/merge/push. AGENTS.md đã cập nhật để thu hồi workflow Git tự động cũ.
- Application: CreateProcedureCommand tạo active procedure + snapshot version 1 atomically; UpdateProcedureCommand chụp trạng thái trước thay đổi và cấp version tiếp theo; ToggleProcedureStatusCommand lưu trạng thái/lý do; GetProcedureVersionsQuery trả lịch sử giảm dần. MediatR, FluentValidation và Result Pattern; validation tiếng Việt.
- Infrastructure: transaction PostgreSQL, khóa hàng FOR UPDATE để tuần tự hóa update/status và số version; unique code conflict trả 409 kể cả request đồng thời. Migration `20261001012015_ProcedureStatusReason` thêm nullable text `status_change_reason`; snapshot bao gồm trường này.
- API: JWT HS256 issuer/audience/lifetime, role PROCEDURE_MANAGER hoặc IT_ADMIN; Swagger Bearer; gateway chuyển tiếp nguyên đường dẫn manager. ProblemDetails tiếng Việt, JSON DTO bỏ null.

### API mới / hướng dẫn Frontend

| Method | Route | Request body | Response codes |
|---|---|---|---|
| POST | `/api/v1/procedure-manager/procedures` | categoryId, procedureCode, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, contentPayload, checklistSchema?, formDefinitions? | 201 ProcedureDetailDto + Location; 400; 401; 403; 409 |
| PUT | `/api/v1/procedure-manager/procedures/{id}` | Toàn bộ trường POST + decisionNumber, effectiveDate (YYYY-MM-DD) | 200 ProcedureDetailDto; 400; 401; 403; 404; 409 |
| PATCH | `/api/v1/procedure-manager/procedures/{id}/status` | `{ "isActive": false, "reason": "Tạm ngưng tiếp nhận" }` (reason tùy chọn) | 200 {id,isActive,reason?,updatedAt}; 400; 401; 403; 404 |
| GET | `/api/v1/procedure-manager/procedures/{id}/versions` | Không | 200 array {id,versionNumber,decisionNumber?,effectiveDate,snapshotData,createdAt}; 401; 403; 404 |

- Cấu hình `Jwt__Key` giống IAM (không ghi secret vào source); issuer/audience mặc định wardmate/wardmate-client. Gửi `Authorization: Bearer <accessToken>`. Token phải có role PROCEDURE_MANAGER hoặc IT_ADMIN. Role được xác minh từ JWT, không tra IAM realtime; token cũ giữ quyền đến hết hạn khi IAM đổi role/disable account.
- Swagger Development trực tiếp: http://localhost:5002/swagger. Qua Gateway: http://localhost:5000/api/v1/procedure-manager/procedures. Không tự khởi động lại ứng dụng local trong phiên này.
- PUT thay thế toàn bộ input, giữ nguyên trạng thái active; effectiveDate là metadata, không hẹn giờ áp dụng. Snapshot lưu dữ liệu CŨ: version 1 lúc tạo, version 2 ở lần cập nhật đầu cùng nội dung ban đầu. Seed TASK-07 chưa có version nên lần update seed đầu tiên sinh version 1.
- `snapshotData` trả object JSON, không phải JSON string; null bên trong snapshot được giữ để phản ánh lịch sử. PATCH không tạo version; lý do trạng thái được lưu DB. Chặn tiếp nhận hồ sơ mới cần ApplicationWorkflow kiểm tra IsActive khi tích hợp; không tác động hồ sơ đã nộp.
- ProblemDetails: code, title, status, traceId, instance; errors khi validation. Codes: validation.failed, procedure.category_not_found, procedure.code_exists, procedure.not_found, auth.unauthorized, auth.forbidden.
- Request mẫu và hướng dẫn cấu hình: `docs/procedure-manager.md`.

### Kết quả xác minh

- `dotnet build WardMate.sln -c Release --no-restore --verbosity quiet`: **0 errors, 0 warnings**.
- `dotnet test WardMate.sln -c Release --no-build --verbosity quiet --logger trx --results-directory TestResults`: **32 passed / 0 failed / 0 skipped**. Gồm 11 test integration TASK-07 và **21 test integration mới** TASK-08; không thêm unit test riêng (0).
- PostgreSQL 16 thật qua Testcontainers: complex JSONB + version 1, snapshot trước cập nhật, trạng thái/lý do, cập nhật khi đóng, concurrent update giữ chuỗi snapshot, concurrent duplicate create, validation rollback, 401/403/token hết hạn/chữ ký sai, IT_ADMIN và Swagger.
- EF `has-pending-model-changes`: không có thay đổi model thiếu migration.
- Migration được áp dụng trong database kiểm thử tạm. Chưa áp dụng vào database Local Dev đang dùng; Development sẽ migrate khi chủ sở hữu chạy lại service với đúng connection string/JWT. Không build Docker image và không khởi động stack ứng dụng.
- TRX local (generated, không bàn giao commit): `TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_08_21_09.trx`.

### File tạo mới

- `docs/procedure-manager.md`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Security/ProcedureAuthentication.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Security/ManagerSecurityOperationFilter.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureManagementStore.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureResult.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInput.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/UpdateProcedureInputValidator.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureCommands.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureCommandHandlers.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/GetProcedureVersionsQuery.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001012015_ProcedureStatusReason.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001012015_ProcedureStatusReason.Designer.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureManagerTests.cs`

### File cập nhật

- `AGENTS.md`
- `PROGRESS.md`
- `docs/procedure-catalog.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/WardMate.Services.ProcedureCatalog.Application.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs`

Gợi ý commit cho Antigravity: `feat(procedure-catalog): add manager APIs with automatic versioning`.

## IAM-006 — Refresh Token qua HttpOnly Cookie cho Frontend

- Hoàn thành: **2026-10-01 10:01 (Asia/Saigon, UTC+07:00)**.
- Theo đề xuất MD của FE: Access Token giữ trong RAM; Refresh Token chỉ qua cookie HttpOnly, không trả JSON và không nhận fallback từ body. Chưa có domain deploy; local hỗ trợ http://localhost:5173 và http://localhost:3000 theo xác nhận của chủ sở hữu.
- Cookie host-only refreshToken, Path=/api/v1/auth, SameSite=Strict, Expires theo hạn refresh token. Ngoài Development luôn Secure; Development localhost cho phép HTTP, có cấu hình bật Secure. Không cần migration DB.
- Refresh xác định user từ refresh-token digest trong database, không cần access token cũ để khôi phục phiên sau F5. Giữ rotation nguyên tử, kiểm tra expiry/revoked/account active, hash SHA-256. Logout vẫn Authorized, kiểm tra quyền sở hữu token, revoke trong DB rồi xóa cookie cùng path.
- Header X-CSRF-Protection: 1 bắt buộc trên 4 POST auth; kiểm tra Origin; CORS credentialed exact-origin ở IAM/Gateway; Cache-Control no-store; Swagger thêm header và loại secret khỏi response schema.
- Không chạy Git. Không build Docker image. Kiểm thử dùng container database và process ứng dụng tạm riêng, đã dọn sau khi chạy; không thay đổi database Local Dev.

### API / hợp đồng FE thay đổi

| Method | Route | Request | Response |
|---|---|---|---|
| POST | /api/v1/auth/register | JSON {username,email,password,fullName}; header CSRF | 201 CurrentUserDto; 400; 403 CSRF; 409 |
| POST | /api/v1/auth/login | JSON {usernameOrEmail,password}; header CSRF | 200 BrowserAuthResponse + Set-Cookie; 400; 401; 403 |
| POST | /api/v1/auth/refresh-token | Không body, cookie tự gửi; header CSRF | 200 BrowserAuthResponse + cookie mới; 400 malformed cookie; 401; 403 |
| POST | /api/v1/auth/revoke-token | Không body, cookie tự gửi; Bearer + header CSRF | 204 + xóa cookie; 400 malformed cookie; 401; 403 |

- BrowserAuthResponse gồm accessToken, accessTokenExpiresAt, refreshTokenExpiresAt, tokenType; không có refreshToken.
- FE dùng credentials: include / withCredentials: true, lưu access token trong RAM, bỏ localStorage/sessionStorage token fallback. Khi F5 gọi refresh rồi /api/v1/users/me. Single-flight refresh kể cả nhiều tab dùng chung cookie; không lặp refresh khi endpoint refresh trả 401.
- Lỗi mới 403 iam.csrf_rejected; các lỗi tiếng Việt/ProblemDetails cũ giữ nguyên. Refresh thất bại không gửi xóa cookie để tránh response đến chậm xóa cookie mới từ request đồng thời. Logout cần Bearer còn hạn (refresh trước nếu cần); JWT đã phát vẫn có hiệu lực tới hạn, chưa có blacklist.
- Dùng route chuẩn /api/v1/auth qua Gateway localhost:5000 hoặc IAM localhost:5001 nhất quán; không dùng alias /api/iam vì cookie path không khớp. Swagger localhost:5001/swagger sau khi chủ sở hữu chạy lại IAM.
- Tài liệu FE đầy đủ: docs/iam-httponly.md, cập nhật docs/iam-auth.md. Domain deploy/CORS production chưa cấu hình vì chưa có thông tin. Nếu khác site, cần xem lại SameSite và hạn chế third-party cookie; HttpOnly không bảo vệ tuyệt đối trước XSS.

### File tạo mới

- src/BuildingBlocks/WardMate.SharedKernel/Web/BrowserCorsExtensions.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Security/RefreshTokenCookie.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Security/BrowserAuthProtectionAttribute.cs
- scripts/verify-iam-cookie.ps1
- docs/iam-httponly.md

### File chỉnh sửa

- src/Gateways/WardMate.YarpGateway/Program.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AuthController.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/appsettings.json
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/OpenApi/BearerSecurityOperationFilter.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Commands/AuthCommands.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Commands/RefreshTokenHandler.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs
- docs/iam-auth.md
- PROGRESS.md

### Kết quả xác minh

- dotnet build WardMate.sln -c Release --no-restore --verbosity quiet: **0 errors, 0 warnings**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet: **32 passed, 0 failed, 0 skipped** (ProcedureCatalog suite hiện có; không phải test IAM).
- scripts/verify-iam-cookie.ps1: **22/22 HTTP checks passed** với PostgreSQL 16 thật, IAM/Gateway processes riêng: register/login, JSON không chứa refresh secret, cookie HttpOnly/path/SameSite/host-only, local HTTP, no-store, credentialed CORS, refresh cookie-only/F5, rotation, replay 401, không nhận body fallback, CSRF/Origin, logout/revoked, preflight, Swagger, Secure Production.
- Unit tests mới: **0**; không tái tạo test project IAM đã gỡ. Có script kiểm thử HTTP có thể chạy lại. Chưa chạy FE/browser E2E; FE cần kiểm tra trên trình duyệt thật.

Gợi ý commit cho Antigravity: feat(iam)!: move refresh tokens to HttpOnly cookies with CSRF protection
Breaking change: refresh/logout không nhận token trong JSON; mọi POST auth yêu cầu X-CSRF-Protection: 1 và credentials; response login/refresh bỏ refreshToken.

## TASK-08-LIST — API danh sách thủ tục công khai và quản lý

- Hoàn thành: **2026-10-01 11:18 (Asia/Saigon, UTC+07:00)**.
- Bổ sung CQRS GetProceduresQuery, FluentValidation tiếng Việt, projection DTO tóm tắt bằng EF Core, phân trang tại database. Không tải JSONB lớn cho danh sách. Không cần migration hoặc thay route Gateway. Không chạy Git.

### API mới

| Method | Route | Request body / query | Response |
|---|---|---|---|
| GET | /api/v1/procedures | Không body. page=1, pageSize=20, search?, categoryId? | 200 ProcedureListDto; 400 ProblemDetails |
| GET | /api/v1/procedure-manager/procedures | Không body. page=1, pageSize=20, search?, categoryId?, isActive? | 200 ProcedureListDto; 400; 401; 403 |

- Public luôn chỉ lấy IsActive=true, không cho client ghi đè. Manager yêu cầu PROCEDURE_MANAGER hoặc IT_ADMIN; không truyền isActive thì lấy cả hai trạng thái.
- page>=1, pageSize 1..100; kiểm tra overflow offset. Search tối đa 255 ký tự, trim, tìm chứa trong code/title không phân biệt hoa/thường (vẫn phân biệt dấu). Escape ký tự %, _ và backslash, pattern được parameterize. Lọc danh mục dương và trạng thái kết hợp AND. Sort cố định ProcedureCode rồi Id.
- FE nhận `{items,page,pageSize,totalCount,totalPages}`. Item gồm id, categoryId, categoryName, procedureCode, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, isActive, createdAt, updatedAt. Không bao gồm contentPayload/checklistSchema/formDefinitions/versions; gọi API chi tiết khi cần.
- Trang vượt dữ liệu / danh mục không tồn tại / không có kết quả: 200 items=[]; totalPages=0 khi totalCount=0. Query không hợp lệ: 400 application/problem+json, code validation.failed, thông báo tiếng Việt, errors, traceId. Token handling không thay đổi.
- Count và items là hai query riêng; khi có concurrent writes, metadata có thể lệch tức thời. Không cung cấp snapshot pagination giữa các request.

### File mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureListDto.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProceduresQuery.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs

### File sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs
- docs/procedure-catalog.md
- docs/procedure-manager.md
- PROGRESS.md

### Kiểm thử

- Release solution build --no-restore: **0 warnings, 0 errors**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet: **116 passed / 0 failed / 0 skipped**: ProcedureCatalog 44, DocumentForm 72 trong solution hiện tại.
- Viết thêm **12 integration cases**, unit tests mới 0: public không lộ inactive dù truyền isActive=false; phân trang/sort/summary; manager status/category/search; tìm title tiếng Việt case-insensitive; literal wildcard; query sai/overflow; 401/403/admin/public. PostgreSQL 16 thật qua Testcontainers tạm. Không thêm test project mới.
- Chưa khởi động/restart ứng dụng Local Dev, không build Docker image. Swagger hiển thị endpoint mới sau khi chủ sở hữu chạy lại service với code mới.

Gợi ý commit cho Antigravity: feat(procedure-catalog): add paginated public and manager procedure lists

## TASK-08-DOCKER — Đóng gói Docker và tích hợp Local Dev cho Procedure Catalog

- Hoàn thành: **2026-10-01 11:25 (Asia/Saigon, UTC+07:00)**.
- Thêm Dockerfile đa tầng (.NET 8 SDK build & ASP.NET runtime) cho `WardMate.Services.ProcedureCatalog.API`.
- Tích hợp service `procedure-catalog` vào `docker/docker-compose.yml`, kết nối mạng nội bộ với `procedure-db`, bật `Database__AutoMigrate=true`, cấu hình JWT đồng bộ với IAM (`docker/.env`).
- Mở cổng `5002:8080`, cho phép khởi chạy và dừng trực tiếp qua Docker Desktop GUI mà không cần chạy lệnh `dotnet run`.

### File mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Dockerfile

### File sửa

- docker/docker-compose.yml
- PROGRESS.md

### Kiểm thử & Xác minh

- Release solution build `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **0 warnings, 0 errors**.
- Docker compose build: Image `wardmate-procedure-catalog:local` được build thành công.
- Khởi chạy container `wardmate_procedure_catalog`:
  - EF Core migration `20261001012015_ProcedureStatusReason` tự động áp dụng thành công.
  - Liveness check `GET http://localhost:5002/health`: **HTTP 200 Healthy**.
  - Kiểm tra API danh sách mới `GET http://localhost:5002/api/v1/procedures`: Trả về 200 kèm `items`, `page`, `pageSize`, `totalCount`, `totalPages`.
  - Kiểm tra API quản lý `GET http://localhost:5002/api/v1/procedure-manager/procedures`: Xác thực JWT thành công với vai trò `PROCEDURE_MANAGER`.
  - Swagger UI sẵn sàng tại `http://localhost:5002/swagger`.

Gợi ý commit cho Antigravity:
1. `build(procedure-catalog): add Dockerfile for Procedure Catalog API`
2. `feat(docker): add procedure-catalog service to docker-compose`

## TASK-09 — Tìm kiếm nâng cao & import (ĐANG THỰC HIỆN)

- Cập nhật phiên: **2026-10-01 13:34 (Asia/Saigon, UTC+07:00)**. Chưa hoàn thành toàn task.
- Bước 0 đã xác minh: GET /api/v1/procedures luôn ép IsActive=true; GET /api/v1/procedure-manager/procedures có JWT role policy và mặc định lấy cả hai trạng thái. Giữ nguyên phân tách này khi nâng cấp.
- Đã triển khai GetProceduresPagedQuery, keyword không dấu qua PostgreSQL unaccent/ILIKE, lọc cấp/danh mục/trạng thái, whitelist sort và phân trang pageNumber/pageSize (default10/max100). PagedResult<ProcedureSummaryDto> dùng currentPage,totalPages,totalCount,pageSize,hasPrevious,hasNext; DTO tóm tắt không tải JSONB. Alias search/page vẫn được nhận; response page cũ đã thay bằng currentPage.
- Migration ProcedureUnaccentSearch cài unaccent. Build Release: 0 errors, 0 warnings. EF has-pending-model-changes: không có thay đổi thiếu migration.
- dotnet test toàn solution: 120 passed, 0 failed, 0 skipped (ProcedureCatalog 48; DocumentForm 72). Thêm 4 integration cases cho keyword không dấu, cấp/sort/metadata và validation mới; cập nhật 12 test danh sách theo DTO mới. Các con số này CHƯA xác minh importer CSV.
- API GET không body, query mới và response như docs/procedure-catalog.md; status 200/400; Manager thêm401/403. Không thay token handling.
- Phần import CSV đã có bản nháp: CsvHelper 33.0.1, batch200, upsert và snapshot, endpoint multipart /api/v1/procedure-manager/procedures/import-csv. CHƯA có kiểm thử import/performance, chưa xác nhận ready. Không import dữ liệu thật.
- Trong phiên, chủ sở hữu cho biết muốn import từng DOCX/PDF và hỏi khả năng tự đọc/lưu database. Đã đề xuất DOCX/PDF có chữ → trích xuất → người quản lý xem/sửa → xác nhận lưu; PDF scan cần OCR (AIOCR hiện chỉ có khung service). Đang chờ chọn thay CSV trong TASK-09 hay làm tài liệu ở task sau và có bước duyệt hay tự lưu. Không suy diễn trả lời thay chủ sở hữu; chưa triển khai luồng DOCX/PDF.
- Không chạy Git; chưa bàn giao task hoàn chỉnh cho Antigravity.

### Các đường dẫn đã tác động trong phiên (bao gồm bản nháp chưa hoàn tất)

- Application/DTOs/PagedResult.cs (mới), Application/Queries/GetProceduresPagedQuery.cs (mới).
- Application/Import/ImportProceduresFromCsvCommand.cs (mới, bản nháp import).
- Application/Interfaces/IProcedureRepository.cs (sửa).
- Application/DTOs/ProcedureListDto.cs và Application/Queries/GetProceduresQuery.cs (xóa, thay bằng hợp đồng mới).
- Infrastructure/Persistence/ProcedureRepository.cs, ProcedureDbContext.cs, Migrations/ProcedureDbContextModelSnapshot.cs (sửa).
- Infrastructure/Persistence/Migrations/*ProcedureUnaccentSearch.cs và *.Designer.cs (mới).
- Infrastructure/Import/ProcedureCsvRow.cs, ProcedureCsvBatchWriter.cs, ProcedureCsvImporter.cs (mới, bản nháp import).
- Infrastructure/DependencyInjection.cs, WardMate.Services.ProcedureCatalog.Infrastructure.csproj (sửa cho CsvHelper/import).
- API/Controllers/ProceduresController.cs, ProcedureManagerController.cs (sửa).
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs, ProcedurePersistenceTests.cs (sửa).
- docs/procedure-catalog.md, docs/procedure-manager.md, PROGRESS.md (sửa).

Các đường dẫn Application/Infrastructure/API trên thuộc src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.<tầng>/. Chưa đề xuất commit toàn task vì phạm vi import đang được điều chỉnh.

## TASK-09 (điều chỉnh) — HOÀN THÀNH: tìm kiếm và xuất bản bản nháp đã đối soát

- Hoàn thành: **2026-10-01 14:10 (Asia/Saigon, UTC+07:00)**.
- Thay thế phạm vi import cũ: đã xóa toàn bộ 4 file importer CSV, endpoint import-csv, DI và dependency CsvHelper; xóa các thư mục Import rỗng. Không triển khai CSV. Các mục TASK-09 trước đây ở trạng thái nháp/chờ phạm vi chỉ là lịch sử.
- Đã xác minh lại: Public luôn active-only (cả query và repository); Manager lấy cả active/inactive nếu không truyền filter, có JWT role PROCEDURE_MANAGER/IT_ADMIN. Public cap50, Manager cap100, mặc định10.
- Tách GetPublicProceduresPagedQuery/GetManagerProceduresPagedQuery; tìm không dấu unaccent/ILIKE, lọc danh mục/cấp, phân trang. Giữ tùy chọn sort và alias page/search cũ. DTO Public có OriginalPdfUrl, không có IsActive; Manager thêm IsActive, VersionCount, CreatedAt/UpdatedAt.
- Bổ sung OriginalPdfUrl (nullable varchar500), PdfFileName (nullable varchar255) cho Procedure và ProcedureVersion. API chi tiết/lịch sử và snapshot JSON cũng có hai trường. Migration mới: 20261001070638_ProcedureOriginalPdf. Migration unaccent trước đó được giữ.
- PublishReviewedProcedureCommand: validation tiếng Việt; category tồn tại; mã mới tạo active + version1; mã có sẵn snapshot trạng thái cũ + version tiếp theo rồi thay dữ liệu. Transaction toàn vẹn; advisory lock theo mã bảo vệ publish concurrent khi chưa có row, row lock bảo vệ cập nhật. Giữ nguyên trạng thái đóng/mở, Id, CreatedAt khi cập nhật; không tự mở lại thủ tục đã đóng.
- Task nhận JSON đã đối soát, không thực hiện upload/OCR/AI/split-view FE. Không đọc/lưu PDF thật trong phiên này. Backend chỉ lưu URL, không xác minh nội dung file trên Azure Blob. Không thay schema/service IAM.
- Đã build Docker image `wardmate-procedure-catalog:local` và khởi động lại container trên Docker Desktop (cổng 5002); migrations đã tự động áp dụng vào PostgreSQL Local Dev khi container khởi chạy.

### Endpoints / hợp đồng FE

| Method | Route | Request | Response |
|---|---|---|---|
| GET | /api/v1/procedures | Không body. keyword?, categoryId?, levelOfImplementation?, pageNumber=1, pageSize=10 (max50); sortBy/isAscending và alias page/search còn hỗ trợ | 200 PagedResult<ProcedureSummaryDto>; 400 |
| GET | /api/v1/procedure-manager/procedures | Không body. Cùng filter; isActive nullable; pageSize max100 | 200 PagedResult<ProcedureManagerSummaryDto>; 400; 401; 403 |
| POST | /api/v1/procedure-manager/procedures/publish | JSON ReviewedProcedureInput: procedureCode, categoryId, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, originalPdfUrl?, pdfFileName?, contentPayload, checklistSchema?, formDefinitions? | 200 ProcedureDetailDto cho cả tạo/cập nhật; 400; 401; 403; 409 |

- Public item: id, procedureCode, title, categoryName, levelOfImplementation, feeSummary, processingTimeSummary, originalPdfUrl?, updatedAt. Manager item thêm isActive, versionCount, createdAt. Không tải contentPayload/checklist/form/version snapshots trong danh sách.
- Pagination: items/currentPage/totalPages/totalCount/pageSize/hasPrevious/hasNext. Danh sách rỗng trả200, totalPages=0; count và items có thể lệch tức thời nếu có concurrent writes (hai SQL riêng).
- JWT Bearer có role PROCEDURE_MANAGER hoặc IT_ADMIN cho manager/publish; token handling không thay đổi.
- OriginalPdfUrl nullable, HTTPS tuyệt đối, không userinfo, tối đa500 ký tự; PdfFileName tối đa255. JSON null bị bỏ khỏi response. Dùng URL ổn định; không nhúng secret/SAS dài hạn vào URL công khai. Blob private cần cơ chế cấp link của storage riêng.
- Publish thay toàn bộ input; snapshot trước sửa giữ URL/tên PDF cũ. Metadata ngày hiệu lực mặc định ngày UTC xuất bản do request không có EffectiveDate. Publish lặp vẫn tạo version; FE chặn double-submit. FeeSummary/ProcessingTimeSummary phải gửi rõ, không tự điền miễn phí/1 ngày.
- 400 validation.failed hoặc procedure.category_not_found, 409 procedure.code_exists; ProblemDetails tiếng Việt có traceId, instance, errors khi validation. Giao diện/AI gửi đầy đủ dữ liệu đã đối soát, không gửi file/multipart tới publish.
- Hướng dẫn và request mẫu: docs/procedure-reviewed-publishing.md. Swagger http://localhost:5002/swagger sau khi chủ sở hữu chạy lại service. Gateway chuyển nguyên route hiện có.

### Kiểm thử và migration

- dotnet build WardMate.sln -c Release --no-restore --verbosity quiet: **0 errors, 0 warnings**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet --logger trx --results-directory TestResults: **131 passed / 0 failed / 0 skipped** (ProcedureCatalog59 + DocumentForm72).
- Thêm **11 integration cases**, unit tests mới0: JSONB10 cases + checklist/forms roundtrip; cột PDF/live/version/snapshot; update và versionCount; giữ trạng thái đóng; public/manager filters; concurrent publish cùng mã mới; invalid payload/category/URL/length rollback; rollback lỗi DB thật; giới hạn pageSize; 401/403/IT_ADMIN; Swagger không còn CSV.
- Cập nhật test danh sách theo DTO public/manager mới và test migration count. Không tạo thêm test project.
- EF has-pending-model-changes: không có thay đổi thiếu migration. Kiểm tra migration thêm đúng 4 cột nullable, không xóa dữ liệu nghiệp vụ. Database Local Dev đã được migrate đầy đủ 2 migration (`20261001063051_ProcedureUnaccentSearch` và `20261001070638_ProcedureOriginalPdf`) tự động qua Docker container startup.
- TRX local: TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_14_07_33.trx (DocumentForm), TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_14_07_40.trx (ProcedureCatalog); generated artifacts không bàn giao commit.

### File mới trong phiên điều chỉnh

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/ProcedureSearchOptions.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetPublicProceduresPagedQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetManagerProceduresPagedQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/PublishReviewedProcedureCommand.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001070638_ProcedureOriginalPdf.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001070638_ProcedureOriginalPdf.Designer.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ReviewedProcedureTests.cs
- docs/procedure-reviewed-publishing.md

### File chỉnh sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/PagedResult.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureDetailDto.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInput.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/GetProcedureVersionsQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureVersionConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs
- docs/procedure-catalog.md
- docs/procedure-manager.md
- PROGRESS.md

### File đã gỡ

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProceduresPagedQuery.cs (thay bằng query riêng public/manager và options chung)
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Import/ImportProceduresFromCsvCommand.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvRow.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvBatchWriter.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvImporter.cs

Gợi ý commit cho Antigravity: feat(procedure-catalog): publish reviewed procedures with PDF version history

## IAM-007 — Sửa CSRF trên Swagger sau Azure ingress bằng Forwarded Headers

- Hoàn thành: 2026-10-02 01:44:58 Asia/Saigon (UTC+07:00).
- Phạm vi: cấu hình IAM API nhận X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host; xóa KnownNetworks/KnownProxies theo yêu cầu. Middleware chạy ngay sau UseGlobalExceptionHandling, trước logging, CORS, Swagger và authentication/authorization. Giữ ForwardLimit mặc định 1.
- Đã kiểm tra BrowserAuthProtectionAttribute: sameOrigin sử dụng Request.Scheme/Host sau chuẩn hóa; không cần sửa attribute. Vẫn bắt buộc X-CSRF-Protection: 1 và từ chối origin không cùng domain, không thuộc CORS allowlist.

### File thay đổi

| File | Nội dung |
| --- | --- |
| src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs | Cấu hình và thứ tự middleware Forwarded Headers |
| scripts/verify-iam-cookie.ps1 | Thêm 5 kiểm tra HTTP cho forwarded origin và bảo toàn CSRF/CORS |
| docs/iam-httponly.md | Hướng dẫn ingress, trust boundary và giới hạn proxy nhiều hop |
| PROGRESS.md | Nhật ký IAM-007 và kết quả kiểm chứng |

### API / Frontend

- Không thêm endpoint hay thay đổi DTO/request body/token contract.
- POST /api/v1/auth/login vẫn nhận UsernameOrEmail và Password, trả 200 với access token và cookie refresh HttpOnly khi hợp lệ; CSRF không hợp lệ trả 403 ProblemDetails với code iam.csrf_rejected.
- Swagger cùng public domain không cần thêm domain đó vào CORS allowlist nếu ingress chuyển đúng Host/Proto. FE khác domain vẫn phải nằm trong AllowedOrigins và gửi credentials cùng X-CSRF-Protection: 1.
- Container phải chỉ nhận traffic từ ingress/proxy được kiểm soát; proxy phải làm sạch/ghi đè forwarded headers. Với nhiều hop, proxy gần IAM nhất phải chuyển đúng public host/proto.

### Kiểm chứng

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release: PASS 131/131, 0 failed, 0 skipped (DocumentForm 72, ProcedureCatalog 59). Không tạo mới unit test project.
- pwsh -NoProfile -File scripts/verify-iam-cookie.ps1: PASS 27/27 HTTP checks trên PostgreSQL tạm và IAM/Gateway local, gồm 5 checks mới. Origin HTTPS của Swagger Azure được chấp nhận với forwarded host/proto dù không nằm trong CORS allowlist; thiếu forwarding, thiếu CSRF header hoặc origin lạ bị từ chối; CORS cho FE được phép vẫn hoạt động.
- Không chạy Git. Chưa deploy hoặc kiểm tra trực tiếp trên Azure; cần Antigravity triển khai và kiểm tra lại ingress thực tế.

Gợi ý commit: fix(iam): honor forwarded headers before CSRF validation

## IAM-008 — Chuẩn bị tạo 5 tài khoản kiểm thử trên deploy (chưa hoàn tất)

- Thời gian ghi nhận: 2026-10-02, Asia/Saigon (UTC+07:00).
- Đã kiểm tra Swagger Azure IAM: GET /swagger/v1/swagger.json trả HTTP 200; có API đăng ký, đăng nhập và quản lý role.
- Chưa tạo tài khoản hoặc thay đổi dữ liệu deploy: đang chờ phiên/thông tin đăng nhập IT_ADMIN để gán role qua API được bảo vệ. Đăng ký công khai chỉ gán REGISTERED_CITIZEN.
- Dự kiến sử dụng POST /api/v1/auth/register (Username, Email, Password, FullName; 201), POST /api/v1/auth/login (UsernameOrEmail, Password; 200), GET /api/v1/rbac/roles (200), PUT/DELETE /api/v1/rbac/users/{userId}/roles/{roleId} (204), và xác minh đăng nhập sau khi gán role.
- File thay đổi trong phiên này: PROGRESS.md. Không sửa mã nguồn, không chạy lại build/unit tests; chưa có kiểm thử tạo tài khoản thành công. Không chạy Git.
- Token/Frontend: auth POST cần X-CSRF-Protection: 1; quản lý role cần token IT_ADMIN. Không lưu thông tin đăng nhập/token vào tài liệu hoặc mã nguồn.

### IAM-008 — Cập nhật điều kiện bootstrap (2026-10-02, Asia/Saigon UTC+07:00)

- Chủ hệ thống xác nhận chưa có IT_ADMIN và yêu cầu tạo mỗi role một tài khoản trên deploy.
- Đã xác định quy trình bootstrap trong docs/iam-rbac-admin.md: gán IT_ADMIN đầu tiên trực tiếp qua kết nối database quản trị tin cậy, sau đó quản lý role bằng API.
- Chưa có kết nối quản trị Azure/PostgreSQL trong môi trường thao tác (không có Azure CLI hoặc biến môi trường kết nối phù hợp). Đã yêu cầu đường dẫn file kết nối deploy hoặc phiên Azure Portal được đăng nhập.
- Chưa tạo tài khoản, chưa thay đổi database deploy; chưa chạy kiểm thử cấp tài khoản. Không thêm endpoint tự cấp quyền, không chạy Git.

## IAM-009 — Khởi tạo 5 tài khoản chính thức cho từng vai trò và dọn dẹp tài khoản kiểm thử cũ

- Hoàn thành: 2026-10-02 13:16:00 Asia/Saigon (UTC+07:00).
- Phạm vi:
  - Loại bỏ các tài khoản kiểm thử tạm thời (`citizentest`, `officertest`, `managertest`, `procmanagertest`, `admintest`, `testcitizen1`).
  - Thiết lập 5 tài khoản chính thức chuẩn hóa theo từng vai trò của hệ thống WardMate, không chứa hậu tố `test`:
    1. **IT Admin**: `wardmateadmin` (Tên hiển thị: `WardMate Admin`, Email: `admin@wardmate.vn`, Vai trò: `IT_ADMIN` - Id 5)
    2. **Procedure Manager**: `wardmateprocmanager` (Tên hiển thị: `WardMate Procedure Manager`, Email: `procmanager@wardmate.vn`, Vai trò: `PROCEDURE_MANAGER` - Id 4)
    3. **Manager**: `wardmatemanager` (Tên hiển thị: `WardMate Manager`, Email: `manager@wardmate.vn`, Vai trò: `MANAGER` - Id 3)
    4. **Front Desk Officer**: `wardmateofficer` (Tên hiển thị: `WardMate Officer`, Email: `officer@wardmate.vn`, Vai trò: `FRONT_DESK_OFFICER` - Id 2)
    5. **Citizen**: `wardmatecitizen` (Tên hiển thị: `WardMate Citizen`, Email: `citizen@wardmate.vn`, Vai trò: `REGISTERED_CITIZEN` - Id 1)
  - Mật khẩu mặc định: `Password123!` (đáp ứng đầy đủ chính sách độ phức tạp mật khẩu: >=8 ký tự, có chữ hoa, ký tự đặc biệt, UTF-8 <= 72 byte).
  - Phương án triển khai: Khởi tạo trực tiếp qua Database Bootstrap Script vào cơ sở dữ liệu PostgreSQL (đã áp dụng và xác minh trên container `wardmate-iam-db-1` local, sẵn sàng thực thi trên Supabase Deploy qua SQL Editor).
  - Giữ mã nguồn hệ thống hoàn toàn sạch (clean architecture), không hardcode thông tin tài khoản mẫu trong code backend để đảm bảo an toàn bảo mật và tránh nguy cơ vô tình ghi đè mật khẩu khi khởi động lại.

### File thay đổi

| File | Nội dung |
| --- | --- |
| `PROGRESS.md` | Ghi nhận hoàn thành task IAM-009 |

### API / Frontend

- Đăng nhập: POST `/api/v1/auth/login` với `UsernameOrEmail` là username (`WardMateAdmin`, `wardmateadmin`, ...) hoặc email (`admin@wardmate.vn`, ...), `Password` là `Password123!`.
- Header bắt buộc cho browser/cookie authentication: `X-CSRF-Protection: 1`.
- Không tạo endpoint công khai mới; bảo toàn kiến trúc bảo mật RBAC.

### Kết quả kiểm chứng

- `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **PASS, 0 errors, 0 warnings**.
- `dotnet test WardMate.sln -c Release --no-build`: **PASS 131/131, 0 failed, 0 skipped** (DocumentForm 72, ProcedureCatalog 59).
- Đã xác minh dữ liệu thực tế tại PostgreSQL container local: 5 tài khoản hiển thị đầy đủ kèm đúng vai trò và thông tin profile.
- Tuân thủ quy định `AGENTS.md`: không tự ý thực thi các lệnh Git.

Gợi ý commit cho Antigravity:
- `docs: update PROGRESS.md with official role accounts seeding`


## PDF-001 — Backend upload PDF, bóc tách AIOCR và đối soát bản nháp thủ tục

- Hoàn thành mã nguồn và kiểm thử local: 2026-10-02 14:57:59 Asia/Saigon (UTC+07:00).
- Trạng thái: hoàn thành backend và adapter có thể cấu hình. Chưa kích hoạt/kiểm chứng Azure Blob, OCR và AI thật; chủ dự án xác nhận chưa có OCR. Chưa xây màn hình FE Split-view trong repository backend, chưa deploy, chưa áp dụng migration lên database Local Dev/deploy.
- Không chạy Git; không build Docker image hoặc khởi động cụm ứng dụng. Các container PostgreSQL được tạo bởi integration tests là tạm thời và độc lập database người dùng.

### Chức năng

- Procedure Catalog quản lý PDF nguồn private trên Azure Blob, bảng procedure_drafts với JSONB payload/warnings, trạng thái Queued/Processing/NeedsReview/Failed/Published và người tạo/đối soát.
- Nghiệp vụ ở tầng Application qua IDraftPersistence/IDraftFileStorage; Infrastructure triển khai EF Core, Blob, HTTP AIOCR và background worker. AIOCR không tham chiếu assembly/database Procedure Catalog.
- Khi OCR tắt, upload tạo bản nháp thủ công NeedsReview. Thiếu cấu hình Blob trả 503, không giả vờ upload thành công. File PDF tối đa 20 MiB, kiểm tra phần mở rộng và chữ ký đầu file.
- Worker sử dụng hàng đợi PostgreSQL, row locks SKIP LOCKED và lease phục hồi khi restart; có thời hạn xử lý, số lần nhận giới hạn và retry thủ công. Kết quả AI không tự xuất bản.
- Revision GUID chống ghi đè; xuất bản yêu cầu confirmed=true, giữ nguồn PDF của bản nháp, dùng handler publish hiện có. Trạng thái bản nháp, thủ tục và version commit/rollback cùng transaction; xác nhận trùng trả 409.
- Adapter AIOCR dùng Azure Document Intelligence prebuilt-layout + Azure OpenAI JSON mode, giữ trường chưa biết ở dạng null và warnings. Publish không tự biến trường thiếu thành các giá trị POCO mặc định. Adapter mặc định tắt, không gọi cloud trong tests.
- Link PDF read SAS 10 phút chỉ cấp cho manager hoặc thủ tục đã xuất bản còn active; SAS không lưu vào DB. Dockerfile AIOCR, Compose profile ai và biến môi trường mẫu đã được thêm; Compose đã validate, image chưa build.

### API và hợp đồng FE

| Method | Route | Request body | Response |
| --- | --- | --- | --- |
| POST | /api/v1/procedure-manager/drafts | Multipart file PDF | 202 DraftDto; 400/413; 503 thiếu Blob |
| GET | /api/v1/procedure-manager/drafts?page=1&pageSize=10 | Không | 200 DraftSummaryDto[], pageSize tối đa 50; 400 |
| GET | /api/v1/procedure-manager/drafts/{id} | Không | 200 DraftDto; 404 |
| GET | /api/v1/procedure-manager/drafts/{id}/source | Không | 200 {url,expiresInSeconds:600}; 404/503 |
| PUT | /api/v1/procedure-manager/drafts/{id} | {revision,payload} | 200 DraftDto; 400/404/409 |
| POST | /api/v1/procedure-manager/drafts/{id}/retry | {revision} | 200 DraftDto; 404/409/503 |
| POST | /api/v1/procedure-manager/drafts/{id}/publish | {revision,confirmed:true} | 200 ProcedureDetailDto; 400/404/409 |
| GET | /api/v1/procedures/{id}/source | Không, public | 200 {url,expiresInSeconds:600}; 404/503 |
| POST | AIOCR /internal/v1/procedure-extractions | Binary PDF, X-Service-Key | 200 {payload,extractedText,warnings}; 400/401/413/502/503/504 |

Manager endpoints yêu cầu Bearer role PROCEDURE_MANAGER hoặc IT_ADMIN (401/403 khi không đủ quyền). DTO bản nháp có revision/status/payload/warnings/extractedText/pdfFileName/failureCode/publishedProcedureId và timestamps. FE poll GET khi Queued/Processing, lấy link source hiển thị PDF, sửa payload và gửi revision mới nhất. Chỉ bấm publish sau đối soát. Lỗi ProblemDetails tiếng Việt có code/traceId/errors khi phù hợp. Không thay đổi token contract IAM. Hướng dẫn FE, giới hạn và cấu hình: docs/procedure-pdf-drafts.md.

### Kiểm thử thực tế

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-restore: PASS 144/144, 0 failed, 0 skipped: DocumentForm 72; ProcedureCatalog và adapter AIOCR 72.
- Thêm 13 test cases trong project hiện có (10 kiểm thử luồng draft/PostgreSQL/HTTP, 3 kiểm thử adapter HTTP mô phỏng); không tạo test project mới.
- Kiểm tra: upload/manual edit, 10 cases, PDF source/private URL, active filtering, role authorization, revision conflict, publish trùng, rollback khi ghi draft thất bại, khôi phục lease, worker không tự publish, field unknown không biến thành 0, provider response bị cắt, thiếu cấu hình provider không gọi mạng.
- dotnet ef migrations has-pending-model-changes: không có model change chưa được đưa vào migration. Migration mới chạy thành công trên PostgreSQL tạm của tests.
- docker compose -f docker/docker-compose.yml --profile ai config --quiet: PASS. Không build image.
- Chưa thử OCR trên PDF thật, chưa đo độ chính xác/chi phí AI, chưa xác minh cloud hoặc UI frontend. Blob storage trong integration tests là fake; provider HTTP được mô phỏng và không phát sinh phí.

### File tạo mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureDraft.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftContracts.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftPayloadRequirements.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftPersistence.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/ProcedureDraftService.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Drafts/DraftPersistence.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Drafts/DraftExtractionWorker.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureDraftConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261002072134_ProcedureDrafts.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261002072134_ProcedureDrafts.Designer.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureDraftsController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureSourcesController.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Application/Extraction/ProcedureExtraction.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/AzureProcedureDocumentExtractor.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Controllers/ProcedureExtractionsController.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Dockerfile
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureDraftTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/DraftTestBlobStorage.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AzureExtractionAdapterTests.cs
- docs/procedure-pdf-drafts.md

### File chỉnh sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContext.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/appsettings.json
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/WardMate.Services.AIOCR.Infrastructure.csproj
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Program.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/appsettings.json
- docker/docker-compose.yml
- docker/.env.example
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/WardMate.Services.ProcedureCatalog.IntegrationTests.csproj
- PROGRESS.md

Gợi ý Conventional Commit theo chức năng để Antigravity tách từng file theo quy định: feat(procedure-catalog): add PDF drafts and reviewed publication; feat(aiocr): add configurable Azure procedure extraction; test(procedure-catalog): verify PDF draft lifecycle and extraction; docs: document PDF review workflow and configuration.

## PDF-002 — Ưu tiên đọc văn bản PDF trực tiếp và API đọc thử


- Hoàn thành: 2026-10-03 08:43:28 Asia/Saigon (UTC+07:00).
- Thực hiện theo yêu cầu đọc PDF có lớp văn bản, không bắt buộc OCR. Giữ nguyên dữ liệu và các thay đổi khác trong workspace; không chạy Git.

### Chức năng / API / hướng dẫn FE

- AIOCR dùng PdfPig 0.1.16 + ContentOrderTextExtractor để đọc từng trang, chuẩn hóa Unicode NFC; giữ dấu phân trang trong extractedText. Không cần OCR hoặc AI credentials để lấy chữ.
- Hai cờ riêng: Extraction:UseAI=false và Extraction:OcrFallbackEnabled=false mặc định. Khi bật UseAI và có AzureOpenAI config, văn bản đi trực tiếp sang bước map JSON; không gọi Document Intelligence cho PDF đủ chữ theo kiểm tra sơ bộ.
- Trang ít chữ, có ký tự hỏng/control được cảnh báo; không gọi AI trên văn bản thiếu trang khi OCR fallback tắt. Trang có ảnh luôn được cảnh báo vì reader không đọc chữ trong ảnh. Đây là heuristic, không đảm bảo chính xác/đầy đủ tuyệt đối; cán bộ vẫn đối soát.
- Khi AI tắt/lỗi: giữ extractedText, trả khung payload chưa điền và warnings để nhập tay; không giả lập dữ liệu thủ tục. OCR chỉ gọi khi bật fallback riêng. PDF quá 20 MiB/100 trang/100.000 ký tự bị từ chối thay vì âm thầm cắt; file hỏng/khóa có lỗi có kiểm soát.
- API mới: POST /api/v1/procedure-manager/drafts/extract-preview, multipart/form-data trường file; 200 {payload,extractedText,warnings}, Cache-Control no-store. Manager/IT_ADMIN Bearer bắt buộc (401/403). Không lưu Blob hoặc tạo bản nháp/ghi DB. Lỗi: 400 file sai; 503 thiếu config AIOCR; 502 service/PDF lỗi; 504 timeout; request quá lớn có thể 413 từ server/ingress.
- Không đổi endpoint AIOCR nội bộ, DTO lưu nháp hoặc token IAM; không thêm migration. Luồng lưu bản nháp vẫn cần Blob và ProcedureDrafts:ExtractionEnabled=true để worker gọi AIOCR. Preview gọi trực tiếp extractor, không phụ thuộc cờ hàng đợi này.
- Script scripts/verify-pdf-text.ps1 nhận PdfPath và ExpectedText tùy chọn; chạy AIOCR tạm trên loopback bằng khóa ngẫu nhiên, ép AI/OCR tắt, kiểm tra HTTP rồi dừng process. Không gọi cloud/Blob/DB.
- Hướng dẫn test Swagger, script, cấu hình service key và các giới hạn: docs/procedure-pdf-text.md. Chưa triển khai FE Split-view hoặc deploy Azure; không tự build Docker.

### Kiểm chứng

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-build --no-restore: PASS 152/152 (DocumentForm72 + ProcedureCatalog/AIOCR80), 0 failed, 0 skipped.
- Thêm 8 test cases trong project có sẵn: 7 test reader/adapter (PDF thật tạo trong bộ nhớ, không cần OCR/AI credentials/network, trang thiếu chữ, AI-only mapping, AI failure fallback, file hỏng, giới hạn trang); 1 test HTTP preview phân quyền và không cần Blob. Điều chỉnh test Azure cũ để bật UseAI rõ ràng. Không tạo test project mới.
- Smoke test bằng PDF mẫu Đăng ký tạm trú người dùng đã cung cấp: 13 trang, PdfPig qua HTTP đọc 21.367 ký tự, kiểm tra tiêu đề có dấu Đăng ký tạm trú thành công; đoạn đầu có mã 1.116789. AI/OCR đều tắt, không truyền tài liệu ra cloud. Đối chiếu độc lập pypdf (13 trang, 20.917 ký tự) và ảnh render trang đầu bằng pypdfium2; khác số ký tự do cách xuống dòng/phân trang. Không khẳng định toàn bộ layout được giữ nguyên.
- docker compose -f docker/docker-compose.yml --profile ai config --quiet: PASS; chỉ validate, không build/start.
- PDF gốc không sao chép vào source; ảnh kiểm tra nằm trong TestResults đã được ignore. Chưa kiểm chứng mapping AI thật do chưa có nhà cung cấp/key.

### File tạo mới

- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/TextFirstProcedureExtractor.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/TextPdfExtractionTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/PdfPreviewTests.cs
- scripts/verify-pdf-text.ps1
- docs/procedure-pdf-text.md

### File chỉnh sửa

- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/WardMate.Services.AIOCR.Infrastructure.csproj
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/AzureProcedureDocumentExtractor.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Program.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/appsettings.json
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureDraftsController.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AzureExtractionAdapterTests.cs
- docker/docker-compose.yml
- docker/.env.example
- docs/procedure-pdf-drafts.md
- PROGRESS.md

Gợi ý commit cho Antigravity (tách file theo quy định): feat(aiocr): extract PDF text before optional OCR and AI; feat(procedure-catalog): add PDF extraction preview; test(aiocr): cover native PDF extraction and preview authorization; docs: explain OCR-free PDF testing.

## DOC-PDF-003 — Tài liệu sử dụng API đọc thử PDF trong hệ thống

- Hoàn thành: 2026-10-04 21:47:13 Asia/Saigon (UTC+07:00).
- Tạo docs/api-pdf-extract-preview.md: mô tả mục đích, endpoint, role, cấu hình Procedure/AIOCR, multipart file, response, Swagger/Postman/curl, ví dụ FE fetch, ProblemDetails và liên kết luồng lưu bản nháp/xuất bản.
- File thay đổi: docs/api-pdf-extract-preview.md (mới), PROGRESS.md (cập nhật).
- Không thêm/sửa API: tài liệu cho POST /api/v1/procedure-manager/drafts/extract-preview, request multipart file, 200 ExtractionResult; 400/401/403/413/502/503/504 và lỗi hệ thống 500. Preview không lưu Blob/DB.
- FE: Bearer PROCEDURE_MANAGER hoặc IT_ADMIN; không truyền service key, không tự đặt multipart boundary, hiển thị warnings; payload trống khi AI tắt là bình thường. Không đổi token IAM.
- Kiểm chứng: đối chiếu controller và tài liệu triển khai hiện tại; kiểm tra file Markdown và liên kết nội bộ. Không chạy build/test vì chỉ thêm tài liệu; số test mới 0. Không dùng kết quả 152 tests của phiên trước như kết quả chạy lại phiên này.
- Không chạy Git, không build Docker, không deploy.
- Gợi ý commit: docs: add PDF extraction preview API usage guide

## DOC-API-001 — Danh mục và hướng dẫn sử dụng toàn bộ API

- Hoàn thành: 2026-10-04 22:00:20 Asia/Saigon (UTC+07:00).
- File mới: docs/api-guide.md. File cập nhật: PROGRESS.md. Không sửa mã nguồn hoặc cấu hình hệ thống.
- Tài liệu bao phủ 53 thao tác controller: IAM 29, Procedure Catalog 17, Document Form 6, AIOCR 1; thêm 6 endpoint root, 7 health endpoint và Swagger theo môi trường.
- Không tạo endpoint mới. Mỗi endpoint hiện có được ghi HTTP method, route, quyền, request/body/query, response DTO và status; có ví dụ cho login/HttpOnly, hồ sơ, RBAC, tìm kiếm/phân trang, version/publish, PDF draft/review và DOCX/schema.
- FE: phân biệt route trực tiếp/Gateway, CSRF header/cookie refresh không có JSON token, policy ProcedureManager, revision draft, thời hạn SAS, các kiểu metadata phân trang và lỗi khác nhau. Ghi đúng hiện trạng DocumentForm chưa gắn xác thực, AIOCR nội bộ dùng service key, Workflow/Analytics chưa có API nghiệp vụ.
- Kiểm chứng: đối chiếu tự động đủ 53/53 controller operations với bảng tài liệu; 4/4 ví dụ JSON parse thành công. Đối chiếu DTO, validators và cấu hình route trong source.
- Build: dotnet build WardMate.sln -c Release --no-restore -warnaserror — thành công, 0 errors, 0 warnings.
- Tests: 0 test mới; không chạy dotnet test vì thay đổi chỉ là tài liệu. Không tuyên bố kết quả test từ phiên trước là kết quả của phiên này.
- Không chạy Git, không build Docker, không deploy.
- Gợi ý commit tách file cho Antigravity: docs: add complete API reference and usage guide (docs/api-guide.md); docs: record API documentation completion (PROGRESS.md).

## DOC-API-002 — Giải thích từng API theo tình huống sử dụng

- Hoàn thành: 2026-10-04 22:11:00 Asia/Saigon (UTC+07:00).
- File thay đổi: docs/api-guide.md, AGENTS.md, PROGRESS.md.
- Bổ sung 53 mục riêng cho 53 API controller: mục đích, cách gọi/quyền, kết quả, lỗi sử dụng và hướng xử lý bằng tiếng Việt dễ hiểu; hướng dẫn root/health/Swagger. Giữ phần DTO/request/status chi tiết làm tài liệu tra cứu phía sau.
- AGENTS.md: yêu cầu cập nhật chính docs/api-guide.md khi thêm hoặc thay đổi API, bao gồm tình huống sử dụng sai và cách khắc phục.
- Endpoint mới/thay đổi: không có. Không sửa hành vi API, request body hoặc response code.
- FE: làm rõ 401/403, cookie và refresh, khác nhau giữa tài khoản/hồ sơ, role/quyền, preview/draft/publish, revision và cập nhật version, xử lý schema dưới dạng chuỗi JSON.
- Kiểm chứng: đối chiếu 53/53 route controller có mục giải thích riêng; Release build với --no-restore -warnaserror thành công, 0 lỗi, 0 cảnh báo.
- Tests: 0 test mới; không chạy dotnet test vì chỉ sửa tài liệu/quy tắc tài liệu. Không khẳng định coverage kiểm thử nghiệp vụ từ kiểm tra tài liệu.
- Không chạy Git, Docker hoặc deploy.
- Commit gợi ý theo file: docs: explain each API with usage scenarios and troubleshooting; docs: require ongoing API guide updates; docs: record API guide clarification.

## DOC-API-003 — Trình bày API theo mẫu Service / Nhóm chức năng / Bảng

- Hoàn thành: 2026-10-05 12:42:00 Asia/Saigon (UTC+07:00).
- File thay đổi: docs/api-guide.md, PROGRESS.md.
- Sắp xếp lại theo mẫu chủ dự án: trách nhiệm từng service, các nhóm chức năng, bảng Method & Đường dẫn / Quyền / Mục đích-cách dùng-kết quả / Trường hợp sai-cách xử lý. Giữ phụ lục DTO và ví dụ kỹ thuật để tra cứu.
- Bổ sung đầy đủ nhóm draft/PDF, publish, AIOCR và tuyến Gateway còn thiếu trong mẫu. Giữ thông tin đúng code: REGISTERED_CITIZEN, reason tùy chọn, DocumentForm chưa gắn xác thực, route Gateway DocumentForm có prefix, thời hạn token lấy từ cấu hình/response.
- API mới/thay đổi: không có; không thay request, response hoặc quyền. Nội dung mô tả 53 API nghiệp vụ cùng root/health/Swagger và reverse proxy.
- Kiểm chứng: đối chiếu 53/53 endpoint controller có trong bảng nhóm; dotnet build WardMate.sln -c Release --no-restore -warnaserror thành công, 0 lỗi, 0 cảnh báo.
- Tests: 0 test mới; không chạy dotnet test vì chỉ thay đổi tài liệu. Không dùng kết quả build để tuyên bố tests đã pass.
- FE: giữ hướng dẫn cookie/CSRF, quyền, revision, trạng thái draft, DTO và tình huống lỗi theo từng API; tiếp tục cập nhật chính docs/api-guide.md khi API thay đổi.
- Không chạy Git, không build Docker, không deploy.
- Gợi ý commit: docs: organize API guide by service and feature group; docs: record API guide reorganization.

## DOC-API-004 — Markdown dễ đọc trực tiếp trong IDE

- Hoàn thành: 2026-10-05 12:45:30 Asia/Saigon (UTC+07:00).
- File thay đổi: docs/api-guide.md, PROGRESS.md.
- Giữ nhóm service/chức năng; chuyển bảng dài thành 53 mục API với quyền, mục đích, cách dùng, kết quả và lỗi riêng dòng. Loại bỏ HTML br và bảng rộng; ngắt dòng văn xuôi khoảng 110 ký tự, giữ code block và nội dung kỹ thuật.
- Không thay endpoint, request, response, quyền hoặc hành vi token. FE tiếp tục dùng các DTO/ProblemDetails và hướng dẫn hiện có.
- Kiểm tra: đủ 53 mục API, 4 ví dụ JSON hợp lệ, không còn HTML br/bảng rộng. Release build --no-restore -warnaserror: 0 lỗi, 0 cảnh báo.
- Tests: 0 test mới; không chạy dotnet test vì chỉ sửa tài liệu. Không chạy Git/Docker/deploy.
- Gợi ý commit: docs: make API guide readable in source editors; docs: record Markdown readability update.

## IAM-WARD-001 — Quản lý tài khoản theo phường và danh mục để Front Desk tự lọc

- Hoàn thành: 2026-10-05 15:00:00 Asia/Saigon (UTC+07:00).
- Yêu cầu đã chốt: IT_ADMIN quản lý toàn hệ thống; MANAGER quản lý Front Desk cùng phường, được tạo tài khoản và cấp/gỡ riêng role FRONT_DESK_OFFICER. Mỗi Manager có một phường; một phường có nhiều Front Desk. Catalog chỉ cung cấp lĩnh vực/bộ lọc, chưa gán cứng lĩnh vực cho cán bộ.

### Thay đổi nghiệp vụ và bảo vệ phạm vi

- Thêm Ward và User.WardId nullable, FK/index; tài khoản cũ chưa gán phường giữ null. Manager không có phường bị từ chối quản lý (403).
- Scope dựa trên DB hiện tại, không nhận phường từ JWT/request để quyết định quyền. Lọc trước count/paging và bảo vệ lookup/update/delete theo ID.
- Admin = IT_ADMIN có iam.manage đang hoạt động. Manager chỉ quản lý tài khoản Front Desk cùng phường, không phải chính mình; loại các tài khoản kiêm role quản trị/role tùy chỉnh khỏi phạm vi Manager để tránh tác động tài khoản đặc quyền.
- Không cấp iam.manage toàn cục cho Manager. Các API RBAC tạo/sửa/xóa role, gán quyền, audit vẫn Admin-only. PUT/DELETE user-role mở riêng FRONT_DESK_OFFICER cùng phường cho Manager; người nhận không được có role khác ngoài Front Desk/Citizen.
- Tạo Front Desk tạo user/profile/role/phường trong một transaction. Admin phải chọn phường; Manager dùng phường DB của mình, truyền phường khác bị 403. Mật khẩu dùng BCrypt và validator đăng ký hiện có.
- Kiểm tra scope và mutation cùng transaction/advisory lock với RBAC/đổi phường/khóa tài khoản. Sửa IdentityStore để dùng transaction có sẵn khi quản trị ghi profile, tránh transaction lồng nhau.
- Đổi phường chỉ Admin; cập nhật DB và thu hồi refresh token. GET users/me bổ sung wardId; access token cũ không vượt scope IAM mới.

### Endpoints

- Mới GET /api/v1/users?page=1&pageSize=20: không body; 200 {items,page,pageSize,total}; items có id/username/email/isActive/wardId?/wardName?/profile?/roles[{id,roleName}]; 400 phân trang, 401/403 quyền. Không trả hash/token; pageSize tối đa 100.
- Mới POST /api/v1/accounts/front-desk: JSON {username,email,password,fullName,wardId?}; 201 ManagedUserDto; 400 dữ liệu, 403 phường/quyền, 404 phường không hợp lệ, 409 trùng tài khoản. Không cấp token đăng nhập.
- Mới GET /api/v1/accounts/wards: không body; 200 WardDto[] {id,code,name}; Admin tất cả, Manager phường mình; 401/403.
- Mới POST /api/v1/accounts/wards: Admin; JSON {code,name}; 201 WardDto, 400 validation, 401/403, 409 mã trùng.
- Mới PUT /api/v1/accounts/{userId}/ward: Admin; JSON {wardId:Guid|null}; 204, 401/403, 404 user/phường. Null gỡ phường; body {} cũng có wardId null theo DTO, FE phải gửi rõ lựa chọn.
- Mới GET /api/v1/procedures/categories: public, không body; 200 [{id,categoryName,description?}], dùng id với categoryId của API thủ tục hiện có; không gán lĩnh vực vào tài khoản. Gateway route procedures hiện có đã bao phủ endpoint.
- Đổi quyền GET /api/v1/accounts, GET /api/v1/accounts/{userId}, PUT /api/v1/accounts/{userId}/status và GET/POST/PUT/DELETE /api/v1/users/{userId}/profile: Admin toàn hệ thống hoặc Manager trong scope. User ngoài scope trả 404 ở nhóm này.
- Đổi quyền PUT/DELETE /api/v1/rbac/users/{userId}/roles/{roleId}: Manager chỉ cấp/gỡ FRONT_DESK_OFFICER cùng phường, ngoài phạm vi/quyền trả 403; Admin giữ chức năng hiện có.

### File tạo mới hoặc sửa

IAM Domain (src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/):
- Entities/Ward.cs (mới)
- Entities/User.cs

IAM Application (src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/):
- Accounts/IManagementScope.cs (mới)
- Accounts/StaffRequests.cs (mới)
- Accounts/AccountRequests.cs
- DTOs/IdentityDtos.cs
- Rbac/RbacCommandHandlers.cs

IAM Infrastructure (src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/):
- DependencyInjection.cs
- Persistence/ManagementScope.cs (mới)
- Persistence/StaffAdministration.cs (mới)
- Persistence/AccountStore.cs
- Persistence/IdentityStore.cs
- Persistence/IamDbContext.cs
- Persistence/Configurations/IdentityConfigurations.cs
- Persistence/Configurations/WardConfiguration.cs (mới)
- Persistence/Migrations/20261005074640_WardScopedAccountManagement.cs (mới)
- Persistence/Migrations/20261005074640_WardScopedAccountManagement.Designer.cs (mới)
- Persistence/Migrations/IamDbContextModelSnapshot.cs

IAM API (src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/):
- Authorization/AccountManagementAuthorization.cs (mới)
- Authorization/PermissionAuthorization.cs
- Controllers/StaffAdministrationController.cs (mới)
- Controllers/AccountsController.cs
- Controllers/AdminProfilesController.cs
- Controllers/UsersController.cs
- Controllers/RbacController.cs

Procedure Catalog (src/Services/WardMate.Services.ProcedureCatalog/):
- WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureCategoryDto.cs (mới)
- WardMate.Services.ProcedureCatalog.Application/Queries/GetProcedureCategoriesQuery.cs (mới)
- WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs
- WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs
- WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs

Kiểm thử và tài liệu:
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs (thêm 1 test vào project đã có, không tạo lại project test IAM)
- docs/api-guide.md (59 API được đối chiếu; hướng dẫn mới, cập nhật scope/role/DTO)
- PROGRESS.md

### Kiểm chứng

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: thành công, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-build --no-restore: 153/153 passed, 0 failed, 0 skipped (DocumentForm 72; ProcedureCatalog 81).
- Thêm 1 integration test vào test project hiện hữu: danh mục public cung cấp ID và lọc được nhiều lĩnh vực khác nhau mà không gán tài khoản.
- Smoke test IAM độc lập: 52 HTTP checks PASS trên database PostgreSQL tạm mới, cùng assertions count/paging/profile/phường. Bao gồm migration DB sạch, Manager chưa có phường, tạo Front Desk, dữ liệu sai/trùng, chặn khác phường, bảo vệ account nhiều role, không cấp Admin, cấp/gỡ Front Desk, CRUD profile, khóa/mở, scope đổi ngay với token cũ, Manager bị khóa.
- Smoke script nằm trong thư mục tạm, không tạo project test mới. Database tạm và tiến trình IAM cổng 5198 đã dọn sau chạy. Không dùng DB deploy để seed hoặc kiểm thử.
- EF has-pending-model-changes: không còn thay đổi model chưa có migration.
- Đối chiếu tài liệu đủ 59/59 controller operations.

### Hướng dẫn bàn giao

- Migration mới chỉ được áp dụng/kiểm chứng trên DB tạm. Chưa migrate DB Local Dev đang dùng hoặc Azure. Runtime AutoMigrate hiện có sẽ áp dụng khi bản mới được triển khai với cấu hình tương ứng.
- Admin tạo phường, gán role MANAGER và wardId cho quản lý hiện có; Manager sau đó tự tạo Front Desk trong phường. Không tự gán phường giả cho tài khoản thật.
- FE dùng GET users cho màn hình nhân sự, AccountPage cho danh sách gọn; CurrentUserDto thêm wardId nullable; ManagedUserDto.roles có id/roleName, khác roles:string[] trong users/me. Cookie/CSRF/token handling giữ nguyên.
- FE không dựa riêng JWT role để quyết định phạm vi; xử lý 403 thiếu phường/quyền, 404 đối tượng ngoài phạm vi và 409 xung đột. Mọi message validation mới là tiếng Việt.
- Catalog là bộ lọc tự chọn; chưa có phân công/giới hạn lĩnh vực theo cán bộ, chưa có CRUD danh mục.
- Không chạy lệnh Git, không build image Docker, không deploy. Antigravity xử lý commit từng file theo quy tắc hiện hành.
- Gợi ý Conventional Commits theo phần thay đổi: feat(iam): scope account management by ward; feat(iam): let managers provision front desk accounts; feat(procedure-catalog): expose procedure category filters; test(procedure-catalog): verify category discovery and filtering; docs: describe ward scoped management APIs.

## IAM-PROFILES-001 — API danh sách hồ sơ người dùng

Hoàn thành: 2026-10-05 21:44:37 +07:00 (Asia/Saigon, UTC+07:00).

- Thêm GET /api/v1/users/profiles?page=1&pageSize=20, không request body, Bearer JWT của IAM.
- 200 ProfilePage {items,page,pageSize,total}; item có userId, fullName, identityNumber, phoneNumber,
  dateOfBirth, gender, permanentAddress, temporaryAddress, updatedAt. Null bị bỏ theo cấu hình IAM.
- Chỉ lấy profile tồn tại; bao gồm profile tài khoản bị khóa trong phạm vi. Sắp xếp FullName, UserId.
- Admin theo policy quản trị hiện tại xem toàn hệ thống; Manager chỉ Front Desk cùng phường, loại tài khoản
  có role đặc quyền khác. Tái sử dụng ManagementScope, lọc trước Count/Skip/Take. Không nhận wardId từ FE.
- 400 query sai (page 1..1000000, pageSize 1..100, validation tiếng Việt); 401 token sai/hết hạn;
  403 thiếu quyền/phường hoặc actor bị khóa. Trang không có hồ sơ trả 200 items rỗng.
- FE lấy userId để mở API profile chi tiết; gọi các trang liên tiếp nếu cần lấy toàn bộ phạm vi.
  Không đổi cookie/CSRF hoặc database schema, không cần migration.

File thay đổi (đường dẫn trong src/Services/WardMate.Services.IAM/):

- WardMate.Services.IAM.API/Controllers/AdminProfilesController.cs
- WardMate.Services.IAM.Application/Profiles/ProfileRequests.cs
- WardMate.Services.IAM.Application/Profiles/ProfileHandlers.cs
- WardMate.Services.IAM.Application/Profiles/ProfileValidators.cs
- WardMate.Services.IAM.Application/Interfaces/IProfileStore.cs
- WardMate.Services.IAM.Infrastructure/Persistence/ProfileStore.cs
- docs/api-guide.md và PROGRESS.md tại repository (hướng dẫn có 60 mục API).

Kiểm chứng:

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-build --no-restore: 152 passed, 0 failed, 0 skipped
  (DocumentForm 71; ProcedureCatalog 81).
- Số test IAM mới: 0; không tạo lại project test theo AGENTS.md. Bộ test hiện có không bao phủ API IAM mới;
  đã rà soát route, DTO, validation, DI và tái sử dụng scope quản lý. Chưa kiểm thử HTTP API mới trên DB thật.
- Gợi ý Conventional Commit: feat(iam): add paginated profiles within management scope.

## DF-DEPLOY-LOCAL-20261006 — Docker recovery and Azure deployment preparation

Completion time: 2026-10-06 17:13:48 +07:00 (Asia/Saigon). Local checkpoint complete; Azure deployment incomplete pending actual resource names/access. No Git commands or remote writes performed.

Changed paths:
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Dockerfile — fix malformed UseAppHost publish argument.
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Queries/UserSubmissionQueries.cs — fix enum status filtering that caused PostgreSQL-backed Officer GET to return 500; unknown status now returns 400.
- docs/documentform-deployment.md (new) — local restart instructions, actual API contracts, Azure checkpoints and verified gaps.
- PROGRESS.md — this entry.

Runtime work: restarted existing wardmate_postgres, wardmate_pgadmin, wardmate_azurite without deleting volumes. Created separate UTF8 wardmate_documentform_db; old wardmate_db preserved. Built wardmate-documentform:v1, started wardmate_documentform on 127.0.0.1:5004 with PostgreSQL host postgres and Azurite proxy URI http://azurite. Docker health is healthy. Both EF migrations applied: 20261002151540_InitialDocumentFormSchema, 20261004142021_AddUserSubmissions; five document schema tables verified.

Validation:
- Final dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1: PASS, 0 warnings, 0 errors. Initial default parallel build returned exit 1 without diagnostics; serial build succeeded, and was repeated successfully after the filter fix.
- Docker build and publish: PASS, 0 compiler warnings/errors.
- Automated test runner: 0 tests run; no test projects added or recreated. Existing test csproj files were present in checkout and were compiled by solution build; no automated coverage claim.
- 13 HTTP smoke checks passed (includes pgAdmin login, health, template listing, status variants/invalid statuses, create template/draft, populated filtering and DOCX download). Initial Officer status check reproduced 500 before fix; subsequent checks passed.
- Synthetic local data retained for inspection only: template 9a8ffa13-4f51-44a1-a9fd-ce216caa6d3a and draft d532741b-b92b-4ed7-85e0-a86af0f6c212; generated temporary DOCX removed. No real citizen data used.

Endpoints/request bodies/status codes:
- GET localhost:5050/login: 200.
- GET /health: 200 Healthy (liveness only).
- GET /api/v1/form-templates: 200 paginated DTO (items array).
- POST /api/v1/form-templates: JSON {code,title}, 201.
- POST /api/v1/citizen/submissions/draft: multipart templateId, applicantId, file (.docx), 201. JSON from proposed deployment plan is not the current contract.
- GET /api/v1/officer/submissions?status=Submitted, Draft, submitted: 200; invalid and 999: 400 {code:document.invalid_submission_status,message}. Populated check: Draft count 1, Submitted count 0.
- GET /api/v1/citizen/submissions/{submissionId}/download-docx: 200, 829-byte synthetic DOCX downloaded from Azurite.

Frontend/ProblemDetails/token notes: no DTO or token shape changes. Status filter now validates enum values; business errors use code/message, while model validation/global errors use ProblemDetails. DocumentForm and Gateway source currently lack JWT/role enforcement on these routes. Public access to real records requires authorization/ownership work before release. Draft storage currently uses configured form-templates container with user-submissions prefix; separate submissions container is not used automatically.

Outstanding Azure checkpoints: verify PostgreSQL FQDN/admin, ACR name/login server/image, Storage account, Gateway URL/environment; configure secrets, private ingress, database/Blob network access; verify revision/logs and gateway APIs, then CI/CD. Screenshot establishes IAM in managedEnvironment-rgwardmateprod-bd51, Japan East, not planned cae-wardmate-prod. No Azure deployment/CI/CD success claimed. User chose Azure Container Apps, not VM.

Antigravity commit suggestions (separate functional updates/new files; no squash): fix(documentform): correct Docker publish argument; fix(documentform): filter submissions using mapped status enum; docs(documentform): add local recovery and Azure deployment checklist; docs(progress): record DocumentForm deployment preparation.

## DF-SWAGGER-ACR-20261006 — Production Swagger and registry image

Completed local preparation at 2026-10-06 18:19:55 +07:00 (Asia/Saigon); cloud deployment still pending.
Changed: src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Program.cs; docs/documentform-deployment.md; PROGRESS.md.
Swagger:Enabled enables Swagger outside Development. Relative ./v1/swagger.json supports Gateway prefix /api/document-form/swagger/; OpenAPI server / targets the existing gateway /api/v1 routes. No business DTO, ProblemDetails or token changes.
Validation: solution Release build --no-restore -warnaserror -m:1 PASS (0 warnings/errors); Docker build/publish PASS. Production container with Swagger__Enabled=true and Database__AutoMigrate=false: GET /swagger/index.html 200; GET /swagger/v1/swagger.json 200 with server /. Temporary container removed. No database accessed. Two HTTP checks; 0 automated tests run, no test project created. Gateway end-to-end Swagger not yet verified.
Local image: acrwardmate2026.azurecr.io/wardmate-documentform:demo-20261006. Not pushed. Azure CLI unavailable on PATH; user authentication pending. Registry hostname and separate Supabase project connection fields supplied by user; no password stored. Supabase migration, cloud Blob and Azure revision remain unverified.
Suggested commits: feat(documentform): enable configurable Swagger behind gateway; docs(documentform): record registry and Swagger deployment configuration; docs(progress): record Swagger preparation. Antigravity handles Git; none executed.


## DF-GATEWAY-V2-DIAG — 2026-10-06 22:11:04 +07:00 (Asia/Saigon)
Changed path: PROGRESS.md only. Read-only cloud registry access: refreshed local ACR login and pulled Gateway v2; no cloud resource mutation or Git. Image digest sha256:d596e2d292a79649f39a13500b175255e2b1231ace30adc7cf1b3dc9cf7b83c5. Image appsettings confirms document-form / primary identifiers, but lacks direct DocumentForm /api/v1 routes. Local temporary Gateway with environment override targeted http://127.0.0.1:15099/health as expected. One HTTP smoke request GET /api/document-form/health (no body) returned expected 502 because synthetic destination had no listener; log proves override applied. Temporary container removed. 0 automated tests run; build not rerun for documentation-only diagnosis (previous Release build 0 warnings/errors). Azure user-supplied template contains correct override but runtime logs still show localhost:5004; runtime environment/deployed image identity remains to verify. No DTO/ProblemDetails/token changes. Azure deployment remains incomplete. Suggested commit: docs(progress): record Gateway v2 override diagnosis.



## DF-ONLINE-ORIGINAL-DOCX — 2026-10-07 07:52 +07:00 (Asia/Saigon)

Completed local backend implementation for an electronic form within a ProcedureCatalog checklist. ProcedureCatalog remains owner of procedures/checklists; DocumentForm does not approve the whole administrative application. Existing ProcedureCatalog FormDefinitionSchema.FormTemplateId links the services without cross-database access.

Paths changed/new (relative to src/Services/WardMate.Services.DocumentForm unless stated otherwise; Domain/Application/Infrastructure/API below mean WardMate.Services.DocumentForm.Domain/.Application/.Infrastructure/.API respectively):
- Domain: Models/DocxFieldMapping.cs (new); Entities/FormTemplateVersion.cs; Entities/UserSubmission.cs.
- Application: Interfaces/IDocxFormEngine.cs (new), IDocumentDbContext.cs, IFormSchemaEngine.cs; Services/OnlineFormSupport.cs (new), FormSchemaEngine.cs; Commands/ConfigureOnlineFormCommand.cs (new), SaveDraftSubmissionCommand.cs, SubmitSubmissionCommand.cs, UploadFormTemplateDocxCommand.cs, CreateFormTemplateCommand.cs; Queries/GetFormTemplateByIdQuery.cs, DownloadFormTemplateDocxQuery.cs, UserSubmissionQueries.cs; DTOs/FormTemplateDto.cs, UserSubmissionDto.cs. Deleted Commands/OfficerReviewCommands.cs.
- Infrastructure: DependencyInjection.cs; OpenXml/DocxFormEngine.cs (new), DocxPlaceholderEngine.cs (missing OpenXml using fixed); Persistence/DocumentDbContext.cs; Persistence/Configurations/FormTemplateVersionConfiguration.cs (new), UserSubmissionConfiguration.cs; Migrations/20261006184319_OnlineFormDrafts.cs and .Designer.cs (new), DocumentDbContextModelSnapshot.cs.
- API: Controllers/FormTemplatesController.cs, CitizenSubmissionsController.cs; deleted Controllers/OfficerSubmissionsController.cs.
- src/Gateways/WardMate.YarpGateway/appsettings.json: removed obsolete DocumentForm officer route.
- tests/WardMate.Services.DocumentForm.Tests/OnlineFormTests.cs (new in existing project); scripts/verify-documentform-online.py (new); azure-documentform-api.http; root docs/api-guide.md, docs/documentform-deployment.md, PROGRESS.md.

API contract (prefix /api/v1):
- GET /form-templates: existing pagination; 200. POST /form-templates JSON {code,title}: 201, validation400, duplicate409.
- GET /form-templates/{id}: 200 now includes onlineReady, templateVersionId, versionNumber, schemaDefinition object; 404.
- POST /form-templates/{id}/upload-docx multipart file: 200, invalid400, missing404; original bytes stored intact. GET /form-templates/{id}/download-docx: 200 original binary, missing404.
- NEW GET /form-templates/{id}/docx-structure: 200 {originalSha256,paragraphs:[{index,text}]}; 400/404/503.
- NEW PUT /form-templates/{id}/online-config JSON {schemaDefinition,originalSha256,mappings:[{fieldId,paragraphIndex,start,length,expectedText}]}: 200 {templateVersionId}; 400 invalid mapping/schema, 404 missing, 409 changed source/concurrent config, 503 dependency.
- POST /citizen/submissions/draft JSON {templateId,applicantId,templateVersionId?,formData:{...}}: 201; 400 validation, 404 missing, 409 unconfigured/legacy/version conflict, 503 dependency. Multipart no longer accepted (415).
- PUT /citizen/submissions/{id}/draft JSON {applicantId,templateVersionId?,formData:{...}}: 200; replaces all JSON, pins original version; 400 finalized/invalid, 403 ownership mismatch, 404 missing, 409 concurrency/legacy, 503 dependency.
- GET /citizen/submissions?applicantId=...: 200 paged; 400 empty applicant/invalid paging/status. GET /citizen/submissions/{id}?applicantId=...: 200 includes stored formData and pinned schema; 403/404.
- POST /citizen/submissions/{id}/submit JSON {applicantId}: 200 Submitted after complete required-field validation; 400 incomplete/already finalized, 403/404/409. Pass submissionId to Workflow separately.
- GET /citizen/submissions/{id}/download-docx?applicantId=...: 200 generated binary; 403/404/503.
- REMOVED GET /form-templates/{id}/docx-url and all /officer/submissions list/detail/download/request-revision/approve endpoints. Old entity columns/data retained for compatibility; legacy file-only submissions cannot be edited/submitted through new JSON workflow.

Frontend: schemaDefinition is an object with existing custom snake_case sections/field_id schema (not generic JSON Schema). Reopen drafts using their pinned schema; PUT sends complete formData; missing required permitted in drafts, blocked at submit. Citizen and online-config errors use ProblemDetails with code/traceId and fieldErrors (camelCase) for field validation. Existing template endpoints retain code/message error shape. No token changes; controllers still lack JWT authorization, applicantId is supplied data, not proof of identity. Production identity binding/admin authorization remains pending IAM integration.

DOCX preservation: source remains byte-for-byte unchanged, SHA256 protects mapping against source changes; generated output replaces mapped ranges in a copy, retaining other package parts and run formatting. No placeholder insertion into original. Admin configures fields and UTF-16 paragraph offsets once per source version. Supports body/table text, not automatic semantic understanding of every official form; headers/footers/images preserved but not mapped. Long input may reflow pages. Real government DOCX visual verification and FE admin mapping screen remain outside this local backend delivery; no real sample supplied. Replaced draft output blobs are not garbage-collected yet.

Validation of final source: dotnet build WardMate.sln -c Release --no-restore --nologo -m:1 PASS, 0 warnings, 0 errors. Existing DocumentForm test project: 76 passed, 0 failed, 0 skipped (includes 5 new tests; no test project recreated). Local-only HTTP script: 29 checks PASS against rebuilt API and separate PostgreSQL database wardmate_online_smoke_20261007 / Azurite container online-smoke-20261007, synthetic data only. Covers original byte equality, generated DOCX, partial/complete validation, ownership checks, pinned versions/new original behavior and removed Swagger endpoints. Local API stopped after verification; dedicated synthetic data retained. Migration applied only to dedicated local test DB. No full ProcedureCatalog integration test run; no cloud migration/deployment, no Git commands.

Suggested Conventional Commit subject for feature: feat(documentform): support online drafts with preserved original DOCX
Antigravity commit granularity: each new file listed above gets its own commit (feat for source/migration, test for OnlineFormTests and verification script); existing-file updates separated by functional change; docs(api-guide), docs(deployment), docs(progress) separate. Do not squash. Complete feature validated together; intermediate dependency commits may not compile. No commits or push executed by Codex.



## DF-AZURE-ONLINE-DEPLOY — 2026-10-07 08:12 +07:00 (Asia/Saigon)

Completed direct Azure deployment at user's explicit request. No Git. Local paths changed this session: docs/documentform-deployment.md, PROGRESS.md only; deployed prior completed source. Image acrwardmate2026.azurecr.io/wardmate-documentform:online-20261007-0755; digest sha256:9b32a1a6c00ca1fe010cf3f8278cc5fec0369abf23e32cc9247c1edff3b74bfb. Docker build 0 warnings/errors and local health/Swagger PASS. Existing whole-solution final build and 76 tests/29 local HTTP checks from DF-ONLINE-ORIGINAL-DOCX still apply; no source changes here.

Azure wardmate-documentform Succeeded, image verified, Swagger enabled, AutoMigrate enabled, existing secret references preserved. Internal new API returns schema and DB-backed results; public template detail queries form_template_versions successfully, citizen list queries new columns successfully. Gateway destination duplicate fixed to one HTTPS address; citizen route added; template route preserved. Revision restart returned InternalServerError. Automatic review initially rejected shared Gateway stop due downtime scope; user explicitly approved stop/start, then completed successfully. Gateway runtime had stale environment before restart, final public tests passed afterward. CLI log stream still fails eventStreamEndpoint on Express; used container console and HTTP checks instead.

Seven final public HTTP checks: GET /api/document-form/health200; GET /api/document-form/swagger/index.html200; GET /api/document-form/swagger/v1/swagger.json200 (online-config present, draft application/json, obsolete endpoints absent); GET /api/v1/form-templates200; GET /api/v1/citizen/submissions?applicantId=<synthetic UUID>200; GET /api/v1/form-templates/{existing-id}200; POST /api/v1/citizen/submissions/draft body {}400 (validation, no record created). Cloud create/upload/fill/submit not run; 29 end-to-end checks already passed locally. No new cloud test records. Temporary local Docker check container removed.

Frontend request/DTO/ProblemDetails unchanged from DF-ONLINE-ORIGINAL-DOCX. User clarified login belongs to shared system; no separate account/login implementation and no token changes. Test Swagger at https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html. Configure schema/mappings after uploading an original DOCX, then draft JSON and submit. Existing legacy records preserved, not auto-converted to online templates. Suggested Antigravity commit: docs(documentform): record verified Azure online-form deployment (separate docs/progress commits per standing workflow).

## DF-ONLINE-DOCX-APPEND � 2026-10-07 08:52 +07:00 (Asia/Saigon)

Completed a major refactor of the DOCX generation logic to avoid modifying the original Official DOCX templates directly (which was a hard requirement). We now automatically append a "D? LI?U KHAI B�O TR?C TUY?N" (Online Declaration Data) page at the end of the generated PDF/DOCX containing all the citizen's filled data.

Paths changed:
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/ConfigureOnlineFormCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/SaveDraftSubmissionCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/UploadFormTemplateDocxCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Interfaces/IDocxFormEngine.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Services/OnlineFormSupport.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/OpenXml/DocxFormEngine.cs
- tests/WardMate.Services.DocumentForm.Tests/OnlineFormTests.cs
- docs/api-guide.md

API changes:
- "PUT /api/v1/form-templates/{id}/online-config": Removed mappings from the required JSON payload. It now only takes schemaDefinition.
- "GET /api/v1/form-templates/{id}/docx-structure": Removed entirely.

This replaces the strict positional replacement engine with an automated append-to-end engine. Build and Tests succeeded (75/75 passed). Committed as eeab7626a64670c775485d6dbc07f13372e5540c. Waiting for user to confirm Docker execution.

## DF-ONLINE-DOCX-APPEND � 2026-10-07 08:52 +07:00 (Asia/Saigon)

Completed a major refactor of the DOCX generation logic to avoid modifying the original Official DOCX templates directly (which was a hard requirement). We now automatically append a "D? LI?U KHAI B�O TR?C TUY?N" (Online Declaration Data) page at the end of the generated PDF/DOCX containing all the citizen's filled data.

Paths changed:
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/ConfigureOnlineFormCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/SaveDraftSubmissionCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/UploadFormTemplateDocxCommand.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Interfaces/IDocxFormEngine.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Services/OnlineFormSupport.cs
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/OpenXml/DocxFormEngine.cs
- tests/WardMate.Services.DocumentForm.Tests/OnlineFormTests.cs
- docs/api-guide.md

API changes:
- "PUT /api/v1/form-templates/{id}/online-config": Removed mappings from the required JSON payload. It now only takes schemaDefinition.
- "GET /api/v1/form-templates/{id}/docx-structure": Removed entirely.

This replaces the strict positional replacement engine with an automated append-to-end engine. Build and Tests succeeded (75/75 passed). Committed as eeab7626a64670c775485d6dbc07f13372e5540c. Waiting for user to confirm Docker execution.

## DF-LOCAL-API-REPAIR — 2026-10-07 14:35 +07:00 (Asia/Saigon)

Completed local-only diagnosis/fix after owner refactored DOCX output to append online data. No Git, Azure, registry push, or deployment in this session. Preserved owner's no-mapping API and appended declaration-page design.

Root causes: running Docker DocumentForm targeted legacy wardmate_db; startup migration failed because application_forms already existed. Legacy form_template_versions had created_at instead of created_at_utc and lacked original/checksum/mapping columns; user_submissions absent. Detail queries returned500 and configure caught DB schema errors. Upload had already succeeded200 in later logs. Also ConfigureOnlineForm stored literal auto-fill as checksum, causing every new draft to fail SHA256 validation409. Generated Word appended paragraphs after final sectPr (invalid body order), and original download did not decode percent-escaped Blob names.

Changed paths:
- docker/docker-compose.yml: DocumentForm DB=wardmate_documentform_db; default Azurite proxy host.docker.internal for container access, environment override preserved.
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/ConfigureOnlineFormCommand.cs: download pinned original and save real SHA256; schema-only request preserved.
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Queries/DownloadFormTemplateDocxQuery.cs: decode Blob URL path for filenames containing Unicode/spaces.
- src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/OpenXml/DocxFormEngine.cs: append paragraphs before final sectPr, preserve label spacing and convert multiline values into Word breaks.
- tests/WardMate.Services.DocumentForm.Tests/OnlineFormTests.cs: strengthened existing append test with OpenXmlValidator and section/newline assertions; existing fixture table schema corrected. No test project created.
- scripts/verify-documentform-online.py: optional real DOCX input, Unicode filename, assert removed docx-structure endpoint.
- docs/api-guide.md: correct current schema-only/append semantics, removed obsolete structure row, local recovery instructions and checksum recovery.
- PROGRESS.md: this record.

Local operations: created dedicated wardmate_documentform_db, applied all three existing EF migrations via rebuilt container, copied four form_templates rows into empty destination (IDs and Blob paths unchanged). Old wardmate_db and all old data remain untouched; other legacy tables were not migrated/copied. Existing blobs retained. Recreated only wardmate_document_form via docker compose up --build --no-deps. Container remains running, Gateway unchanged. Synthetic SMOKE templates/drafts from verification retained only in new local DB/Blob storage.

Endpoint validation: POST /api/v1/form-templates JSON {code,title}201; POST /{id}/upload-docx multipart file200; GET /{id}/download-docx200 byte-identical; PUT /{id}/online-config JSON {schemaDefinition}200; GET /{id}200 onlineReady/schema/version; POST /api/v1/citizen/submissions/draft JSON {templateId,templateVersionId,applicantId,formData}201; PUT /{id}/draft JSON {applicantId,formData}200; GET /{id}?applicantId200; generated download200; POST /{id}/submit JSON {applicantId}200 when complete,400 missing required/already submitted; wrong owner403; unknown field400; missing applicant400; new original invalidates new draft409 and pinned existing draft stays usable. Routes tested through localhost:5000/api/document-form plus three final direct Gateway/UI checks200.

Final validation: whole solution Release build PASS 0 warnings/errors. Existing DocumentForm tests75 passed,0 failed,0 skipped. HTTP script27 checks PASS with synthetic DOCX,27 PASS with the actual previously uploaded DOCX from template cdfd09b9-dd5a-4703-b326-8404bd3d7e0f; source remains byte-identical, generated output contains form data. Three final Gateway checks200 (Swagger UI, original template detail, citizen list):57 HTTP checks total excluding download used as test input. Docker build/publish PASS. No visual pagination validation of official document; OpenXml schema validated by unit fixture. Docker Compose warns unrelated JWT_KEY undefined; no IAM/workflow containers recreated.

Frontend/ProblemDetails/token: no DTO or token change; configuration still only {schemaDefinition}. No positional mappings or automatic detection of official blank fields; output appends declaration data. Previously configured auto-fill versions must be replaced by reconfiguring template before creating new drafts. Existing identity integration unchanged. Owner should refresh http://localhost:5000/api/document-form/swagger/index.html and test existing template. Azure remains previous deployment, untouched.

Suggested Antigravity functional commits (no Git executed): fix(documentform): pin original checksum for schema-only drafts; fix(documentform): preserve Word section order and multiline values; fix(documentform): decode original blob filenames; fix(docker): isolate DocumentForm local database; test(documentform): verify refactored online workflow; docs(documentform): document local schema recovery. Separate file/function commits per AGENTS; do not squash.

## DF-LOCAL-DATA-LOCATION — 2026-10-07 14:40:33 +07:00 (Asia/Saigon)
Read-only verification: local DocumentForm uses PostgreSQL host postgres, database wardmate_documentform_db. Confirmed the user-specified KHAM_SUC_KHOE template exists locally with the same ID shown in their Swagger201 response; no record contents persisted in this log. Supabase screenshot is a separate cloud database. No automatic local/cloud replication configured by this work. Changed path: PROGRESS.md only. No API/DTO/ProblemDetails/token changes; no cloud mutation or Git. One read-only SQL check passed; 0 automated tests run, build not rerun for documentation-only diagnosis (previous Release build0 warnings/errors). Suggested commit: docs(progress): record local template storage verification.


## DF-DOCX-EDITOR-ROUNDTRIP — 2026-10-07 15:17 +07:00 (Asia/Saigon)

Completed local backend change requested by owner: official DOCX is downloaded into FE Word editor, FE exports edited DOCX for draft save, reopens saved bytes and explicitly finalizes. Supersedes prior schema/formData/append-page API workflow. No Git commands, cloud mutation or deployment executed. Existing unrelated work and historical schema columns retained; no migration needed.

Changed paths (under src/Services/WardMate.Services.DocumentForm unless stated):
- WardMate.Services.DocumentForm.API/Controllers/CitizenSubmissionsController.cs: POST/PUT draft multipart file DTOs, updated summaries, removed schema error extension.
- WardMate.Services.DocumentForm.API/Controllers/FormTemplatesController.cs: removed online-config endpoint/request; original download for editor.
- WardMate.Services.DocumentForm.API/Program.cs: Swagger describes file round-trip.
- WardMate.Services.DocumentForm.Application/Commands/SaveDraftSubmissionCommand.cs: validate/store exact edited bytes in separate unique blob; ownership/status checks and best-effort orphan cleanup on DB save failure.
- WardMate.Services.DocumentForm.Application/Commands/SubmitSubmissionCommand.cs: verify saved blob exists, owner and Draft transition; no required-field/schema validation.
- WardMate.Services.DocumentForm.Application/Commands/UploadFormTemplateDocxCommand.cs: shared DOCX validator, original bytes preserved.
- WardMate.Services.DocumentForm.Application/Commands/ConfigureOnlineFormCommand.cs: removed obsolete command/handler.
- WardMate.Services.DocumentForm.Application/Services/DocxUpload.cs: NEW shared bounded validation, 20 MiB input, 5,000 ZIP entries/100 MiB expanded, DOCX body required/no macros; returns original bytes.
- WardMate.Services.DocumentForm.Application/DTOs/FormTemplateDto.cs and UserSubmissionDto.cs: remove schema/formData/version fields from public editor DTOs; onlineReady based on active original upload.
- WardMate.Services.DocumentForm.Application/Queries/GetFormTemplateByIdQuery.cs and UserSubmissionQueries.cs: remove schema/version retrieval, byte download retained with decoded blob path.
- documentform-api.http and azure-documentform-api.http: current multipart examples; cloud sample explicitly unverified pending deployment.
- tests/WardMate.Services.DocumentForm.Tests/OnlineFormTests.cs: add validation/preservation test in existing project. Old utility tests remain but do not describe current public API workflow.
- scripts/verify-documentform-online.py: replace JSON workflow with 30 local HTTP checks for file workflow.
- docs/api-guide.md: sections3/6 rewritten with all11 business endpoints, statuses, FE editor import/export adapter and error recovery; stale appendix updated.
- PROGRESS.md: this entry.

Endpoint contract: POST /api/v1/form-templates JSON {code,title}->201; GET list/detail->200; POST /{templateId}/upload-docx multipart file->200; GET /{templateId}/download-docx->200 binary. POST /api/v1/citizen/submissions/draft multipart {templateId,applicantId,file}->201; PUT /{submissionId}/draft multipart {applicantId,file}->200. GET list/detail with applicantId->200 metadata; GET /{submissionId}/download-docx?applicantId->200 exact saved binary. POST /{submissionId}/submit JSON {applicantId}->200 Submitted. Invalid file/missing required file/already-finalized edits400; wrong owner403; missing original404; inactive template/concurrency409; JSON draft415; draft storage failure503. PUT online-config removed from Swagger/controller. Existing original/sample blob not overwritten by verification.

Verification completed: full WardMate.sln Release build0 warnings/0 errors; existing DocumentForm suite76 passed/0 failed/0 skipped (no new test project). Docker build/publish passed and only document-form local container rebuilt/recreated. HTTP smoke30 passed using synthetic DOCX and30 passed using existing official DOCX copied via local API as test input:60 assertions/check calls total. Both runs check exact original/edited bytes, draft reopen/update, separate citizen blobs, unchanged saved draft after later admin upload, ownership, submit locking, missing/invalid uploads, JSON415 and removed Swagger API. Synthetic DOCX-* records retained in local dedicated DocumentForm DB/Blob only. Gateway source/config untouched. No visual editor test: no FE editor implemented in this task.

FE: API now accepts multipart instead of JSON formData (breaking contract change). Fetch template binary into editor; export DOCX Blob into FormData; browser supplies boundary. Save submissionId and reopen submission binary, not current admin template. Await save before submit. Submitted means this one document completed, not full procedure submitted. Backend cannot prove arbitrary DOCX blanks are complete or guarantee FE layout fidelity. Citizen errors ProblemDetails/code/traceId; template business errors retain code/message. Existing applicantId integration unchanged; these controllers do not themselves authenticate JWT, so trusted identity integration remains a separate system responsibility. No changes to shared IAM or JWT configuration.

Suggested Conventional Commit for Antigravity: feat(documentform): support DOCX editor draft round-trip. Split new file, functional existing-file updates and documentation commits according to AGENTS.md; no commits executed.

## DOCX editor deployment — 2026-10-07 15:40:24 +07:00

User approved local tests and explicitly requested Azure deployment. Deployed image
`acrwardmate2026.azurecr.io/wardmate-documentform:editor-20261007-01`, digest
`sha256:0e67569f1b39dad6ca3360253a05daee7629daaeb8ca608e63b435cf91fb0f26`.
Container App wardmate-documentform in rg-wardmate-prod reports Succeeded. Previous image was
online-20261007-0854; previous provisioning error was BuildFailed: no build sandbox capacity.
Built image locally and pushed to ACR; updated image only, preserved existing secrets/config.
Gateway unchanged; no restart required. No Git commands or branch pushes.

Five Azure HTTP checks passed200: /api/document-form/health, Swagger UI, Swagger JSON,
/api/v1/form-templates?page=1&pageSize=1 and citizen list for synthetic applicant UUID.
OpenAPI confirms multipart draft and absence of online-config. No cloud test records created;
full save/download/submit verified locally earlier (76 tests,60 HTTP checks). Docker release build
this session0 warnings/errors. Cloud file round-trip still available for owner testing, not claimed tested here.

Current contract: template create JSON/upload DOCX/download binary; citizen POST draft multipart
{templateId,applicantId,file}201, PUT draft multipart {applicantId,file}200,
GET list/detail/download200, POST submit JSON {applicantId}200. See docs/api-guide.md sections3/6
for errors and FE adapter. No JWT/identity or database migration changes in this deployment.

Swagger: https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html

Changed paths: docs/documentform-deployment.md, src/Services/WardMate.Services.DocumentForm/azure-documentform-api.http, PROGRESS.md. Suggested Antigravity commit: docs(documentform): record verified DOCX editor Azure release. No Git executed.

## VERIFY-AZURE-DRAFTS â€” Kiá»ƒm tra endpoint draft trÃªn Azure (chÆ°a hoÃ n táº¥t upload)

Thá»i gian: 2026-10-05 23:02:20 +07:00 (Asia/Saigon).
- Host: https://wardmate-procedure-catalog.blackmeadow-a2f12767.japaneast.azurecontainerapps.io
- GET /health: 200; GET /swagger/v1/swagger.json: 200.
- Swagger deploy cÃ³ POST /api/v1/procedure-manager/drafts, multipart/form-data trÆ°á»ng File,
  Bearer authentication, response thÃ nh cÃ´ng Ä‘Æ°á»£c khai bÃ¡o 202 DraftDto.
- GET /api/v1/procedure-manager/drafts khÃ´ng token: 401 auth.unauthorized.
- ChÆ°a gá»i upload cÃ³ xÃ¡c thá»±c, chÆ°a xÃ¡c minh cáº¥u hÃ¬nh/ghi Ä‘á»c Azure Blob trÃªn deploy do chÆ°a cÃ³ phiÃªn
  hoáº·c tÃ i khoáº£n test Ä‘Æ°á»£c cung cáº¥p. KhÃ´ng káº¿t luáº­n Blob thiáº¿u cáº¥u hÃ¬nh chá»‰ tá»« 401.
- MÃ£ nguá»“n local dÃ¹ng AzureBlob:ConnectionString, container procedure-sources;
  thiáº¿u cáº¥u hÃ¬nh tráº£ 503 draft.storage_not_configured sau khi vÆ°á»£t qua xÃ¡c thá»±c.
- KhÃ´ng Ä‘á»•i code/API; chá»‰ cáº­p nháº­t PROGRESS.md. KhÃ´ng build/test suite (kiá»ƒm tra HTTP remote, khÃ´ng sá»­a code).
- KhÃ´ng cháº¡y Git, khÃ´ng deploy, khÃ´ng táº¡o tÃ i nguyÃªn Azure hoáº·c dá»¯ liá»‡u draft.

## VERIFY-AZURE-DRAFTS-02 â€” XÃ¡c minh báº£n deploy thiáº¿u route draft

Thá»i gian: 2026-10-05 23:46:24 +07:00 (Asia/Saigon).
- Kiá»ƒm tra trá»±c tiáº¿p host wardmate-procedure-catalog.blackmeadow-a2f12767.japaneast.azurecontainerapps.io.
- GET /swagger/v1/swagger.json: 200; OpenAPI hiá»‡n chá»‰ liá»‡t kÃª 8 paths (procedure manager CRUD/publish/version,
  public list/detail vÃ  root), khÃ´ng cÃ³ /api/v1/procedure-manager/drafts hoáº·c cÃ¡c route draft con.
- GET /api/v1/procedure-manager/drafts khÃ´ng token: 404 body rá»—ng; GET /api/v1/procedures: 200.
- KhÃ¡c láº§n kiá»ƒm tra trÆ°á»›c tá»«ng cÃ³ drafts trong Swagger. Báº±ng chá»©ng hiá»‡n táº¡i cho tháº¥y báº£n phá»¥c vá»¥ request
  khÃ´ng cÃ´ng bá»‘ endpoint draft; cáº§n kiá»ƒm tra image/revision vÃ  traffic, chÆ°a xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c cáº¥u hÃ¬nh Azure
  cá»¥ thá»ƒ do khÃ´ng cÃ³ quyá»n Ä‘á»c Azure quáº£n trá»‹. KhÃ´ng quy lá»—i nÃ y cho JWT hoáº·c Blob.
- ChÆ°a test upload cÃ³ xÃ¡c thá»±c; khÃ´ng táº¡o dá»¯ liá»‡u. KhÃ´ng sá»­a code, khÃ´ng build/test, khÃ´ng Git/deploy.
- File cáº­p nháº­t: PROGRESS.md. API má»›i: khÃ´ng. Test tá»± Ä‘á»™ng má»›i: 0.

## PROC-FE-001 â€” HoÃ n thiá»‡n API quáº£n trá»‹ Procedure Catalog theo yÃªu cáº§u FE

HoÃ n thÃ nh: 2026-10-06 22:07:56 +07:00 (Asia/Saigon, UTC+07:00).

### Káº¿t quáº£

- Bá»• sung Ä‘á»§ 8 operations FE Ä‘á» nghá»‹, CQRS MediatR, validation tiáº¿ng Viá»‡t, policy ProcedureManager
  (PROCEDURE_MANAGER hoáº·c IT_ADMIN); role MANAGER phÆ°á»ng khÃ´ng tá»± cÃ³ quyá»n quáº£n trá»‹ thá»§ tá»¥c.
- Chi tiáº¿t quáº£n trá»‹ tráº£ cáº£ inactive, public detail giá»¯ bá»™ lá»c active.
- Category CRUD: trim tÃªn, chá»‘ng trÃ¹ng khÃ´ng phÃ¢n biá»‡t hoa/thÆ°á»ng, serialize táº¡o/Ä‘á»•i tÃªn báº±ng advisory lock;
  cháº·n xÃ³a náº¿u cÃ³ thá»§ tá»¥c active hoáº·c inactive liÃªn káº¿t, FK báº£o vá»‡ dá»¯ liá»‡u.
- Migration AlignCategoryIdentitySequence Ä‘iá»u chá»‰nh sequence ID sau dá»¯ liá»‡u seed 1â€“3, khÃ´ng tua lÃ¹i bá»™ Ä‘áº¿m
  database Ä‘Ã£ cÃ³ dá»¯ liá»‡u. KhÃ´ng Ä‘á»•i schema báº£ng hoáº·c xÃ³a dá»¯ liá»‡u cÅ©.
- Discard: khÃ³a draft, cháº·n Published/Processing vÃ  PDF Ä‘Æ°á»£c tham chiáº¿u bá»Ÿi thá»§ tá»¥c/version. Ghi Deleting
  trÆ°á»›c khi xÃ³a Blob; chá»‰ xÃ³a báº£n ghi sau Blob thÃ nh cÃ´ng. Lá»—i giá»¯ báº£n ghi Ä‘á»ƒ retry DELETE; khÃ´ng cÃ³ worker
  tá»± retry xÃ³a. Máº¥t káº¿t ná»‘i sau xÃ³a Blob váº«n cho phÃ©p gá»i láº¡i Ä‘á»ƒ hoÃ n táº¥t. KhÃ´ng tuyÃªn bá»‘ transaction phÃ¢n tÃ¡n.
- Rollback: khÃ³a thá»§ tá»¥c, xÃ¡c minh snapshot/category/code, snapshot tráº¡ng thÃ¡i trÆ°á»›c khÃ´i phá»¥c thÃ nh version
  káº¿ tiáº¿p, phá»¥c há»“i ná»™i dung cÃ¹ng PDF/checklist/forms. Giá»¯ isActive, StatusChangeReason, Id, CreatedAt;
  cáº­p nháº­t UpdatedAt vÃ  quyáº¿t Ä‘á»‹nh hiá»‡n táº¡i tá»« input. snapshotData.rollback ghi actor/lÃ½ do/phiÃªn báº£n nguá»“n.
- Nguá»“n PDF lá»‹ch sá»­: chá»‰ kÃ½ Blob thuá»™c draft Published cÃ¹ng thá»§ tá»¥c/URL Ä‘Æ°á»£c lÆ°u trong version,
  kiá»ƒm tra Blob tá»“n táº¡i; khÃ´ng kÃ½ URL bÃªn ngoÃ i. SAS read-only 600 giÃ¢y, response no-store.
- Dropdown biá»ƒu máº«u: gá»i HTTP DocumentForm, khÃ´ng tham chiáº¿u database/domain service khÃ¡c; chá»‰ active,
  phÃ¢n trang vÃ  tÃ¬m mÃ£. Há»£p Ä‘á»“ng DocumentForm hiá»‡n lÃ  DOCX nÃªn formType=DOCX_TEMPLATE, chÆ°a cÃ³ provider
  cho ONLINE_INTERACTIVE. KhÃ´ng suy Ä‘oÃ¡n ráº±ng thiáº¿u file DOCX lÃ  biá»ƒu máº«u online.

### File táº¡o má»›i

DÆ°á»›i src/Services/WardMate.Services.ProcedureCatalog/:
- WardMate.Services.ProcedureCatalog.Application/Management/AdministrationRequests.cs
- WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureAdministration.cs
- WardMate.Services.ProcedureCatalog.Infrastructure/DocumentFormsClient.cs
- WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureAdministrationController.cs
- WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261006145135_AlignCategoryIdentitySequence.cs
- WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261006145135_AlignCategoryIdentitySequence.Designer.cs

Tests má»›i trong project hiá»‡n cÃ³:
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AdministrationTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/DocumentFormsClientTests.cs

### File chá»‰nh sá»­a/Ä‘Æ°á»£c tooling táº¡o láº¡i

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/appsettings.json
- Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs trong service (EF tÃ¡i sinh,
  model khÃ´ng thay Ä‘á»•i; migration chá»‰ Ä‘iá»u chá»‰nh sequence).
- docker/docker-compose.yml: thÃªm DocumentForm__BaseUrl cho procedure-catalog.
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/DraftTestBlobStorage.cs (giáº£ láº­p lá»—i xÃ³a theo blob).
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs (Ä‘á»‘i chiáº¿u toÃ n bá»™
  migration thay vÃ¬ cá»‘ Ä‘á»‹nh sá»‘ lÆ°á»£ng lÃ  5).
- docs/api-guide.md: 8 má»¥c API má»›i 66â€“73, DTO/lá»—i/tÃ¬nh huá»‘ng FE, cáº­p nháº­t ná»™i dung cÅ© bá»‹ mÃ¢u thuáº«n.
- PROGRESS.md.

### Endpoints vÃ  DTO

Prefix /api/v1/procedure-manager, táº¥t cáº£ dÃ¹ng Bearer vÃ  policy ProcedureManager; 401/403 theo xÃ¡c thá»±c/quyá»n.

- GET /procedures/{id}: khÃ´ng body; 200 ProcedureDetailDto ká»ƒ cáº£ inactive; 404 khÃ´ng tá»“n táº¡i.
- DELETE /drafts/{id}: khÃ´ng body; 204 hoÃ n táº¥t; 404 khÃ´ng tá»“n táº¡i; 409 Ä‘ang xá»­ lÃ½/Ä‘Ã£ xuáº¥t báº£n/Ä‘ang tham chiáº¿u;
  503 draft.storage_unavailable. Cho phÃ©p NeedsReview, Failed, Queued hoáº·c retry Deleting.
- POST /categories: {categoryName, description?}; 201 ProcedureCategoryDto {id,categoryName,description};
  400 validation; 409 tÃªn trÃ¹ng. TÃªn tá»‘i Ä‘a 255, mÃ´ táº£ tá»‘i Ä‘a 4000 kÃ½ tá»±.
- PUT /categories/{id}: body nhÆ° POST; 200 ProcedureCategoryDto; 400/404/409.
- DELETE /categories/{id}: khÃ´ng body; 204; 404; 409 category.in_use.
- POST /procedures/{id}/versions/{versionNumber}/rollback: {reason,decisionNumber,effectiveDate: yyyy-MM-dd};
  200 ProcedureDetailDto; 400 validation; 404 thiáº¿u thá»§ tá»¥c/version; 409 snapshot/danh má»¥c/mÃ£ khÃ´ng phÃ¹ há»£p.
  Version má»›i lÆ°u tráº¡ng thÃ¡i TRÆ¯á»šC rollback theo quy Æ°á»›c cÅ©, thÃªm audit trong snapshotData.rollback.
- GET /document-forms?page=1&pageSize=50&searchCode=...: khÃ´ng body; 200 máº£ng
  {id,formCode,formName,formType}; 400 query; 502 response nguá»“n sai; 503 thiáº¿u cáº¥u hÃ¬nh/upstream lá»—i;
  504 timeout. PageSize tá»‘i Ä‘a 100, khÃ´ng cÃ³ metadata trong response máº£ng; FE táº£i thÃªm trang Ä‘áº¿n máº£ng rá»—ng.
- GET /procedures/{id}/versions/{versionId}/source: khÃ´ng body; 200 {url,expiresInSeconds:600};
  404 version/file khÃ´ng há»£p lá»‡ hoáº·c ngoÃ i há»‡ thá»‘ng; 503 Blob chÆ°a sáºµn sÃ ng.

### Kiá»ƒm chá»©ng

- dotnet restore WardMate.sln: thÃ nh cÃ´ng sau khi Ä‘Æ°á»£c phÃ©p truy cáº­p NuGet ngoÃ i sandbox.
  LÆ°á»£t build Ä‘áº§u cÃ³ assets cÅ© thiáº¿u dependency; khÃ´ng chá»‰nh code ApplicationWorkflow ngoÃ i pháº¡m vi Ä‘á»ƒ nÃ© lá»—i.
- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-build --no-restore: 186 passed, 0 failed, 0 skipped.
  ProcedureCatalog 96 (tÄƒng 15: 11 integration cases + 4 HTTP adapter unit cases), DocumentForm 71,
  ApplicationWorkflow 19. KhÃ´ng táº¡o test project má»›i.
- Kiá»ƒm tra PostgreSQL 16 táº¡m: migration, category ID/duplicate/constraint, inactive detail vs public,
  rollback snapshot/audit/giá»¯ inactive/Ä‘á»“ng thá»i/invalid snapshot, xÃ³a Blob+row/Processing/Published,
  lá»—i Blob retry, nguá»“n lá»‹ch sá»­/no-store/khÃ¡c thá»§ tá»¥c vÃ  phÃ¢n quyá»n.
- HTTP DocumentForm test dÃ¹ng handler giáº£ láº­p Ä‘á»ƒ kiá»ƒm tra DTO, escaping query vÃ  lá»—i upstream; Blob dÃ¹ng
  fake trong integration tests. ChÆ°a test káº¿t ná»‘i DocumentForm hoáº·c Blob Azure tháº­t trong task nÃ y.
- EF has-pending-model-changes: khÃ´ng cÃ³ thay Ä‘á»•i model ngoÃ i migration.
- LÆ°á»£t test Ä‘áº§u phÃ¡t hiá»‡n assertion migration cÅ© cá»‘ Ä‘á»‹nh 5; Ä‘Ã£ cáº­p nháº­t vÃ  cháº¡y láº¡i toÃ n suite thÃ nh cÃ´ng.

### BÃ n giao

- Cáº§n deploy source má»›i vÃ  Ã¡p dá»¥ng migration AlignCategoryIdentitySequence; kiá»ƒm thá»­ chá»‰ Ã¡p dá»¥ng trÃªn DB táº¡m.
- Azure cáº§n DocumentForm__BaseUrl trá» Ä‘Ãºng service DocumentForm (khÃ´ng dÃ¹ng localhost); giá»¯ JWT vÃ  Blob
  Ä‘ang hoáº¡t Ä‘á»™ng. Compose Ä‘Ã£ cáº¥u hÃ¬nh URL ná»™i bá»™. Frontend dÃ¹ng manager detail má»›i cho form sá»­a inactive.
- Gáº·p Deleting thÃ¬ khÃ³a sá»­a/publish, cho thá»­ DELETE láº¡i; khÃ´ng tá»± xÃ³a file gá»‘c cá»§a báº£n Published.
- Danh má»¥c chá»‰ cÃ²n trong snapshot lá»‹ch sá»­ cÃ³ thá»ƒ xÃ³a, nhÆ°ng rollback vá» danh má»¥c Ä‘Ã£ xÃ³a tráº£ 409 Ä‘á»ƒ Ä‘á»‘i soÃ¡t.
- CÃ¡c thay Ä‘á»•i trong workspace khÃ´ng Ä‘á»“ng nghÄ©a Azure Ä‘Ã£ cÃ³ API má»›i. KhÃ´ng cháº¡y Git, khÃ´ng build image,
  khÃ´ng deploy hoáº·c Ä‘á»•i tÃ i nguyÃªn Azure. KhÃ´ng commit secrets.
- Gá»£i Ã½ Conventional Commits (Antigravity tÃ¡ch tá»«ng file theo quy táº¯c hiá»‡n hÃ nh):
  feat(procedure-catalog): complete manager administration APIs;
  fix(procedure-catalog): align category identity sequence after seeding;
  test(procedure-catalog): cover rollback and draft deletion safeguards;
  docs: document procedure manager administration APIs.

## PROC-SYNC-GIT-20261007 â€” Pull latest remote commits and prepare procedure administration commits

HoÃ n thÃ nh: 2026-10-07 09:10:00 +07:00 (Asia/Saigon, UTC+07:00).

- NhÃ¡nh: kha.
- ÄÃ£ kÃ©o cÃ¡c commit má»›i tá»« origin/kha (bao gá»“m tÃ­nh nÄƒng DocumentForm E-Form online drafts vÃ  tÃ i liá»‡u deploy Azure).
- Giáº£i quyáº¿t xung Ä‘á»™t merge thÃ nh cÃ´ng trong docs/api-guide.md vÃ  PROGRESS.md, khÃ´ng cÃ³ xung Ä‘á»™t mÃ£ nguá»“n C#.
- BiÃªn dá»‹ch Release: dotnet build WardMate.sln -c Release -warnaserror Ä‘áº¡t 0 lá»—i, 0 cáº£nh bÃ¡o.
- Kiá»ƒm thá»­ unit: DocumentForm 76/76 passed; ProcedureCatalog DocumentFormsClientTests 4/4 passed. (CÃ¡c integration tests dÃ¹ng Testcontainers yÃªu cáº§u Docker Desktop Ä‘ang dá»«ng).
- Sáº¯p xáº¿p vÃ  chuáº©n bá»‹ commit theo quy táº¯c háº¡t nhÃ¢n cá»§a AGENTS.md (má»—i file má»›i 1 commit, tÃ¡ch riÃªng functional updates). BÃ¡o cÃ¡o sá»‘ lÆ°á»£ng commit cho ngÆ°á»i dÃ¹ng vÃ  chá» xÃ¡c nháº­n trÆ°á»›c khi thá»±c hiá»‡n git push origin kha.
## PROC-SWAGGER-001 — Tách controller theo nhóm nghiệp vụ

Thời gian: 2026-10-07 14:43:49 +07:00 (Asia/Saigon, UTC+07:00).
- Xóa controller gom chung ProcedureAdministrationController.cs.
- Chuyển GET /api/v1/procedure-manager/procedures/{id}, POST .../{id}/versions/{versionNumber}/rollback
  và GET .../{id}/versions/{versionId}/source vào ProcedureManagerController.cs.
- Chuyển DELETE /api/v1/procedure-manager/drafts/{id} vào ProcedureDraftsController.cs.
- Tạo ProcedureCategoriesController.cs cho POST /api/v1/procedure-manager/categories,
  PUT/DELETE /api/v1/procedure-manager/categories/{id}.
- Tạo ProcedureDocumentFormsController.cs cho GET /api/v1/procedure-manager/document-forms.
- Các file controller nằm dưới src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/.
- Cập nhật docs/api-guide.md và PROGRESS.md. FE giữ nguyên HTTP method, URL, request body, DTO, token và
  xử lý ProblemDetails/status codes; chỉ thay nhóm Swagger. Không migration hoặc thay đổi nghiệp vụ.
- Swagger: ProcedureManager (chi tiết/rollback/source lịch sử), ProcedureDrafts (xóa draft),
  ProcedureCategories (3 API danh mục), ProcedureDocumentForms (tra cứu biểu mẫu).
- Release build toàn solution với --no-restore -warnaserror -m:1: PASS, 0 errors, 0 warnings.
  Lượt build song song trước đó kết thúc thất bại không có compiler diagnostic; build tuần tự thành công.
- Không tạo test mới cho việc tổ chức controller. Kết quả test hồi quy ghi bổ sung bên dưới.
- Không chạy Git, không build Docker hoặc deploy; cần build lại Procedure image để xem nhóm mới.
- Gợi ý commit: refactor(procedure-catalog): organize controllers by feature.
- Test hồi quy AdministrationTests, ProcedureDraftTests, ProcedureManagerTests: 42 passed, 0 failed, 0 skipped trên PostgreSQL tạm; không chạy lại toàn suite trong phiên chỉ tổ chức controller này.


## DF-FORM-TEMPLATES-GIT-HANDOFF — 2026-10-07T15:46:42.175659+07:00
Owner explicitly authorized Codex Git and main/nghia updates for this session, superseding standing no-Git restriction for this task. Created22 scoped file commits for completed form templates/editor, local fixes, tests and docs. Integrated origin/main d3084f4 (16 ProcedureCatalog commits); documentation conflicts resolved preserving both feature guides and progress histories. Docker compose auto-merge preserved both services. Full solution Release build0 warnings/errors; DocumentForm tests76 passed0 failed0 skipped. No API changes beyond documented DOCX editor contract; no new token changes. Intended atomic fast-forward push of integrated HEAD to main and nghia, no force and no push to kha/deploy. Push success to be verified by remote refs. Suggested/release merge message: feat(documentform): complete form templates and online DOCX editing.


## DF-ROLE-AUTHORIZATION — 2026-10-08T07:25:33.360146+07:00 (Asia/Saigon)
Completed local source role enforcement requested by owner: all5 FormTemplates endpoints require PROCEDURE_MANAGER, all6 Citizen endpoints require REGISTERED_CITIZEN. No IT_ADMIN override. JWT matches IAM HS256/role/sub contract, checks signature/issuer/audience/expiry with zero skew. Startup requires Jwt:Key >=32 bytes and issuer/audience; no default secret. Citizen applicantId must equal validated JWT sub (nonempty UUID), preventing caller-selected identity. Request DTOs unchanged; added auth401/403 ProblemDetails code/traceId. Wrong/missing applicantId now403; malformed GUID remains model validation400. Swagger Bearer configured. Health remains anonymous.
Changed/new paths: API/Security/DocumentAuthentication.cs (new), API/Program.cs, API/WardMate.Services.DocumentForm.API.csproj, API/Controllers/CitizenSubmissionsController.cs, API/Controllers/FormTemplatesController.cs under src/Services/WardMate.Services.DocumentForm; docker/docker-compose.yml (JWT env only DocumentForm); documentform-api.http and azure-documentform-api.http; docs/api-guide.md; scripts/verify-documentform-authorization.py (new); scripts/verify-documentform-online.py (requires two token env vars); PROGRESS.md.
Validation: WardMate.sln Release build PASS0 errors0 warnings. Existing DocumentForm tests76 passed0 failed0 skipped. Isolated real API HTTP auth script34 checks passed, no DB writes: all11 routes no-token401 and wrong-role403; expired/wrong issuer/wrong audience401; IT_ADMIN403; invalid sub403; spoofed applicantId403 on list/detail/download/submit; correct roles reach business validation400; Swagger security scheme exists. Uses random temporary JWT signing key and terminates test server. No test projects created. Full file smoke script updated but not rerun with real IAM tokens.
Integration note: owner explicitly requested entire template group manager-only, so citizen direct template GET/download and Procedure inter-service calls now require an authorized design (forward manager token when caller is manager, or future separate citizen read endpoints). Do not share manager token with citizen FE. These changes are source-only; existing Docker runtime and Azure not rebuilt/deployed, no Git executed in this task. Configure Jwt__Key/Issuer/Audience equal IAM before starting updated service. Deployment and real IAM login verification not performed.
Suggested Conventional Commit: feat(documentform): enforce citizen and procedure manager roles.

## DF-LOCAL-AUTH-RUNTIME — 2026-10-08 (Asia/Saigon)
User reports missing Swagger Authorize on localhost5000. Verified running Swagger lacks Bearer definition; old container still running. IAM local is restarting with empty Jwt__Key; both .env sources lack nonempty JWT_KEY. No secrets printed or changed. Built wardmate-document-form:local with JWT code: Docker Release build0 warnings/errors, publish passed. Runtime replacement pending user choice of shared local key versus team IAM key; did not change IAM, Azure or Gateway. Changed path PROGRESS.md only; no API changes this turn;0 tests rerun (prior34 auth checks/76 tests passed). Suggested commit docs(progress): record local JWT configuration blocker.


## DF-AZURE-ROLES — 2026-10-08T07:33:04.759580+07:00 (Asia/Saigon)
Owner requested immediate Azure deployment using shared IAM JWT configuration. Deployed acrwardmate2026.azurecr.io/wardmate-documentform:roles-20261008-01; digest sha256:a18fc3db2053af513707df5a5deca0f4c9b94b3f942f6e3db9c20a89bab126e5. Copied IAM signing key in memory into DocumentForm secret iam-jwt-key; configured Jwt__Key secretref and same IAM issuer/audience. No keys written into source/log/chat. IAM and Gateway unchanged. Local container remains old (local IAM key absent); user selected Azure instead of local key setup.
Cloud verification5 HTTP checks: health200, Swagger UI200, OpenAPI200 with Bearer definition/operation security, templates unauthenticated401, citizen list unauthenticated401. Does not claim real IAM login tested; role/identity checks were tested locally34 HTTP checks and76 existing tests. Docker build0 warnings/errors. All5 template routes require PROCEDURE_MANAGER, all6 citizen routes REGISTERED_CITIZEN and applicantId=JWT sub. No request DTO changes; auth401/403 ProblemDetails. Template reads are manager-only per request; citizen first-open template integration requires a separate read-access design.
Changed documentation paths: docs/documentform-deployment.md, docs/api-guide.md, PROGRESS.md. No Git/push or database writes. Suggested commit: feat(documentform): deploy IAM-backed role authorization.

## IAM-RBAC-DEPLOYED-001 — Bổ sung danh mục quyền cho chức năng đã deploy

Hoàn thành: 2026-10-07 16:07:47 +07:00 (Asia/Saigon, UTC+07:00).

### Phạm vi và đối chiếu

- Đọc OpenAPI Azure của IAM, ProcedureCatalog và DocumentForm qua Gateway. IAM gồm quản lý tài khoản/
  phường/RBAC; Procedure có CRUD/draft/category/rollback/source; DocumentForm có templates và citizen submissions.
- Chỉ bổ sung dữ liệu RBAC tương ứng các chức năng đã thấy trên deploy. Không thêm quyền Analytics/RAG/SMS
  hoặc ApplicationWorkflow khi chưa xác nhận endpoint deploy của chúng trong phiên này.
- Danh mục tăng từ 3 lên 30 permission: 6 IAM quản trị có phạm vi, 15 ProcedureCatalog, 6 DocumentForm.
  Tên quyền tiếng Việt; mapping chi tiết trong docs/api-guide.md.
- Phân quyền mặc định: IT_ADMIN có quyền nghiệp vụ toàn danh mục; PROCEDURE_MANAGER quản trị thủ tục/mẫu;
  MANAGER chỉ bổ sung account/ward read theo phạm vi; mỗi role có nhóm biểu mẫu và đơn điện tử của chính mình.
- SQL dùng permission_code/role_name để tra ID, ON CONFLICT DO NOTHING, cùng advisory lock RBAC hiện hữu.
  Đồng bộ sequence permissions để không trùng ID seed. Không overwrite tên/quyền custom đã có.
- Migration chạy một lần, không tự tái cấp permission đã bị Admin thu hồi mỗi lần restart.
  Down chủ động chặn rollback tự động tránh xóa các quyền đã được quản trị tùy chỉnh; muốn thu hồi cần
  migration điều chỉnh có đối soát hoặc API RBAC hiện hữu.

### Kiểm chứng và cập nhật database deploy

- Connection được người dùng trực tiếp cung cấp để cập nhật IAM deploy. Chỉ truyền qua environment của
  tiến trình; không ghi vào source/config/docs hoặc file credential. Không ghi secret vào báo cáo.
- Read-only preflight xác nhận public.roles, permissions, role_permissions, users và các IAM migrations.
  Database postgres hiện dùng chung IAM/Procedure; migration mới không chạm bảng Procedure hoặc user.
- Pending IAM trước áp dụng đúng một migration mới; nếu có migration khác, công cụ đã cấu hình dừng.
- Smoke PostgreSQL16 tạm: fresh migration PASS, 30 permission, sequence không trùng seed, không cấp
  iam.manage/Procedure quyền cho Manager trên DB sạch, citizen chỉ quyền cá nhân/đọc mẫu, chạy migrate lại
  không tái cấp quyền đã thu hồi. Sáu nhóm assertion PASS. Không tạo project test mới.
- Release solution build --no-restore -warnaserror -m:1: PASS 0 errors, 0 warnings.
- EF has-pending-model-changes: không còn thay đổi model ngoài migration.
- Áp dụng thành công migration 20261007090139_DeployedFeaturePermissionCatalog trên DB deploy.
  Trước: 3 permissions, 12 role_permissions. Sau: 30 permissions, 78 role_permissions.
  Added 27 permissions và 66 grants; assertion mọi permission definition và role grant cũ còn nguyên PASS.
- Tổng quyền theo role deploy: REGISTERED_CITIZEN 7, FRONT_DESK_OFFICER 7, MANAGER 11,
  PROCEDURE_MANAGER 23, IT_ADMIN 30. Manager có một grant cũ khác DB seed sạch, giữ nguyên theo yêu cầu
  bảo toàn cấu hình, không tự ý thu hồi. Còn 0 pending IAM migrations.
- Không chạy lại full suite: thay đổi chỉ migration dữ liệu, đã kiểm tra thực tế trên PostgreSQL tạm và
  đọc lại dữ liệu deploy. Không tuyên bố test HTTP đăng nhập/RBAC trên deploy (không có phiên đăng nhập test).

### File thay đổi

- Mới: src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261007090139_DeployedFeaturePermissionCatalog.cs
- Mới: src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261007090139_DeployedFeaturePermissionCatalog.Designer.cs
- EF tái sinh IamDbContextModelSnapshot.cs cùng thư mục (không có model schema change).
- docs/api-guide.md: danh mục quyền, mapping role, hiệu lực và cách refresh thông tin quyền.
- PROGRESS.md. Công cụ smoke nằm trong thư mục tạm ngoài repository, không chứa connection string.

### Hướng dẫn FE và giới hạn hiệu lực

- Không thêm/sửa endpoint hay request/response DTO. GET /api/v1/rbac/permissions (Admin) nay đọc 30 quyền;
  GET /api/v1/rbac/roles/{roleId}/permissions phản ánh grants; các status code/token handling giữ nguyên.
- Đăng nhập/refresh để nhận permissions mới trong JWT; tải GET /api/v1/users/me để cập nhật giao diện.
- Đây là catalog và gán quyền DB; không đổi chính sách authorization runtime. Procedure vẫn kiểm role,
  gỡ riêng permission mới chưa chặn endpoint. DocumentForm chưa kiểm JWT/ownership từ token; bổ sung quyền
  không được coi là đã bảo vệ API. IAM giữ nguyên kiểm tra phạm vi phường hiện có.
- Không deploy image, không chạy bất kỳ lệnh Git nào. Database đã được cập nhật theo yêu cầu trực tiếp.
- Credential đã gửi trong chat: đề nghị người dùng rotate và cập nhật secrets liên quan sau công việc;
  không tự rotate gây ngắt kết nối các service.
- Gợi ý Conventional Commit: feat(iam): seed deployed feature permission catalog.

## TASK-11 — Thẩm định hồ sơ, nhận xét in-line và diff phiên bản

- Hoàn thành local: 2026-10-08 07:54 (Asia/Saigon, UTC+07:00).
- Kết quả: triển khai chu trình SUBMITTED → UNDER_REVIEW → NEED_REVISION → SUBMITTED;
  tự tiếp nhận, nhận xét theo version/ô/checklist, nộp lại, snapshot JSONB và diff.
- Không chạy lệnh Git, không build/deploy image và không thay database Azure.

### Domain, dữ liệu và tính toàn vẹn

- Thêm ApplicationComment, ApplicationVersion; bổ sung AssignedOfficerId, ResubmitCount,
  Notes, ApprovedAt và các trạng thái cho ApplicationRecord.
- Giữ UserId (CitizenId) và History/ChangedBy hiện có để tương thích hợp đồng cũ.
- ApplicationCode nay varchar(50), unique, NOT NULL; cấp ngay lúc tạo DRAFT, giữ mã khi nộp lại.
- Version 1 tạo cùng transaction với lần submit đầu. Các lần nộp lại tăng ResubmitCount,
  chụp version kế tiếp, đóng nhận xét version trước và ghi lịch sử cùng transaction.
- SELECT FOR UPDATE tuần tự hóa tiếp nhận/nộp lại. Unique(application_id, version_number),
  FK kép application_id/application_version_id ngăn nhận xét trỏ sang version hồ sơ khác.
- Migration 20261008004706_ApplicationReviewVersions: backfill mã cho DRAFT cũ, snapshot cho
  SUBMITTED cũ dựa trên dữ liệu đã được API cũ khóa sửa. Không ghi snapshot giả cho draft.
- Không đổi tên database local đang tồn tại wardmate_workflow_db. ConnectionStrings__WorkflowDatabase
  có thể trỏ đến database độc lập wardmate_application_db theo đặc tả ở môi trường mới.

### API, DTO và HTTP

Các API mới trả 200 khi thành công; đều cần JWT. Các route officer yêu cầu FRONT_DESK_OFFICER.

- GET /api/v1/officer/applications/pending — query status, procedureId, submittedFrom, submittedTo,
  page/pageSize; trả ApplicationPage {items,page,pageSize,total}, FIFO theo SubmittedAt + Id.
- POST /api/v1/officer/applications/{id}/assign — không body; tự nhận bằng sub, trả ReviewResultDto.
- POST /api/v1/officer/applications/{id}/comments — body
  {versionNumber,targetType,targetId,fieldLabel,commentText}, trả ReviewResultDto.
- POST /api/v1/officer/applications/{id}/request-revision — body {reason}, trả ReviewResultDto;
  phải có ít nhất một OPEN comment của version hiện tại.
- POST /api/v1/applications/{id}/resubmit — body {expectedVersionNumber,formData,checklists:[{id,status,fileUrl,note}]},
  trả ReviewResultDto; yêu cầu đủ checklist và các mục bắt buộc COMPLETED.
- GET /api/v1/applications/{id}/comments — query versionNumber/status tùy chọn, trả CommentDto[].
- GET /api/v1/applications/{id}/versions — không body, trả VersionDto[] gồm snapshotData.
- GET /api/v1/applications/{id}/diff — query fromVersion/toVersion, trả FieldDiffDto[] chỉ các thay đổi.
- ReviewResultDto: application, assignedOfficerId, resubmitCount, currentVersionNumber.
- ApplicationDto hiện có bổ sung assignedOfficerId/resubmitCount/currentVersionNumber/notes/approvedAt.
- Lỗi: 400 validation/target/checklist sai; 401 JWT sai; 403 sai role/cán bộ thụ lý;
  404 ngoài phạm vi/không có hồ sơ/version; 409 trạng thái hoặc version xung đột/thiếu comment;
  422 thiếu checklist bắt buộc (missingItems). Lỗi nghiệp vụ có code application.* và traceId;
  validation dùng validation.failed. Role middleware 403 hiện không có ProblemDetails body.

### Quyền và giới hạn

- Công dân chỉ nộp lại hồ sơ của mình. Cán bộ chỉ bắt lỗi/yêu cầu sửa trên hồ sơ mình thụ lý,
  không được tự thẩm định hồ sơ của mình. Cán bộ khác không đọc versions/comments/diff.
- Hàng chờ gồm hồ sơ chưa phân công và của cán bộ hiện tại. Chưa có WardId nhận hồ sơ trong
  Application; chưa triển khai phạm vi phường cho hàng chờ. Không tuyên bố cách ly dữ liệu theo phường.
- JWT dùng role hiện có; không chỉnh RBAC deploy trong task này. Không tự cấp quyền thẩm định cho Admin/Manager.
- APPROVED/CANCELLED mới được khai báo trong schema, không có endpoint duyệt/hủy trong TASK-11.
- Snapshot đóng băng form/checklist/URL, không sao chép binary file. Chưa kiểm chứng file từ DocumentForm.
- RESOLVED là đã nộp lại, không đồng nghĩa cán bộ đã duyệt thay đổi.

### File mới (đường dẫn tương đối từ repository)

- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Domain/ApplicationReview.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewContracts.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewHandlers.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewValidation.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/ApplicationReviewStore.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261008004706_ApplicationReviewVersions.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261008004706_ApplicationReviewVersions.Designer.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationReviewController.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/ReviewTests.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/ReviewDiffTests.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/ReviewMigrationTests.cs

### File sửa

- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Domain/ApplicationRecord.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/WorkflowContracts.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/WorkflowRequests.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/ApplicationStore.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowDbContext.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/ProcedureCatalogClient.cs
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs
- src/Gateways/WardMate.YarpGateway/appsettings.json
- tests/WardMate.Services.ApplicationWorkflow.Tests/WorkflowFixture.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/WorkflowTests.cs
- docs/api-guide.md
- PROGRESS.md

### Kiểm thử

- Thêm 10 tests vào project có sẵn theo yêu cầu task: 7 API/integration, 2 unit diff,
  1 upgrade migration PostgreSQL thật. Không tạo test project mới.
- Kiểm tra cả chu trình hai lần nộp lại, snapshot cũ không đổi, diff nested/array/file/null/xóa field,
  kiểm soát chủ hồ sơ/cán bộ, lỗi thiếu comment/checklist, version cũ, FIFO/phân trang,
  unique version, nhận/nộp lại đồng thời chỉ một request thành công.
- dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1: PASS, 0 warning, 0 error.
- dotnet test WardMate.sln -c Release --no-restore -m:1: PASS 200/200, 0 fail, 0 skipped.
  ProcedureCatalog 96; DocumentForm 75; ApplicationWorkflow 29 (19 cũ + 10 mới).
- dotnet ef migrations has-pending-model-changes (Workflow, Release): không có thay đổi model chưa migration.
- PostgreSQL 16 test containers tạm được test fixture dọn sau kiểm thử; không build Docker image ứng dụng.

### Hướng dẫn FE / bàn giao

- docs/api-guide.md đã giải thích 8 API, ví dụ body, trình tự dùng, quyền, kết quả và cách sửa lỗi.
- Gửi targetId dạng JSON Pointer với FORM_FIELD, UUID checklist với CHECKLIST_ITEM.
- Resubmit gửi toàn bộ form và checklist, không chỉ các ô thay đổi; gửi expectedVersionNumber đang xem.
- 409 cần tải lại dữ liệu; 422 hiển thị missingItems; không điều khiển giao diện dựa trên câu tiếng Việt.
- Giữ JWT trong Authorization; không thêm cookie/CSRF flow mới cho Workflow.
- Gợi ý nhóm thông điệp Conventional Commits cho Antigravity (tách từng file theo quy định):
  feat(workflow): add application review and revision snapshots
  feat(gateway): route officer application endpoints
  test(workflow): cover review resubmission diff and migration
  docs(workflow): document application review APIs

## FE-CORS-001 — Cho phép FE Vercel gọi Backend

- Hoàn thành cấu hình local: 2026-10-09 15:58 Asia/Saigon (UTC+07:00).
- Origin chính xác: https://wardmate-frontend.vercel.app, không wildcard.
- File sửa:
  - src/BuildingBlocks/WardMate.SharedKernel/Web/BrowserCorsExtensions.cs
  - src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/appsettings.json
  - src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs
  - src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Program.cs
  - docs/iam-httponly.md
  - docs/api-guide.md
  - PROGRESS.md
- Shared CORS mặc định cho phép Vercel; Development giữ localhost:5173/3000. Explicit Cors:AllowedOrigins
  vẫn ghi đè mặc định. Gateway/IAM/Workflow đã dùng policy; Procedure/DocumentForm nay đăng ký và chạy policy.
- IAM appsettings SameSite=None, AllowInsecureLocalhost=false; cookie vẫn HttpOnly/Secure/host-only.
  Giữ CSRF header và kiểm origin theo cùng CORS policy.
- Không thêm endpoint/DTO/status code nghiệp vụ. FE gửi credentials:include và X-CSRF-Protection:1 cho POST auth,
  Bearer cho API bảo vệ. CORS không cấp quyền nghiệp vụ.
- Build solution Release --no-restore -warnaserror -m:1: PASS 0 errors, 0 warnings.
- Không viết test mới. Full suite bị dừng do Docker Desktop Linux Engine pipe không tồn tại;
  chưa xác nhận integration tests trong phiên này. Không sử dụng số 200/200 của phiên trước để kết luận.
- Chưa deploy hoặc xác minh preflight/cookie trên Azure. Antigravity cần rebuild/deploy các API liên quan và Gateway;
  kiểm tra Azure overrides Cors__AllowedOrigins__* / AuthCookie__SameSite=None / AuthCookie__AllowInsecureLocalhost=false.
- Không chạy Git, không build Docker. Commit gợi ý: fix(cors): allow deployed Vercel frontend and cross-site auth cookies.
- Lượt test chỉ chọn WorkflowUnitTests/ReviewDiffTests cũng dừng ở bước khởi chạy test, không trả kết quả; đã hủy lượt chạy. Không có số test pass được xác nhận cho phiên này.

## W1-W4-CLOSEOUT-001 — IAM directory, permission chi tiết, hàng đợi theo phường và quyết định cuối

- Hoàn thành code/kiểm thử local: 2026-10-09 17:07 Asia/Saigon (UTC+07:00).
- Phạm vi: 3 mục trong yêu cầu hoàn thiện Tuần 1–4. Không tuyên bố hoàn thành các backlog khác
  ngoài yêu cầu này (QR, xuất kết quả, RAG, Analytics, v.v.). Không sửa API DocumentForm của BE khác.
- Không chạy bất kỳ lệnh Git nào. Không build/deploy image và không cập nhật database Azure.

### Kết quả

1. GET /api/v1/manager/officers: Manager + iam.accounts.read, phạm vi phường từ DB trước phân trang;
   bộ lọc WardCode/AssignedCategory/Search/Role, page/pageSize. Cán bộ có role đặc quyền ngoài FD/Citizen
   vẫn bị loại theo ManagementScope hiện có.
2. GET /api/v1/admin/users: IT_ADMIN + iam.accounts.read, tra họ tên/email/CCCD, role, phân trang,
   hỗ trợ thêm phường/category; có cả tài khoản inactive, không trả hash/token.
3. PUT /api/v1/manager/officers/{userId}/categories: iam.accounts.manage + scope; body {categories:[1,2]},
   204. Metadata int[] theo ID Procedure category, không FK xuyên database và không khóa luồng FD theo lĩnh vực.
4. GET /api/v1/wards public: {id,code,name}[] cho công dân chọn phường.
5. GET /api/v1/users/access-context authenticated: quyền/phường/role hiện hành của chính token,
   no-store, 401 tài khoản không khả dụng. Không nhận userId từ request.
6. IAM endpoints accounts/users/profiles/RBAC/wards thêm permission tương ứng, giữ chống leo thang
   và phạm vi phường. Procedure quản trị thay policy role chung bằng 15 permission đã có.
7. Application.WardCode, migration/index; POST create bắt buộc wardCode được xác minh qua IAM.
   Snapshot mới, detail và summary có wardCode. Ward null của dữ liệu cũ không lọt vào queue.
8. Workflow lấy AccessContext từ IAM trước authorization trên mỗi request authenticated, ghi đè claims
   role/permissions/ward cũ; lỗi IAM trả 503, bị khóa trả 401. Thay phường/thu hồi quyền có hiệu lực request kế tiếp.
9. Queue, assign, comment, request-revision, versions/comments/diff và quyết định cuối chặn FD khác phường.
10. POST /api/v1/officer/applications/{id}/approve (không body): workflow.approve, FD thụ lý đúng phường,
    UNDER_REVIEW, không OPEN comment; APPROVED + ApprovedAt UTC + history trong transaction.
11. POST /api/v1/officer/applications/{id}/reject body {reason}: workflow.reject, cùng phạm vi,
    UNDER_REVIEW → REJECTED, lý do bắt buộc, lưu notes/history. APPROVED/REJECTED khóa mọi mutation cũ.
12. Row locks tuần tự hóa quyết định đồng thời; giữ nguyên snapshot đã nộp.

### DTO, status code và hướng dẫn FE

- DirectoryFilter: search?, role?, wardCode?, assignedCategory? (integer ID), page=1, pageSize=20 (1–100).
- DirectoryPage: items/page/pageSize/total; DirectoryUser: id/username/email/fullName/identityNumber/
  isActive/wardCode/roles/assignedCategories. Search dùng ILIKE với escape wildcard; không bỏ dấu.
- POST create Application thêm WardCode bắt buộc, uppercase/trim; không cho FE đổi phường khi resubmit.
- API đọc/create/approve/reject trả 200 hoặc 201 như mô tả trong docs/api-guide.md; categories PUT 204.
- 400 validation hoặc phường không tồn tại; 401 token/tài khoản; 403 permission/phạm vi;
  404 user/hồ sơ ngoài phạm vi hoặc không có; 409 trạng thái/OPEN comment/thiếu phường legacy;
  503 IAM không khả dụng. Workflow errors application.*; IAM errors theo Result/validation hiện có.
- IAM và Workflow dùng quyền hiện hành; Procedure dùng permission JWT, cần refresh/login sau đổi quyền.
- Workflow thêm 14 permission bằng migration IAM; 8 quyền cá nhân/đọc trong phạm vi cho 5 role chuẩn,
  6 quyền thẩm định chỉ cấp FRONT_DESK_OFFICER. Admin/Manager không tự có quyền duyệt mọi hồ sơ.
- IAM__BaseUrl trên Workflow trỏ về IAM origin (key chính xác: Iam__BaseUrl); Compose đã cấu hình
  http://iam-api:8080/ và depends_on iam-api. Không dùng địa chỉ frontend cho cấu hình này.
- Triển khai IAM + migration trước Workflow; sau đó Procedure/Gateway và FE chọn phường.

### Migration và lưu ý dữ liệu cũ

- IAM: 20261009095945_OfficerDirectoryCategories, thêm assigned_categories integer[] mặc định rỗng,
  seed permission/grants idempotent theo code/role_name, khóa cùng RbacStore; không xóa quyền tùy chỉnh.
  Down chỉ gỡ cột, không tự thu hồi các permission đã có thể được gán cho role tùy chỉnh.
- Workflow: 20261009095949_WardScopedFinalDecisions, ward_code nullable cho bản ghi legacy,
  index (ward_code,status,submitted_at), check constraint thêm REJECTED.
- Không tự suy đoán phường cho hồ sơ cũ. Bản ghi legacy thiếu ward bị chặn queue/submit; phải đối soát
  và bổ sung địa bàn có kiểm soát trước khi dùng lại. Chưa có endpoint chuyển phường hồ sơ trong task này.
- AssignedCategories chỉ kiểm integer dương/không trùng/tối đa 100, chưa gọi Procedure để xác minh tồn tại;
  FE chọn từ danh mục thật. Đây là metadata bộ lọc, không quyết định authorization.

### Kiểm thử và chất lượng

- Tạo project IAM.Tests theo yêu cầu mới của user cho Unit/Integration Tests.
- Thêm 17 test cases: IAM 7 (bao gồm validator unit), Workflow 7, Procedure permission 3.
- Full solution: dotnet test WardMate.sln -c Release --no-restore -m:1 PASS 218/218,
  0 failed, 0 skipped: Procedure 99, DocumentForm 76, Workflow 36, IAM 7.
- Test PostgreSQL16 bằng container riêng tạm, đã được fixtures dọn; không sửa DB local đang sử dụng.
- Bao phủ ward/category/search/paging, role không có permission không được vượt kiểm tra,
  revoke quyền cùng token, đổi phường cùng token, IAM unavailable fail closed,
  DRAFT→SUBMITTED→UNDER_REVIEW→APPROVED/REJECTED, reason rỗng, OPEN comment,
  freeze sau kết thúc và approve/reject đồng thời một winner.
- Một lượt đầu fail do fixture Ward không uppercase như API thật; đã sửa fixture và chạy toàn suite PASS.
- Final Release build --no-restore -warnaserror -m:1: PASS 0 errors, 0 warnings.
- EF has-pending-model-changes cho IAM/Workflow: không có thay đổi model thiếu migration.

### Danh sách file tạo/sửa (tương đối từ repository)

Shared/Gateway/config:
- src/BuildingBlocks/WardMate.SharedKernel/Web/FeaturePermissions.cs (mới)
- src/Gateways/WardMate.YarpGateway/appsettings.json
- docker/docker-compose.yml
- WardMate.sln

IAM, dưới src/Services/WardMate.Services.IAM/:
- WardMate.Services.IAM.Domain/Entities/User.cs
- WardMate.Services.IAM.Application/Accounts/UserDirectory.cs (mới)
- WardMate.Services.IAM.Infrastructure/Persistence/UserDirectory.cs (mới)
- WardMate.Services.IAM.Infrastructure/Persistence/Configurations/IdentityConfigurations.cs
- WardMate.Services.IAM.Infrastructure/DependencyInjection.cs
- WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261009095945_OfficerDirectoryCategories.cs (mới)
- WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261009095945_OfficerDirectoryCategories.Designer.cs (mới)
- WardMate.Services.IAM.Infrastructure/Persistence/Migrations/IamDbContextModelSnapshot.cs
- WardMate.Services.IAM.API/Controllers/UserDirectoryController.cs (mới)
- WardMate.Services.IAM.API/Controllers/UsersController.cs
- WardMate.Services.IAM.API/Controllers/AccountsController.cs
- WardMate.Services.IAM.API/Controllers/AdminProfilesController.cs
- WardMate.Services.IAM.API/Controllers/StaffAdministrationController.cs
- WardMate.Services.IAM.API/Controllers/RbacController.cs
- WardMate.Services.IAM.API/Authorization/PermissionAuthorization.cs

Workflow, dưới src/Services/WardMate.Services.ApplicationWorkflow/:
- WardMate.Services.ApplicationWorkflow.Domain/ApplicationRecord.cs
- WardMate.Services.ApplicationWorkflow.Application/IWorkflowDirectory.cs (mới)
- WardMate.Services.ApplicationWorkflow.Application/FinalDecisionRequests.cs (mới)
- WardMate.Services.ApplicationWorkflow.Application/WorkflowContracts.cs
- WardMate.Services.ApplicationWorkflow.Application/WorkflowValidation.cs
- WardMate.Services.ApplicationWorkflow.Application/WorkflowRequests.cs
- WardMate.Services.ApplicationWorkflow.Application/ReviewContracts.cs
- WardMate.Services.ApplicationWorkflow.Application/ReviewHandlers.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowDirectory.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/ProcedureCatalogClient.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/ApplicationStore.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/ApplicationReviewStore.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowDbContext.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261009095949_WardScopedFinalDecisions.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261009095949_WardScopedFinalDecisions.Designer.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs
- WardMate.Services.ApplicationWorkflow.API/WorkflowAccessMiddleware.cs (mới)
- WardMate.Services.ApplicationWorkflow.API/WorkflowSecurity.cs
- WardMate.Services.ApplicationWorkflow.API/Program.cs
- WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationsController.cs
- WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationReviewController.cs

Procedure, dưới src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/:
- Security/ProcedureAuthentication.cs
- Controllers/ProcedureManagerController.cs
- Controllers/ProcedureDraftsController.cs
- Controllers/ProcedureCategoriesController.cs
- Controllers/ProcedureDocumentFormsController.cs

Tests/tài liệu:
- tests/WardMate.Services.IAM.Tests/WardMate.Services.IAM.Tests.csproj (mới)
- tests/WardMate.Services.IAM.Tests/IamFixture.cs (mới)
- tests/WardMate.Services.IAM.Tests/UserDirectoryTests.cs (mới)
- tests/WardMate.Services.ApplicationWorkflow.Tests/WorkflowFixture.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/WorkflowTests.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/ReviewTests.cs
- tests/WardMate.Services.ApplicationWorkflow.Tests/WardDecisionTests.cs (mới)
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/PermissionTests.cs (mới)
- docs/api-guide.md
- PROGRESS.md

### Commit gợi ý cho Antigravity

- feat(iam): add scoped user directory and officer category filters
- feat(auth): enforce granular IAM and procedure permissions
- feat(workflow): enforce live ward scope and final decisions
- test: cover scoped directories permissions and final workflow states
- docs: document week 1-4 completion and migration handoff

Giữ quy tắc commit từng file/thay đổi chức năng; không squash. Codex không chạy Git.

## DRAFT-CONFIG-001 — Cấu hình upload PDF local và bàn giao Azure

- Thời gian: 2026-10-10 09:43 UTC+07:00 (Asia/Saigon).
- Thay đổi: docker/.env (local, không commit), docs/azure-deployment.md, PROGRESS.md.
- Bổ sung biến Blob để chủ dự án điền connection string thật; bật PROCEDURE_EXTRACTION_ENABLED; bảo đảm khóa AIOCR nội bộ tối thiểu 32 ký tự, sinh ngẫu nhiên nếu thiếu/không đủ. Không in giá trị secret.
- Endpoint không thay đổi: POST /api/v1/procedure-manager/drafts, multipart File, 202 khi lưu thành công; 503 draft.storage_not_configured nếu thiếu Blob. GET /api/v1/procedure-manager/drafts/{id} lấy trạng thái, nội dung và cảnh báo. DTO/token không thay đổi; cần permission upload/read tương ứng.
- Chưa hoàn tất kết nối Blob: connection string thật chưa có trong local. Chưa chạy Docker, chưa deploy hoặc kiểm thử Azure. Hướng dẫn Azure có trong docs/azure-deployment.md; chỉ bật PDF text extraction, chưa bật AI/OCR.
- Tests: 0 test mới, không chạy suite vì chỉ sửa cấu hình/tài liệu. Release build đang được kiểm tra; kết quả ghi tiếp bên dưới.
- Commit gợi ý (chỉ tài liệu, không docker/.env): docs(deploy): document PDF draft storage and extraction configuration.

- Kết quả cuối (2026-10-10 09:47:32 +07:00, Asia/Saigon): dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 thành công, 0 errors, 0 warnings. Không chạy tests/Docker/Git. Blob vẫn cần chủ dự án điền connection string thật.

## TASK-25 — Thực thể thông báo và SignalR Hub realtime

- Hoàn thành local: 2026-10-10 11:10 UTC+07:00 (Asia/Saigon).
- Service: WardMate.Services.AnalyticsSystem (.NET 8, Clean Architecture), database riêng wardmate_notifications_db.
- Notification đủ các trường yêu cầu; logical references sang IAM/Workflow, không cross-service FK. Migration InitialNotifications tạo bảng, UUID mặc định, UTC timestamps, chỉ mục recipient/read + created_at + recipient/created_at/id và ràng buộc type/channel/read state.
- NotificationHub kế thừa Hub<INotificationClient>, JWT HS256 kiểm tra issuer/audience/lifetime/sub. Query access_token chỉ được nhận ở /hubs/notifications. Đóng connection khi token hết hạn.
- Nhóm user lấy từ JWT; nhóm ward chỉ cho FD/Manager. Do JWT hiện không có ward_code, OnConnected kiểm tra live IAM access-context để lấy phường và trạng thái tài khoản, fail closed khi IAM lỗi. Không có JoinGroup hoặc Send tùy ý cho browser.
- Hợp đồng tạo thông báo nội bộ lưu DB trước rồi phát ReceiveNotification/UpdateUnreadCount. Lỗi realtime không rollback dữ liệu. Hub cho phép người dùng truy vấn lại dữ liệu khi reconnect và đánh dấu đã đọc idempotent, chỉ trên bản ghi của chính mình.
- Profile Compose notifications gồm PostgreSQL riêng cổng local 5436 và AnalyticsSystem 5006; NOTIFICATIONS_DB_PASSWORD phải điền trước khi bật. Có Dockerfile nhưng chưa build image/deploy. Profile không bật mặc định; không thay docker/.env trong task này.

### Danh sách file mới/thay đổi

- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Domain/Notification.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Application/Notifications.cs (mới: DTO và interfaces)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationDbContext.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationDirectory.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationHub.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationService.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/DependencyInjection.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/20261010040428_InitialNotifications.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/20261010040428_InitialNotifications.Designer.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/NotificationDbContextModelSnapshot.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/WardMate.Services.AnalyticsSystem.Infrastructure.csproj
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/NotificationSecurity.cs (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Dockerfile (mới)
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Program.cs
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/WardMate.Services.AnalyticsSystem.API.csproj
- src/BuildingBlocks/WardMate.SharedKernel/Web/BrowserCorsExtensions.cs (cho phép header X-SignalR-User-Agent)
- docker/docker-compose.yml
- docker/.env.example
- docs/api-guide.md
- docs/azure-deployment.md
- PROGRESS.md

### API/Hub và FE

- POST /hubs/notifications/negotiate?negotiateVersion=1: SDK tự gọi, không body nghiệp vụ; 200 transport metadata hoặc 401 JWT không hợp lệ.
- GET /hubs/notifications: WebSocket upgrade 101 khi thành công; transport fallback do SignalR SDK quản lý.
- Hub methods: GetNotifications(page=1,pageSize=20) -> NotificationDto[]; GetUnreadCount() -> int; MarkRead(notificationId) -> hoàn tất hoặc HubException tiếng Việt. Không phải REST endpoints và không hiển thị trong Swagger.
- Event contract: ReceiveNotification(NotificationDto), UpdateUnreadCount(int). DTO có recipient/application id, title/content/type/channel/read/readAt/actionUrl/createdAt. FE render text, dùng accessTokenFactory với access token mới; refresh cookie không phải token Hub. Invocation errors theo SignalR, không phải HTTP ProblemDetails.
- Gateway dùng route có sẵn /api/analytics-system/hubs/notifications; local trực tiếp http://localhost:5006/hubs/notifications. Docs hướng dẫn reconnect/refetch/unread đồng bộ, logout stop connection.

### Kết quả kiểm chứng

- dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-restore -m:1: 218/218 PASS, 0 failed, 0 skipped (Procedure 99, DocumentForm 76, Workflow 36, IAM 7).
- Không tạo test project trong repository theo AGENTS.md. Harness tạm ngoài repository chạy PostgreSQL 16 + Kestrel + WebSocket thật: 20/20 checks PASS. Bao gồm migration và model aligned, persistence/defaults/UTC, unread isolation, JWT/expired/query-token restriction, CORS, hub list, validation, mark-read ownership/idempotency, private delivery, ward A/B và citizen isolation. PostgreSQL test container đã được hủy sau kiểm tra; không dùng dữ liệu thật.
- docker compose --env-file docker/.env -f docker/docker-compose.yml --profile notifications config --quiet: PASS; không khởi động ứng dụng qua Compose.

### Giới hạn / bàn giao

- Chưa nối sự kiện Workflow/RabbitMQ (TASK-27); không tự phát sinh thông báo từ thao tác phê duyệt hiện tại. SMS/Email không thuộc task này.
- Chưa có Redis backplane/Azure SignalR: triển khai tối đa 1 replica cho realtime hiện tại.
- Nhóm/phường kiểm tra tại connect; đổi quyền/phường cần reconnect, chưa có đẩy thu hồi tức thì tới connection đang mở. Token hết hạn được đóng tự động.
- Chưa có outbox/retry/exactly-once realtime; dữ liệu DB cho phép tải lại khi offline. DB local ứng dụng/Azure chưa migrate bởi Codex, chỉ test DB đã migrate.
- Không chạy Git, không build/push Docker image, không deploy Azure.
- Commit gợi ý cho Antigravity: feat(notifications): add persisted in-app notifications and authenticated SignalR hub. Tách từng file/thay đổi chức năng theo quy tắc hiện hành; chỉ là gợi ý, Codex không commit.

## TASK-27 / TASK-26 / NOTIFY-AUTH-001 — Outbox, RabbitMQ, retry và thu hồi kết nối

- Hoàn tất phần code có thể triển khai local: 2026-10-12 08:23 UTC+07:00 (Asia/Saigon).
- Chủ dự án xác nhận chưa chọn nhà cung cấp SMS/Email. TASK-26 chưa hoàn tất tích hợp nhà cung cấp: SMTP adapter có code, SMS có interface/Blocked adapter; chưa gửi thật, chưa kiểm chứng sandbox/provider. Không gắn nhãn SMS đã hoàn thành.
- TASK-27 đã có Workflow -> Outbox -> RabbitMQ -> Inbox -> notification/delivery -> SignalR, kiểm thử broker/PostgreSQL/WebSocket thật. Không tạo/gửi dữ liệu thật.

### Nghiệp vụ / tính toàn vẹn

- Workflow SaveChangesAsync ghi event trong cùng transaction với status history/comment mới. DRAFT không phát event. Submit, resubmit, assign/UNDER_REVIEW, revision request, approve, reject và comment mới đều được capture qua history/comment; không phát lại lịch sử cũ.
- Event v1 chỉ chứa mã định danh/phường/trạng thái/thời điểm, không chứa nội dung form, CCCD, email/phone hoặc JWT.
- Outbox publisher persistent messages + mandatory routing + publisher confirms (deadline 10 giây). Broker gián đoạn không làm fail nghiệp vụ Workflow; Outbox giữ và retry khi bật lại.
- Consumer ACK sau lưu Inbox; EventId PK chống giao trùng. Invalid schema/version đi broker dead queue; lỗi xử lý đã vào Inbox giữ Pending/Dead trong DB. Inbox và delivery worker retry tối đa 10 lần với backoff tới 300 giây. Outbox không tự bỏ sự kiện chưa gửi.
- Notification và các delivery tạo trong cùng transaction, unique index chống trùng. CreateAsync nội bộ cũng lưu IN_APP delivery để worker gửi sau commit. Trạng thái gửi: Pending/Sent/Blocked/Suppressed/Dead. Sent là transport chấp nhận, không phải xác nhận người nhận đọc và không bảo đảm exactly-once.
- IAM directory nội bộ dùng khóa riêng constant-time, chỉ active users; danh sách theo phường chỉ FD có workflow.queue.read. Trước gửi kiểm tra lại tài khoản/phường; không còn phù hợp thì Suppressed. Danh sách/unread/mark-read loại thông báo thuộc phường cũ.
- Connection guard kiểm IAM trước invocation, trước worker gửi realtime, và mỗi 15 giây (5–300 cấu hình): role/permission/phường thay đổi, khóa tài khoản, IAM unavailable -> remove groups + abort. Không gọi đây là thu hồi tức thì; bounded polling còn phụ thuộc timeout/số connection.
- Email SMTP dùng STARTTLS, mặc định disabled; cấu hình thiếu -> Blocked, lỗi gửi -> retry/Dead. Chưa có delivery receipts. Timeout SMTP có thể gây gửi trùng khi retry.
- SMS mặc định disabled, ISmsSender chưa gắn nhà cung cấp; adapter trả Blocked. Không giả lập Sent.

### API / DTO / FE

- Hub /hubs/notifications và methods/events giữ hợp đồng TASK-25. FE khử trùng notification.id, reconnect bằng access token hiện hành, refetch danh sách/count sau reconnect. SignalR invocation errors tiếng Việt, không phải ProblemDetails mỗi message.
- POST /internal/v1/notification-recipients mới trên IAM: header X-Notification-Service-Key; body {userIds: Guid[] tối đa 10, wardCode?: string <=50, page: int 1..10000}; mỗi trang 200, active users. 200 -> [{userId,email,phoneNumber,wardCode,isOfficer}], 400 filter sai, 401 khóa sai/thiếu, 503 chưa cấu hình khóa. Không dành cho browser, ẩn khỏi Swagger công khai; Cache-Control no-store. WardCode chuẩn hóa trim/uppercase.
- Không có endpoint công khai tự tạo thông báo hay replay. Hướng dẫn DBA requeue từng Dead/Blocked đã đối soát nằm trong docs/notifications-operations.md; không reset Sent/Processed.

### File mới/chỉnh sửa (tương đối từ repository)

BuildingBlocks:
- src/BuildingBlocks/WardMate.EventBus/WardMate.EventBus.csproj
- src/BuildingBlocks/WardMate.EventBus/WorkflowEvent.cs (mới)
- src/BuildingBlocks/WardMate.EventBus/RabbitTransport.cs (mới)

Workflow (prefix src/Services/WardMate.Services.ApplicationWorkflow/):
- WardMate.Services.ApplicationWorkflow.API/WardMate.Services.ApplicationWorkflow.API.csproj
- WardMate.Services.ApplicationWorkflow.Infrastructure/WardMate.Services.ApplicationWorkflow.Infrastructure.csproj
- WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowDbContext.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/ProcedureCatalogClient.cs
- WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowOutbox.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261012010910_WorkflowNotificationOutbox.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261012010910_WorkflowNotificationOutbox.Designer.cs (mới)
- WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs

AnalyticsSystem (prefix src/Services/WardMate.Services.AnalyticsSystem/):
- WardMate.Services.AnalyticsSystem.Domain/Notification.cs
- WardMate.Services.AnalyticsSystem.Application/Notifications.cs
- WardMate.Services.AnalyticsSystem.Infrastructure/WardMate.Services.AnalyticsSystem.Infrastructure.csproj
- WardMate.Services.AnalyticsSystem.Infrastructure/DependencyInjection.cs
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationDbContext.cs
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationService.cs
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationHub.cs
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationPipelineModels.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/WorkflowNotificationConsumer.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationInboxWorker.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationDeliveryWorker.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/ExternalNotificationSender.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/NotificationConnectionGuard.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/20261012010837_ReliableNotificationDelivery.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/20261012010837_ReliableNotificationDelivery.Designer.cs (mới)
- WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/NotificationDbContextModelSnapshot.cs

IAM (prefix src/Services/WardMate.Services.IAM/):
- WardMate.Services.IAM.Application/Accounts/NotificationRecipients.cs (mới)
- WardMate.Services.IAM.Infrastructure/Persistence/NotificationRecipients.cs (mới)
- WardMate.Services.IAM.Infrastructure/DependencyInjection.cs
- WardMate.Services.IAM.API/Controllers/NotificationRecipientsController.cs (mới)

Kiểm thử/cấu hình/tài liệu:
- tests/WardMate.Services.ApplicationWorkflow.Tests/NotificationOutboxTests.cs (mới, 3 cases)
- tests/WardMate.Services.IAM.Tests/NotificationRecipientsTests.cs (mới, 1 case)
- tests/WardMate.Services.IAM.Tests/IamFixture.cs
- docker/docker-compose.yml
- docker/.env.example
- docs/api-guide.md
- docs/azure-deployment.md
- docs/notifications-operations.md (mới)
- PROGRESS.md

### Kiểm chứng

- Release build toàn solution --no-restore -warnaserror -m:1: 0 errors, 0 warnings.
- Suite cuối: 222/222 PASS, 0 failed/0 skipped (Procedure 99, DocumentForm 76, Workflow 39, IAM 8). Thêm 4 integration cases trong projects hiện có; không tạo test project mới trong repository.
- Harness tạm ngoài repository: 23/23 checks PASS với PostgreSQL16 riêng theo service, RabbitMQ thật và Kestrel/SignalR WebSocket. IAM contact lookup được stub trong harness; endpoint IAM được test riêng ở suite, không tuyên bố toàn bộ service IAM thật nằm trong lượt harness.
- Harness kiểm chứng: DRAFT không event; submit atomic; event đi broker; đúng owner/ward; duplicate EventId; invalid message DLQ; durable deliveries; realtime; SMTP/SMS unconfigured Blocked; transfer/ban/permission changes đóng connection; lọc lịch sử phường; retry/exhaustion/replay; dừng/khởi động RabbitMQ giữ và phục hồi Outbox; delivery backoff và retry.
- EF has-pending-model-changes Workflow/Analytics: không có thay đổi model thiếu migration. EF Analytics dùng design-time factory do không cung cấp connection string runtime cho CLI; không tác động DB deploy.
- docker compose --env-file docker/.env -f docker/docker-compose.yml --profile notifications config --quiet: PASS.
- Lượt đầu tests lỗi vì Docker Engine tắt; đã khởi động Engine và chạy lại. Lỗi chuẩn hóa WardCode trong test mới được sửa trước lượt cuối. Harness readiness RabbitMQ được sửa để không tạo Erlang cookie bằng root trong lúc startup; retry test chờ đúng hàng của nhiều kênh. Không giấu các lượt chưa đạt.
- Test containers được dọn sau test; không build Docker image ứng dụng, không khởi động stack ứng dụng bằng Compose, không chạy Git.

### Bàn giao / việc còn thiếu

- Chưa deploy Azure hoặc migrate database ứng dụng đang dùng. Phải triển khai IAM + secret S2S, hai migration, broker persistent và cấu hình workers trước khi kiểm thử cloud.
- Chưa có nhà cung cấp/credentials Email/SMS: cần xác nhận nhà cung cấp, From/Brandname, sandbox, adapter SMS và receipt contract. TASK-26 là hoàn thành hạ tầng/SMTP adapter local, chưa hoàn thành dịch vụ gửi thật.
- Kiểm thử tích hợp trên môi trường deploy chưa làm vì code/images mới chưa được triển khai bởi chủ dự án/Antigravity. Checklist có tại docs/notifications-operations.md.
- Single replica SignalR vẫn là giới hạn; chưa có backplane/Azure SignalR. Chưa có retention job hoặc API quản trị queue; không tự xóa Inbox chống trùng.
- docker/.env không bị sửa trong task này. Điền NOTIFICATIONS_ENABLED, NOTIFICATIONS_SERVICE_KEY, RABBITMQ_PASSWORD và password DB theo runbook; Email/SMS giữ tắt khi chưa có provider.
- Commit gợi ý cho Antigravity (tách mỗi file/chức năng theo quy tắc): feat(notifications): add durable workflow event pipeline and connection revalidation; feat(notifications): add configurable SMTP delivery and SMS provider contract; test(notifications): verify transactional outbox and scoped recipients; docs(notifications): document retry and deployment operations.

## BE-TASK-PROCEDURE-GEMINI-AI — Kết nối Gemini cho bóc tách thủ tục
- Hoàn thành phần adapter/cấu hình: 2026-10-12 23:53 +07:00 (Asia/Saigon).
- Sửa: src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/AzureProcedureDocumentExtractor.cs;
  docker/docker-compose.yml; docker/.env.example; docs/api-guide.md; PROGRESS.md.
- Tạo: tests/WardMate.Services.ProcedureCatalog.IntegrationTests/GeminiExtractionTests.cs.
- Chọn Extraction:Provider=Gemini, dùng Gemini:Key/Model riêng, endpoint Google cố định và Bearer;
  mặc định AzureOpenAI giữ hành vi cũ. Từ chối choices rỗng và kết quả bị cắt.
- Không thêm endpoint/migration; preview POST multipart PDF trả 200, tạo draft POST multipart PDF trả 202.
  Giữ quyền/token và luồng review/publish. FE hiển thị warnings và chọn categoryId.
- Build Release --no-restore -warnaserror: 0 lỗi, 0 cảnh báo.
- Test mới 3 cases; kiểm thử adapter Azure/Gemini 6/6; toàn solution 225/225 passed
  (Procedure 102, DocumentForm 76, Workflow 39, IAM 8).
- Lần test sandbox ban đầu không hoàn tất; dừng và chạy lại thành công ngoài sandbox.
- Chưa gọi Gemini thật (chưa có key/model), chưa đánh giá độ chính xác với PDF thật;
  chưa deploy, chưa build Docker, không chạy Git. OCR scan vẫn cần provider OCR riêng.
- Không cam kết AI luôn trả đủ dữ liệu hoặc gói miễn phí; cần test thực tế sau khi cấu hình.

## FIX-PDF-SPARSE-PAGES — Không chặn AI vì trang ít chữ
- Hoàn thành 2026-10-13 00:18 UTC+07:00 (Asia/Saigon).
- Sửa src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/TextFirstProcedureExtractor.cs:
  tiếp tục map văn bản sang payload khi OCR tắt và tổng chữ/số >=20; giữ cảnh báo trang thiếu chữ.
  Phân biệt cảnh báo quota, key/quyền, timeout, lỗi AI; không lộ response/key nhà cung cấp.
- Sửa tests/WardMate.Services.ProcedureCatalog.IntegrationTests/TextPdfExtractionTests.cs:
  cập nhật 2 cases PDF trống không gọi AI và PDF hỗn hợp vẫn điền title từ AI, giữ cảnh báo.
- Sửa docs/api-guide.md và PROGRESS.md. Không thêm API/migration; request/status/quyền không đổi.
- Release build: 0 lỗi, 0 cảnh báo. Toàn solution 225/225 passed; không tạo test project mới.
- FE dùng payload điền form, hiển thị warnings, chọn categoryId và đối soát trước publish.
- Chưa test gọi Gemini thật với PDF của người dùng; tests dùng HTTP giả lập. Không build Docker, deploy hoặc Git.

## FIX-AI-TRANSIENT — Chẩn đoán và retry Gemini 503
- Hoàn thành sửa local: 2026-10-13 00:50 UTC+07:00 (Asia/Saigon).
- Log container xác nhận 3 lần 503. Request chẩn đoán rất ngắn dùng key/model từ .env trả HTTP 200;
  không in secret, không đổi model/billing. Chưa xác nhận request PDF 30 trang thành công.
- Sửa AzureProcedureDocumentExtractor.cs và TextFirstProcedureExtractor.cs tại
  src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/:
  retry 502/503/504 tối đa 2 lần, chờ 2/4 giây, giữ cancellation; cảnh báo 503 cụ thể.
- Sửa tests/WardMate.Services.ProcedureCatalog.IntegrationTests/GeminiExtractionTests.cs:
  thêm 1 case hai lần 503 rồi 200. Toàn solution 226/226 passed. Release build 0 lỗi/0 cảnh báo.
- Cập nhật docs/api-guide.md và PROGRESS.md. Không đổi endpoint/request/status/token/quyền;
  FE tiếp tục hiển thị warnings khi AI không trả payload. Không retry quota hoặc tự bật billing.
- Không Git, không build Docker, không deploy. Cần người dùng rebuild AIOCR và kiểm thử PDF thực tế.

## AI-RESPONSE-DIAGNOSTICS — 2026-10-13 01:06:42 +07:00 (Asia/Saigon)
- Lỗi mới: provider HTTP 200 nhưng MapText rơi vào fallback; không còn là 503.
- Tái hiện trực tiếp cùng extractedText: HTTP 200, finish_reason stop, payload/warnings hợp lệ,
  procedureCode 1.116905. Chưa tái hiện exception của request container; không khẳng định đã sửa nguyên nhân.
- Sửa TextFirstProcedureExtractor.cs: cảnh báo riêng JSON sai, thiếu trường, InvalidDataException
  (thông báo kiểm tra nội bộ), cấu hình/kiểu dữ liệu sai. Không in key/response thô.
- Release build 0 lỗi/0 cảnh báo; 14/14 tests PDF/adapter passed, không thêm tests.
- Cập nhật docs/api-guide.md và PROGRESS.md. API/status/token không đổi. FE cần hiển thị warnings.
- Chưa build Docker/Git/deploy. Cần xác minh request qua container sau cập nhật.

## WEEK-05-REVIEW — Đối chiếu các việc còn thiếu (2026-10-13 17:04:04 +07:00, Asia/Saigon)
- Phạm vi tạm xác định theo task gần nhất: Tuần 5, TASK-25/26/27; chưa có bảng phân công tuần mới.
- TASK-25: entity/migration/SignalR/nhóm user-phường/read-unread và kiểm thử local đã có.
- TASK-27: Workflow Outbox -> RabbitMQ -> Inbox/delivery, chống trùng/retry, guard thu hồi kết nối đã có; chưa xác minh deploy.
- TASK-26: SMTP STARTTLS adapter đã có; chưa có thông tin nhà cung cấp/key/sender để gửi thật.
  SMS chỉ có UnconfiguredSmsSender trả Blocked, chưa có adapter Brandname thật.
- Đã hỏi chủ dự án chọn hướng SMTP thật hoặc kiểm thử máy chủ local, không tự đăng ký dịch vụ/billing.
- Giới hạn vận hành: Analytics tối đa 1 replica khi chưa có backplane; chưa retention/replay API.
  Đây là phần mở rộng vận hành, không tự coi là yêu cầu bắt buộc của TASK-25/27.
- Procedure: chủ dự án chấp nhận chuyển sang task khác; preview AI đã có payload theo response được cung cấp.
  Upload draft 202 Queued đã được quan sát; chưa xác minh publish/end-to-end Azure.
- Phiên rà soát chỉ sửa PROGRESS.md; không đổi API/code/config, không chạy build/test/Git/Docker.
  Không tuyên bố TASK-26 hoặc kiểm thử deploy đã hoàn tất. Hướng dẫn vận hành hiện có: docs/notifications-operations.md.

## FIX-AI-WARNINGS — Chuẩn hóa cảnh báo AI (2026-10-13 23:05:11 +07:00, Asia/Saigon)
- Sửa src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/AzureProcedureDocumentExtractor.cs:
  warnings string[]/object field+reason/mixed -> string[]; thiếu/null/sai kiểu thêm cảnh báo chuẩn,
  không mất payload chỉ vì lỗi warnings. Payload thiếu/không phải object vẫn bị từ chối; không sửa JSON hỏng.
- Sửa cùng thư mục TextFirstProcedureExtractor.cs: cập nhật thông báo lỗi parser cho hợp đồng mới.
- Tạo tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AiWarningsTests.cs: 15 cases.
- Sửa docs/api-guide.md và PROGRESS.md. Route/request/status/permission không đổi; FE vẫn nhận warnings string[].
- Release build --no-restore -warnaserror: 0 lỗi/0 cảnh báo. Test suite cuối 241/241 passed
  (Procedure 118, DocumentForm 76, Workflow 39, IAM 8).
- Lượt đầu 1 assertion lỗi do JsonReaderException kế thừa JsonException; sửa ThrowsAnyAsync và chạy lại đạt.
- Chưa xác minh Azure hoặc gọi AI thật trong phiên này; chỉ kiểm thử phản hồi giả lập.
  Không khẳng định đây là nguyên nhân duy nhất trên Azure. Không Git/build Docker/deploy.
- Commit gợi ý: fix(aiocr): normalize AI warnings without discarding valid payloads.

## MAIN-NGHIA-SYNC — 2026-10-13T23:57:15.973885+07:00 (Asia/Saigon)
Owner explicitly authorized Codex Git exception for this synchronization. Fetched origin; nghia had43 commits absent from main and main had35 absent from nghia. Merged origin/main into nghia without squash, preserving all original commits. Resolved PROGRESS history conflict by retaining both histories; DocumentForm API Program.cs retains main CORS before nghia JWT authentication/authorization. docs/api-guide.md auto-merged preserving both service guides. No new business API/DTO/token changes in this merge.
Verification: solution-wide Release build0 errors/0 warnings; existing tests226 passed,0 failed,0 skipped (ProcedureCatalog103, DocumentForm76, ApplicationWorkflow39, IAM8). Docker wardmate-documentform:main-sync-20261013 built0 errors/0 warnings. Local container wardmate-main-sync-preview localhost15044: health200, OpenAPI200, templates without token401. Uses a temporary local JWT key, migrations disabled; smoke does not claim authenticated business end-to-end acceptance.
In-progress application-document linkage is preserved separately in stash (message codex: preserve in-progress application document linking before main sync); it is NOT part of this merge or deployed. Must reconcile with the newly fetched Workflow before reapplying, rather than blindly restoring old source. Azure unchanged.
Main remote push awaits owner Docker acceptance under AGENTS.md release gate; local main may advance by fast-forward to this merge. No force-push. Suggested/used Conventional Commit: chore(repo): merge main and nghia service updates.

## MAIN-PUSH-CONFIRMED — 2026-10-14T00:00:32.815165+07:00 (Asia/Saigon)
Owner explicitly approved push main after Docker handoff. Fetched origin; main was0 behind/44 ahead and working tree clean. Normal push succeeded: origin/main advanced4e9f046 -> c7b43bc (chore(repo): merge main and nghia service updates). No force push; no nghia/deploy branch push. Previous verification remains solution Release0 warnings/0 errors,226 tests passed, Docker build succeeded, HTTP smoke health200/OpenAPI200/no-token templates401. This follow-up changes only PROGRESS.md; no API/DTO/token changes, no additional tests needed for documentation. WIP application-document linkage remains saved in stash and excluded from main. Azure/CI deployment not verified or claimed. Follow-up commit: docs(progress): record confirmed main synchronization.

## AIOCR-V9-AZURE-DEPLOY — Đồng bộ main/deploy & Build image AIOCR v9 (2026-10-14 00:15:00 +07:00, Asia/Saigon)
- Đồng bộ origin/main vào kha (tích hợp DocumentForm, giải quyết conflict PROGRESS.md tuần tự), Release build 0 lỗi/0 cảnh báo, 241/241 tests passed.
- Đã push đầy đủ lên cả 3 nhánh remote: `kha`, `main`, `deploy` (`b33c98f`).
- Build Docker image AIOCR từ mã nguồn mới nhất: `acrwardmate2026.azurecr.io/wardmate-aiocr:v9-gemini` (--no-cache, 0 warnings/errors).
  - ACR Tag: `v9-gemini` (tag mới chưa từng sử dụng trước đó).
  - Digest: `sha256:b0f99cdbadf7c6b09c2e8b1663632138a6574a2657d02e763a00289e275ee603`.
- Đã push thành công image lên ACR `acrwardmate2026.azurecr.io`.
- Giữ nguyên cấu hình billing và model Gemini (`gemini-3.1-flash-lite`, Provider `Gemini`).
- Đã kiểm tra trực tiếp endpoint Azure Container App `wardmate-aiocr`:
  - `GET /health` -> 200 OK (Healthy).
  - `POST /internal/v1/procedure-extractions` -> 200 OK.


## AI-PARTIAL-DRAFT — Schema và bảo toàn dữ liệu từng trường (2026-10-14 00:36:45 +07:00, Asia/Saigon)
- Tạo src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/ProcedureAiContract.cs:
  một contract sinh JSON Schema strict và chuẩn hóa payload đệ quy. Scalar thiếu/sai kiểu -> null + cảnh báo đường dẫn;
  object/array giữ cấu trúc, không dùng mặc định phí/ngày. categoryId=0, bỏ các khóa ngoài contract.
  Cảnh báo caseCode checklist/form không liên kết được với cases. Không khẳng định dữ liệu đúng/đủ nghiệp vụ.
- Sửa AzureProcedureDocumentExtractor.cs cùng thư mục: gửi json_schema; retry lỗi cấu trúc tối đa một lần,
  giới hạn tổng request cùng retry HTTP hiện có; không tự hạ schema, đổi model hoặc billing.
- Sửa tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AiWarningsTests.cs và GeminiExtractionTests.cs:
  thêm 2 cases (partial fields/liên kết case, retry JSON sai), kiểm tra request schema.
- Sửa docs/api-guide.md, PROGRESS.md. Không thay route/request/status/token/quyền/migration;
  áp dụng preview và draft worker; FE phải biểu diễn null là chưa điền và hiển thị warnings.
- Build Release --no-restore -warnaserror: 0 lỗi/0 cảnh báo; suite cuối 243/243 PASS
  (Procedure 120, DocumentForm 76, Workflow 39, IAM 8).
- Chưa kiểm chứng schema với Gemini thật/Azure trong phiên này. Model cần hỗ trợ json_schema;
  provider từ chối sẽ có fallback thủ công như trước. Chưa chia tài liệu theo mục nghiệp vụ (giai đoạn sau).
- Không Git, build Docker, deploy. Commit gợi ý: feat(aiocr): enforce structured output and preserve partial draft fields.

## DOC-GLOSSARY — 2026-10-14 11:25:29 +07:00 (Asia/Saigon)
- Tạo docs/wardmate-thuat-ngu.txt UTF-8 cho Notepad: thuật ngữ, vai trò, checklist mẫu/hồ sơ và giới hạn hiện tại.
- Chỉ cập nhật tài liệu; không thay API/code/config, không chạy build/test/Git/Docker.
- Commit gợi ý: docs: add Vietnamese WardMate terminology notes.

## PROCEDURE-CHECKLIST — Quản lý checklist thủ tục — 2026-10-14 11:38:01 +07:00 (Asia/Saigon)
- Tạo `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureChecklist.cs`.
- Sửa `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs`, `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureManagerTests.cs`, `docs/api-guide.md` và `PROGRESS.md`.
- API mới: `PUT /api/v1/procedure-manager/procedures/{id}/checklist`, Bearer JWT với permission `procedure.update`.
- Body: `expectedUpdatedAt`, `decisionNumber`, `effectiveDate`, `items` (toàn bộ danh sách ChecklistItemSchema theo thứ tự mong muốn). Dùng GET chi tiết quản trị hiện có để lấy checklist và updatedAt trước khi lưu.
- Thêm/sửa/xóa/sắp xếp bằng thay thế toàn bộ danh sách; `items: []` xóa toàn bộ checklist mẫu. Kiểm tra mã trùng, CaseCode tồn tại, loại giấy tờ, số lượng và dữ liệu bắt buộc bằng thông báo tiếng Việt.
- Lưu snapshot trạng thái cũ vào version kế tiếp và cập nhật checklist trong cùng transaction có khóa hàng. So sánh expectedUpdatedAt để chống ghi đè dữ liệu cũ; chuẩn hóa timestamp tới microsecond tương thích PostgreSQL.
- Response: 200 ProcedureDetailDto; 400 dữ liệu không hợp lệ; 401 chưa xác thực; 403 thiếu quyền; 404 thủ tục không tồn tại; 409 dữ liệu đã thay đổi. FE gặp 409 cần tải lại và đối soát trước khi lưu.
- Thêm 1 integration test cho thay danh sách/thứ tự, xóa toàn bộ, CaseCode sai, xung đột và số lượng versions. Toàn bộ suite 244/244 PASS (ProcedureCatalog 121, DocumentForm 76, ApplicationWorkflow 39, IAM 8).
- `dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1`: 0 errors, 0 warnings. `dotnet test WardMate.sln -c Release --no-restore -v minimal`: thành công.
- Không cần migration. Không thay checklist của Application đã tạo. Chưa bổ sung metadata/file verification cho Application trong task này; chưa kiểm thử Azure.
- Không chạy Git hoặc build/deploy Docker. Commit gợi ý: `feat(procedure): add versioned checklist management`.

## APPLICATION-CHECKLIST-LIFECYCLE — Đóng băng checklist hồ sơ & kiểm tra nộp minh chứng — 2026-10-14 12:08:00 +07:00 (Asia/Saigon)
- Hoàn thành đầy đủ 5 bước vòng đời checklist:
  1. Procedure Manager: Thêm, sửa, xóa, sắp xếp checklist; quản lý trường hợp áp dụng, số lượng, loại bản sao (`ORIGINAL`, `CERTIFIED_COPY`, `REGULAR_COPY`), hình thức nộp (`NOP`, `XUAT_TRINH`).
  2. Application: Sao chép snapshot toàn bộ thuộc tính checklist khi tạo hồ sơ (`sortOrder`, `caseCode`, `submissionType`, `documentCopyType`, `quantity`, `conditionNote`). Checklist của hồ sơ được đóng băng độc lập, không bị thay đổi âm thầm khi thủ tục cập nhật sau đó. Migration `SnapshotChecklistRequirements` (ApplicationWorkflow).
  3. DocumentForm: Bổ sung khai báo thuộc tính giấy tờ minh chứng (`SubmissionType`, `DocumentCopyType`, `Quantity`) khi upload qua `POST /api/v1/applications/{id}/supporting-documents`. Migration `SupportingEvidenceRequirements` (DocumentForm).
  4. Pre-submit Validation: Kiểm tra phía server trước khi Submit / Resubmit / Đánh dấu hoàn thành: đối soát qua `GET /api/v1/applications/{id}/contents`, kiểm tra file thực sự tồn tại trong Blob Storage (`blobs.ExistsAsync`), khớp loại bản sao, hình thức nộp và tổng số lượng giấy tờ. Trả về ProblemDetails 422 `application.documents_incomplete` kèm `missingChecklistIds`.
  5. E2E Testing: Viết kịch bản `scripts/verify-checklist-lifecycle.py` kiểm thử toàn luồng tích hợp với 3 service thật (`ProcedureCatalog`, `ApplicationWorkflow`, `DocumentForm`) chạy trên PostgreSQL và Azurite độc lập: xuất bản thủ tục -> tạo hồ sơ -> cập nhật checklist thủ tục (xác nhận hồ sơ không bị đổi) -> nộp file sai loại bản sao (bị từ chối) -> nộp file hợp lệ -> xóa file/mô phỏng mất file blob (bị từ chối submit 422) -> khôi phục blob (submit thành công) -> cán bộ yêu cầu bổ sung -> nộp lại -> cán bộ phê duyệt -> kiểm tra lịch sử snapshot phiên bản. Kết quả: 56/56 HTTP checks PASS!
- Test suite: 250/250 tests PASS (ProcedureCatalog: 121, DocumentForm: 76, Workflow: 45, IAM: 8).
- `dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1`: 0 errors, 0 warnings.
- Chưa push Git, chưa build Docker, chưa deploy Azure.



## REVIEW-CLEANUP — Rà soát bàn giao và gỡ bộ kiểm thử — 2026-10-14 15:28:04 +07:00 (Asia/Saigon)
- Theo yêu cầu mới của chủ dự án: kiểm tra hướng dẫn Antigravity, xác minh source, sau đó gỡ các file test không tham gia runtime. Không chạy Git/build Docker/deploy Azure.
- Xác minh trước khi xóa: 250/250 tests PASS (ProcedureCatalog 121, DocumentForm 76, Workflow 45, IAM 8); 56/56 HTTP checks PASS chạy ProcedureCatalog, Workflow và DocumentForm thật với PostgreSQL/Azurite tạm. IAM là stub, không khẳng định IAM/Azure hoặc kết xuất PDF thật đã được kiểm thử trong 56 bước này.
- Lượt smoke cũ bị lỗi khởi động Azurite, chưa chạy nghiệp vụ. Đã chạy lại thành công toàn bộ 56 bước trước khi dọn.
- Giữ nguyên phần runtime checklist: metadata độc lập trong Application, liên kết ChecklistId/FormTemplateId, kiểm tra Blob tồn tại và khai báo loại/số lượng trước COMPLETE/submit/resubmit; giữ hai migration SnapshotChecklistRequirements và SupportingEvidenceRequirements cùng snapshots EF.
- Xóa: tests/ (4 test projects và outputs), TestResults/, scripts/ (7 script verify cùng hỗ trợ nếu có), src/Services/WardMate.Services.DocumentForm/TestEfCore/, test_dummy.docx, src/Services/WardMate.Services.DocumentForm/documentform-api.http và azure-documentform-api.http.
- Sửa WardMate.sln: gỡ 4 test projects, solution folder tests và mọi cấu hình GUID tương ứng. Sửa .github/workflows/iam-ci.yml: chỉ restore/build Release -warnaserror, không còn bước dotnet test tạo cảm giác có coverage.
- Sửa README.md; docs/api-guide.md, docs/docker-local.md, docs/iam-httponly.md, docs/api-pdf-extract-preview.md, docs/procedure-pdf-text.md, docs/procedure-catalog.md, docs/procedure-manager.md, docs/procedure-reviewed-publishing.md: bỏ lệnh gọi script/test đã xóa, đánh dấu kết quả cũ là lịch sử.
- Hướng dẫn API được đính chính: tạo Application cần wardCode; formDefinitions cần liên kết template với checklist; upload DOCX đã điền khác editor FE; generate-pdf nhận multipart và cần UserSubmission Approved riêng, Workflow APPROVED không tự duyệt đơn DocumentForm. Quantity/loại bản sao là khai báo để cán bộ đối chiếu, không chứng minh tính xác thực của giấy tờ.
- Không thêm/xóa endpoint trong bước dọn. Request/response/token runtime không đổi do dọn file. Giới hạn còn lại: kiểm tra HTTP liên service chưa có giao dịch phân tán chống mọi race; PDF/Gotenberg và Azure chưa xác minh ở phiên này.
- Build SAU khi xóa: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 thành công 0 errors, 0 warnings. Sau dọn không còn test suite; không gọi dotnet test để báo pass giả.
- Commit gợi ý cho Antigravity: chore(repo): remove test assets and update verification documentation.

## APPROVAL-PDF-FLOW — Phân quyền thụ lý, duyệt một lần và PDF thật — 2026-10-14 16:08:18 +07:00 (Asia/Saigon)
- Hoàn thiện 5 hạng mục BE: phạm vi cán bộ, Workflow làm nguồn quyết định duy nhất, chuyển PDF thật, chặn ghi sau duyệt và đối soát local.
- File sửa trong Workflow: API/Controllers/ApplicationReviewController.cs (GET chi tiết cán bộ); Infrastructure/ApplicationReviewStore.cs (nạp checklist/lịch sử cho DTO).
- File sửa trong DocumentForm API: Security/ApplicationAccess.cs; Controllers/OfficerSubmissionsController.cs, GeneratedDocumentsController.cs, CitizenSubmissionsController.cs, SupportingDocumentsController.cs.
- File sửa trong DocumentForm: Application/Commands/GeneratePdfDocumentCommand.cs; Domain/Errors/DocumentFormErrors.cs; Infrastructure/Pdf/GotenbergPdfRenderEngine.cs; Infrastructure/DependencyInjection.cs.
- File cấu hình/tài liệu: docker/docker-compose.yml, docs/api-guide.md, PROGRESS.md. Không migration mới, không tạo lại test projects.
- API mới GET /api/v1/officer/applications/{id}: không body, permission workflow.read, cán bộ đúng phường đang thụ lý; 200 ApplicationDto, 401/403 thiếu xác thực/quyền, 404 ngoài phạm vi, 503 IAM không khả dụng.
- DocumentForm kiểm tra phạm vi hiện tại qua Workflow trước khi cán bộ đọc/annotate/duyệt hoặc tải PDF, không mặc nhiên cho MANAGER/PROCEDURE_MANAGER/IT_ADMIN toàn quyền thao tác đơn. OfficerId client không được khác sub.
- Workflow APPROVED là nguồn quyết định. Đồng bộ đọc: đơn Submitted được chuyển Approved khi DocumentForm xác minh Workflow; generate-pdf tự kích hoạt đồng bộ, không cần người dùng duyệt hai lần. DB/HTTP lỗi thì trả lỗi để thử lại, không coi thành công. Đây không phải worker/outbox đồng bộ ngay toàn bộ đơn.
- POST officer/submissions/{id}/approve body {} và request-revision body {comment}: proxy quyết định TOÀN BỘ hồ sơ qua Workflow, không duyệt riêng đơn. 200 {submissionId,message}, lặp quyết định đã áp dụng trả thành công không lặp lịch sử; 409 Workflow không chấp nhận, 503 mất kết nối. FE cần cập nhật response và ưu tiên gọi Workflow trực tiếp.
- Annotation chỉ UNDER_REVIEW; citizen ghi chỉ DRAFT/NEED_REVISION. Xem/download PDF và generate đều cần Workflow APPROVED; old local Approved không đủ quyền. Cán bộ khác phường/chưa assign bị chặn.
- POST applications/{id}/generate-pdf: multipart SubmissionId, tùy chọn DocumentType, không file thay thế. 200 GeneratedDocumentDto; 409 chưa được duyệt, 404 sai liên kết/phạm vi, 503 chuyển đổi/kết nối lỗi. GET danh sách/download kiểm tra cùng phạm vi.
- Xóa fallback tạo PDF giả chỉ có tiêu đề; Gotenberg bắt buộc trả PDF có chữ ký %PDF-. Giữ DOCX gốc có comment, chỉ strip bản sao để convert. Mỗi lần xuất có GeneratedDocument riêng để tránh ghi đè PDF của đơn khác trong cùng Application.
- Compose thêm gotenberg/gotenberg:8 nội bộ (không publish cổng), Gotenberg__Url và dependency document-form. docker compose config --quiet thành công. Chỉ pull/run container tạm phục vụ kiểm tra, không build image WardMate hay thay đổi stack người dùng.
- Build Release cuối: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1, 0 errors/0 warnings.
- Kiểm thử tạm qua HTTP: 38 checks PASS với Workflow/DocumentForm, PostgreSQL, Azurite, Gotenberg thật; IAM/ProcedureCatalog stub. Gồm wrong ward, same ward unassigned, giả officerId, chặn PDF trước duyệt, annotate, comment OPEN/request revision/resubmit, Workflow approve một lần, đồng bộ Approved, retry approve idempotent, chặn citizen/officer sửa sau duyệt, render/download, kiểm tra text PDF chứa nội dung đơn và không chứa comment nội bộ trong khi DOCX gốc giữ comment, lỗi kết nối/IAM và Gotenberg trả 503.
- Lượt kiểm tra đầu thiếu comment OPEN nên Workflow từ chối request revision đúng quy tắc; đã sửa kịch bản và chạy lại 38 bước đạt. Không phải lỗi bypass ở runtime.
- Script hỗ trợ tạm được xóa sau kiểm tra; không khôi phục tests/. Unit tests: không chạy vì bộ test đã gỡ theo yêu cầu. Không tuyên bố 250 tests của lần trước là coverage cho task này.
- Giới hạn: chưa xác minh FE/Azure/IAM thật; chưa có giao dịch phân tán/lease khóa mọi request đang chạy đồng thời giữa hai service. Đồng bộ đọc đảm bảo kiểm tra trạng thái trên các request kế tiếp, không tuyên bố đóng băng nguyên tử mọi race.
- Không Git/commit/push/deploy. Antigravity tự quản lý Git và chủ dự án build Docker. Gợi ý: fix(document-form): enforce workflow approval and scoped PDF access.

## DOC-CONSTRAINTS-001 — Sổ tay ràng buộc hệ thống

- Hoàn thành: 2026-10-14 20:48:33 +07:00 (Asia/Saigon, UTC+07:00).
- Tạo docs/system-constraints.md: tổng hợp ràng buộc theo source hiện hành của IAM, Procedure Catalog, PDF drafts/AIOCR, Workflow/checklist, DocumentForm/PDF, Notifications, database và bảo mật trình duyệt. Phân biệt điều kiện kiểm tra, giới hạn môi trường và phần chưa bảo đảm; có mã nhóm quy tắc và nguồn đối chiếu.
- Sửa AGENTS.md: bắt buộc cập nhật sổ tay trong cùng task khi thêm/sửa/xóa ràng buộc; giữ mã nhóm, cập nhật lịch sử và phạm vi kiểm tra thực tế.
- Sửa docs/api-guide.md: thêm liên kết tới sổ tay dùng chung. Cập nhật PROGRESS.md (file này).
- API mới/thay đổi: không có. Không thay method/route/request/response, DTO, ProblemDetails, token handling hay logic runtime trong task tài liệu này.
- Frontend: tra cứu giới hạn trường, trạng thái, quyền/phường/chủ sở hữu, bằng chứng tài liệu và lỗi trong sổ tay; hướng dẫn gọi API vẫn ở docs/api-guide.md.
- Kiểm tra: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 thành công, 0 errors, 0 warnings. Kiểm tra liên kết tương đối trong sổ tay: không có đường dẫn hỏng.
- Unit/integration tests mới: 0; không chạy dotnet test vì chủ dự án đã gỡ test projects. Không khôi phục test project và không coi build là kiểm thử nghiệp vụ.
- Đã xóa file quét tạm .constraints-scan.tmp.txt. Không chạy Git, build Docker hoặc deploy Azure.
- Gợi ý Conventional Commit: docs: document system constraints and maintenance rules.
## FLOW-REVIEW-001 — Rà soát luồng hồ sơ liên service và sửa lỗi hợp đồng dữ liệu

- Hoàn thành phần rà soát/sửa code/kiểm tra tạm: 2026-10-15 07:15:51 +07:00 (Asia/Saigon).
- Phạm vi: Procedure → Workflow → DocumentForm → Notifications. Chưa xác nhận toàn bộ luồng Docker/Azure; chủ dự án tự build Docker và kiểm thử thực tế theo hướng dẫn.

### File thay đổi trong task

- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs`: kiểm tra chung số lượng/ID checklist, tham chiếu case, case trùng/quá dài, giới hạn form code/name, GUID template rỗng và form trùng phạm vi. Cho phép cùng mã form ở hai case riêng biệt.
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureChecklist.cs`: truyền ContentPayload thật vào validation checklist chọn thuộc tính để case hợp lệ không bị từ chối.
- `src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationReviewController.cs`: validate command resubmit trước khi duyệt mảng/call service; phần tử null không gây NullReferenceException trước MediatR pipeline.
- `src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/DocumentContentsClient.cs`: phản hồi chứa null/ID rỗng/ID trùng bị từ chối 503, không cộng trùng số lượng bằng chứng.
- `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Security/ApplicationAccess.cs`: shared read kiểm tra owner trước, chỉ fallback cán bộ được giao khi owner lookup trả 404. Không fallback khi IAM/Workflow không xác minh được hoặc trạng thái bị khóa. Officer-only review vẫn cấm tự thẩm định.
- `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Controllers/SupportingDocumentsController.cs`: dùng kiểm tra shared read cho danh sách.
- `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Controllers/GeneratedDocumentsController.cs`: dùng shared read cho xuất/danh sách PDF, vẫn bắt buộc Workflow APPROVED.
- `docs/api-guide.md`: cập nhật hành vi/lỗi và hướng dẫn kiểm tra Docker theo chín bước, bao phủ năm nhóm công việc.
- `docs/system-constraints.md`: cập nhật PROC-01/02, WF-04, DOC-03 và nhật ký quy tắc.
- `PROGRESS.md`: bản ghi này. Không tạo migration, không thay schema, không đổi cấu hình JWT/billing/model.

### API và lưu ý FE

- Không thêm route. POST `/api/v1/procedure-manager/procedures`, PUT `/api/v1/procedure-manager/procedures/{id}`, POST `/api/v1/procedure-manager/procedures/publish`, POST `/api/v1/procedure-manager/drafts/{id}/publish`: giữ body hiện hành, bổ sung lỗi 400 `validation.failed` nếu checklist/case/form sai hợp đồng. Draft save vẫn cho phép dữ liệu chưa hoàn chỉnh, publish mới validate đầy đủ.
- PUT `/api/v1/procedure-manager/procedures/{id}/checklist`: giữ `{expectedUpdatedAt, decisionNumber, effectiveDate, items}`, success 200, timestamp cũ 409; dùng đúng case từ thủ tục hiện hành.
- POST `/api/v1/applications/{id}/resubmit`: giữ `{expectedVersionNumber, formData, checklists}`; dữ liệu null/trùng bị validation trước gọi phụ thuộc, success vẫn 200, dữ liệu sai 400, thiếu tài liệu 422, lỗi service 503.
- PATCH `/api/v1/applications/{id}/checklists/{checklistId}`, POST `/api/v1/applications/{id}/submit` và resubmit: 503 `application.documents_unavailable` khi phản hồi bằng chứng malformed/trùng ID; không xem đây là lỗi người dùng cần upload lại toàn bộ.
- Các GET danh sách/download supporting documents, generated documents và POST generate-pdf: kiểm tra chính chủ trước; tài khoản có cả citizen/Front Desk không mất quyền đọc hồ sơ mình. Ngoài phạm vi vẫn 404, PDF chưa APPROVED vẫn 409. Body/DTO thành công, permission và token handling giữ nguyên.

### Kết quả xác minh và giới hạn

- Release build: `dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1` đạt 0 errors, 0 warnings.
- 72/72 kiểm tra tạm PASS trực tiếp trên assembly Release: validator Procedure, controller resubmit trước dependency, DocumentContentsClient với HTTP giả lập, owner/assigned officer/khóa sau duyệt, Workflow handlers tạo/checklist/submit/comment/revision/resubmit/diff/approve/reject với store trong bộ nhớ, lỗi renderer, hợp đồng notification, EF metadata unique index và capture outbox không lặp/không chứa formData.
- Kiểm tra ban đầu của harness có lỗi cú pháp target-typed new mơ hồ, đã sửa harness rồi chạy đạt. Khi bổ sung metadata Notifications, compiler trực tiếp gặp CS1702 do tham chiếu EF 8.0.10/8.0.11; harness tạm dùng cơ chế bỏ cảnh báo hợp nhất assembly này, chạy thành công. Không sửa/suppress warning trong project nghiệp vụ; solution build -warnaserror vẫn sạch.
- Không tạo `.csproj` test hoặc khôi phục tests/. Harness biên dịch trực tiếp bằng csc, đã xóa toàn bộ `.flow-check-tmp` sau kiểm tra. Không chạy `dotnet test` vì không còn test project.
- 72 checks không phải kiểm thử integration với PostgreSQL/RabbitMQ/Azure Blob/Gotenberg/Gemini thật. Chưa kiểm thử transaction SQL, giao lại message thực, kết nối SignalR thực hoặc PDF nội dung thực trong phiên này. Không lấy kết quả kiểm thử lịch sử làm bằng chứng cho bản mới.
- Khóa request đang chạy đồng thời giữa Workflow/DocumentForm chưa được giải quyết bằng lease/transaction phân tán. Các kiểm tra khóa trạng thái chứng minh chặn request sau khi trạng thái đã đổi, không chứng minh loại bỏ mọi race đang diễn ra.
- Kiểm tra liên kết sổ tay: 0 đường dẫn hỏng. Không chạy Git, không build/run Docker, không deploy. Email/SMS vẫn để sau theo yêu cầu.
- Gợi ý commit theo chức năng cho Antigravity: `fix(procedure): validate checklist and case references consistently`; `fix(workflow): reject malformed resubmissions and document evidence`; `fix(document-form): preserve owner access for dual-role accounts`; `docs: record cross-service review and local verification steps`. Antigravity tách từng file/chức năng theo AGENTS.md.


## DOC-DIAGRAMS-001 — ERD, Activity, State Machine và Structure Analysis

- Hoàn thành: 15/10/2026 15:01:16, Asia/Saigon (UTC+07:00).
- Tạo `docs/system-diagrams.md`: 17 sơ đồ Mermaid, gồm 5 ERD (28 bảng), 1 sơ đồ liên kết logic liên service, 3 activity, 3 state machine và 5 sơ đồ phân tích cấu trúc (FDD, service, Clean Architecture, DFD context/mức 1).
- Đối chiếu entity, EF mapping và handlers hiện có; phân biệt FK thật với ID logic, JSONB với bảng, Application với UserSubmission và ProcedureDraft. Ghi rõ CANCELLED chỉ được khai báo nhưng chưa có handler chuyển trạng thái trong phần đã kiểm tra.
- File thay đổi: `docs/system-diagrams.md` (mới), `PROGRESS.md` (nối nhật ký).
- API mới/thay đổi: không có; request body, response code, DTO, ProblemDetails và token handling giữ nguyên. Không thay đổi ràng buộc runtime.
- Kiểm tra: 17 khối Mermaid có cặp Markdown fence đầy đủ; đối chiếu 28 entity trong ERD. Chưa chạy renderer Mermaid để xác nhận bố cục hình ảnh trong từng IDE.
- `dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1`: PASS, 0 errors, 0 warnings (06:03.40).
- Tests: 0 test mới; không chạy dotnet test do test projects đã được chủ dự án gỡ bỏ. Đây là cập nhật tài liệu, không phải kiểm thử toàn luồng.
- Không chạy Git, build Docker hoặc deploy Azure. Sơ đồ mô tả workspace, không xác nhận revision trên môi trường deploy.
- Gợi ý Conventional Commits cho Antigravity, tách từng file: `docs: document WardMate system diagrams`; `docs: record system diagram verification`.

## DOC-DIAGRAMS-002 — ERD tương lai đầy đủ 6 microservices

- Hoàn thành: 15/10/2026 15:19:19, Asia/Saigon (UTC+07:00).
- Thay đổi: `docs/system-diagrams.md`, `PROGRESS.md`.
- Đối chiếu đặc tả gốc 35 bảng và source hiện tại. Bổ sung thiết kế 6 bảng AIOCR (Knowledge Base/chunks/chat/log AI/OCR), 8 bảng AnalyticsSystem (QR, feedback, metrics, audit, settings, health, backup), cùng sơ đồ tổng hợp đủ 6 service.
- Phân biệt 28 bảng đã có và 14 bảng chưa triển khai. Tổng đích 42 do giữ thêm 7 bảng phát sinh ngoài đặc tả gốc. Không tạo entity, migration, database hay endpoint.
- Ghi rõ điểm cần chốt: chiều phường của metrics; kích thước embedding; citation UUID array không có FK; AI request không thuộc chat; một QR và nhiều PDF kết quả. Đây là phân tích thiết kế, không thay đổi ràng buộc đang thực thi.
- Kiểm tra tài liệu: PASS 20 khối Mermaid với fence ghép đôi và 42 tên entity riêng biệt. Chưa render để kiểm tra bố cục hình.
- Build/test: không chạy lại trong lượt chỉ bổ sung tài liệu; Release build lượt DOC-DIAGRAMS-001 trước đó đạt 0 errors/0 warnings. Không dùng kết quả đó làm kiểm thử cho các bảng tương lai. 0 tests mới; không khôi phục test projects.
- API/DTO/ProblemDetails/token: không thay đổi. Frontend không được gọi các bảng/chức năng dự kiến như thể đã có API.
- Không chạy Git, Docker, deployment. Gợi ý tách commit theo file cho Antigravity: `docs: extend ERD with planned AI and analytics schemas`; `docs: record future ERD design review`.

## DOC-DIAGRAMS-003 — Activity, State Machine và Structure Analysis đích cho 6 service

- Hoàn thành: 15/10/2026 15:28:31, Asia/Saigon (UTC+07:00).
- File mới: `docs/system-future-flows.md`. File cập nhật: `docs/system-diagrams.md` (mục lục và mục 7 dẫn sang phần bổ sung), `PROGRESS.md`.
- Nội dung: 9 Activity Diagram, 10 State Machine, 4 Structure Analysis; ma trận CÓ / MỘT PHẦN / DỰ KIẾN cho IAM, ProcedureCatalog, ApplicationWorkflow, DocumentForm, AIOCR, AnalyticsSystem.
- Bao quát auth/quyền, publish/indexing, hồ sơ/hủy dự kiến, PDF có QR, KB/RAG/chat, OCR CCCD, notification đa kênh, QR/feedback/dashboard, audit/settings/health/backup. Phân biệt OCR PDF hiện có với pipeline CCCD chưa có, Sent với Delivered/Read, enum hiện tại với trạng thái job đề xuất.
- Quy tắc tương lai được đánh dấu là đề xuất chưa thực thi, không thay đổi `docs/system-constraints.md` để tránh mô tả nhầm như check đã có. Không thêm entity/migration/API.
- Kiểm tra tài liệu: PASS 23 khối Mermaid, 46 fence ghép đôi, 10 stateDiagram-v2 và 13 flowchart; các file đích liên kết tồn tại. Chưa chạy renderer Mermaid để kiểm tra bố cục hình.
- Build/test: không chạy lại cho lượt chỉ sửa tài liệu; 0 tests mới, không khôi phục test projects. Release build ở DOC-DIAGRAMS-001 trước đó đạt 0 errors/0 warnings; không phải kiểm thử cho tính năng tương lai.
- Endpoints/methods/request bodies/response codes, frontend DTO, ProblemDetails và token handling: không thay đổi. FE không được giả định các luồng DỰ KIẾN đã có endpoint.
- Không chạy Git, Docker, triển khai Azure, gọi AI trả phí hoặc thao tác backup thật.
- Gợi ý commit theo từng file cho Antigravity: `docs: add six-service target activities and state machines`; `docs: link target flows from system diagrams`; `docs: record target workflow diagram review`.

## DOC-DIAGRAMS-004 — ERD hợp nhất toàn hệ thống

- Hoàn thành: 15/10/2026 15:45:50, Asia/Saigon (UTC+07:00).
- Tạo `docs/system-unified-erd.md`: một ERD Mermaid, 42 bảng (28 hiện có / 14 FUTURE) và 63 quan hệ có nhãn FK/FK_FUTURE/REF/REF_FUTURE. Nội dung cột lấy từ ERD chi tiết đã đối chiếu.
- Cập nhật `docs/system-diagrams.md`: mục lục và mục 8 liên kết ERD tổng; giữ nguyên ERD riêng. Cập nhật `PROGRESS.md`.
- Phân biệt quan hệ vật lý với tham chiếu ID/string/JSON/event; không biến sơ đồ hợp nhất thành database chung. Health/backup logs giữ không có cạnh vì chưa có khóa tham chiếu trong thiết kế; không bịa FK. QR URL/PDF và snapshot checklist được giải thích riêng.
- Kiểm tra tài liệu PASS: 42 entity duy nhất, 14 FUTURE, 63 cạnh có hai đầu tồn tại, một block Mermaid với fence ghép đôi. Chưa render để xác nhận bố cục/khả năng đọc của sơ đồ lớn.
- Build/test: không chạy lại cho thay đổi tài liệu thuần túy; không tạo test project. Release build DOC-DIAGRAMS-001 trước đó 0 errors/0 warnings không được xem là xác nhận tính năng tương lai.
- API, DTO, request/response, ProblemDetails, token và ràng buộc runtime: không đổi. Không tạo bảng/migration, không chạy Git, Docker hoặc deploy.
- Gợi ý commit từng file cho Antigravity: `docs: add unified cross-service ERD`; `docs: link unified ERD from system diagrams`; `docs: record unified ERD verification`.

## DOC-ACTIVITY-UML-005 — Activity Diagram tiếng Anh theo ảnh mẫu

- Hoàn thành: 15/10/2026 21:05:46, Asia/Saigon (UTC+07:00).
- Theo yêu cầu mới: nhãn tiếng Anh ngắn, hành động bắt đầu bằng động từ; initial node đen, action xanh bo góc, decision/merge hình thoi, final node hai vòng. Phần dự kiến dùng nét đứt, Email một phần dùng viền vàng; không ghi mô tả dài trong ô.
- Tạo thư mục `docs/diagrams/activities/`: 9 SVG, 9 PNG, 9 Mermaid `.mmd`, `wardmate-activities.drawio` (9 trang chỉnh sửa được), `index.html` offline, `overview.png`, `README.md`, `generate-activities.cjs` (nguồn sinh sơ đồ). Tổng 33 file tài liệu/đồ họa trong thư mục.
- Cập nhật `docs/system-diagrams.md`, `docs/system-future-flows.md` để liên kết bộ vẽ mới; giữ nguyên phân tích tiếng Việt và ERD. Cập nhật `PROGRESS.md`.
- Kiểm tra: sinh 9 SVG và render 9 PNG thành công bằng Sharp; kiểm tra trực quan bản Application Workflow và contact sheet của cả 9 hình. Đã sửa đường nối vào merge để không xuyên action và đưa đường vòng ra ngoài cột trái.
- Parse XML draw.io PASS: 9 trang, cell ID duy nhất theo trang, mọi connector tham chiếu node tồn tại. Kiểm tra 9 Mermaid source PASS với nhãn ASCII-English và không dùng node ID end vốn là từ khóa Mermaid. Chưa kiểm tra thao tác import thật trong ứng dụng draw.io hoặc chạy parser Mermaid; bố cục auto-layout Mermaid có thể khác SVG/draw.io.
- Build/tests: không chạy dotnet build/test cho thay đổi tài liệu và hình; không tạo test project. Không thay đổi endpoint, DTO, request/response, ProblemDetails, token hoặc ràng buộc runtime.
- Các hình là luồng rút gọn; README ghi rõ giới hạn và bước tương lai. Không khẳng định QR, RAG, OCR CCCD, Email/SMS production hoặc backup đã triển khai.
- Không chạy Git, Docker, deployment hoặc dịch vụ AI. Gợi ý Conventional Commit cho Antigravity (tách file theo quy tắc): `docs: add editable English UML activity diagrams`; `docs: link activity diagram exports and record verification`.

## DOC-ACTIVITY-SCOPE-006 — Điều chỉnh sơ đồ sang hỗ trợ chuẩn bị giấy tờ

- Hoàn thành: 15/10/2026 21:55 (Asia/Saigon, UTC+07:00).
- Phạm vi: chỉ sơ đồ và tài liệu. Không sửa backend, API, database, migration hoặc cấu hình; không chạy Git/Docker.
- Application: Create Application → Complete Forms and Attach Documents → Submit for Document Review → Review Documents. Thiếu giấy tờ → Request Revision → Revise and Resubmit → kiểm tra lại. Đủ giấy tờ → Confirm Document Readiness → Download Prepared Documents → Visit Ward Office. Hoạt động tại phường nằm ngoài WardMate; không có quyết định phê duyệt/từ chối hành chính trên hệ thống đích.
- DocumentForm/Analytics: đổi ý nghĩa PDF/QR sang giấy tờ chuẩn bị và tham chiếu; cập nhật State Machine, FDD/DFD liên quan. DOCUMENTS_READY và ReadyForDownload chỉ là tên trạng thái đề xuất, chưa phải hợp đồng API hiện hành.
- File thay đổi: docs/diagrams/activities/generate-activities.cjs, README.md, index.html, wardmate-activities.drawio; các bản 03-application-workflow, 04-document-form, 08-analytics-results (.mmd/.svg/.png); bộ 9 Mermaid/SVG/PNG được tái sinh đồng bộ và overview.png; docs/system-diagrams.md, docs/system-future-flows.md, docs/system-unified-erd.md; PROGRESS.md.
- ERD giữ nguyên bảng/cột và quan hệ; thêm chú thích phân biệt mô hình cũ với phạm vi mới. Không suy ra database đã được cập nhật.
- Kiểm tra: sinh và render 9 sơ đồ + overview; kiểm tra cấu trúc XML 9 trang draw.io (ID duy nhất, nguồn/đích cạnh tồn tại); kiểm tra 9 nguồn Mermaid (nhãn tiếng Anh, không dùng reserved end, không còn Approve/Reject Application). Xem ảnh Application và Analytics sau render. Đây là kiểm tra cấu trúc, chưa phải nhập thử bằng ứng dụng draw.io hoặc chạy Mermaid parser.
- Build/test: không chạy dotnet build/test vì chỉ đổi tài liệu; 0 test mới, không có kết quả kiểm thử runtime trong phiên này.
- API mới/thay đổi: không có; method/route/body/status code không đổi. FE tiếp tục dùng hợp đồng hiện hành cho đến task sửa backend. docs/api-guide.md và docs/system-constraints.md không đổi vì mô tả hành vi code hiện tại.
- Còn lại: sau khi chốt sơ đồ, sửa lifecycle backend, permission/API, đồng bộ DocumentForm/PDF, event/notification/metrics và tài liệu ràng buộc tương ứng. Không được dùng trạng thái đề xuất để gọi API hiện tại.
- Gợi ý commit: docs: align workflow diagrams with document preparation scope


## WF-READINESS-001 — Luồng kiểm tra giấy tờ thay cho quyết định hành chính

- Hoàn tất mã nguồn cục bộ: 17/10/2026 01:54 Asia/Saigon (UTC+07:00). Chưa chạy Docker, chưa áp dụng DB/deploy Azure; không chạy Git.
- Luồng: DRAFT → SUBMITTED → UNDER_REVIEW → NEED_REVISION → nộp lại, hoặc UNDER_REVIEW → DOCUMENTS_READY. Giấy tờ sẵn sàng để mang tới phường, không phải phê duyệt/từ chối thủ tục.
- API mới: POST /api/v1/officer/applications/{id}/confirm-readiness, không body, quyền workflow.readiness.confirm; 200 ReviewResultDto có application.documentsReadyAt; 401/403 thiếu xác thực/quyền hoặc sai cán bộ/phường; 404 ngoài phạm vi; 409 sai trạng thái, thiếu snapshot, nhận xét OPEN, checklist chưa đủ; 422 tài liệu không đáp ứng; 503 dịch vụ tài liệu không khả dụng. Bỏ POST /approve và /reject của Workflow.
- DocumentForm: đổi POST /api/v1/officer/submissions/{id}/approve thành /confirm-readiness (giữ DTO officerId tùy chọn và đối chiếu danh tính), chuyển tiếp toàn hồ sơ sang Workflow. Đồng bộ Submitted → ReadyForDownload khi đọc Workflow DOCUMENTS_READY. PDF/download/QR yêu cầu trạng thái mới; route approved-qr giữ tương thích tên, ý nghĩa là tham chiếu giấy tờ. Không còn handler local tự approve độc lập.
- Chặn chỉnh sửa/bắt lỗi sau DOCUMENTS_READY; kiểm tra cán bộ được phân công, đúng phường, không tự xác nhận; snapshot/checklist/comment được kiểm tra dưới khóa hàng DB. Xác minh tài liệu qua DocumentForm trước transaction; đây không phải transaction phân tán, vẫn có cửa sổ với thao tác tài liệu đang chạy đồng thời.
- Migration Workflow 20261016154813_DocumentReadiness: thêm documents_ready_at và cập nhật status constraint. Giữ APPROVED/REJECTED/approved_at cũ để bảo toàn lịch sử, không tự chuyển thành DOCUMENTS_READY; các hồ sơ cũ không đi qua gate xuất PDF mới. Down bị chặn khi có DOCUMENTS_READY.
- Migration IAM 20261016154952_WorkflowReadinessPermission: tạo quyền mới, chuyển grant từ workflow.approve, gỡ grant/quyền approve và reject; không tự chuyển grant reject-only. Down cần phục hồi/đối soát thủ công. Dùng DO/PERFORM để hỗ trợ SQL idempotent.
- Analytics: thông báo DOCUMENTS_READY có nội dung giấy tờ sẵn sàng; tiếp nhận event APPROVED/REJECTED tồn từ phiên bản cũ với nhãn lịch sử trung tính. Workflow ghi status history/outbox cùng transaction hiện có.
- Kiểm tra: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 đạt 0 lỗi, 0 cảnh báo (lần cuối 00:01:17.88). EF has-pending-model-changes Workflow và IAM đều báo không có thay đổi thiếu migration. Sinh/đọc script idempotent cho hai migration thành công; chưa thực thi script trên DB. Rà source không còn route approve/reject hay tham chiếu workflow.approve/reject ngoài migration.
- Automated tests: 0 chạy, 0 tạo mới (owner đã gỡ test projects); chưa xác nhận tích hợp/runtime toàn luồng. Cần test Docker citizen submit → officer nhận → yêu cầu bổ sung → resubmit → confirm → PDF, cùng lỗi sai phường, OPEN comment, thiếu file và khóa sau xác nhận.
- FE: đổi trạng thái/menu/nút, dùng documentsReadyAt thay approvedAt; giữ hiển thị trạng thái cũ như lịch sử. ProblemDetails có code/traceId; 422 có thể có thông tin mục thiếu. Workflow đọc quyền hiện hành từ IAM; refresh/login lại để menu FE nhận claims mới.
- Triển khai: tạm dừng thao tác review trong cutover, áp dụng migration Workflow+IAM và cập nhật đồng bộ IAM/Workflow/DocumentForm/AnalyticsSystem; không để API cũ phục vụ sau khi đã gỡ quyền cũ.

### Paths thay đổi trong tác vụ

- src/BuildingBlocks/WardMate.SharedKernel/Web/FeaturePermissions.cs.
- src/Services/WardMate.Services.ApplicationWorkflow/: Domain/ApplicationRecord.cs; Application/WorkflowContracts.cs; Application/DocumentReadinessRequests.cs mới thay FinalDecisionRequests.cs; API/Controllers/ApplicationReviewController.cs; API/DocumentContentsClient.cs; Infrastructure/WorkflowDbContext.cs; Infrastructure/Migrations/20261016154813_DocumentReadiness.cs và .Designer.cs; WorkflowDbContextModelSnapshot.cs (các đường dẫn lớp bắt đầu bằng WardMate.Services.ApplicationWorkflow.).
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/: 20261016154952_WorkflowReadinessPermission.cs và .Designer.cs; snapshot EF được scaffold lại, không thay đổi mô hình IAM.
- src/Services/WardMate.Services.DocumentForm/: Domain/Entities/UserSubmission.cs, ApprovedQr.cs; Domain/Models/SubmissionStatus.cs; Domain/Errors/DocumentFormErrors.cs; Application/Commands/OfficerReviewCommands.cs, GeneratePdfDocumentCommand.cs; Application/Queries/GetGeneratedDocumentsQuery.cs, DownloadGeneratedDocumentQuery.cs; Application/DTOs/UserSubmissionDto.cs; API/Security/ApplicationAccess.cs; API/Controllers/OfficerSubmissionsController.cs, GeneratedDocumentsController.cs; Infrastructure/DependencyInjection.cs (các đường dẫn lớp bắt đầu bằng WardMate.Services.DocumentForm.).
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/: WorkflowNotificationConsumer.cs; NotificationInboxWorker.cs.
- docs/api-guide.md; docs/system-constraints.md; docs/system-diagrams.md; docs/system-future-flows.md; docs/system-unified-erd.md; docs/diagrams/activities/README.md; PROGRESS.md. Các sơ đồ cũ có chú thích bản mới thay thế trạng thái “backend chưa làm”; không tái render ảnh trong tác vụ code.
- Gợi ý Conventional Commit cho Antigravity: feat(workflow): replace final decisions with document readiness. Tách commit từng file mới và từng thay đổi chức năng theo AGENTS.md; không thực hiện Git trong phiên này.


## TASK-25/27 — Rà soát và gia cố thông báo realtime (17/10/2026)

- Phạm vi: IN_APP/SignalR và Workflow → Outbox → RabbitMQ → Inbox → delivery. SMS/Email tiếp tục hoãn, không bật provider, không thay đổi billing.
- Đã kiểm tra source: outbox cùng SaveChanges với lịch sử/comment; publish persistent + publisher confirms; inbox lưu EventId trước ACK; unique event/người nhận/channel; khóa SKIP LOCKED; retry và dead-letter; IAM kiểm tra người nhận/phường; revalidate connection 5–300 giây và ngắt khi quyền thay đổi. Đây là kiểm tra mã nguồn, không phải kết quả E2E Azure.
- Sửa Inbox: savepoint bao trọn tạo notification/delivery và SaveChanges; rollback trước khi lưu retry để không giữ dữ liệu một phần. Lỗi xử lý/lưu đủ 10 lần chuyển Dead, log EventId/attempt/state. DB không ghi được thì transaction rollback, chưa thể tăng retry.
- Sửa realtime: xác minh từng connection với IAM, đối chiếu phường đối với notification gắn phường, gửi theo connectionId đã xác minh. Áp dụng cả ReceiveNotification/UpdateUnreadCount khi gửi và UpdateUnreadCount khi đánh dấu đã đọc. Delivery log DeliveryId/attempt/state; không log token/payload.
- Endpoint/method: không thêm/sửa route/body/status HTTP; Hub /hubs/notifications, methods GetNotifications(page,pageSize), GetUnreadCount(), MarkRead(notificationId), callback ReceiveNotification/UpdateUnreadCount. Lỗi invocation là SignalR HubException; negotiate JWT không hợp lệ trả401. JWT token refresh/reconnect do FE; sau reconnect tải danh sách/bộ đếm và deduplicate theo notification id.
- Giới hạn vận hành: một replica AnalyticsSystem/một revision phục vụ do chưa có backplane. Sent không chứng minh client online/đã nhận. Chưa có API replay; tài liệu hướng dẫn người vận hành đối soát Dead và reset đúng bản ghi sau khi sửa nguyên nhân. Không tuyên bố exactly-once realtime hoặc thu hồi quyền tức thì.
- File sửa: src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationInboxWorker.cs; NotificationDeliveryWorker.cs; NotificationConnectionGuard.cs; NotificationService.cs; docs/api-guide.md; docs/system-constraints.md; PROGRESS.md. Không đổi entity/schema, không thêm migration.
- EF has-pending-model-changes (startup Infrastructure, Release --no-build): không thiếu migration. Lần dùng startup API trước đó thiếu cấu hình DB nên EF fallback factory; lần kiểm tra Infrastructure thành công không cần secret.
- Kiểm thử tự động: 0 chạy/0 tạo test project theo AGENTS.md. Chưa chạy RabbitMQ/DB/SignalR integration, chưa kiểm thử các tình huống outage/duplicate trên runtime. Không chạy Docker/Git hoặc deploy Azure.
- Cần kiểm chứng triển khai: đúng DB migrations cũ, RabbitMq__Enabled/Notifications__WorkersEnabled, IAM service key/CORS/JWT; ca submit/assign/comment/revision/readiness, khác phường, khóa tài khoản, reconnect, RabbitMQ/IAM gián đoạn và replay EventId. Có SQL chỉ đọc và checklist trong docs/api-guide.md.
- Gợi ý commit: fix(notifications): harden inbox retries and realtime authorization. Antigravity tách các thay đổi chức năng theo AGENTS.md.

- Hoàn tất kiểm tra local: 2026-10-17 10:41:15 +07:00 (Asia/Saigon). Solution Release build: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 thành công, 0 Error(s), 0 Warning(s), thời gian 00:06:29.39. TASK-25/27 đã gia cố local; kiểm chứng tích hợp runtime/Azure còn chờ thực hiện theo checklist, không đánh dấu E2E hoàn tất.


## P0-1.1–1.4 — Workflow read scope và đổi mật khẩu (21/10/2026)

- Phạm vi đúng Chặng1; không thực hiện P1/P2, không chạy Git/Docker/deploy hoặc cập nhật database Azure. Đọc audit FE, đối chiếu source; giữ danh sách ListAll/query/handler hiện có sau khi rà soát lại bản file, loại khai báo bổ sung trùng trong lần build đầu.
- Task1.1: GET /api/v1/officer/applications/{id} cho FD/Manager có workflow.read xem cùng phường, trừ hồ sơ tự nộp; không yêu cầu assignment. 200 ApplicationDto, thiếu role/phường403, ngoài phạm vi/tự nộp404. Store.Read mở phạm vi đọc đồng bộ, giữ nhánh chủ hồ sơ cho API công dân.
- Task1.2: ApplicationSummaryDto thêm Guid? AssignedOfficerId=null; map ở Pending/ListAll và danh sách công dân. JSON assignedOfficerId nullable; metadata total giữ nguyên. Giữ bộ lọc/tìm kiếm/phân trang ListAll hiện có.
- Task1.3: ReviewActor tách CanReadWard và CanMutate; chỉ FD không có role Manager được thao tác, kể cả người có đồng thời hai role. Policy queue yêu cầu FD/Manager + permission; mutation policies bắt buộc FD và không Manager. Handler vẫn kiểm tra cùng phường/assignment/không tự xử lý. Pending của Manager đọc các hồ sơ pending cùng phường, không giới hạn assignedOfficerId. Migration IAM 20261021090000_ManagerWorkflowReadScope cấp workflow.queue.read/workflow.read cho Manager và gỡ bốn quyền thao tác; Down không tự phục hồi grant vì cần đối soát backup.
- Task1.4: POST /api/v1/users/me/change-password, Bearer; body currentPassword,newPassword,confirmNewPassword (không nhận userId), giới hạn4096byte; 200 {message}. Validator min8, hoa/thường/số/đặc biệt, <=72 byte UTF-8, xác nhận khớp. Sai mật khẩu hiện tại400 iam.current_password_invalid; trùng mật khẩu400 iam.password_unchanged; validation400 validation_failed; user/token không hợp lệ401; tranh chấp409 iam.concurrent_update.
- Đổi hash và thu hồi refresh token trong transaction khóa user; CAS expectedHash ngăn ghi đè đổi mật khẩu đồng thời. Token issuance đối chiếu hash sau khi lấy khóa để chặn phiên cấp bằng password cũ đang chạy cạnh tranh. Không thu hồi JWT access token tức thì; FE xóa token/đưa về đăng nhập, không quảng bá đăng xuất tức thì mọi thiết bị. Không đổi policy registration hiện có.
- Không có schema mới; migration chỉ cập nhật RBAC. EF pending-model-check IAM và Workflow đều pass. SQL idempotent migration Manager được sinh và đọc thành công; chưa chạy trên database. Sáu kiểm tra cấu trúc source/projection/route JSON đều đạt (không phải unit/integration tests).
- Test: dotnet test WardMate.sln -c Release --no-build --no-restore (và lần verbosity normal) exit1 ở VSTest target, không chạy test nào, 0 diagnostic errors/warnings. Solution không có test projects theo chính sách owner; không tạo lại project test và không tuyên bố 100% tests passed. Cần kiểm thử runtime bằng FD cùng/khác phường, Manager, tài khoản có hai role và các ca đổi mật khẩu/refresh/login.

### Danh sách file P0

- Workflow API: src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationReviewController.cs; WorkflowSecurity.cs.
- Workflow Application: src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewContracts.cs; ReviewHandlers.cs; DocumentReadinessRequests.cs; WorkflowContracts.cs.
- Workflow Infrastructure: src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/ApplicationReviewStore.cs; ApplicationStore.cs.
- IAM Application: src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Commands/ChangePasswordCommand.cs (mới, command/handler/validator); Interfaces/IIdentityStore.cs; Common/Result.cs.
- IAM Infrastructure: src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/IdentityStore.cs; Persistence/Migrations/20261021090000_ManagerWorkflowReadScope.cs (mới, data migration).
- IAM API: src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/UsersController.cs; Errors/ResultExtensions.cs.
- Gateway: src/Gateways/WardMate.YarpGateway/appsettings.json (giữ root route officer/applications hiện có); docs/api-guide.md; docs/system-constraints.md; PROGRESS.md.
- OfficerListQuery.cs được tạo tạm rồi bỏ trong phiên vì trùng triển khai sẵn có; không phải file bàn giao mới.
- Gợi ý commit chức năng cho Antigravity: fix(workflow): allow ward-scoped read-only manager access; feat(iam): add authenticated password change; fix(iam): grant manager workflow read permissions. Tách từng file mới riêng theo AGENTS.md. Chờ chỉ thị P1.

- Hoàn tất local: 2026-10-21 09:11:55 +07:00 (Asia/Saigon). Build cuối: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 PASS, 0 lỗi/0 cảnh báo, 00:01:55.03. Lần build trước có 5 lỗi khai báo danh sách trùng, đã sửa trước lần build đạt này. Chưa xác nhận integration/runtime hoặc triển khai Azure.
<<<<<<< Updated upstream
## TASK-31..36 - Source implementation assessment (2026-10-21T09:58:56+07:00)
- Scope: inspected current local source, EF mappings/migrations, workers and documentation; no Git commands or Azure changes.
- TASK-31/32/33: no metrics aggregation/Hangfire, manager analytics endpoints or citizen feedback implementation found. Planned tables remain SYS_FUTURE in docs/system-unified-erd.md.
- TASK-34: partial foundation only: IAM rbac_audit_logs and shared HTTP/IP logging exist; no global data-change audit_logs or system_settings implementation found.
- TASK-35: basic /health endpoints exist; no registered DB/RabbitMQ health probes, persisted health history or automated database backup/restore workflow found.
- TASK-36: existing B-Tree/GIN and business indexes plus Swagger/API documentation; no HNSW tuning, benchmark evidence or Postman export found. Historical 35-table scope must be reconciled with current migrations.
- Changed paths: PROGRESS.md only. No endpoint/request/status-code, DTO, ProblemDetails, token, authorization or configuration behavior changed.
- Verification: static source/document inspection only; 0 tests executed; build not run because no executable code changed. No claim about deployed Azure/database state.
- Suggested Conventional Commit for Antigravity: docs(progress): assess TASK-31 through TASK-36 implementation status
## TASK-31/32/33 — Analytics aggregation, dashboard and citizen feedback
Completed local implementation/verification: 2026-10-21T10:25:12+07:00 (Asia/Saigon).

Plan executed: inspect Workflow/IAM contracts -> implement daily aggregate HTTP source -> persist metrics and Hangfire job -> ward-scoped dashboard -> owner-checked feedback -> migration/config/docs -> local verification. No Git commands or Azure changes.

Status:
- TASK-31: implemented two metric tables plus refresh watermark, Hangfire PostgreSQL storage/cron/retry, transactional day replacement and advisory lock, IT_ADMIN rebuild/status APIs. Default jobs disabled pending configuration.
- TASK-32: overview/trends/procedure/officer APIs implemented; MANAGER live IAM ward scope. Timeliness remains explicitly unavailable (null and UNAVAILABLE_NO_WORKFLOW_DEADLINE): Workflow has no normalized deadline/working-calendar contract. Do not mark SLA computation complete or infer from procedure text. Metrics are daily history events, not current queue-size snapshots.
- TASK-33: POST/GET owner feedback, 1..5 stars, optional <=2000-char comment, DOCUMENTS_READY eligibility, DB unique ApplicationId, manager ward-scoped summary/list. No feedback update/delete. Status validation is an HTTP check, not a distributed transaction.

New source paths (prefix src/Services/):
- WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/AnalyticsContracts.cs
- WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowAnalyticsSource.cs
- WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/AnalyticsSourceController.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Domain/AnalyticsModels.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Application/AnalyticsContracts.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsDbContext.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsWorkflowClient.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsService.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsAggregationJob.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsRegistration.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261021031538_AnalyticsMetricsAndCitizenFeedback.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261021031538_AnalyticsMetricsAndCitizenFeedback.Designer.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/AnalyticsDbContextModelSnapshot.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/AnalyticsAccess.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Controllers/AnalyticsController.cs
- WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Controllers/CitizenFeedbackController.cs
- scripts/verify-analytics-local.py (standalone disposable integration smoke; no test project created).
Modified existing paths:
- ApplicationWorkflow.Infrastructure/ProcedureCatalogClient.cs: source registration.
- AnalyticsSystem.Infrastructure/*.csproj and AnalyticsSystem.API/*.csproj: Hangfire/PostgreSQL/Swagger packages.
- AnalyticsSystem.API/Program.cs and appsettings.json: DI, Swagger, migration, recurring job/config.
- src/Gateways/WardMate.YarpGateway/appsettings.json: analytics route; specific citizen feedback route precedes DocumentForm catch-all.
- docker/docker-compose.yml, docker/.env.example: optional analytics profile and non-secret settings.
- docs/api-guide.md, docs/system-constraints.md, docs/system-unified-erd.md, PROGRESS.md.

API contract summary:
- Workflow GET /internal/analytics/v1/daily?date=yyyy-MM-dd: dedicated X-Analytics-Key >=32 bytes, 200 versioned aggregate; 400/401/503. Excluded from Swagger.
- MANAGER GET /api/v1/analytics/{overview,trends,procedures,officers}: from/to <=366 days, optional procedureId and bounded page/pageSize; 200 DTO + coverage; 400/401/403/503.
- MANAGER GET /api/v1/analytics/feedback and /feedback/summary: same date/procedure scope, 200 page or weighted rating/distribution/satisfaction summary; 400/401/403/503.
- REGISTERED_CITIZEN POST /api/v1/citizen/feedback/{applicationId}: {rating,comment}, 201; 400/401/403/404/409/413/503. GET same route: 200 own feedback or 404, IAM auth applies.
- IT_ADMIN POST /api/v1/analytics/jobs/rebuild: {from,to}, 202 {jobId}; GET /api/v1/analytics/jobs/{jobId}: 200 {jobId,state,createdAtUtc}; validation/auth/not-found/disabled errors 400/401/403/404/503.
- FE: Bearer JWT; IAM roles/ward checked per request; no citizenId/applicantId accepted in body; comments are plain text. Display missingDates/stale source timestamps, never turn unavailable SLA or empty ratings into zero. Problems include code/traceId for business errors; binding errors use ValidationProblemDetails and JWT challenge may be bodyless.

Validation:
- Solution-wide Release build: succeeded, 0 errors, 0 warnings (logs/task31-33-build.log).
- 44/44 integration smoke checks passed (logs/task31-33-smoke.log): real PostgreSQL16 temporary container; real Workflow/Analytics/Hangfire/Gateway; simulated IAM access-context and synthetic JWT/users. Verified date boundary, metrics replay, ownership, ward scope, role revocation, rating validation, concurrent duplicate, coverage, weighted score, gateway routing, source outage retry and retention of old metrics.
- First smoke run found record validation metadata error; corrected before final successful run.
- EF has-pending-model-changes: none. Design-time factory used; host startup notes missing NotificationsDatabase in shell, does not affect factory result. Actual migration applied successfully on disposable DB in smoke.
- Docker Compose analytics profile config --quiet: passed. Existing containers/data not changed; temporary processes/container removed by script.
- No new test projects, no Azure deploy, no real IAM integration verification. Production remains dependent on shared secret configuration, migrations, running Hangfire server and normalized Workflow deadline for SLA.

Antigravity handoff: suggested commit themes feat(analytics): add daily metrics aggregation; feat(analytics): add ward-scoped dashboard; feat(feedback): add citizen satisfaction feedback; docs(analytics): document metrics and feedback contracts. Follow repository granularity: each new file own commit, separate functional updates for existing files; no commits executed here.
## TASK-31–33 handoff/deployment preflight — 2026-10-21 (Asia/Saigon)
- User explicitly requested ordered commits and Azure deployment; this turn authorizes local Git commit on current kha. Push/release gate still requires exact commit report and Docker user acceptance.
- Added docs/workflow-deadline-contract.md: proposed source fields, calendar/pause policy, versioned aggregate contract, null/unknown handling, readiness-vs-administrative-SLA distinction and acceptance cases. Proposal only, no deadline implementation/API changes.
- Release solution build reconfirmed 0 warnings/0 errors (logs/task31-33-handoff-build.log); prior 44-check smoke applies to unchanged source.
- Azure read-only preflight confirmed existing wardmate-application-workflow and wardmate-analytics-system, both Succeeded; analytics currently harden-20261017-01, workflow workflow-20261020-02. New Analytics ServiceKey/Jobs/Workflow URL settings absent; no Azure mutations performed.
- Prepared local Docker builds for Analytics, Workflow and Gateway. Push/release awaits repository gate; CI workflow currently builds only, no Azure deployment steps. Do not equate push with deploy.
- Handoff result: 30 ordered local commits on kha, working tree clean before this log amendment. Types: 17 feat, 2 build, 4 chore, 6 docs, 1 test. Full commit manifest: logs/task31-33-commits.txt (ignored). Docker images wardmate-analytics/workflow/gateway:task31-33-local all built successfully. No push/deploy; pending explicit push approval and Docker user acceptance under AGENTS.md.

## TASK-31–33 push attempt — 2026-10-21 (Asia/Saigon)
- User approved pushing the reported 30 commits to kha. git push origin kha was rejected (fetch first); no remote mutation succeeded.
- Read-only fetch found 5 newer remote commits, origin/kha=1bcfc59 (IAM password change, manager Workflow read scope, documentation). Local HEAD remains 7cbaec6 with 30 unique local commits.
- Proposed rebase was rejected before execution by automatic approval review: explicit authorization to rewrite the 30 local commits is required; risk of conflicts with team changes. No rebase/merge/force push executed.
- Pending user approval for synchronization, then revalidation before push. Azure not deployed; previous build/smoke results predate remote integration. PROGRESS.md updated only; no API/DTO/token behavior changed this session.

## TASK-31–33 authorized synchronization — 2026-10-21T10:45:07+07:00
- User explicitly approved rebasing 30 commits onto latest kha, resolving conflicts, revalidating and pushing. Rebased onto origin/kha 1bcfc59; kept all 30 commits. Only conflict was appended PROGRESS sections; both retained. Session stash restored.
- Post-rebase solution Release build PASS: 0 warnings/0 errors (logs/task31-33-rebase-build.log). Integration smoke PASS 44/44 (logs/task31-33-rebase-smoke.log), simulated IAM and actual Workflow/Analytics/PostgreSQL/Hangfire/Gateway; temporary resources cleaned.
- No API/source edits for conflict resolution; existing Docker images predate integration and must be rebuilt before release. Preparing authorized normal push to kha only; no force-push/main/deploy/Azure action.

## 2026-10-21 — Chặng 2 P1: tiếp nhận tại quầy, Mobile và tiện ích tài liệu

Hoàn tất code và kiểm tra build local lúc 2026-10-21 14:38 +07:00 (Asia/Saigon). Không chạy Git, Docker, deploy hoặc cập nhật database. Không tạo test project.

### Hợp đồng FE

- Task2.1 POST /api/v1/officer/applications/{id}/officially-received: body paperDossierVerified=true, notes nullable<=4000, appointmentDate nullable (ISO offset, không quá khứ). Bearer + workflow.receipt.confirm, FD đang thụ lý/cùng phường/không tự xử lý; Manager bị chặn. Chỉ DOCUMENTS_READY → OFFICIALLY_RECEIVED. 200 {applicationId,status,receiptNumber,receivedAt,appointmentDate}; validation400, unauthorized401, forbidden403, missing404, invalid status409. Body16KiB. Receipt BN-yyyyMMdd-GuidN unique, UTC. Không phê duyệt kết quả hành chính.
- Task2.2 POST /api/v1/applications/{id}/cancel: không body, Bearer + workflow.cancel, chính chủ; DRAFT/NEED_REVISION → CANCELLED. 200 ApplicationDto, wrong owner404, state409. Hủy giữ dữ liệu/lịch sử, không sửa/nộp lại. Draft chưa nộp giữ currentVersionNumber=0.
- Task2.3 login/refresh thêm header X-Client-Type: Mobile, trả refreshToken JSON không cookie. Refresh/revoke body {refreshToken}; revoke vẫn Bearer,204. Web không header giữ cookie/CSRF, không lộ refresh JSON. Native không Origin/Sec-Fetch-Site miễn CSRF; browser vẫn qua kiểm tra. Login/refresh4096byte. Rotation hiện có; token sai401, CSRF403; không log token. Không thay đổi RBAC theo header.
- Task2.4 GET /api/v1/supporting-documents/{documentId}/preview: quyền download giữ nguyên, stream inline PDF/PNG/JPEG200, định dạng khác415 hãy download; nosniff/no-store. Lỗi truy cập403/404, storage503. Không tự chuyển DOCX thành PDF.
- Task2.5 GET /api/v1/procedures/featured: public200 mảng tối đa5 summary active mới tạo nhất, không phân trang/không thống kê view, không load content_payload.
- Task2.6 POST /api/v1/citizen/submissions/upload-filled-docx: alias draft, multipart TemplateId,ApplicantId,ApplicationId,ChecklistId,File; trả201 có submissionId. Giữ kiểm tra quyền/owner/trạng thái/template/file/duplicate của draft, các lỗi400/403/404/409. Hai ID hồ sơ/checklist vẫn bắt buộc, không bỏ liên kết vì đề xuất FE chỉ liệt kê3 trường.
- Chi tiết ApplicationDto có receiptNumber/receivedAt/appointmentDate nullable. Lỗi Workflow vẫn ProblemDetails và code theo Respond/validator; preview415 dùng ProblemDetails. FE tải lại khi409 và refresh/login sau cập nhật quyền.

### File thay đổi / mới

Các đường dẫn nhóm dưới đây tính từ src/Services tương ứng:

- ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Domain/ApplicationRecord.cs.
- ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/WorkflowContracts.cs; CounterReceiptRequests.cs (mới).
- ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/ApplicationReviewController.cs; Controllers/ApplicationsController.cs; WorkflowSecurity.cs.
- ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/WorkflowDbContext.cs; WorkflowOutbox.cs; Migrations/20261021073245_CounterReceipt.cs (mới); Migrations/20261021073245_CounterReceipt.Designer.cs (mới); Migrations/WorkflowDbContextModelSnapshot.cs.
- IAM/WardMate.Services.IAM.API/Controllers/AuthController.cs; Security/RefreshTokenCookie.cs; Security/BrowserAuthProtectionAttribute.cs.
- IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261021100000_CounterReceiptPermissions.cs (mới).
- DocumentForm/WardMate.Services.DocumentForm.API/Controllers/CitizenSubmissionsController.cs; Controllers/SupportingDocumentsController.cs; Security/ApplicationAccess.cs.
- ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs; Queries/GetFeaturedProceduresQuery.cs (mới).
- ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs; ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs.
- AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/WorkflowNotificationConsumer.cs; NotificationInboxWorker.cs: nhận trạng thái tiếp nhận/hủy và tiêu đề thông báo. Workflow Outbox dùng GUID khi draft hủy chưa có mã; không thay mã DB.
- src/BuildingBlocks/WardMate.SharedKernel/Web/FeaturePermissions.cs.
- docs/api-guide.md; docs/system-constraints.md; docs/iam-httponly.md; PROGRESS.md.

### Xác minh và bàn giao

- dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1: PASS, 0 warnings, 0 errors, 00:00:56.96.
- EF has-pending-model-changes IAM/Workflow: PASS cả2, model khớp snapshot. Sinh và đọc SQL idempotent cho cả2 migration thành công, chưa apply database.
- Workflow migration thêm3 cột nullable, unique receipt_number, cập nhật check constraint; Down chặn nếu đã có dữ liệu biên nhận. IAM migration cấp receipt cho FD và cancel cho role đang có workflow.submit; không tự restore quyền khi rollback.
- Số automated tests chạy:0 (owner đã gỡ test projects). Chưa xác nhận runtime HTTP/transaction/RabbitMQ/Blob/Mobile trên Docker hoặc Azure. Không tuyên bố 100% tests passed.
- Khi bàn giao: apply migration IAM và Workflow, cập nhật IAM/Workflow/DocumentForm/ProcedureCatalog/AnalyticsSystem đồng bộ; không yêu cầu đổi Gemini/billing. Kiểm thử FD đúng/sai phường, Manager403 khi tiếp nhận, lặp nhận409, cancel chính chủ/sai trạng thái, mobile rotation và Web cookie, preview415, alias DOCX đúng/sai checklist, featured inactive bị loại.
- Gợi ý Conventional Commits theo chức năng: feat(workflow): add counter receipt and citizen cancellation; feat(iam): support native refresh token transport; feat(document-form): add inline preview and filled docx upload alias; feat(procedure): expose featured active procedures; fix(notifications): handle counter receipt and cancellation events; docs: document phase two API contracts. Antigravity phải tách mỗi file mới thành commit riêng và mỗi cập nhật chức năng riêng theo AGENTS.md; đây không phải lệnh Git hay số commit đã tạo.

## 2026-10-21 — Chặng 3 P2: CCCD OCR, chat và tiền kiểm tài liệu

Hoàn tất triển khai local lúc 2026-10-21 15:15 +07:00 (Asia/Saigon). Không chạy Git, không tạo test project, không chạy Docker/deploy/apply database, không gọi Gemini thật hoặc thay billing/model. SMS/Email ngoài phạm vi.

### Kết quả API

- Task3.1 POST /api/v1/ai-ocr/cccd/extract: Bearer, multipart frontImage bắt buộc/backImage optional. ICccdExtractionService nhận stream, Gemini multimodal inlineData + JSON Schema. 200 {citizenId,fullName,dateOfBirth,gender,nationality,placeOfOrigin,placeOfResidence,dateOfExpiry,confidenceScore}; field không đọc được nullable; không lưu DB. JPEG/PNG,8MiB/file,17MiB/request. 400/413/415 input;401 token;502 output;503 disabled/config/provider;504 timeout. Confidence tự đánh giá, không eKYC.
- Task3.2 POST /api/v1/ai/chat/completions body {message,conversationId:null|guid,wardCode};200 {conversationId,answer,relatedProcedures:[]}. GET /api/v1/ai/chat/sessions và GET /api/v1/ai/chat/sessions/{sessionId}/messages có pageNumber/pageSize, trả {items,pageNumber,pageSize,totalCount}. Bearer, chỉ chủ phiên;404 nếu khác chủ,409 ward mismatch/phiên bận. Chat thuần LLM, chưa RAG/pgvector/nguồn trích dẫn. Transaction lưu cặp User/Assistant, lỗi AI rollback lượt; context10tin gần nhất; không có idempotency/retention/deleteAPI.
- Task3.3 POST /api/v1/ai-ocr/documents/verify body {applicationId,documentId,expectedType}; Bearer được chuyển DocumentForm để đọc danh sách hồ sơ, kiểm tra document/application khớp rồi download; không bypass token.200 {isValid,documentTypeDetected,qualityIssues,confidence}. PDF/PNG/JPEG<=8MiB; không DOCX. Không ghi checklist hoặc phê duyệt hồ sơ. expectedType do user nhập chưa match checklist server; AI không xác minh pháp lý/thật giả. Sai quyền401/403/404, input400/413/415, phụ thuộc503,output502,timeout504.
- Tất cả5endpoints mới có limiter10request/user/phút/replica, không phải quota phân tán. JWT HS256 issuer/audience/sub/expiry; không thêm role mới;401/429 có thể không body, validation có errors, lỗi AI/DB ProblemDetails có code/traceId. CORS theo sharedpolicy. FE không render HTML trả về từ model.
- Endpoint đọc thủ tục nội bộ giữ X-Service-Key, không chuyển sang Bearer. Gateway thêm2route giữ nguyên path /api/v1/ai-ocr và /api/v1/ai/chat; giữ prefix service cũ.

### Migration/cấu hình

- 20261021080951_ChatSessions: chat_sessions (id,user_id,ward_code,created_at); chat_messages (id,session_id,role,content,created_at), FK nội bộ cascade, role check User/Assistant, chỉ mục user+created/session+created. DB riêng AIOCR, không FK chéo IAM. Down xóa lịch sử nên phải đối soát/backup trước rollback.
- Cần ConnectionStrings__AiOcrDatabase PostgreSQL riêng + migration; Assistant__Enabled=true + Gemini__Key/Model; Jwt__Key/Issuer/Audience khớp IAM; Services__DocumentForm__BaseUrl. Database__AutoMigrate mặc địnhfalse; chỉ bật khi đã cấu hình đúng DB.
- Compose profile ai bổ sung AIOCR_ASSISTANT_ENABLED, AIOCR_DATABASE_CONNECTION_STRING, AIOCR_AUTO_MIGRATE và JWT/DocumentForm. Không thêm DB container hoặc sửa .env chứa secret. Cần chuẩn bị DB riêng trước dùng chat. Health chỉ liveness.

### Danh sách file mới

Prefix src/Services/WardMate.Services.AIOCR/:
- WardMate.Services.AIOCR.Domain/ChatSession.cs.
- WardMate.Services.AIOCR.Application/AssistantContracts.cs.
- WardMate.Services.AIOCR.Infrastructure/ChatDbContext.cs; ChatService.cs; GeminiAssistant.cs; AuthorizedDocumentSource.cs.
- WardMate.Services.AIOCR.Infrastructure/Migrations/20261021080951_ChatSessions.cs; 20261021080951_ChatSessions.Designer.cs; ChatDbContextModelSnapshot.cs.
- WardMate.Services.AIOCR.API/AssistantRegistration.cs; Controllers/AssistantController.cs.

### File hiện có đã sửa

- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/WardMate.Services.AIOCR.Infrastructure.csproj.
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/WardMate.Services.AIOCR.API.csproj; Program.cs; appsettings.json.
- src/Gateways/WardMate.YarpGateway/appsettings.json; docker/docker-compose.yml.
- docs/api-guide.md; docs/system-constraints.md; PROGRESS.md.

### Kiểm tra

- dotnet restore WardMate.sln: PASS sau khi cho phép truy cập NuGet; lần đầu sandbox bị NU1900 không tải được vulnerability metadata, không tắt audit để che lỗi.
- dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1: PASS0warnings0errors, 00:00:56.67 (sau migration và kiểm tra schema JSON). Build trước đó cũng0warnings0errors.
- EF has-pending-model-changes: PASS. Sinh SQL idempotent và đọc script thành công; chưa thực thi DB.
- Smoke local bằng JWT tạm, AI disabled, không dữ liệu thật: Swagger đủ5route; không token401; chat input rỗng400; database chưa cấu hình503 chat.not_configured; OCR chưa bật503 ai.disabled. Tổng5kiểm tra được xác nhận. Một assertion PowerShell ban đầu không decode byte[] response nên báo thất bại dù HTTP503 đúng; đọc lại response xác nhận code đúng. Sandbox ban đầu chặn loopback/DataProtection path, thực hiện lại local ngoài sandbox; không đổi code để bypass bảo vệ hệ thống.
- Automated test project/count:0, không tạo mới theo chỉ thị. Chưa kiểm thử provider thật, transaction với PostgreSQL, ownership chéo user trên DB, quyền download liên service, hoặc Azure. Không tuyên bố production-ready/toàn bộ integration đã pass.
- Cần test bàn giao: migrate DB riêng; cấu hìnhsecret/model đã chọn; CCCD mẫu/mờ/null; chat2user không đọc chéo, gửi đồng thời409; verify đúng/sai applicationId, FD được giao/khác người, fileDOCX415; lỗi quota/timeout AI. Không dùng tài liệu cá nhân thật khi chưa có đồng ý xử lý bởi nhà cung cấp.
- Hợp đồng Gemini đối chiếu tài liệu chính thức https://ai.google.dev/api/generate-content (inlineData, responseJsonSchema). Adapter không tự đổi model, không auto retry/quota-upgrade.
- Gợi ý commit chức năng: feat(aiocr): add citizen card extraction; feat(aiocr): persist authenticated chat sessions; feat(aiocr): add authorized document prevalidation; chore(aiocr): configure public endpoints and chat database; docs: document phase three AI contracts. Antigravity tách mỗi file mới thành commit riêng và từng cập nhật chức năng riêng theo AGENTS.md; chưa có Git/commit/push từ Codex.

## 2026-10-21 — WF-DEADLINE-001: hạn tiền kiểm giấy tờ và Analytics

### Hiện trạng và quyết định

- Trước sửa: có DocumentsReadyAt, SubmittedAt bị ghi đè khi nộp lại; chưa có due/first-submit/policy/calendar/pause, overview trảnull tỷ lệ với UNAVAILABLE_NO_WORKFLOW_DEADLINE.
- Chủ dự án chốt trong lượt này:1tuần=5ngày làm việc45giờ, thứHai–thứSáu07:30–16:30 giờVN, pause khi chờ bổ sung. Không phải hạn giải quyết hành chính, không dùng AppointmentDate. Holidays hiện[] chưa loại ngày lễ; cần chốt lịch trước khi sử dụng như SLA chính thức.

### Đã triển khai

- FirstSubmittedAtUtc bất biến từ submit đầu; dueAtUtc, deadlineStatus KNOWN/UNKNOWN/NOT_APPLICABLE; mã/phiênbản policy/calendar, snapshot đầy đủ JSONB và lịch sử PAUSE/RESUME/CANCEL. AlltimestampsUTC. documentsReadyAtUtc là aliasDocumentsReadyAt cũ, giữ tương thích.
- Submit đầu tính45giờ bằng lịch đã chụp; nộp ngoài giờ/cuối tuần chuyển thời gian tính sang cửa sổ làm việc. Request-revision pause, resubmit cộng workingticks tạm dừng vào due cũ, không reset45giờ. Giữ phần quá hạn trướcpause. Hủy đóngpause mà không tạo hoàn thành.
- Detail/list cho công dân/cán bộ có deadlinefields; quyền ward/owner/Managerread-only không đổi. Không có API sửa hạn tùy ý từFE. Metadata+deadline+lịch sử trạng thái lưu cùng transaction nghiệp vụ hiện có.
- Analytics GET /api/v1/analytics/overview bổ sung deadlines gồm completedOnTime,completedLate,completedMissingDeadline,completedNotApplicable,completedSampleCount,openOverdue,openMissingDeadline,openNotApplicable,openPaused,asOfUtc. OnTime<=due;Late>due;rate0..100 chỉ chia cho known completed;sample0=>null. UNKNOWN vàNA không tínhmẫu.
- Completed cohort theo ngàyready trongFrom/To; openbacklog snapshot hiện tại cùngward/procedure, không gộp vào completedLate, không lọc ngày nộp và không phải lịch sử tạiTo. Pause trướcdue không tính thêmchậm; pause sau due vẫnopenOverdue. CANCELLED/DRAFT/legacyApproved/Rejected không giảhoàn thànhready.
- Nguồn HTTP GET /internal/analytics/v1/deadlines?ward=...&from=...&to=...&procedureId=... dùng X-Analytics-Key>=32bytes, không DBcross-service; nguồn>100000 trả503 khôngtruncation. Analytics tự tổnghợp facts và kiểmtra version/UTC/status/knownconsistency; Managerward lấy accesscontext, không FEward.
- Overviewdeadline đọc Workflow live và cóasOfUtc riêng; totals/cache giữCoverage riêng. Trends/procedure/officer metrics chưa có deadline. KhiWorkflow chưa deploy/mấtkết nối/key sai,overview lỗi thayvìfake0. Cần Workflow:BaseUrl và Analytics:ServiceKey kể cả jobsdisabled.

### Migration/config

- Mới 20261021144055_ReviewDeadline:10cột metadata/deadline/audit, index ward+deadlineStatus+due, checkconstraint. Tất cả dữliệu cũ defaultUNKNOWN/dueNULL/firstNULL, khôngUPDATEbackfill. Downchặn nếu đã có chứngcứ mới.
- ReviewDeadline trong appsettings Workflow: Enabled=true,Applicable=true,Code DOCUMENT_PREPARATION,Version2026-10-21-v1,CalendarCode VN_MON_FRI_0730_1630,CalendarVersion2026-10-21-v1,WorkingMinutes2700,PauseOnRevisiontrue,StartMinute450,EndMinute990,WorkingDaysMon..Fri,Holidays[]. Configinvalidfailstartup;EnabledfalseUNKNOWN;ApplicablefalseNA. Tăngversion khi đổi rule/lịch, không sửa snapshot hồ sơcũ.
- Chỉ cần migration Workflow; Analytics không thêm bảng vì phần deadline aggregate trực tiếp từ Workflow, không sửa DBcache/dailycontractv1.

### File mới

- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Domain/ReviewDeadline.cs.
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/DeadlineFacts.cs.
- src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261021144055_ReviewDeadline.cs và 20261021144055_ReviewDeadline.Designer.cs.
- src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Application/DeadlineStatistics.cs.

### File sửa

- Workflow Domain: ApplicationRecord.cs.
- Workflow Application: WorkflowRequests.cs,ReviewHandlers.cs,CounterReceiptRequests.cs,WorkflowContracts.cs,AnalyticsContracts.cs.
- Workflow Infrastructure: WorkflowDbContext.cs,ApplicationStore.cs,ApplicationReviewStore.cs,WorkflowAnalyticsSource.cs,Migrations/WorkflowDbContextModelSnapshot.cs.
- Workflow API: Program.cs,appsettings.json,Controllers/AnalyticsSourceController.cs.
- AnalyticsSystem Application: AnalyticsContracts.cs; Infrastructure:AnalyticsWorkflowClient.cs,AnalyticsService.cs.
- docs/api-guide.md,docs/system-constraints.md,PROGRESS.md.

### Xác minh

- Release solution build: dotnet build WardMate.sln -c Release --no-restore -warnaserror -m:1 PASS0errors0warnings,00:00:58.01. Build trước migration cũngđạt.
- EF modelkhớp migration; SQLidempotent sinh/đọc thànhcông. HaiEFcommands chạy đồngthời banđầu tranh fileobj; chạy lại tuần tự modelcheckPASS. ChưaapplyDB.
-21assertions trực tiếp trên DLL qua PowerShell/reflection PASS:9calendar/firstsubmit/snapshot/audit/weekend/holiday/legacy/NA;8Analytics equality/late/missing/NA/denominator/openoverdue/unknown/paused;2overduepreservation/weekendpause;2invalidsourceguards. Khôngtạo testproject; khôngtuyênbố integrationtests đãđạt. Lần thửreflection trướcDomainbuild xong chưa thấytypenew; thực hiện lại saubuild vàPASS.
- Chưa kiểmthửDBtransaction/APIHTTP/Rabbit/Azure với migration mới. KhôngchạyGit,Docker,deploy hoặc dùngdữliệu thật. Bàn giao cần migrateWorkflow, deployWorkflow+Analytics, thửsubmit→revision→resubmit→ready, xácminhUNKNOWNold vàManagerphường.
- Gợi ý Conventional Commits: feat(workflow): track versioned document review deadlines; feat(analytics): report deadline compliance and open overdue dossiers; docs: document review deadline rules and reporting scope. Antigravity tách mỗi file mới và chức năng theoAGENTS.md; Codex chưa tạo commit.

- Hoàn tất local: 2026-10-21 21:44:59 +07:00 (Asia/Saigon).

