# Bàn giao WardMate cho Claude

## Cập nhật 24/10/2026 — TASK-28 Knowledge Base (KB-01)

AIOCR có Knowledge Base: bảng `knowledge_documents`/`knowledge_chunks` (migration `20261024034956_KnowledgeBase`, SQL `docs/sql/aiocr-knowledge-base.sql`), API `/api/v1/ai/knowledge` cho PROCEDURE_MANAGER/IT_ADMIN có `knowledge.manage` (IAM migration `20261024100000_KnowledgeManagePermission`, SQL `docs/sql/iam-knowledge-permission.sql`). Nguồn: đồng bộ thủ tục từ API công khai Catalog, văn bản/FAQ, PDF có lớp chữ. Chia đoạn tất định trong `Application/KnowledgeChunker.cs` (phiên bản `v1-paragraph-1200-1500-200-900`).

Cần cấu hình AIOCR `Services__ProcedureCatalog__BaseUrl`. Chưa apply DB, chưa deploy.

## Cập nhật 24/10/2026 — TASK-29 tìm kiếm vector và RAG (KB-02)

Đã có: Gemini embedding (`Infrastructure/Knowledge/GeminiEmbeddingProvider.cs`), worker `KnowledgeIndexingWorker`, tìm kiếm cosine trong bộ nhớ `KnowledgeRetriever` (vector `real[]`, **không dùng pgvector**, giới hạn 20.000 đoạn), chat RAG trong `ChatService` có `citations`/`grounded`, API `index-status`, `documents/{id}/reindex`, `search`. Migration AIOCR `20261024035926_KnowledgeRetrieval`, SQL `docs/sql/aiocr-knowledge-retrieval.sql`. Mặc định `Knowledge:EmbeddingEnabled=false`, `Knowledge:RagEnabled=false`: chat như cũ, không phí embedding. Chưa gọi Gemini embedding thật (định dạng request theo tài liệu batchEmbedContents/embedContent v1beta, cần thử khi bật), chưa apply DB, chưa deploy.

**Yêu cầu chủ dự án 24/10/2026:** Antigravity commit cả TASK-28 và TASK-29 trong phiên tiếp theo theo kế hoạch commit ở mục TASK-29 cuối PROGRESS.md; báo số commit/loại/message cho chủ dự án và chờ xác nhận trước khi push `kha` (AGENTS.md). Claude không chạy Git.

Còn mở sau TASK-29: ai_request_logs/thống kê chi phí AI, lọc nguồn theo phường, reranker, chuyển pgvector khi vượt 20.000 đoạn. Rút nhận xét OPEN đã làm 29/10 (WORKFLOW-COMMENT-01).

## Cập nhật 24/10/2026 — Quản trị delivery lỗi và đối soát SMS (NOTIFY-ADMIN-01)

Codex bắt đầu rồi dừng giữa chừng (lỗi model); Claude hoàn tất. API IT_ADMIN `/api/v1/admin/notification-deliveries`: GET danh sách/chi tiết/audit, POST `{id}/replay`, POST `{id}/sms-reconciliation`; quyền `notifications.deliveries.read`/`.manage` seed cho IT_ADMIN. Migration Notifications `20261024020118_NotificationDeliveryAdministration` (cột revision/updated_at, bảng notification_delivery_audits; Down chặn khi có audit) và IAM `20261024090000_NotificationDeliveryAdministration`; SQL idempotent có preflight: `docs/sql/notification-delivery-administration.sql`, `docs/sql/iam-notification-delivery-permissions.sql`. Gateway có route riêng trước `/api/v1/admin/...` của IAM.

**Bắt buộc chạy SQL Notifications trước khi deploy AnalyticsSystem mới**: code mới ghi `revision`/`updated_at` mỗi lần lưu delivery, thiếu cột thì toàn bộ luồng thông báo lỗi. Chưa apply DB, chưa deploy, không gửi SMS. Chưa có quota chi phí SMS toàn hệ thống, gửi lại hàng loạt hay DLR webhook.

Đã làm 29/10/2026 (WORKFLOW-COMMENT-01): cán bộ đang phụ trách rút nhận xét OPEN có lý do qua POST `/api/v1/officer/applications/{id}/comments/{commentId}/withdraw`; cần chạy `docs/sql/workflow-comment-withdrawal.sql` rồi deploy Workflow.

## Cập nhật 24/10/2026 — IAM lĩnh vực cán bộ

Đã thêm ICategoryDirectory/HTTP Catalog validation, audit OFFICER_CATEGORIES_ASSIGNED và worker CategoryReconciliationWorker loại ID bị xóa có audit OFFICER_CATEGORIES_RECONCILED. Chạy cùng advisory lock RBAC, tối đa100 tài khoản/lượt mặc định300s. Không migration. Quyền/ward và ưu tiên mềm giữ nguyên. Xóa ởCatalog không bị chặn bởi IAM; đồng bộ eventual, lỗiCatalog giữ dữ liệu. Sau dọn mảngrỗng nghĩa mọi lĩnh vực. Không sửa snapshot hồ sơ, không ID reuse thủ công.

