# WardMate — Mô tả các liên kết giữa các bảng trong ERD

Cập nhật 28/10/2026 (UTC+07:00) từ [model-inventory.json](diagrams/erd/model-inventory.json) (EF model ngày 2026-10-24): **41 bảng**, **22 khóa ngoại (FK)**, **46 liên kết logic (REF)**, cộng 4 liên kết của 6 bảng thiết kế dự kiến. Tài liệu đi kèm các sơ đồ: [ERD tương tác](diagrams/erd/wardmate-erd-interactive.html), [draw.io](diagrams/erd/wardmate-erd-en.drawio), [hướng dẫn tự vẽ](erd-drawing-guide.txt).

## Cách đọc

- **FK (khóa ngoại):** ràng buộc thật trong database, chỉ có giữa các bảng **cùng một service** (cùng database). Database bảo đảm bản ghi cha tồn tại và áp dụng hành vi ON DELETE.
- **REF (liên kết logic):** bảng con chỉ lưu ID (hoặc mã) của bảng cha, **không có ràng buộc trong database**. Dùng khi hai bảng thuộc hai service khác nhau (mỗi service một database), hoặc khi cố ý không ràng buộc (dữ liệu lịch sử, audit, JSON). Tính đúng đắn do code kiểm tra qua API nội bộ.
- **Bản số cha — con:** `1 — 0..N` nghĩa là một bản ghi cha có không hoặc nhiều bản ghi con, mỗi con thuộc đúng một cha; `0..1` ở phía cha nghĩa là cột liên kết ở bảng con được phép rỗng.
- **ON DELETE:** CASCADE = xóa cha thì xóa luôn con; RESTRICT = không cho xóa cha khi còn con.
- Tên `approved_qrs`, `approved_at` là tên kỹ thuật cũ, không có nghĩa phê duyệt hành chính.

## 1. Khóa ngoại trong từng service (22)

### IAM (9)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK01 | `public.permissions` → `public.role_permissions` | permission_id → id | 1 — 0..N | CASCADE | Quyền được gán cho vai trò qua bảng nối role_permissions (vai trò – quyền là nhiều–nhiều). |
| FK02 | `public.roles` → `public.role_permissions` | role_id → id | 1 — 0..N | CASCADE | Vai trò sở hữu tập quyền; xóa vai trò thì xóa luôn các dòng gán quyền. |
| FK03 | `public.wards` → `public.users` | ward_id → id | 0..1 — 0..N | RESTRICT | Tài khoản cán bộ/Manager thuộc một phường; công dân và IT_ADMIN có thể không gán phường. Không xóa được phường còn người dùng. |
| FK04 | `public.users` → `public.refresh_tokens` | user_id → id | 1 — 0..N | CASCADE | Mỗi refresh token thuộc một tài khoản; xoay vòng khi refresh và bị thu hồi khi đổi mật khẩu. |
| FK05 | `public.users` → `iam.user_otps` | user_id → id | 1 — 0..N | CASCADE | OTP quên/đổi mật khẩu và xác minh số điện thoại thuộc một tài khoản; chỉ lưu hash, không lưu mã. |
| FK06 | `public.users` → `public.user_profiles` | user_id → id | 1 — 0..1 | CASCADE | Hồ sơ cá nhân (họ tên, CCCD, số điện thoại, địa chỉ) quan hệ 1–1 với tài khoản. |
| FK07 | `public.roles` → `public.user_roles` | role_id → id | 1 — 0..N | CASCADE | Vai trò được gán cho người dùng qua bảng nối user_roles. |
| FK08 | `public.users` → `public.user_roles` | user_id → id | 1 — 0..N | CASCADE | Người dùng có thể có nhiều vai trò qua user_roles (người dùng – vai trò là nhiều–nhiều). |
| FK09 | `public.users` → `iam.welcome_emails` | user_id → id | 1 — 0..1 | CASCADE | Mỗi tài khoản có tối đa một bản ghi email chào mừng trong hàng đợi gửi. |