IAM cần ProcedureCatalog__BaseUrl đúng môi trường, CategoryReconciliation__Enabled/IntervalSeconds. Compose đã cấu hình; appsettings URLrỗng. Chưa deploy/chưa gọi Catalog thật hoặc chạycleanupDB trong phiên này. Xem IAM-CATEGORY-01/api-guide và PROGRESS. Các mục cũ ghi chưa validate categories/chưa audit/chưa dọn ID đã được thay trong phạm vi này. Không tự làm analytics-category hoặc Git.

### Cập nhật 24/10/2026 — summary resubmitCount

Ba projection danh sách đã có x.ResubmitCount từ đợt phân công. Nay chuẩn hóa ApplicationSummaryDto thành int ResubmitCount=0 (trước là int?=null), không đổi thứ tự tham số đang dùng hoặc các field mới khác. Không migration, không đổi resubmit/assign; FE đọc số thật thay suy luận AssignedOfficerId. Xem PROGRESS để biết build, không coi là đã deploy.

## Sửa sau review — 24/10/2026: lý do phân công và Email

Đã sửa lỗ hổng còn lại của ForApplicant: ẩn assignmentHistory chưa đủ vì history.reason từng sao chép lý do giao/trả. Nay DTO công dân redact reason của SUBMITTED→UNDER_REVIEW và UNDER_REVIEW→SUBMITTED, bao gồm dữ liệu cũ; các lý do yêu cầu bổ sung giữ nguyên. ASSIGN/RELEASE mới chỉ ghi lý do vào application_assignment_history, cán bộ/Manager đọc qua AssignmentHistory. Không cần migration hoặc cập nhật DB cũ. Không bỏ lớp lọc DTO dù handler mới đã ngừng sao chép lý do.

Giữ Email thông báo phân công theo yêu cầu chủ dự án. Báo cáo trước nói chỉ STATUS gửi Email là sai: inbox vẫn tạo EMAIL delivery; EmailSource trả context null cho event phân công và sender render fallback rồi gửi SMTP. api-guide và system-constraints đã được đính chính. Không gửi Email/SMS thật hoặc chạy Git; xem PROGRESS cho kết quả build phiên này.

## Cập nhật 24/10/2026 — Phân công lĩnh vực

Đã bổ sung BE phân công ưu tiên mềm: snapshot lĩnh vực, tên/thời điểm cán bộ, assignmentVersion và bảng application_assignment_history; filters categoryIds/scope/assignee/overdue/sort; release và Manager assignee API. IAM trả assignedCategories và tra cán bộ đích tối thiểu; quyền workflow.assign.manage chỉ seedManager. Không mở quyền review choManager.

Manager PUT assignee bắt buộc expectedAssignmentVersion; đây là bổ sung hợp đồng FE để tránh lệnh giao cũ biến thành chuyển sau race. Dữ liệu lịch sử category chưa biết giữNULL (không backfill Catalog hiện tại). Chi tiết mới nhất ở đầu api-guide.md và system-constraints.md WORKFLOW-ASSIGN-01.

Claude tiếp quản phần dang dở của Codex (24/10/2026): migration Workflow `20261023173500_CategoryAssignment` và IAM `20261024010000_ManagerAssignmentPermission` đã có, EF xác nhận model khớp; build Release toàn solution 0 lỗi/0 cảnh báo. Đã sửa thêm: route Gateway `/api/v1/manager/applications/{**catch-all}` → Workflow; overdue tính đúng khi pause; người nhận thông báo theo action (CLAIM → công dân, ASSIGN → công dân + cán bộ mới, TRANSFER → cán bộ mới/cũ, RELEASE → cán bộ cùng phường); ẩn assignmentHistory trong response API công dân. Chưa apply DB local/cloud, chưa Docker/Azure/FE.

TASK-27 có eventVersion2 APPLICATION_ASSIGNED, previousOfficerId/action; Analytics phải deploy trướcWorkflow, consumer vẫn nhậnVersion1. Gateway cần deploy cùng Workflow để có route Manager. SMS mẫu riêng OFFICER_ASSIGNED/OFFICER_UNASSIGNED; chưa bật/gửiSMS. Kiểm tra PROGRESS phiên này cho build/migration thực tế; chưa xác nhận cloud/FE. Các mục cũ nói Manager chỉread hoặc không córelease đã được thay thế trong phạm vi phân công này. Không tự làm backlog analytics-category/category-audit/auto-expiry hoặc Git.

Cập nhật: 23/10/2026, Asia/Saigon (UTC+07:00). Bản tổng hợp để tiếp tục backend, không phải bản xuất toàn bộ hội thoại hoặc xác nhận production mới.

## 1. Đọc thế nào và mức độ bằng chứng