### Procedure Catalog (2)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK10 | `public.procedure_categories` → `public.procedures` | category_id → id | 1 — 0..N | RESTRICT | Mỗi thủ tục thuộc một lĩnh vực; không xóa được lĩnh vực còn thủ tục. |
| FK11 | `public.procedures` → `public.procedure_versions` | procedure_id → id | 1 — 0..N | RESTRICT | Thủ tục lưu các phiên bản đã xuất bản (snapshot) để xem lịch sử và khôi phục. |

### Application Workflow (6)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK12 | `public.applications` → `public.application_assignment_history` | application_id → id | 1 — 0..N | CASCADE | Mỗi lần nhận, trả lại, giao hoặc chuyển người phụ trách ghi một dòng lịch sử phân công. |
| FK13 | `public.applications` → `public.application_checklists` | application_id → id | 1 — 0..N | CASCADE | Checklist giấy tờ của hồ sơ, sao chép từ checklist thủ tục lúc tạo hồ sơ; thủ tục đổi sau đó không ảnh hưởng. |
| FK14 | `public.applications` → `public.application_status_history` | application_id → id | 1 — 0..N | CASCADE | Mỗi lần đổi trạng thái hồ sơ ghi một dòng lịch sử (trạng thái cũ/mới, người đổi, lý do). |
| FK15 | `public.applications` → `public.application_versions` | application_id → id | 1 — 0..N | CASCADE | Mỗi lần nộp hoặc nộp lại tạo một phiên bản (snapshot dữ liệu) để cán bộ so sánh các lần nộp. |
| FK16 | `public.application_versions` → `public.application_comments` | application_id, application_version_id → application_id, id | 1 — 0..N | RESTRICT | Nhận xét gắn với đúng phiên bản cán bộ đang xem; FK ghép (application_id, version) bảo đảm phiên bản thuộc cùng hồ sơ. |
| FK17 | `public.applications` → `public.application_comments` | application_id → id | 1 — 0..N | CASCADE | Nhận xét thuộc một hồ sơ; xóa hồ sơ thì xóa nhận xét. |

### Document Form (2)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK18 | `document.form_templates` → `document.form_template_versions` | template_id → id | 1 — 0..N | RESTRICT | Mẫu biểu có nhiều phiên bản (file DOCX gốc, schema trường, ánh xạ). |
| FK19 | `document.form_template_versions` → `document.user_submissions` | template_version_id → id | 0..1 — 0..N | RESTRICT | Đơn đã điền tham chiếu phiên bản mẫu dùng khi điền; có thể rỗng với dữ liệu cũ. |

### AIOCR (2)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK20 | `public.chat_sessions` → `public.chat_messages` | session_id → id | 1 — 0..N | CASCADE | Phiên chat chứa các tin nhắn của người dùng và trợ lý AI. |
| FK21 | `public.knowledge_documents` → `public.knowledge_chunks` | document_id → id | 1 — 0..N | CASCADE | Tài liệu tri thức được chia thành các đoạn; đổi nội dung thì thay toàn bộ đoạn. |

### Analytics System (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | ON DELETE | Ý nghĩa |
|---|---|---|---|---|---|
| FK22 | `public.notifications` → `public.notification_deliveries` | NotificationId → id | 1 — 0..N | CASCADE | Mỗi thông báo có một lượt gửi cho mỗi kênh (IN_APP, EMAIL, SMS). |

## 2. Liên kết logic trong cùng service (10)