- Quy tắc hiện tại: `../AGENTS.md`, `../CLAUDE.md`. Không Git, không tạo test project, không tự tiếp tục các quyền deploy trong lịch sử.
- Nhật ký đầy đủ: `../PROGRESS.md`. Các mục mới thay thế mô tả cũ khi nghiệp vụ đổi; nhật ký có nhiều tác giả và nhiều giai đoạn. Các số test/build hoặc rollout ở đó là kết quả lịch sử, không phải kết quả phiên bàn giao này.
- API thực tế: `api-guide.md`; ràng buộc: `system-constraints.md`; schema kiểm kê: `diagrams/erd/model-inventory.json`.
- Phiên này đọc nhật ký, danh mục migration/controller và một số điểm source quan trọng (SMS Analytics, chat, JWT AIOCR, deadline, feedback, liên kết checklist). Không audit lại từng endpoint, không gọi Azure/database/provider, không chạy build.
- Source là căn cứ triển khai; yêu cầu mới nhất của chủ dự án là căn cứ ý định. Nếu source và yêu cầu lệch nhau, nêu rõ thay vì âm thầm coi một bên đúng. Tài liệu cũ có approve/reject không còn mô tả luồng đích.
- Trạng thái dùng dưới đây: **Có trong source**, **Lịch sử xác nhận**, **Chưa xác nhận**, **Chưa triển khai/giới hạn**. Không gộp thành “100% hoàn tất”.

## 2. Kiến trúc và bản đồ code

Workspace: `E:\Code\CODE_FPT\9\SEP490\Code\wardmate-backend`.

| Thành phần | Vị trí | Trách nhiệm |
|---|---|---|
| IAM | src/Services/WardMate.Services.IAM | Tài khoản, JWT/refresh, RBAC, phường, OTP, xác minh số |
| Procedure Catalog | src/Services/WardMate.Services.ProcedureCatalog | Thủ tục, version, draft PDF/AI, checklist/schema |
| Application Workflow | src/Services/WardMate.Services.ApplicationWorkflow | Hồ sơ, snapshot checklist, thẩm định giấy tờ, deadline, outbox |
| Document Form | src/Services/WardMate.Services.DocumentForm | Template DOCX, đơn đã điền, minh chứng, PDF, QR |
| AIOCR | src/Services/WardMate.Services.AIOCR | Bóc tách thủ tục, CCCD, tiền kiểm giấy tờ, chat |
| Analytics System | src/Services/WardMate.Services.AnalyticsSystem | Thông báo, SignalR, inbox/delivery, dashboard, đánh giá |
| Shared Kernel | src/BuildingBlocks/WardMate.SharedKernel | Quyền, web middleware, session check, tiện ích dùng chung |
| Gateway | src/Gateways/WardMate.YarpGateway | YARP, định tuyến service |

Mỗi service giữ ranh giới database. Liên kết ID xuyên service thường là REF nghiệp vụ, không phải FK SQL. Gọi nội bộ qua HTTP/key chuyên dụng hoặc sự kiện, không đọc bảng service khác để bỏ qua quyền.

Bộ EF model kiểm kê ngày 24/10: 41 bảng ứng dụng, 410 cột, 22 FK thật, 46 nhóm REF; 7 DbContext/6 service. IAM 11, Catalog 4, Workflow 7 (thêm application_assignment_history), DocumentForm 7, AIOCR 4 (thêm knowledge_documents, knowledge_chunks), AnalyticsSystem 8 (Notification 4 gồm notification_delivery_audits + Analytics 4). Không đếm Hangfire, EF history, broker, Blob. Không tự coi mọi bảng đã có trên cloud chỉ vì có migration.

## 3. Các chức năng đã triển khai

### IAM — TASK-02/03 và các lượt mở rộng

- Đăng ký/đăng nhập, JWT, refresh rotation/revoke, profile, RBAC, quản lý tài khoản theo phường/lĩnh vực, directory cán bộ và người dùng.
- Web dùng refresh cookie HttpOnly + bảo vệ CSRF/origin. Mobile có X-Client-Type: Mobile và refresh JSON; header này không nâng quyền hay được dùng để bỏ bảo vệ browser một cách tùy ý.
- P0: MANAGER có quyền đọc giám sát Workflow; giữ quyền chuyển trạng thái riêng cho cán bộ.
- Đổi mật khẩu trực tiếp; welcome email HTML qua queue `iam.welcome_emails`; OTP password EMAIL/SMS trong `iam.user_otps`.
- OTP dùng HMAC-SHA256 có ngữ cảnh challenge, không hạ xuống SHA256 thuần; 6 số, 5 phút, tối đa 5 lần sai, cooldown 60 giây. Purpose CHANGE_PASSWORD/FORGOT_PASSWORD/VERIFY_PHONE phải tách đúng.
- eSMS OTP đã có adapter `Infrastructure/Security/IamSmsSender.cs`; không chứa credential trong bàn giao. Provider chấp nhận không đồng nghĩa người nhận đã đọc.
- Gia cố: xác minh quyền sở hữu số, 10 OTP/5 SMS rolling 24 giờ mỗi account, cleanup, SecurityStamp, revoke refresh và invalidate OTP khi đổi mật khẩu. SMS recovery chỉ dùng số verified khớp profile.
- Shared `AccountSessionMiddleware.cs` kiểm tra IAM access-context trên authenticated request của 5 service. 401 session_revoked cần đăng nhập lại; 503 session_unavailable không tự xóa phiên FE. Middleware có chẩn đoán an toàn/traceId; không log token.
- Giới hạn: không chống timing tuyệt đối, chưa có ngân sách SMS toàn hệ thống/chống nhiều account cùng số; không exactly-once gửi SMS. Access-context mỗi request tăng phụ thuộc/tải IAM, chưa có benchmark mới.

Điểm đọc: IAM API Controllers/AuthController.cs, UsersController.cs; Infrastructure/Security/PasswordOtpService.cs, PhoneVerificationService.cs, OtpLimits.cs, WelcomeEmailWorker.cs, AccountSecurityCleanupWorker.cs; SharedKernel/Web/AccountSessionMiddleware.cs.

### Procedure Catalog — TASK-07/08/09 và PDF/checklist

- Entity/JSONB, quản trị và danh sách public, tìm kiếm, category, version/source/rollback, draft upload–đọc thử–đối soát–publish.
- `POST /api/v1/procedure-manager/drafts/extract-preview` phục vụ xem kết quả bóc tách; tạo draft là luồng lưu bản nháp, có worker và trạng thái Queued. 202/payload rỗng lúc Queued không chứng minh AI đã hoàn tất.
- Bóc tách đã nâng cấp schema JSON, validate/retry có giới hạn và giữ dữ liệu một phần/cảnh báo; tài liệu dài/provider503 vẫn cần xử lý thiếu dữ liệu và đối soát.
- Checklist thủ tục cho phép quản lý yêu cầu, trường hợp, số lượng, bản sao/nộp-xuất trình và liên kết template; Workflow sao chép yêu cầu khi tạo hồ sơ.
- Featured lấy tối đa 5 active mới tạo, không phải thống kê view thật.
- TASK-09 đã đổi hướng sang PDF/draft theo nhật ký; không tự khẳng định CSV import truyền thống hoàn tất chỉ từ tên task ban đầu.