Cùng database nhưng cố ý không đặt FK (dữ liệu lịch sử/audit, ID nằm trong JSON, hoặc bảng có vòng đời dọn dẹp riêng).

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF02 | `public.procedures` → `public.procedure_drafts` | published_procedure_id | 0..1 — 0..N | Bản nháp sau khi xuất bản trỏ tới thủ tục được tạo hoặc cập nhật. |
| REF09 | `public.applications` → `public.workflow_outbox` | Payload.ApplicationId | 0..1 — 0..N (JSON) | Sự kiện outbox mang ApplicationId trong payload để phát sang Analytics qua RabbitMQ. |
| REF13 | `document.form_templates` → `document.user_submissions` | template_id | 1 — 0..N | Mẫu biểu gốc của đơn đã điền. |
| REF21 | `document.user_submissions` → `document.approved_qrs` | submission_id | 1 — 0..N | Mã QR gắn với đơn đã điền dùng khi kết xuất PDF. |
| REF28 | `public.notification_inbox` → `public.notifications` | source_event_id | 0..1 — 0..N | Thông báo được tạo từ sự kiện inbox nào. |
| REF38 | `public.users` → `public.rbac_audit_logs` | actor_user_id, target_user_id | 0..1 — 0..N | Người thực hiện và người bị tác động trong nhật ký phân quyền. |
| REF39 | `public.roles` → `public.rbac_audit_logs` | role_id | 0..1 — 0..N | Vai trò bị thay đổi trong nhật ký phân quyền. |
| REF40 | `public.permissions` → `public.rbac_audit_logs` | permission_id | 0..1 — 0..N | Quyền bị thay đổi trong nhật ký phân quyền. |
| REF42 | `public.notification_deliveries` → `public.notification_delivery_audits` | DeliveryId | 1 — 0..N | Audit thao tác quản trị trên một lượt gửi; cố ý không đặt FK để giữ bằng chứng khi lượt gửi bị dọn. |
| REF46 | `public.knowledge_chunks` → `public.chat_messages` | citations | 0..1 — 0..N (JSON) | Câu trả lời chatbot lưu danh sách đoạn tri thức đã trích dẫn trong JSON citations. |

## 3. Liên kết logic giữa các service (36)

Đây là các liên kết nối 6 service với nhau trong ERD toàn hệ thống.

### IAM → Procedure Catalog (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF01 | `public.users` → `public.procedure_drafts` | created_by, reviewed_by | 0..1 — 0..N | Người tạo và người đối soát bản nháp thủ tục. |

### IAM → Application Workflow (6)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF03 | `public.users` → `public.applications` | user_id, assigned_officer_id | 0..1 — 0..N | Công dân sở hữu hồ sơ (UserId) và cán bộ đang phụ trách (AssignedOfficerId). |
| REF04 | `public.wards` → `public.applications` | ward_code | 0..1 — 0..N | Phường tiếp nhận hồ sơ, theo mã phường; dùng để giới hạn phạm vi xem/xử lý của cán bộ và Manager. |
| REF06 | `public.users` → `public.application_comments` | officer_id | 1 — 0..N | Cán bộ viết nhận xét. |
| REF07 | `public.users` → `public.application_versions` | submitted_by | 1 — 0..N | Người nộp phiên bản hồ sơ (công dân). |
| REF08 | `public.users` → `public.application_status_history` | changed_by | 1 — 0..N | Người thực hiện thao tác đổi trạng thái. |
| REF41 | `public.users` → `public.application_assignment_history` | from_officer_id, to_officer_id, actor_id | 0..1 — 0..N | Cán bộ cũ, cán bộ mới và người thực hiện trong lịch sử phân công. |

### Procedure Catalog → Application Workflow (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF05 | `public.procedures` → `public.applications` | procedure_id | 1 — 0..N | Thủ tục mà hồ sơ áp dụng; hồ sơ giữ snapshot tên, lĩnh vực và checklist của thủ tục. |

### Document Form → Application Workflow (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF10 | `document.form_templates` → `public.application_checklists` | form_template_id | 0..1 — 0..N | Mục checklist yêu cầu điền một mẫu biểu cụ thể. |