Điểm đọc: Catalog API Controllers/ProcedureDraftsController.cs, ProcedureManagerController.cs; Infrastructure/Drafts/DraftExtractionWorker.cs; AIOCR Infrastructure/Extraction/*.cs.

### Workflow — TASK-13..15, review/diff, P0/P1, deadline

- Hồ sơ công dân, checklist snapshot và liên kết giấy tờ/đơn đã điền; submit kiểm tra nguồn chứng cứ thay vì chỉ tin COMPLETED từ client.
- Queue theo WardCode, cán bộ nhận xử lý, comment in-line, yêu cầu bổ sung, resubmit và version/diff/status history.
- Read-only cùng phường cho cán bộ/Manager theo quyền, không tự review hồ sơ do chính mình nộp. AssignedOfficerId có trong summary; action ghi còn ràng buộc người thụ lý.
- Luồng đích: DRAFT -> SUBMITTED -> UNDER_REVIEW -> DOCUMENTS_READY -> OFFICIALLY_RECEIVED. Nhánh NEED_REVISION -> nộp lại. CANCELLED chỉ từ DRAFT/NEED_REVISION theo endpoint hiện tại.
- `officially-received`: giấy tờ gốc đã kiểm tra, ReceiptNumber/ReceivedAt/AppointmentDate, người đang thụ lý cùng phường. Đây là ghi nhận tại quầy, không duyệt hành chính.
- Legacy ApprovedAt/APPROVED/REJECTED và tên bảng/migration cũ có thể còn để tương thích; không xóa dữ liệu hay đổi lại luồng theo tên legacy.
- Deadline: FirstSubmittedAtUtc bất biến; due UTC; mã/version policy/calendar + snapshot; pause/resume history. 45 giờ làm việc, Mon–Fri 07:30–16:30 UTC+07, pause khi bổ sung, resubmit cộng thời gian pause chứ không reset 45 giờ. Holidays hiện rỗng, chưa có lịch ngày lễ được chốt. Dữ liệu cũ UNKNOWN, không suy đoán SLA từ mô tả thủ tục.

Điểm đọc: Workflow Domain/ApplicationRecord.cs, ReviewDeadline.cs; Application/WorkflowContracts.cs, DeadlineFacts.cs; API Controllers/ApplicationsController.cs, ApplicationReviewController.cs; Infrastructure/ApplicationReviewStore.cs, WorkflowAnalyticsSource.cs, WorkflowOutbox.cs.

### Document Form — TASK-10..12,16..23 và P1

- Template DOCX/version/schema/placeholder, điền form online và upload DOCX offline, liên kết ApplicationId/ChecklistId, supporting documents, Blob, download/inline preview.
- Upload-filled-docx là alias của luồng draft; ApplicationId/ChecklistId vẫn bắt buộc theo hợp đồng thật, không chỉ 3 field trong đề xuất FE ban đầu.
- PDF/QR đã có luồng và bảng `document.approved_qrs`, generated_documents. QR/PDF không được diễn giải thành quyết định hành chính chỉ vì tên “approved”.
- Quyền owner/ward/assignment và trạng thái phải giữ khi đọc/sửa/tải file. Metadata “file có” không thay thế kiểm tra quyền/tồn tại nguồn.
- Lịch sử có lượt sửa Gotenberg/Office editor/QR/PDF; không tuyên bố tất cả cloud runtime đã được kiểm tra lại trong phiên này.

Điểm đọc: API Controllers/CitizenSubmissionsController.cs, OfficerSubmissionsController.cs, SupportingDocumentsController.cs, GeneratedDocumentsController.cs, QrLookupController.cs; API/Security/ApplicationAccess.cs.

### AIOCR — procedure AI, P2 CCCD/chat/pre-validation

- CCCD multipart front/back, Gemini multimodal JSON Schema; field không đọc được nullable, confidence tự đánh giá, không eKYC/xác thực giấy tờ pháp lý.
- Chat sessions/messages có owner scope và persistence, 10 tin gần nhất trong context; gọi Gemini, relatedProcedures hiện mảng rỗng. Chưa retrieval/chunking/embedding/pgvector/citation thật.
- Verify giấy tờ lấy tài liệu đúng quyền từ DocumentForm và title checklist snapshot từ Workflow; expectedType client cũ bị bỏ qua. Không tự hoàn thành checklist hoặc cho phép submit dựa vào AI.
- AIOCR-JWT-01 mới nhất: default issuer/audience `wardmate` / `wardmate-client`; key tối thiểu 32 byte, fail startup nếu thiếu. Giá trị deploy phải khớp IAM thực tế, không chỉ mặc định code.

Điểm đọc: API/AssistantRegistration.cs, Controllers/AssistantController.cs; Infrastructure/GeminiAssistant.cs, ChatService.cs, AuthorizedDocumentSource.cs, Extraction/ProcedureAiContract.cs.

### AnalyticsSystem — TASK-25/26/27,31/32/33

- Notification entity, JWT SignalR `/hubs/notifications`, user/ward scope, list/unread/mark-read, kiểm tra tài khoản và kết nối.
- Workflow outbox -> RabbitMQ -> inbox -> notification/delivery: lưu outbox cùng transaction, publisher confirm, ACK sau inbox, dedup/event ID, worker retry/Dead. Không cam kết exactly-once.
- Sửa nhiều replica: `NotificationRealtimeListener.cs` LISTEN/NOTIFY PostgreSQL, gửi từng replica; mark-read phát signal. Signal không bền khi listener offline, FE reload list/unread và dedup ID khi reconnect. Sent không phải UI đã nhận/đã đọc.
- EMAIL thông báo hồ sơ có HTML + plain text, 4 trạng thái, nội dung lịch sử/receipt/CTA, timeout15s, retry qua worker. SMTP accepted không chứng minh inbox delivery.
- **SMS thông báo hồ sơ vẫn UnconfiguredSmsSender (Blocked)** trong `Infrastructure/ExternalNotificationSender.cs`. eSMS OTP IAM không tự hoàn thành TASK-26 phần SMS này.
- Daily metrics/Hangfire/rebuild/watermark, dashboard Manager cùng phường, feedback. Deadline overview/trends/procedure/officer có dữ liệu Workflow; completed late khác open overdue; unknown/NA loại mẫu số, tỷ lệ null nếu không có mẫu.
- Feedback hiện nhận cả DOCUMENTS_READY và OFFICIALLY_RECEIVED (đã đối chiếu AnalyticsService.cs), không phải chỉ OFFICIALLY_RECEIVED; owner, unique mỗi hồ sơ, 1–5 sao. Chưa update/delete feedback.
- Metrics hằng ngày là tổng hợp event/history; backlog deadline là thời điểm hiện tại, không giả thành backlog lịch sử của ngày To. Đọc coverage/missingDates/asOfUtc.

Điểm đọc: Infrastructure/Notification*.cs, WorkflowNotificationConsumer.cs, ExternalNotificationSender.cs, AnalyticsService.cs, AnalyticsAggregationJob.cs; Application/DeadlineStatistics.cs.

## 4. Việc còn mở và thứ tự gợi ý

Đây là backlog để chủ dự án chọn, KHÔNG phải lệnh tự làm tất cả.

| Ưu tiên | Việc | Trạng thái và bước tiếp theo |
|---|---|---|
| Gần nhất | Yêu cầu FE 26/10 (WORKFLOW-FE-1026) | Mục 2–5 có trong source 29/10 (applicantName, bỏ bản nháp ở danh sách cán bộ, assignment-summary, officer/activity) và rút nhận xét OPEN (WORKFLOW-COMMENT-01); build pass, đã thử store/handler trên Postgres tạm, chưa qua HTTP. Cần chạy `docs/sql/workflow-applicant-name-activity.sql` rồi `docs/sql/workflow-comment-withdrawal.sql`, deploy Workflow + Gateway. Mục 1 (RabbitMQ/worker Azure), 6 (minReplicas, cache, App Insights), 7 (xác nhận image/migration Azure) cho Antigravity; cache access-context chờ chủ dự án quyết vì trái quy tắc fail closed. |
| Gần nhất | AIOCR JWT rollout | Code/build đã có theo nhật ký; chưa Docker/Azure cho patch mới. Xác minh issuer/audience IAM đang dùng, bàn giao env secret đúng; không tự in key. Cập nhật ví dụ cũ trong docs/azure-deployment.md sau khi có căn cứ. |
| Gần nhất | DocumentForm auth.session_unavailable | Diagnostics/image/config đã được rollout theo nhật ký. Còn thiếu tái hiện authenticated request gốc để kết luận lỗi503 đã hết. Không đổi quyền để né lỗi kết nối. |
| Gần nhất | Analytics authenticated FE | Migration, IAM URL, worker/backfill đã xử lý theo nhật ký; FE/HTTP theo đúng role/ward chưa xác nhận đầy đủ. Không coi health200 là dashboard đúng. |
| Cần đối chiếu deploy | IAM security hardening | Source/migration/SQL có; chưa tìm thấy xác nhận rollout đầy đủ của migration security + IAM và 5 service trong các mục đã đọc. So cloud history khi được phép, không apply lại mù. |
| Cần đối chiếu deploy | TASK-25/27 nhiều replica | Listener đã có/build pass; runtime đa replica, reconnect và DB LISTEN connection cần xác nhận. Không tạo thêm queue/table nếu vấn đề là config. |
| Chưa triển khai | SMS thông báo hồ sơ | Adapter Analytics vẫn stub. Cần mẫu CSKH hợp lệ, config Brandname, mapping trạng thái/provider result, retry và chính sách chi phí; không tái sử dụng mẫu OTP đăng ký cho nội dung hồ sơ. |
| Có trong source, chưa nghiệm thu | RAG (TASK-28/29) | KB-01 nguồn/nhập/chia đoạn; KB-02 embedding Gemini, tìm kiếm trong bộ nhớ (không pgvector), chat có citations. Mặc định tắt. Chưa gọi provider thật/chưa deploy. Còn ai_request_logs, lọc theo phường, reranker. |
| Một phần | TASK-30 CCCD OCR | Endpoint/adaptor có; accuracy/provider/cấu hình cloud chưa được kiểm tra lại. OCR logs/eKYC không được suy ra là đã có. |
| Một phần | TASK-34 | RBAC audit + HTTP logging có; global data-change audit/system settings chưa thấy triển khai trong rà soát lịch sử; cần kiểm tra source trước khi bắt đầu. |
| Một phần | TASK-35 | Health endpoint có; dependency health DB/Rabbit, lịch sử sức khỏe, backup tự động/restore drill chưa thấy bằng chứng hoàn tất. |
| Một phần | TASK-36 | Index/API docs có; benchmark/tuning vector/hiệu năng toàn hệ thống chưa có xác nhận. Không có vector RAG thì chưa thể nói đã tune HNSW. |
| Giới hạn | Deadline calendar | Holidays rỗng; cần chủ dự án chốt ngày lễ/nghỉ và tăng version đúng, không sửa snapshot cũ. |
| Giới hạn | Account abuse | Chưa global OTP/SMS budget, chống abuse nhiều account/số, benchmark tải live session IAM. |
| Giới hạn | Vận hành notification | Chưa admin replay API/alert tự động; runbook thủ công có trong api-guide. Bảo toàn inbox/outbox/delivery khi retry. |
| Giới hạn | Chat lifecycle | Chưa retention/delete API/idempotency lượt chat theo ghi chép P2. |
| Tài liệu | ERD trình bày | Đã tạo 3 trang nhưng chủ dự án thấy rối; chấp nhận bản mô tả để tự vẽ, không cần tiếp tục tự chỉnh ERD nếu chưa được giao. |

Không thấy task backend đang viết dở có chỉ thị tiếp tục trong lượt này. Việc cuối của phiên hiện tại là bàn giao; ưu tiên mới nhất về source là AIOCR-JWT-01 và các xác nhận deploy/FE còn mở. Đọc PROGRESS mới nhất trước mỗi lần tiếp quản vì các phiên khác có thể cập nhật sau bản này.

## 5. Migration và cấu hình cần biết

Không chạy SQL/migration cloud chỉ vì thấy file dưới đây. Xác minh đúng service/database, history/schema và baseline, giữ dữ liệu. Không tự insert migration history để “làm cho qua”.

| Phần | Migration/SQL đáng chú ý |
|---|---|
| IAM quyền đọc/receipt | 20261021090000_ManagerWorkflowReadScope; 20261021100000_CounterReceiptPermissions |
| IAM OTP | 20261022063951_AddUserOtpsTable; docs/sql/iam-add-user-otps.sql; docs/sql/iam-catchup-readiness-to-otp.sql |
| IAM security | 20261022170202_AccountSecurityHardening; docs/sql/iam-account-security-hardening.sql |
| Workflow checklist | 20261014045057_SnapshotChecklistRequirements |
| Workflow readiness/receipt/deadline | 20261016154813_DocumentReadiness; 20261021073245_CounterReceipt; 20261021144055_ReviewDeadline |
| AIOCR chat | 20261021080951_ChatSessions |
| Analytics | 20261021031538_AnalyticsMetricsAndCitizenFeedback |
| Notification | 20261010040428_InitialNotifications; 20261012010837_ReliableNotificationDelivery |
| DocumentForm | Xem Infrastructure/Migrations; QR và SubmissionId generated document đã có migration riêng |

Tên cấu hình quan trọng (không có giá trị bí mật trong tài liệu này):
- Jwt__Key, Jwt__Issuer, Jwt__Audience; Iam__BaseUrl đúng địa chỉ truy cập được từ container. Docker hostname ngắn có thể không resolve trên Azure.
- ConnectionStrings__WorkflowDatabase / AiOcrDatabase / NotificationsDatabase và các connection theo code service. Analytics có fallback NotificationsDatabase theo nhật ký; đừng tự đoán schema đã tồn tại.
- ConnectionStrings__NotificationRealtimeDatabase nếu tách LISTEN; cần direct/session pooling, không transaction pooling cho listener.
- Email__Enabled/Host/Port/Username/Password/From; SMS provider/config tách IAM và Analytics. Không bật Sandbox0 hoặc gửi thật khi chỉ đang đọc code.
- Analytics__JobsEnabled, Workflow__BaseUrl, Analytics__ServiceKey, IAM recipient service key; worker cần replica sống liên tục.
- Services__ApplicationWorkflow__BaseUrl và DocumentForm base URL cho AIOCR prevalidation.
- Database__AutoMigrate là cấu hình tùy service/code; bật flag không chứng minh migration đã chạy. Kiểm tra log/history thực.
- Không thay Gemini model/billing để “sửa503” khi chưa có yêu cầu; lỗi provider/quota và lỗi cấu hình/auth phải phân biệt.

DDL docs/diagrams/erd/schema/*.sql là CREATE từ EF model phục vụ ERD, KHÔNG phải migration idempotent nâng cấp.

## 6. Bằng chứng gần nhất — đọc đúng giới hạn

Nguồn các mốc dưới đây là PROGRESS, phiên bàn giao này không thực hiện lại:

- IAM Email OTP/welcome và eSMS OTP có ghi nhận kiểm tra cloud ngày22/10; các thay đổi security ngày23/10 đến sau đó, không tự suy ra đã được nghiệm thu cùng image cũ.
- TASK25/27 listener: Release 0 lỗi/0 cảnh báo, lần cuối 00:00:32.69; không integration đa replica trong lượt đó.
- DocumentForm diagnostic image `iam-diagnostics-20261023`: nhật ký ghi smoke Docker10/10 với IAM mô phỏng, rollout và đúng DLL/config; authenticated request người dùng vẫn pending.
- Analytics: ghi nhận sửa DNS IAM, áp dụng schema4 bảng + history, bật Hangfire; backfill14..23/10 coverage10/10. Đây là snapshot vận hành, không số liệu trực tiếp hiện tại và không có nghĩa đủ tất cả business endpoint.
- AIOCR-JWT-01: nhật ký ghi solution Release0warnings/0errors 00:00:13.26 và kiểm tra thiếu Jwt key dừng startup. Chưa Docker/Azure patch đó.
- Test projects đã được gỡ. Các con số 241/250 tests và smoke cũ thuộc thời điểm/source cũ; không dùng như automated coverage hiện tại.
- Lượt tạo bàn giao này: docs-only; không build, không test, không Git, không deploy, không gửi email/SMS, không thay cấu hình hoặc database.

## 7. Tài liệu/ERD đang có

- `system-erd-conceptual.md`, `system-unified-erd.md`, `system-erd-physical.md`.
- `diagrams/erd/wardmate-erd.drawio`: đúng 3 tab toàn hệ thống. Người dùng không hài lòng độ rối của bản vẽ; không nói đã được duyệt hình.
- `erd-drawing-guide.txt`: bản text 41 bảng/22 FK/46 REF để người dùng tự vẽ, kèm cột đề xuất của 6 bảng FUTURE; mục 5–8 sinh lại từ model-inventory.json.
- `diagrams/erd/wardmate-erd-interactive.html` (tiếng Anh, 3 tab, có bảng FUTURE) và `diagrams/erd/wardmate-erd-en.drawio` (3 trang, xuất từ cùng bố cục). Nguồn dựng nằm ngoài repo (scratchpad của phiên Claude); khi schema đổi, cập nhật bằng cách dựng lại từ model-inventory.json.
- Bản báo cáo (yêu cầu chủ dự án 26/10/2026): `diagrams/erd/wardmate-erd-report-en.drawio` và `diagrams/erd/report/{conceptual,logical,physical}.png` vẽ 6 bảng FUTURE như bảng thường. Chỉ dùng để trình bày thiết kế; 6 bảng đó vẫn chưa có trong code/DB. Không thay bản chính xác. Xem `diagrams/erd/report/README.md`.
- `diagrams/erd/model-inventory.json`: dữ liệu nguồn ngày 24/10. 6 bảng FUTURE chưa có schema thực: ai_request_logs, ocr_scan_logs, audit_logs, system_settings, service_health_logs, system_backup_logs. knowledge_documents/knowledge_chunks đã triển khai (TASK-28/29). Ảnh *-preview.png đã render lại từ SVG ngày 24/10. Cả hai file .drawio đã mở thử bằng bộ hiển thị chính thức của diagrams.net (viewer-static): 3 trang mỗi file, không cạnh nào thiếu đầu nối, nhãn HTML dựng đúng.
- `system-diagrams.md` còn phần Activity/State/Structure lịch sử; khi đổi luồng phải đối chiếu ngày/phạm vi, không tin toàn bộ hình cũ đã cập nhật.
- Khi tái sinh ERD, chạy generate-erd.cjs rồi generate-drawio.cjs; bước sau cập nhật bản trình bày3trang. Không tự chạy nếu task không cần.

## 8. Cách Claude tiếp tục và bàn giao ngược

1. Đọc CLAUDE.md/AGENTS.md, mục cuối PROGRESS và file liên quan task.
2. Báo điều đã xác định, điều còn thiếu; chờ task nếu chưa được giao. Không hỏi lại quyền sửa code cục bộ đã được giao rõ.
3. Sửa trong phạm vi, giữ thay đổi khác. Không suy đoán clean/branch/commit state vì không chạy Git.
4. Nếu sửa code: Release toàn solution, báo kết quả thật. Không tự thêm test project. Nếu chỉ docs: kiểm tra links/path/nội dung, nói rõ không build lại.
5. Cập nhật API/constraints khi cần; append PROGRESS. Cập nhật mục còn mở của file này khi task có kết quả mới.
6. Handoff tối thiểu: task + thời gian; file đã sửa; phần xong; phần dở và lý do; bước tiếp theo cụ thể; kết quả build; migration/config chưa apply; bằng chứng deploy nếu có.
7. Antigravity nhận commit message gợi ý; mỗi file mới commit riêng theo AGENTS. Claude/Codex không tự Git và không coi bàn giao là quyền deploy.

Không đính kèm nguyên lịch sử chat hoặc .env: hội thoại cũ có secret. Không đưa dữ liệu cá nhân, token, password vào handoff. Nếu cần cấu hình, chỉ hướng tới nơi lưu secret cục bộ/Azure secretref và che giá trị.

## Bổ sung — IAM staff provisioning Option A (23/10/2026)

Đã thêm source cho POST /api/v1/accounts/manager và POST /api/v1/accounts/procedure-manager. IT_ADMIN + permission; Manager cần ward, Procedure Manager ward tùy chọn; chỉ một role lúc tạo; transaction/audit/unique conflict và mật khẩu hoa-thường-số-đặc biệt. Giữ RBAC nâng cao và quyền front-desk hiện tại. Không migration, không token/welcome email ở hai API này. Nullable fields có thể bị bỏ khỏi JSON. Đọc mục IAM Option A trong api-guide.md và rule IAM-STAFF-PROVISION-01.

Source chính: IAM Application/Accounts/ProvisionStaffRequests.cs (mới), IManagementScope.cs; Infrastructure/Persistence/StaffAdministration.cs; API/Controllers/StaffAdministrationController.cs. Kết quả build và phần còn chờ xem mục IAM-STAFF-PROVISION-01 mới nhất trong PROGRESS.md. Chưa deploy/FE nghiệm thu hai API; không tự triển khai cloud hoặc đổi RBAC để tiếp tục. Mục 4 của bản bàn giao trước đó không tự cập nhật trạng thái production vì bổ sung này.

Kết quả kiểm tra Option A: `dotnet build WardMate.sln -c Release -warnaserror -m:1` thành công 0 warnings/0 errors, 00:05:53.88. Không test project/runtime HTTP/DB/deploy/Git. Đã triển khai source, phần còn chờ là rollout IAM và FE nghiệm thu. Không cần migration hoặc cấu hình mới.