### Procedure Catalog → Document Form (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF11 | `public.procedures` → `document.form_templates` | FormDefinitions JSON | 0..1 — 0..N (JSON) | Thủ tục khai báo các mẫu biểu cần dùng trong JSON FormDefinitions. |

### IAM → Document Form (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF12 | `public.users` → `document.user_submissions` | applicant_id | 1 — 0..N | Công dân sở hữu đơn đã điền. |

### Application Workflow → Document Form (7)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF14 | `public.applications` → `document.user_submissions` | application_id | 0..1 — 0..N | Đơn đã điền thuộc hồ sơ nào. |
| REF15 | `public.application_checklists` → `document.user_submissions` | checklist_id | 0..1 — 0..N | Đơn đã điền đáp ứng mục checklist nào. |
| REF16 | `public.applications` → `document.application_forms` | application_id | 1 — 0..N | Dữ liệu biểu mẫu trực tuyến của hồ sơ. |
| REF17 | `public.applications` → `document.supporting_documents` | application_id | 1 — 0..N | Giấy tờ minh chứng thuộc hồ sơ nào. |
| REF18 | `public.application_checklists` → `document.supporting_documents` | checklist_id | 0..1 — 0..N | Giấy tờ minh chứng đáp ứng mục checklist nào. |
| REF19 | `public.applications` → `document.generated_documents` | application_id | 1 — 0..N | Văn bản PDF đã kết xuất của hồ sơ. |
| REF20 | `public.applications` → `document.approved_qrs` | application_id | 1 — 0..1 | Mỗi hồ sơ có tối đa một mã QR tham chiếu (ApplicationId là duy nhất). |

### IAM → AIOCR (3)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF22 | `public.users` → `public.chat_sessions` | user_id | 1 — 0..N | Chủ phiên chat. |
| REF23 | `public.wards` → `public.chat_sessions` | ward_code | 1 — 0..N | Phường người dùng chọn làm ngữ cảnh khi chat; chỉ để tham khảo, không cấp quyền. |
| REF44 | `public.users` → `public.knowledge_documents` | created_by, updated_by | 1 — 0..N | Người tạo và người sửa gần nhất tài liệu tri thức. |

### IAM → Analytics System (8)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF24 | `public.users` → `public.notifications` | recipient_user_id | 1 — 0..N | Người nhận thông báo. |
| REF25 | `public.wards` → `public.notifications` | recipient_ward_code | 0..1 — 0..N | Phường của thông báo dành cho cán bộ; dùng để kiểm tra lại phạm vi trước khi gửi. |
| REF30 | `public.users` → `analytics.citizen_feedback` | CitizenId | 1 — 0..N | Công dân gửi đánh giá. |
| REF33 | `public.users` → `analytics.daily_performance_metrics` | OfficerId | 1 — 0..N | Số liệu theo ngày cho từng cán bộ. |
| REF35 | `public.wards` → `analytics.application_procedure_metrics` | WardCode | 1 — 0..N | Phường của số liệu theo thủ tục. |
| REF36 | `public.wards` → `analytics.daily_performance_metrics` | WardCode | 1 — 0..N | Phường của số liệu theo cán bộ. |
| REF37 | `public.wards` → `analytics.citizen_feedback` | WardCode | 1 — 0..N | Phường của đánh giá, để Manager chỉ xem đánh giá trong phường mình. |
| REF43 | `public.users` → `public.notification_delivery_audits` | ActorId | 1 — 0..N | IT_ADMIN thực hiện gửi lại hoặc đối soát SMS. |

### Application Workflow → Analytics System (3)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF26 | `public.applications` → `public.notifications` | application_id | 0..1 — 0..N | Hồ sơ liên quan, để mở đúng hồ sơ từ thông báo. |
| REF27 | `public.workflow_outbox` → `public.notification_inbox` | EventId | 0..1 — 0..N | Sự kiện outbox của Workflow được nhận vào inbox Analytics với cùng EventId (chống xử lý trùng). |
| REF29 | `public.applications` → `analytics.citizen_feedback` | ApplicationId | 1 — 0..1 | Đánh giá của công dân cho một hồ sơ; mỗi hồ sơ chỉ được đánh giá một lần. |

### Procedure Catalog → Analytics System (3)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF31 | `public.procedures` → `analytics.citizen_feedback` | ProcedureId | 1 — 0..N | Thủ tục của hồ sơ được đánh giá. |
| REF32 | `public.procedures` → `analytics.application_procedure_metrics` | ProcedureId | 1 — 0..N | Số liệu theo ngày cho từng thủ tục. |
| REF34 | `public.procedures` → `analytics.daily_performance_metrics` | ProcedureId | 1 — 0..N | Thủ tục trong số liệu theo cán bộ. |

### Procedure Catalog → AIOCR (1)

| Mã | Bảng cha → bảng con | Cột liên kết (ở bảng con) | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|
| REF45 | `public.procedures` → `public.knowledge_documents` | procedure_id | 0..1 — 0..N | Thủ tục làm nguồn của tài liệu tri thức (đồng bộ từ Catalog hoặc gắn tay). |

## 4. Liên kết của các bảng thiết kế dự kiến (4)

Sáu bảng `ai_request_logs`, `ocr_scan_logs`, `audit_logs`, `system_settings`, `service_health_logs`, `system_backup_logs` là thiết kế theo đặc tả gốc, **chưa có trong code và database**. Cột và kiểu chi tiết: [hướng dẫn tự vẽ, mục 8](erd-drawing-guide.txt).

| Mã | Bảng cha → bảng con | Cột liên kết | Loại | Bản số cha — con | Ý nghĩa |
|---|---|---|---|---|---|
| F1 | `public.chat_sessions` → `ai_request_logs` | SessionId | FK (cùng database AIOCR) | 1 — 0..N | Mỗi lần gọi mô hình AI trong một phiên chat được ghi một dòng nhật ký (model, token, độ trễ, lỗi). |
| F2 | `public.users` → `ocr_scan_logs` | UserId | REF IAM → AIOCR | 0..1 — 0..N | Nhật ký mỗi lần quét CCCD/giấy tờ và người thực hiện. |
| F3 | `public.users` → `audit_logs` | UserId | REF IAM → Analytics | 0..1 — 0..N | Nhật ký thay đổi dữ liệu toàn hệ thống và người thực hiện (rỗng nếu là tác vụ hệ thống). |
| F4 | `public.users` → `system_settings` | UpdatedBy | REF IAM → Analytics | 1 — 0..N | Người cập nhật gần nhất một cấu hình hệ thống. |

`service_health_logs` và `system_backup_logs` đứng độc lập, không liên kết bảng nào.

## 5. Ghi chú

- **Quan hệ nhiều–nhiều** được tách bằng bảng nối: người dùng – vai trò qua `user_roles`, vai trò – quyền qua `role_permissions`. Khóa chính của bảng nối là cặp hai cột.
- **Bảng không có liên kết:** `analytics.metric_refreshes` — bảng kỹ thuật ghi mốc tổng hợp số liệu theo ngày, không tham chiếu bản ghi nghiệp vụ.
- **Mã phường (WardCode)** là REF theo mã (`wards.code`), không theo ID; `users.ward_id` là FK theo ID trong IAM.
- **Lĩnh vực của hồ sơ** (`applications.category_id`) và **lĩnh vực phụ trách của cán bộ** (`users.assigned_categories`, mảng) là giá trị snapshot/mảng ID lĩnh vực của Catalog; không vẽ thành đường riêng để sơ đồ dễ đọc.
- **Một hồ sơ** là trung tâm của hầu hết liên kết: 13 liên kết xuất phát từ `applications`.
- Nguồn số liệu là EF model trong source; chưa đối chiếu schema đang chạy trên Azure.
