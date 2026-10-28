# Hướng dẫn đọc và chỉnh sửa ERD WardMate (`WardMate_ERD.drawio`)

> Tài liệu đi kèm file `docs/diagrams/erd/WardMate_ERD.drawio`. Mục đích: biết file có gì, đọc ký hiệu thế nào, mỗi hình ứng với bảng/cột nào trong code, và khi schema đổi thì sửa chỗ nào.
>
> Cập nhật: 27/10/2026. Nguồn dữ liệu: EF Core `ModelSnapshot` của 7 DbContext trong `src/Services/*/…Infrastructure/…/Migrations` (migration mới nhất: 24/10/2026). Tài liệu này được sinh tự động từ chính schema và file draw.io nên số liệu khớp nhau.

## Mục lục

1. Tổng quan file
2. Ký hiệu dùng trong sơ đồ
3. Trang 1 – Conceptual ERD
4. Trang 2 – Logical ERD
5. Trang 3 – Physical ERD
6. Danh sách quan hệ (FK và REF)
7. Cách chỉnh sửa trong draw.io
8. Khi schema thay đổi – checklist cập nhật
9. Giả định và điểm cần xác nhận

## 1. Tổng quan file

| Trang (tab dưới cùng draw.io) | Nội dung | Số lượng |
|---|---|---|
| **Conceptual ERD** | Các khái niệm nghiệp vụ và quan hệ giữa chúng. Chỉ có tên, không thuộc tính, không khóa. | 33 thực thể, 46 quan hệ + 1 cây generalization |
| **Logical ERD** | Thực thể + tên thuộc tính (theo property C#). Không kiểu dữ liệu, không PK/FK, không phụ thuộc DBMS. | 39 thực thể, 76 quan hệ |
| **Physical ERD** | Bảng PostgreSQL thật: cột, kiểu, NULL/NOT NULL, PK/FK/UK. | 41 bảng, 410 cột, 22 FK thật, 56 REF |

Ba mức theo đúng bảng so sánh của Visual Paradigm:

| Đặc điểm | Conceptual | Logical | Physical |
|---|:-:|:-:|:-:|
| Tên thực thể | ✔ | ✔ | ✔ |
| Quan hệ | ✔ | ✔ | ✔ |
| Thuộc tính/cột | | ✔ | ✔ |
| Kiểu dữ liệu | | | ✔ |
| Khóa chính / khóa ngoại | | | ✔ |
| Quan hệ N–N | vẽ trực tiếp | vẽ trực tiếp | tách thành bảng trung gian |
| Generalization (loại con) | ✔ | | |

**Kiến trúc dữ liệu cần nhớ:** WardMate là microservices, *mỗi service một database*. Vì vậy chỉ quan hệ **trong cùng một database** mới có FOREIGN KEY thật. Quan hệ giữa các service chỉ là cột lưu ID (ví dụ `applications.user_id` lưu id của `users` bên IAM DB) – trên Physical vẽ **nét đứt** và gọi là **REF**.

| Database | DbContext | Số bảng | Các bảng |
|---|---|--:|---|
| IAM DB | `IamDbContext` | 11 | `permissions`, `rbac_audit_logs`, `refresh_tokens`, `roles`, `role_permissions`, `users`, `iam.user_otps`, `user_profiles`, `user_roles`, `wards`, `iam.welcome_emails` |
| Catalog DB | `ProcedureDbContext` | 4 | `procedures`, `procedure_categories`, `procedure_drafts`, `procedure_versions` |
| Workflow DB | `WorkflowDbContext` | 7 | `application_assignment_history`, `application_checklists`, `application_comments`, `applications`, `application_status_history`, `application_versions`, `workflow_outbox` |
| DocumentForm DB | `DocumentDbContext` | 7 | `document.application_forms`, `document.approved_qrs`, `document.form_templates`, `document.form_template_versions`, `document.generated_documents`, `document.supporting_documents`, `document.user_submissions` |
| AIOCR DB | `ChatDbContext` | 4 | `chat_messages`, `chat_sessions`, `knowledge_chunks`, `knowledge_documents` |
| Notification DB | `NotificationDbContext` | 4 | `notifications`, `notification_delivery_audits`, `notification_deliveries`, `notification_inbox` |
| Analytics DB | `AnalyticsDbContext` | 4 | `analytics.citizen_feedback`, `analytics.metric_refreshes`, `analytics.daily_performance_metrics`, `analytics.application_procedure_metrics` |

## 2. Ký hiệu dùng trong sơ đồ

### 2.1 Ký hiệu bản số (crow's foot) ở hai đầu đường nối

Ký hiệu ở **một đầu** cho biết *bao nhiêu bản ghi của bảng ở đầu đó* ứng với **một** bản ghi của bảng ở đầu kia.

| Ký hiệu (gần bảng) | Tên trong draw.io | Nghĩa | Khi nào dùng ở WardMate |
|---|---|---|---|
| `‖` (hai gạch) | `ERmandOne` | đúng 1 | Đầu bảng cha khi cột FK/REF là NOT NULL |
| `o\|` (tròn + gạch) | `ERzeroToOne` | 0 hoặc 1 | Đầu bảng cha khi cột FK/REF cho phép NULL; hoặc đầu bảng con khi cột FK là UNIQUE/PK |
| `o<` (tròn + chân quạ) | `ERzeroToMany` | 0 hoặc nhiều | Đầu bảng con thông thường |
| `\|<` (gạch + chân quạ) | `ERoneToMany` | 1 hoặc nhiều | Không dùng (DB không ép "ít nhất 1") |

**Ví dụ đọc:** đường `users ‖——o< applications` nhãn `REF user_id` ⇒ *mỗi hồ sơ thuộc đúng 1 công dân; mỗi công dân có 0..n hồ sơ*.

### 2.2 Kiểu đường và các dấu khác

| Ký hiệu | Ý nghĩa |
|---|---|
| Đường liền | Quan hệ có FOREIGN KEY thật trong DB (Physical) / quan hệ nghiệp vụ (Conceptual, Logical) |
| Đường **nét đứt** (chỉ Physical) | **REF**: cột chỉ lưu ID, DB không ràng buộc (khác database, hoặc ID nằm trong JSON/mảng) |
| Nhãn `FK cột` | Tên cột khóa ngoại ở bảng con (Physical) |
| Nhãn `REF cột` / `REF (JSON) cột` / `REF (array) cột` | Cột tham chiếu không có FK; ID nằm trực tiếp / trong JSON / trong mảng |
| Nhãn động từ (Conceptual, Logical) | Đọc **từ bảng phía "1" sang bảng phía "nhiều"**, ví dụ *Procedure Category classifies Procedure* |
| Mũi tên tam giác rỗng `—▷` (chỉ Conceptual) | Generalization: loại con *là một loại* User. Các nhánh gộp về **một** tam giác ở `User` |
| `[IAM DB]` sau tên bảng (Physical) | Database của service sở hữu bảng |
| `document.` / `analytics.` / `iam.` trước tên bảng | PostgreSQL schema (không ghi = schema mặc định `public`) |

### 2.3 Cột "khóa" trong bảng Physical

| Dấu | Nghĩa |
|---|---|
| `PK` | Khóa chính (dòng PK in đậm, có gạch ngăn dưới khối PK) |
| `FK` | Khóa ngoại thật |
| `PK,FK` | Vừa là khóa chính vừa là khóa ngoại (ví dụ `user_profiles.user_id`, `user_roles`) |
| `UK` | UNIQUE một cột |
| `U1`, `U2` | UNIQUE nhiều cột (các cột cùng số thuộc một ràng buộc); có thể là UNIQUE có điều kiện (partial) – xem mục 5 |
| Cột thứ 4 | `NOT NULL` / `NULL` |

Kiểu viết tắt: `varchar(n)` = `character varying(n)`, `timestamptz` = `timestamp with time zone`, `float8` = `double precision`. Bảng thuộc AnalyticsSystem có tên cột PascalCase (`MetricDate`, `WardCode`…) vì DbContext đó không cấu hình snake_case – đây là tên cột thật trong DB.

## 3. Trang 1 – Conceptual ERD

Mục đích: nhìn nghiệp vụ, không quan tâm bảng. Đã **bỏ** các bảng kỹ thuật: `refresh_tokens`, `user_otps`, `welcome_emails`, `rbac_audit_logs`, `workflow_outbox`, `notification_inbox`, `notification_delivery_audits`, `metric_refreshes`. `user_profiles` gộp vào User, `notification_deliveries` gộp vào Notification, hai bảng metrics gộp thành Performance Statistic.

### 3.1 Thực thể

| # | Thực thể | Nghĩa | Bảng Physical tương ứng |
|--:|---|---|---|
| 1 | **Ward** | Phường/xã | `wards` |
| 2 | **User** | Tài khoản người dùng (siêu kiểu của 5 vai trò) | `users`, `user_profiles` |
| 3 | **Role** | Vai trò RBAC | `roles`, `user_roles` |
| 4 | **Permission** | Quyền | `permissions`, `role_permissions` |
| 5 | **Citizen** | Công dân (role REGISTERED_CITIZEN) | `users + user_roles` |
| 6 | **Front Desk Officer** | Cán bộ Một cửa (role FRONT_DESK_OFFICER) | `users + user_roles` |
| 7 | **Manager** | Lãnh đạo/quản lý phường (role MANAGER) | `users + user_roles` |
| 8 | **Procedure Manager** | Cán bộ quản lý thủ tục (role PROCEDURE_MANAGER) | `users + user_roles` |
| 9 | **IT Admin** | Quản trị hệ thống (role IT_ADMIN) | `users + user_roles` |
| 10 | **Procedure Category** | Lĩnh vực thủ tục | `procedure_categories` |
| 11 | **Procedure** | Thủ tục hành chính | `procedures` |
| 12 | **Procedure Version** | Phiên bản thủ tục | `procedure_versions` |
| 13 | **Procedure Draft** | Bản nháp thủ tục từ PDF | `procedure_drafts` |
| 14 | **Application** | Hồ sơ tiền kiểm | `applications` |
| 15 | **Application Version** | Lần nộp hồ sơ | `application_versions` |
| 16 | **Checklist Item** | Mục giấy tờ cần nộp | `application_checklists` |
| 17 | **Review Comment** | Nhận xét của cán bộ | `application_comments` |
| 18 | **Status Change** | Lần chuyển trạng thái | `application_status_history` |
| 19 | **Assignment Record** | Lần phân công | `application_assignment_history` |
| 20 | **Form Template** | Mẫu tờ khai | `form_templates` |
| 21 | **Form Template Version** | Phiên bản mẫu tờ khai | `form_template_versions` |
| 22 | **Form Submission** | Tờ khai đã điền | `user_submissions` |
| 23 | **Application Form Data** | Dữ liệu form của hồ sơ | `application_forms` |
| 24 | **Supporting Document** | Giấy tờ minh chứng | `supporting_documents` |
| 25 | **Generated Document** | PDF hệ thống sinh | `generated_documents` |
| 26 | **Approved QR** | Mã QR tra cứu | `approved_qrs` |
| 27 | **Chat Session** | Phiên chat AI | `chat_sessions` |
| 28 | **Chat Message** | Tin nhắn chat | `chat_messages` |
| 29 | **Knowledge Document** | Tài liệu kiến thức | `knowledge_documents` |
| 30 | **Knowledge Chunk** | Đoạn kiến thức | `knowledge_chunks` |
| 31 | **Notification** | Thông báo | `notifications`, `notification_deliveries` |
| 32 | **Citizen Feedback** | Đánh giá của công dân | `citizen_feedback` |
| 33 | **Performance Statistic** | Số liệu thống kê | `daily_performance_metrics`, `application_procedure_metrics` |

**Generalization:** Citizen, Front Desk Officer, Manager, Procedure Manager, IT Admin *là loại con của* User. Một tài khoản có thể có nhiều vai trò cùng lúc (overlapping) vì vai trò gán qua `user_roles`. Trong draw.io cây này gồm cạnh `CGtop` (User → điểm gộp ẩn `GJ`, có tam giác) và các cạnh `CG0…CG4` (điểm gộp → từng loại con).

### 3.2 Quan hệ

| # | Thực thể A | Bản số A | Động từ (A → B) | Bản số B | Thực thể B | Căn cứ trong DB | ID cạnh |
|--:|---|:-:|---|:-:|---|---|---|
| 1 | Ward | 0..1 | *has* | 0..* | User | `users.ward_id` (FK, NULL được) | `CE0` |
| 2 | User | 0..* | *plays* | 0..* | Role | `user_roles` | `CE1` |
| 3 | Role | 0..* | *grants* | 0..* | Permission | `role_permissions` | `CE2` |
| 4 | IT Admin | 1 | *manages* | 0..* | Role | API RBAC chỉ IT_ADMIN; ghi `rbac_audit_logs` | `CE3` |
| 5 | Procedure Category | 1 | *classifies* | 0..* | Procedure | `procedures.category_id` (FK) | `CE4` |
| 6 | Front Desk Officer | 0..* | *specializes in* | 0..* | Procedure Category | `users.assigned_categories` (mảng ID) | `CE5` |
| 7 | Procedure | 1 | *has* | 0..* | Procedure Version | `procedure_versions.procedure_id` (FK) | `CE6` |
| 8 | Procedure Manager | 1 | *uploads* | 0..* | Procedure Draft | `procedure_drafts.created_by` | `CE7` |
| 9 | Procedure | 0..1 | *is published from* | 0..* | Procedure Draft | `procedure_drafts.published_procedure_id` | `CE8` |
| 10 | Procedure | 0..* | *uses* | 0..* | Form Template | `procedures.form_definitions` (JSON) | `CE9` |
| 11 | Citizen | 1 | *submits* | 0..* | Application | `applications.user_id` | `CE10` |
| 12 | Front Desk Officer | 0..1 | *handles* | 0..* | Application | `applications.assigned_officer_id` (NULL = chưa ai nhận) | `CE11` |
| 13 | Ward | 0..1 | *receives* | 0..* | Application | `applications.ward_code` | `CE12` |
| 14 | Procedure | 1 | *is applied for in* | 0..* | Application | `applications.procedure_id` | `CE13` |
| 15 | Application | 1 | *has* | 0..* | Application Version | `application_versions.application_id` (FK) | `CE14` |
| 16 | Application | 1 | *contains* | 0..* | Checklist Item | `application_checklists.application_id` (FK) | `CE15` |
| 17 | Application Version | 1 | *receives* | 0..* | Review Comment | `application_comments.(application_id, application_version_id)` (FK kép) | `CE16` |
| 18 | Front Desk Officer | 1 | *writes* | 0..* | Review Comment | `application_comments.officer_id` | `CE17` |
| 19 | Application | 1 | *logs* | 0..* | Status Change | `application_status_history.application_id` (FK) | `CE18` |
| 20 | User | 1 | *makes* | 0..* | Status Change | `application_status_history.changed_by` | `CE19` |
| 21 | Application | 1 | *logs* | 0..* | Assignment Record | `application_assignment_history.application_id` (FK) | `CE20` |
| 22 | User | 1 | *performs* | 0..* | Assignment Record | `application_assignment_history.actor_id` | `CE21` |
| 23 | Manager | 0..1 | *reassigns* | 0..* | Assignment Record | action ASSIGN/TRANSFER, quyền `workflow.assign.manage` | `CE22` |
| 24 | Form Template | 0..1 | *is required by* | 0..* | Checklist Item | `application_checklists.form_template_id` | `CE23` |
| 25 | Form Template | 1 | *has* | 0..* | Form Template Version | `form_template_versions.template_id` (FK) | `CE24` |
| 26 | Form Template Version | 0..1 | *is filled as* | 0..* | Form Submission | `user_submissions.template_version_id` (FK, NULL được) | `CE25` |
| 27 | Citizen | 1 | *fills* | 0..* | Form Submission | `user_submissions.applicant_id` | `CE26` |
| 28 | Checklist Item | 0..1 | *is fulfilled by* | 0..1 | Form Submission | `user_submissions.checklist_id` + UNIQUE(application_id, checklist_id) | `CE27` |
| 29 | Application | 1 | *stores* | 0..* | Application Form Data | `application_forms.application_id` | `CE28` |
| 30 | Checklist Item | 0..1 | *is proven by* | 0..* | Supporting Document | `supporting_documents.checklist_id` | `CE29` |
| 31 | Application | 1 | *attaches* | 0..* | Supporting Document | `supporting_documents.application_id` | `CE30` |
| 32 | Application | 1 | *produces* | 0..* | Generated Document | `generated_documents.application_id` | `CE31` |
| 33 | Application | 1 | *is identified by* | 0..1 | Approved QR | `approved_qrs.application_id` (UNIQUE) | `CE32` |
| 34 | Citizen | 1 | *opens* | 0..* | Chat Session | `chat_sessions.user_id` | `CE33` |
| 35 | Chat Session | 1 | *contains* | 0..* | Chat Message | `chat_messages.session_id` (FK) | `CE34` |
| 36 | Procedure Manager | 1 | *manages* | 0..* | Knowledge Document | `knowledge_documents.created_by/updated_by`, quyền `knowledge.manage` | `CE35` |
| 37 | Procedure | 0..1 | *is indexed as* | 0..1 | Knowledge Document | `knowledge_documents.procedure_id` (UNIQUE khi source_type = PROCEDURE) | `CE36` |
| 38 | Knowledge Document | 1 | *is split into* | 0..* | Knowledge Chunk | `knowledge_chunks.document_id` (FK) | `CE37` |
| 39 | Chat Message | 0..* | *cites* | 0..* | Knowledge Chunk | `chat_messages.citations` (JSON) | `CE38` |
| 40 | User | 1 | *receives* | 0..* | Notification | `notifications.recipient_user_id` | `CE39` |
| 41 | Application | 0..1 | *triggers* | 0..* | Notification | `notifications.application_id` (NULL được) | `CE40` |
| 42 | Citizen | 1 | *gives* | 0..* | Citizen Feedback | `citizen_feedback.CitizenId` | `CE41` |
| 43 | Application | 1 | *is rated by* | 0..1 | Citizen Feedback | `citizen_feedback.ApplicationId` (UNIQUE) | `CE42` |
| 44 | Front Desk Officer | 1 | *is measured by* | 0..* | Performance Statistic | `daily_performance_metrics.OfficerId` | `CE43` |
| 45 | Procedure | 1 | *is measured by* | 0..* | Performance Statistic | `*_metrics.ProcedureId` | `CE44` |
| 46 | Ward | 1 | *is measured by* | 0..* | Performance Statistic | `*_metrics.WardCode` | `CE45` |

Đọc ví dụ dòng 11: *Citizen (1) submits Application (0..*)* – một công dân nộp 0..n hồ sơ, mỗi hồ sơ do đúng 1 công dân nộp.

## 4. Trang 2 – Logical ERD

Quy tắc đã áp dụng (theo Visual Paradigm):

- Mỗi thực thể ứng với 1 bảng Physical, **trừ** `user_roles` và `role_permissions`: hai bảng này được thay bằng quan hệ N–N trực tiếp *User –is assigned– Role* và *Role –grants– Permission*.
- Thuộc tính lấy theo **tên property C#** (ví dụ `ApplicationCode`), không ghi kiểu, không đánh dấu khóa.
- Cột khóa ngoại/tham chiếu (ví dụ `UserId`, `WardCode`, `ProcedureId`) **không** liệt kê thành thuộc tính vì đã thể hiện bằng đường quan hệ. Riêng các cột JSON có chứa ID (`FormDefinitions`, `Citations`, `Payload`) vẫn giữ vì chúng còn chứa dữ liệu khác.
- Không chia theo service (Logical không phụ thuộc cách triển khai), nên các quan hệ xuyên service vẽ như quan hệ thường.

### 4.1 Thực thể Logical ↔ bảng Physical

| Thực thể Logical | Bảng Physical | Số thuộc tính | Thuộc tính đã thay bằng quan hệ |
|---|---|--:|---|
| ChatMessage | `chat_messages` | 5 | `SessionId` |
| ChatSession | `chat_sessions` | 2 | `UserId`, `WardCode` |
| KnowledgeChunk | `knowledge_chunks` | 10 | `DocumentId` |
| KnowledgeDocument | `knowledge_documents` | 20 | `CreatedBy`, `ProcedureId`, `UpdatedBy` |
| CitizenFeedback | `citizen_feedback` | 4 | `ApplicationId`, `CitizenId`, `ProcedureId`, `WardCode` |
| MetricRefresh | `metric_refreshes` | 3 | – |
| OfficerDailyMetric | `daily_performance_metrics` | 5 | `WardCode`, `OfficerId`, `ProcedureId` |
| ProcedureDailyMetric | `application_procedure_metrics` | 8 | `WardCode`, `ProcedureId` |
| Notification | `notifications` | 9 | `ApplicationId`, `RecipientUserId`, `RecipientWardCode`, `SourceEventId` |
| NotificationDeliveryAudit | `notification_delivery_audits` | 12 | `ActorId`, `DeliveryId` |
| NotificationDelivery | `notification_deliveries` | 12 | `NotificationId` |
| NotificationInbox | `notification_inbox` | 7 | – |
| ApplicationAssignmentHistory | `application_assignment_history` | 4 | `ActorId`, `ApplicationId`, `FromOfficerId`, `ToOfficerId` |
| ApplicationChecklistItem | `application_checklists` | 15 | `ApplicationId`, `FormTemplateId` |
| ApplicationComment | `application_comments` | 8 | `ApplicationId`, `ApplicationVersionId`, `OfficerId` |
| Application | `applications` | 30 | `AssignedOfficerId`, `CategoryId`, `ProcedureId`, `UserId`, `WardCode` |
| ApplicationStatusHistory | `application_status_history` | 5 | `ApplicationId`, `ChangedBy` |
| ApplicationVersion | `application_versions` | 4 | `ApplicationId`, `SubmittedBy` |
| WorkflowOutbox | `workflow_outbox` | 7 | – |
| ApplicationForm | `application_forms` | 8 | `ApplicationId` |
| ApprovedQr | `approved_qrs` | 4 | `ApplicationId`, `SubmissionId` |
| FormTemplate | `form_templates` | 11 | – |
| FormTemplateVersion | `form_template_versions` | 12 | `TemplateId` |
| GeneratedDocument | `generated_documents` | 9 | `ApplicationId`, `SubmissionId` |
| SupportingDocument | `supporting_documents` | 14 | `ApplicationId`, `ChecklistId` |
| FormSubmission | `user_submissions` | 15 | `ApplicantId`, `ApplicationId`, `ChecklistId`, `ReviewedByOfficerId`, `TemplateId`, `TemplateVersionId` |
| Permission | `permissions` | 4 | – |
| RbacAuditLog | `rbac_audit_logs` | 4 | `ActorUserId`, `PermissionId`, `RoleId`, `TargetUserId` |
| RefreshToken | `refresh_tokens` | 5 | `UserId` |
| Role | `roles` | 3 | – |
| User | `users` | 9 | `AssignedCategories`, `WardId` |
| UserOtp | `user_otps` | 9 | `UserId` |
| UserProfile | `user_profiles` | 8 | `UserId` |
| Ward | `wards` | 3 | – |
| WelcomeEmail | `welcome_emails` | 4 | `UserId` |
| Procedure | `procedures` | 18 | `CategoryId` |
| ProcedureCategory | `procedure_categories` | 3 | – |
| ProcedureDraft | `procedure_drafts` | 15 | `CreatedBy`, `PublishedProcedureId`, `ReviewedBy` |
| ProcedureVersion | `procedure_versions` | 8 | `ProcedureId` |

Danh sách thuộc tính đầy đủ của từng thực thể: xem cột **Thuộc tính Logical** trong các bảng ở mục 5.

### 4.2 Quan hệ Logical

| # | Thực thể "1" | Bản số | Động từ | Bản số | Thực thể "nhiều" | Nguồn | ID cạnh |
|--:|---|:-:|---|:-:|---|---|---|
| 1 | ChatSession | 1 | *contains* | 0..* | ChatMessage | FK | `LE0` |
| 2 | KnowledgeDocument | 1 | *is split into* | 0..* | KnowledgeChunk | FK | `LE1` |
| 3 | Notification | 1 | *is delivered via* | 0..* | NotificationDelivery | FK | `LE2` |
| 4 | Application | 1 | *logs assignment* | 0..* | ApplicationAssignmentHistory | FK | `LE3` |
| 5 | Application | 1 | *contains* | 0..* | ApplicationChecklistItem | FK | `LE4` |
| 6 | Application | 1 | *has* | 0..* | ApplicationComment | FK | `LE5` |
| 7 | ApplicationVersion | 1 | *receives* | 0..* | ApplicationComment | FK | `LE6` |
| 8 | Application | 1 | *logs status* | 0..* | ApplicationStatusHistory | FK | `LE7` |
| 9 | Application | 1 | *has* | 0..* | ApplicationVersion | FK | `LE8` |
| 10 | FormTemplate | 1 | *has* | 0..* | FormTemplateVersion | FK | `LE9` |
| 11 | FormTemplateVersion | 0..1 | *is filled as* | 0..* | FormSubmission | FK | `LE10` |
| 12 | User | 1 | *owns* | 0..* | RefreshToken | FK | `LE11` |
| 13 | Ward | 0..1 | *has* | 0..* | User | FK | `LE12` |
| 14 | User | 1 | *requests* | 0..* | UserOtp | FK | `LE13` |
| 15 | User | 1 | *has* | 0..1 | UserProfile | FK | `LE14` |
| 16 | User | 1 | *is sent* | 0..1 | WelcomeEmail | FK | `LE15` |
| 17 | ProcedureCategory | 1 | *classifies* | 0..* | Procedure | FK | `LE16` |
| 18 | Procedure | 1 | *has* | 0..* | ProcedureVersion | FK | `LE17` |
| 19 | User | 0..* | *is assigned* | 0..* | Role | N–N (bảng trung gian) | `LE18` |
| 20 | Role | 0..* | *grants* | 0..* | Permission | N–N (bảng trung gian) | `LE19` |
| 21 | User | 1 | *performs (actor)* | 0..* | RbacAuditLog | REF | `LE20` |
| 22 | User | 0..1 | *is target of* | 0..* | RbacAuditLog | REF | `LE21` |
| 23 | Role | 0..1 | *is audited in* | 0..* | RbacAuditLog | REF | `LE22` |
| 24 | Permission | 0..1 | *is audited in* | 0..* | RbacAuditLog | REF | `LE23` |
| 25 | ProcedureCategory | 0..* | *specializes in* | 0..* | User | REF | `LE24` |
| 26 | User | 1 | *uploads* | 0..* | ProcedureDraft | REF | `LE25` |
| 27 | User | 0..1 | *reviews* | 0..* | ProcedureDraft | REF | `LE26` |
| 28 | Procedure | 0..1 | *is published from* | 0..* | ProcedureDraft | REF | `LE27` |
| 29 | FormTemplate | 0..* | *uses* | 0..* | Procedure | REF | `LE28` |
| 30 | User | 1 | *submits (applicant)* | 0..* | Application | REF | `LE29` |
| 31 | User | 0..1 | *handles (assigned officer)* | 0..* | Application | REF | `LE30` |
| 32 | Ward | 0..1 | *receives* | 0..* | Application | REF | `LE31` |
| 33 | Procedure | 1 | *is applied for in* | 0..* | Application | REF | `LE32` |
| 34 | ProcedureCategory | 0..1 | *categorizes (snapshot)* | 0..* | Application | REF | `LE33` |
| 35 | User | 1 | *submits version* | 0..* | ApplicationVersion | REF | `LE34` |
| 36 | User | 1 | *writes* | 0..* | ApplicationComment | REF | `LE35` |
| 37 | User | 1 | *changes status* | 0..* | ApplicationStatusHistory | REF | `LE36` |
| 38 | User | 1 | *performs assignment* | 0..* | ApplicationAssignmentHistory | REF | `LE37` |
| 39 | User | 0..1 | *is released from* | 0..* | ApplicationAssignmentHistory | REF | `LE38` |
| 40 | User | 0..1 | *is assigned to* | 0..* | ApplicationAssignmentHistory | REF | `LE39` |
| 41 | FormTemplate | 0..1 | *is required by* | 0..* | ApplicationChecklistItem | REF | `LE40` |
| 42 | Application | 1 | *emits event* | 0..* | WorkflowOutbox | REF | `LE41` |
| 43 | User | 1 | *fills* | 0..* | FormSubmission | REF | `LE42` |
| 44 | User | 0..1 | *reviews* | 0..* | FormSubmission | REF | `LE43` |
| 45 | FormTemplate | 1 | *is filled from* | 0..* | FormSubmission | REF | `LE44` |
| 46 | Application | 0..1 | *includes* | 0..* | FormSubmission | REF | `LE45` |
| 47 | ApplicationChecklistItem | 0..1 | *is fulfilled by* | 0..* | FormSubmission | REF | `LE46` |
| 48 | Application | 1 | *stores form data in* | 0..* | ApplicationForm | REF | `LE47` |
| 49 | Application | 1 | *attaches* | 0..* | SupportingDocument | REF | `LE48` |
| 50 | ApplicationChecklistItem | 0..1 | *is proven by* | 0..* | SupportingDocument | REF | `LE49` |
| 51 | Application | 1 | *produces* | 0..* | GeneratedDocument | REF | `LE50` |
| 52 | FormSubmission | 0..1 | *is rendered as* | 0..* | GeneratedDocument | REF | `LE51` |
| 53 | Application | 1 | *is identified by* | 0..1 | ApprovedQr | REF | `LE52` |
| 54 | FormSubmission | 1 | *is encoded in* | 0..* | ApprovedQr | REF | `LE53` |
| 55 | User | 1 | *opens* | 0..* | ChatSession | REF | `LE54` |
| 56 | Ward | 1 | *scopes* | 0..* | ChatSession | REF | `LE55` |
| 57 | User | 1 | *creates* | 0..* | KnowledgeDocument | REF | `LE56` |
| 58 | User | 1 | *updates* | 0..* | KnowledgeDocument | REF | `LE57` |
| 59 | Procedure | 0..1 | *is indexed as* | 0..1 | KnowledgeDocument | REF | `LE58` |
| 60 | KnowledgeChunk | 0..* | *is cited by* | 0..* | ChatMessage | REF | `LE59` |
| 61 | User | 1 | *receives* | 0..* | Notification | REF | `LE60` |
| 62 | Ward | 0..1 | *scopes* | 0..* | Notification | REF | `LE61` |
| 63 | Application | 0..1 | *triggers* | 0..* | Notification | REF | `LE62` |
| 64 | NotificationInbox | 0..1 | *creates* | 0..* | Notification | REF | `LE63` |
| 65 | WorkflowOutbox | 1 | *is consumed as* | 0..1 | NotificationInbox | REF | `LE64` |
| 66 | NotificationDelivery | 1 | *is audited in* | 0..* | NotificationDeliveryAudit | REF | `LE65` |
| 67 | User | 1 | *administers* | 0..* | NotificationDeliveryAudit | REF | `LE66` |
| 68 | Application | 1 | *is rated by* | 0..1 | CitizenFeedback | REF | `LE67` |
| 69 | User | 1 | *gives* | 0..* | CitizenFeedback | REF | `LE68` |
| 70 | Procedure | 1 | *is rated in* | 0..* | CitizenFeedback | REF | `LE69` |
| 71 | Ward | 1 | *collects* | 0..* | CitizenFeedback | REF | `LE70` |
| 72 | User | 1 | *is measured in* | 0..* | OfficerDailyMetric | REF | `LE71` |
| 73 | Procedure | 1 | *is measured in* | 0..* | OfficerDailyMetric | REF | `LE72` |
| 74 | Ward | 1 | *is measured in* | 0..* | OfficerDailyMetric | REF | `LE73` |
| 75 | Procedure | 1 | *is measured in* | 0..* | ProcedureDailyMetric | REF | `LE74` |
| 76 | Ward | 1 | *is measured in* | 0..* | ProcedureDailyMetric | REF | `LE75` |

## 5. Trang 3 – Physical ERD (từng bảng)

Mỗi bảng dưới đây có: mô tả, danh sách cột (đúng thứ tự trên sơ đồ), ràng buộc UNIQUE/CHECK (CHECK **không vẽ** trên sơ đồ, chỉ ghi ở đây), và các quan hệ đi vào/ra. Cột **Thuộc tính Logical** cho biết cột đó hiện ở trang Logical dưới tên gì ("→ quan hệ" = thể hiện bằng đường nối).

### IAM DB (`IamDbContext`)

#### `permissions`

Quyền chi tiết (ví dụ `workflow.assign.manage`, `knowledge.manage`), nhóm theo `module`. Entity C#: `Permission`. ID trong draw.io: `P_permissions` (Physical), `L_permissions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `integer` | NOT NULL | identity (tự tăng) |  | `Id` |
|  | `module` | `varchar(50)` | NOT NULL |  |  | `Module` |
| UK | `permission_code` | `varchar(100)` | NOT NULL |  |  | `PermissionCode` |
|  | `permission_name` | `varchar(255)` | NOT NULL |  |  | `PermissionName` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `permissions` (1) → `role_permissions` (0..*) — FK permission_id — `PE12`
- `permissions` (0..1) → `rbac_audit_logs` (0..*) — REF permission_id — `PE25`

#### `rbac_audit_logs`

Nhật ký thay đổi phân quyền (ai đổi, đổi cho ai, role/permission nào). Entity C#: `RbacAuditLog`. ID trong draw.io: `P_rbac_audit_logs` (Physical), `L_rbac_audit_logs` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `action` | `varchar(100)` | NOT NULL |  |  | `Action` |
|  | `actor_user_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `details` | `jsonb` | NOT NULL |  |  | `Details` |
|  | `permission_id` | `integer` | NULL |  | REF → `permissions.id` | → quan hệ |
|  | `role_id` | `integer` | NULL |  | REF → `roles.id` | → quan hệ |
|  | `target_user_id` | `uuid` | NULL |  | REF → `users.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `rbac_audit_logs` (0..*) — REF actor_user_id — `PE22`
- `users` (0..1) → `rbac_audit_logs` (0..*) — REF target_user_id — `PE23`
- `roles` (0..1) → `rbac_audit_logs` (0..*) — REF role_id — `PE24`
- `permissions` (0..1) → `rbac_audit_logs` (0..*) — REF permission_id — `PE25`

#### `refresh_tokens`

Refresh token đăng nhập (rotation/revoke). Entity C#: `RefreshToken`. ID trong draw.io: `P_refresh_tokens` (Physical), `L_refresh_tokens` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `expires_at` | `timestamptz` | NOT NULL |  |  | `ExpiresAt` |
|  | `is_revoked` | `boolean` | NOT NULL | false |  | `IsRevoked` |
| UK | `token` | `varchar(500)` | NOT NULL |  |  | `Token` |
| FK | `user_id` | `uuid` | NOT NULL |  | FK → `users.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `refresh_tokens` (0..*) — FK user_id — `PE11`

#### `roles`

Vai trò RBAC: REGISTERED_CITIZEN, FRONT_DESK_OFFICER, MANAGER, PROCEDURE_MANAGER, IT_ADMIN. Entity C#: `Role`. ID trong draw.io: `P_roles` (Physical), `L_roles` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `integer` | NOT NULL | identity (tự tăng) |  | `Id` |
|  | `description` | `text` | NULL |  |  | `Description` |
| UK | `role_name` | `varchar(50)` | NOT NULL |  |  | `RoleName` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `roles` (1) → `role_permissions` (0..*) — FK role_id — `PE13`
- `roles` (1) → `user_roles` (0..*) — FK role_id — `PE17`
- `roles` (0..1) → `rbac_audit_logs` (0..*) — REF role_id — `PE24`

#### `role_permissions`

Bảng trung gian N–N giữa `roles` và `permissions`. Entity C#: `RolePermission`. ID trong draw.io: `P_role_permissions` (Physical); không có ở Logical (thay bằng quan hệ N–N).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK,FK | `role_id` | `integer` | NOT NULL |  | FK → `roles.id` | → quan hệ N–N |
| PK,FK | `permission_id` | `integer` | NOT NULL |  | FK → `permissions.id` | → quan hệ N–N |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `permissions` (1) → `role_permissions` (0..*) — FK permission_id — `PE12`
- `roles` (1) → `role_permissions` (0..*) — FK role_id — `PE13`

#### `users`

Tài khoản đăng nhập của mọi vai trò. `ward_id` = phường trực thuộc; `assigned_categories` = các lĩnh vực cán bộ phụ trách (mảng rỗng = mọi lĩnh vực). Entity C#: `User`. ID trong draw.io: `P_users` (Physical), `L_users` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `assigned_categories` | `integer[]` | NOT NULL | ARRAY[]::integer[] | REF → `procedure_categories.id (mảng)` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
| UK | `email` | `varchar(255)` | NOT NULL |  |  | `Email` |
|  | `is_active` | `boolean` | NOT NULL | true |  | `IsActive` |
|  | `password_hash` | `varchar(255)` | NOT NULL |  |  | `PasswordHash` |
|  | `security_stamp` | `uuid` | NOT NULL |  |  | `SecurityStamp` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |
| UK | `username` | `varchar(100)` | NOT NULL |  |  | `Username` |
|  | `verified_phone_number` | `text` | NULL |  |  | `VerifiedPhoneNumber` |
| FK | `ward_id` | `uuid` | NULL |  | FK → `wards.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `refresh_tokens` (0..*) — FK user_id — `PE11`
- `users` (1) → `user_otps` (0..*) — FK user_id — `PE15`
- `users` (1) → `user_profiles` (0..1) — FK user_id — `PE16`
- `users` (1) → `user_roles` (0..*) — FK user_id — `PE18`
- `users` (1) → `welcome_emails` (0..1) — FK user_id — `PE19`
- `users` (1) → `rbac_audit_logs` (0..*) — REF actor_user_id — `PE22`
- `users` (0..1) → `rbac_audit_logs` (0..*) — REF target_user_id — `PE23`
- `users` (1) → `procedure_drafts` (0..*) — REF created_by — `PE27`
- `users` (0..1) → `procedure_drafts` (0..*) — REF reviewed_by — `PE28`
- `users` (1) → `applications` (0..*) — REF user_id — `PE31`
- `users` (0..1) → `applications` (0..*) — REF assigned_officer_id — `PE32`
- `users` (1) → `application_versions` (0..*) — REF submitted_by — `PE36`
- `users` (1) → `application_comments` (0..*) — REF officer_id — `PE37`
- `users` (1) → `application_status_history` (0..*) — REF changed_by — `PE38`
- `users` (1) → `application_assignment_history` (0..*) — REF actor_id — `PE39`
- `users` (0..1) → `application_assignment_history` (0..*) — REF from_officer_id — `PE40`
- `users` (0..1) → `application_assignment_history` (0..*) — REF to_officer_id — `PE41`
- `users` (1) → `user_submissions` (0..*) — REF applicant_id — `PE44`
- `users` (0..1) → `user_submissions` (0..*) — REF reviewed_by_officer_id — `PE45`
- `users` (1) → `chat_sessions` (0..*) — REF user_id — `PE56`
- `users` (1) → `knowledge_documents` (0..*) — REF created_by — `PE58`
- `users` (1) → `knowledge_documents` (0..*) — REF updated_by — `PE59`
- `users` (1) → `notifications` (0..*) — REF recipient_user_id — `PE62`
- `users` (1) → `notification_delivery_audits` (0..*) — REF ActorId — `PE68`
- `users` (1) → `citizen_feedback` (0..*) — REF CitizenId — `PE70`
- `users` (1) → `daily_performance_metrics` (0..*) — REF OfficerId — `PE73`
- `wards` (0..1) → `users` (0..*) — FK ward_id — `PE14`
- `procedure_categories` (0..*) → `users` (0..*) — REF (array) assigned_categories — `PE26`

#### `iam.user_otps`

OTP đổi mật khẩu/quên mật khẩu/xác minh số điện thoại (lưu hash, tối đa 5 lần sai). Entity C#: `UserOtp`. ID trong draw.io: `P_user_otps` (Physical), `L_user_otps` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `attempts` | `integer` | NOT NULL |  |  | `Attempts` |
|  | `channel` | `varchar(10)` | NOT NULL |  |  | `Channel` |
|  | `created_at_utc` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `expires_at_utc` | `timestamptz` | NOT NULL |  |  | `ExpiresAtUtc` |
|  | `is_used` | `boolean` | NOT NULL |  |  | `IsUsed` |
|  | `otp_hash` | `varchar(64)` | NOT NULL |  |  | `OtpHash` |
|  | `purpose` | `varchar(30)` | NOT NULL |  |  | `Purpose` |
|  | `target_phone_number` | `varchar(20)` | NULL |  |  | `TargetPhoneNumber` |
| FK | `user_id` | `uuid` | NOT NULL |  | FK → `users.id` | → quan hệ |

Ràng buộc:

- CHECK `ck_user_otps_attempts`: `attempts BETWEEN 0 AND 5`
- CHECK `ck_user_otps_channel`: `channel IN ('EMAIL','SMS')`
- CHECK `ck_user_otps_purpose`: `purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD','VERIFY_PHONE')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `user_otps` (0..*) — FK user_id — `PE15`

#### `user_profiles`

Thông tin cá nhân, quan hệ 1–1 với `users` (PK cũng là FK `user_id`). Entity C#: `UserProfile`. ID trong draw.io: `P_user_profiles` (Physical), `L_user_profiles` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK,FK | `user_id` | `uuid` | NOT NULL |  | FK → `users.id` | → quan hệ |
|  | `date_of_birth` | `date` | NULL |  |  | `DateOfBirth` |
|  | `full_name` | `varchar(255)` | NOT NULL |  |  | `FullName` |
|  | `gender` | `varchar(10)` | NULL |  |  | `Gender` |
| UK | `identity_number` | `varchar(20)` | NULL |  |  | `IdentityNumber` |
|  | `permanent_address` | `text` | NULL |  |  | `PermanentAddress` |
|  | `phone_number` | `varchar(20)` | NULL |  |  | `PhoneNumber` |
|  | `temporary_address` | `text` | NULL |  |  | `TemporaryAddress` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `user_profiles` (0..1) — FK user_id — `PE16`

#### `user_roles`

Bảng trung gian N–N giữa `users` và `roles`. Entity C#: `UserRole`. ID trong draw.io: `P_user_roles` (Physical); không có ở Logical (thay bằng quan hệ N–N).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK,FK | `user_id` | `uuid` | NOT NULL |  | FK → `users.id` | → quan hệ N–N |
| PK,FK | `role_id` | `integer` | NOT NULL |  | FK → `roles.id` | → quan hệ N–N |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `roles` (1) → `user_roles` (0..*) — FK role_id — `PE17`
- `users` (1) → `user_roles` (0..*) — FK user_id — `PE18`

#### `wards`

Danh mục phường/xã. Các service khác tham chiếu bằng `code` (không phải `id`). Entity C#: `Ward`. ID trong draw.io: `P_wards` (Physical), `L_wards` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| UK | `code` | `varchar(50)` | NOT NULL |  |  | `Code` |
|  | `name` | `varchar(255)` | NOT NULL |  |  | `Name` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `wards` (0..1) → `users` (0..*) — FK ward_id — `PE14`
- `wards` (0..1) → `applications` (0..*) — REF ward_code — `PE33`
- `wards` (1) → `chat_sessions` (0..*) — REF ward_code — `PE57`
- `wards` (0..1) → `notifications` (0..*) — REF recipient_ward_code — `PE63`
- `wards` (1) → `citizen_feedback` (0..*) — REF WardCode — `PE72`
- `wards` (1) → `daily_performance_metrics` (0..*) — REF WardCode — `PE75`
- `wards` (1) → `application_procedure_metrics` (0..*) — REF WardCode — `PE77`

#### `iam.welcome_emails`

Hàng đợi gửi email chào mừng, tối đa 1 bản ghi mỗi user. Entity C#: `WelcomeEmail`. ID trong draw.io: `P_welcome_emails` (Physical), `L_welcome_emails` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `attempts` | `integer` | NOT NULL |  |  | `Attempts` |
|  | `next_attempt_at_utc` | `timestamptz` | NOT NULL |  |  | `NextAttemptAtUtc` |
|  | `sent_at_utc` | `timestamptz` | NULL |  |  | `SentAtUtc` |
| FK,UK | `user_id` | `uuid` | NOT NULL |  | FK → `users.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `welcome_emails` (0..1) — FK user_id — `PE19`

### Catalog DB (`ProcedureDbContext`)

#### `procedures`

Thủ tục hành chính đang công bố; nội dung, checklist giấy tờ và biểu mẫu lưu JSONB. Entity C#: `Procedure`. ID trong draw.io: `P_procedures` (Physical), `L_procedures` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | gen_random_uuid() |  | `Id` |
| FK | `category_id` | `integer` | NOT NULL |  | FK → `procedure_categories.id` | → quan hệ |
|  | `checklist_schema` | `jsonb` | NULL |  |  | `ChecklistSchema` |
|  | `content_payload` | `jsonb` | NOT NULL |  |  | `ContentPayload` |
|  | `created_at` | `timestamptz` | NOT NULL | now() |  | `CreatedAt` |
|  | `executing_agency` | `text` | NULL |  |  | `ExecutingAgency` |
|  | `fee_summary` | `varchar(255)` | NOT NULL | 'Miễn phí' |  | `FeeSummary` |
|  | `form_definitions` | `jsonb` | NULL |  | REF → `form_templates.id (JSON)` | `FormDefinitions` |
|  | `is_active` | `boolean` | NOT NULL | true |  | `IsActive` |
|  | `issuing_authority` | `varchar(255)` | NULL |  |  | `IssuingAuthority` |
|  | `level_of_implementation` | `varchar(50)` | NOT NULL | 'Cấp Xã' |  | `LevelOfImplementation` |
|  | `original_pdf_url` | `varchar(500)` | NULL |  |  | `OriginalPdfUrl` |
|  | `pdf_file_name` | `varchar(255)` | NULL |  |  | `PdfFileName` |
| UK | `procedure_code` | `varchar(50)` | NOT NULL |  |  | `ProcedureCode` |
|  | `processing_time_summary` | `varchar(255)` | NOT NULL | '1 ngày' |  | `ProcessingTimeSummary` |
|  | `status_change_reason` | `text` | NULL |  |  | `StatusChangeReason` |
|  | `target_audience` | `varchar(255)` | NOT NULL | 'Công dân Việt Nam' |  | `TargetAudience` |
|  | `title` | `varchar(500)` | NOT NULL |  |  | `Title` |
|  | `updated_at` | `timestamptz` | NOT NULL | now() |  | `UpdatedAt` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `procedures` (1) → `procedure_versions` (0..*) — FK procedure_id — `PE21`
- `procedures` (0..1) → `procedure_drafts` (0..*) — REF published_procedure_id — `PE29`
- `procedures` (1) → `applications` (0..*) — REF procedure_id — `PE34`
- `procedures` (0..1) → `knowledge_documents` (0..1) — REF procedure_id — `PE60`
- `procedures` (1) → `citizen_feedback` (0..*) — REF ProcedureId — `PE71`
- `procedures` (1) → `daily_performance_metrics` (0..*) — REF ProcedureId — `PE74`
- `procedures` (1) → `application_procedure_metrics` (0..*) — REF ProcedureId — `PE76`
- `procedure_categories` (1) → `procedures` (0..*) — FK category_id — `PE20`
- `form_templates` (0..*) → `procedures` (0..*) — REF (JSON) form_definitions — `PE30`

#### `procedure_categories`

Lĩnh vực thủ tục (ví dụ Hộ tịch, Chứng thực). Entity C#: `ProcedureCategory`. ID trong draw.io: `P_procedure_categories` (Physical), `L_procedure_categories` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `integer` | NOT NULL | identity (tự tăng) |  | `Id` |
|  | `category_name` | `varchar(255)` | NOT NULL |  |  | `CategoryName` |
|  | `description` | `text` | NULL |  |  | `Description` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `procedure_categories` (1) → `procedures` (0..*) — FK category_id — `PE20`
- `procedure_categories` (0..*) → `users` (0..*) — REF (array) assigned_categories — `PE26`
- `procedure_categories` (0..1) → `applications` (0..*) — REF category_id — `PE35`

#### `procedure_drafts`

Bản nháp thủ tục tạo từ PDF + AI bóc tách; khi publish thì ghi `published_procedure_id`. Entity C#: `ProcedureDraft`. ID trong draw.io: `P_procedure_drafts` (Physical), `L_procedure_drafts` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `attempts` | `integer` | NOT NULL |  |  | `Attempts` |
|  | `blob_name` | `varchar(255)` | NOT NULL |  |  | `BlobName` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `created_by` | `varchar(255)` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `extracted_text` | `text` | NOT NULL |  |  | `ExtractedText` |
|  | `failure_code` | `varchar(100)` | NULL |  |  | `FailureCode` |
|  | `lease_id` | `uuid` | NULL |  |  | `LeaseId` |
|  | `lease_until` | `timestamptz` | NULL |  |  | `LeaseUntil` |
|  | `original_pdf_url` | `varchar(500)` | NOT NULL |  |  | `OriginalPdfUrl` |
|  | `payload_json` | `jsonb` | NOT NULL |  |  | `PayloadJson` |
|  | `pdf_file_name` | `varchar(255)` | NOT NULL |  |  | `PdfFileName` |
|  | `published_procedure_id` | `uuid` | NULL |  | REF → `procedures.id` | → quan hệ |
|  | `reviewed_by` | `varchar(255)` | NULL |  | REF → `users.id` | → quan hệ |
|  | `revision` | `uuid` | NOT NULL |  |  | `Revision` |
|  | `status` | `varchar(30)` | NOT NULL |  |  | `Status` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |
|  | `warnings_json` | `jsonb` | NOT NULL |  |  | `WarningsJson` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `procedure_drafts` (0..*) — REF created_by — `PE27`
- `users` (0..1) → `procedure_drafts` (0..*) — REF reviewed_by — `PE28`
- `procedures` (0..1) → `procedure_drafts` (0..*) — REF published_procedure_id — `PE29`

#### `procedure_versions`

Lịch sử phiên bản thủ tục (snapshot JSON, số quyết định, ngày hiệu lực). Entity C#: `ProcedureVersion`. ID trong draw.io: `P_procedure_versions` (Physical), `L_procedure_versions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | gen_random_uuid() |  | `Id` |
|  | `created_at` | `timestamptz` | NOT NULL | now() |  | `CreatedAt` |
|  | `decision_number` | `varchar(100)` | NULL |  |  | `DecisionNumber` |
|  | `effective_date` | `date` | NOT NULL |  |  | `EffectiveDate` |
|  | `original_pdf_url` | `varchar(500)` | NULL |  |  | `OriginalPdfUrl` |
|  | `pdf_file_name` | `varchar(255)` | NULL |  |  | `PdfFileName` |
| FK,U1 | `procedure_id` | `uuid` | NOT NULL |  | FK → `procedures.id` | → quan hệ |
|  | `snapshot_data` | `jsonb` | NOT NULL |  |  | `SnapshotData` |
| U1 | `version_number` | `integer` | NOT NULL |  |  | `VersionNumber` |

Ràng buộc:

- `U1` = UNIQUE(procedure_id, version_number)

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `procedures` (1) → `procedure_versions` (0..*) — FK procedure_id — `PE21`

### Workflow DB (`WorkflowDbContext`)

#### `application_assignment_history`

Lịch sử phân công: CLAIM/ASSIGN/TRANSFER/RELEASE, từ cán bộ nào sang cán bộ nào. Entity C#: `ApplicationAssignmentHistory`. ID trong draw.io: `P_application_assignment_history` (Physical), `L_application_assignment_history` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `action` | `varchar(20)` | NOT NULL |  |  | `Action` |
|  | `actor_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
| FK | `application_id` | `uuid` | NOT NULL |  | FK → `applications.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `from_officer_id` | `uuid` | NULL |  | REF → `users.id` | → quan hệ |
|  | `reason` | `varchar(500)` | NULL |  |  | `Reason` |
|  | `to_officer_id` | `uuid` | NULL |  | REF → `users.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `application_assignment_history` (0..*) — FK application_id — `PE3`
- `users` (1) → `application_assignment_history` (0..*) — REF actor_id — `PE39`
- `users` (0..1) → `application_assignment_history` (0..*) — REF from_officer_id — `PE40`
- `users` (0..1) → `application_assignment_history` (0..*) — REF to_officer_id — `PE41`

#### `application_checklists`

Danh sách giấy tờ cần nộp, sao chép (snapshot) từ checklist thủ tục lúc tạo hồ sơ. Entity C#: `ApplicationChecklist`. ID trong draw.io: `P_application_checklists` (Physical), `L_application_checklists` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| FK,U1 | `application_id` | `uuid` | NOT NULL |  | FK → `applications.id` | → quan hệ |
|  | `case_code` | `text` | NULL |  |  | `CaseCode` |
| U1 | `code` | `varchar(255)` | NOT NULL |  |  | `Code` |
|  | `condition_note` | `text` | NULL |  |  | `ConditionNote` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `document_copy_type` | `varchar(30)` | NULL |  |  | `DocumentCopyType` |
|  | `file_url` | `varchar(500)` | NULL |  |  | `FileUrl` |
|  | `form_template_id` | `uuid` | NULL |  | REF → `form_templates.id` | → quan hệ |
|  | `is_required` | `boolean` | NOT NULL |  |  | `IsRequired` |
|  | `note` | `varchar(4000)` | NULL |  |  | `Note` |
|  | `quantity` | `integer` | NOT NULL | 1 |  | `Quantity` |
|  | `sort_order` | `integer` | NOT NULL |  |  | `SortOrder` |
|  | `status` | `varchar(30)` | NOT NULL | 'PENDING' |  | `Status` |
|  | `submission_type` | `varchar(30)` | NULL |  |  | `SubmissionType` |
|  | `title` | `varchar(255)` | NOT NULL |  |  | `Title` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |

Ràng buộc:

- `U1` = UNIQUE(application_id, code)
- CHECK `ck_checklists_status`: `status IN ('PENDING','COMPLETED','REJECTED')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `application_checklists` (0..1) → `user_submissions` (0..*) — REF checklist_id — `PE48`
- `application_checklists` (0..1) → `supporting_documents` (0..*) — REF checklist_id — `PE51`
- `applications` (1) → `application_checklists` (0..*) — FK application_id — `PE4`
- `form_templates` (0..1) → `application_checklists` (0..*) — REF form_template_id — `PE42`

#### `application_comments`

Nhận xét in-line của cán bộ trên một trường form hoặc một mục checklist của một phiên bản. Entity C#: `ApplicationComment`. ID trong draw.io: `P_application_comments` (Physical), `L_application_comments` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| FK | `application_id` | `uuid` | NOT NULL |  | FK → `applications.id`, FK → `application_versions.application_id` | → quan hệ |
| FK | `application_version_id` | `uuid` | NOT NULL |  | FK → `application_versions.id` | → quan hệ |
|  | `comment_text` | `text` | NOT NULL |  |  | `CommentText` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `field_label` | `varchar(255)` | NOT NULL |  |  | `FieldLabel` |
|  | `officer_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `status` | `varchar(20)` | NOT NULL | 'OPEN' |  | `Status` |
|  | `target_id` | `varchar(100)` | NOT NULL |  |  | `TargetId` |
|  | `target_type` | `varchar(30)` | NOT NULL |  |  | `TargetType` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |

Ràng buộc:

- CHECK `ck_application_comments_status`: `status IN ('OPEN','RESOLVED')`
- CHECK `ck_application_comments_target`: `target_type IN ('FORM_FIELD','CHECKLIST_ITEM')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `application_comments` (0..*) — FK application_id — `PE5`
- `application_versions` (1) → `application_comments` (0..*) — FK application_id, application_version_id — `PE6`
- `users` (1) → `application_comments` (0..*) — REF officer_id — `PE37`

#### `applications`

Hồ sơ tiền kiểm của công dân: trạng thái, phường, thủ tục, cán bộ phụ trách, hạn xử lý 45 giờ làm việc, biên nhận tại quầy. Entity C#: `ApplicationRecord`. ID trong draw.io: `P_applications` (Physical), `L_applications` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| UK | `application_code` | `varchar(50)` | NOT NULL |  |  | `ApplicationCode` |
|  | `appointment_date` | `timestamptz` | NULL |  |  | `AppointmentDate` |
|  | `approved_at` | `timestamptz` | NULL |  |  | `ApprovedAt` |
|  | `assigned_at` | `timestamptz` | NULL |  |  | `AssignedAt` |
|  | `assigned_officer_id` | `uuid` | NULL |  | REF → `users.id` | → quan hệ |
|  | `assigned_officer_name` | `varchar(255)` | NULL |  |  | `AssignedOfficerName` |
|  | `assignment_version` | `bigint` | NOT NULL | 0 |  | `AssignmentVersion` |
|  | `case_code` | `varchar(100)` | NULL |  |  | `CaseCode` |
|  | `category_id` | `integer` | NULL |  | REF → `procedure_categories.id` | → quan hệ |
|  | `category_name` | `varchar(255)` | NULL |  |  | `CategoryName` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `deadline_calendar_code` | `varchar(100)` | NULL |  |  | `DeadlineCalendarCode` |
|  | `deadline_calendar_version` | `varchar(100)` | NULL |  |  | `DeadlineCalendarVersion` |
|  | `deadline_history` | `jsonb` | NOT NULL | '[]' |  | `DeadlineHistory` |
|  | `deadline_paused_at_utc` | `timestamptz` | NULL |  |  | `DeadlinePausedAtUtc` |
|  | `deadline_policy_code` | `varchar(100)` | NULL |  |  | `DeadlinePolicyCode` |
|  | `deadline_policy_snapshot` | `jsonb` | NULL |  |  | `DeadlinePolicySnapshot` |
|  | `deadline_policy_version` | `varchar(100)` | NULL |  |  | `DeadlinePolicyVersion` |
|  | `deadline_status` | `varchar(20)` | NOT NULL | 'UNKNOWN' |  | `DeadlineStatus` |
|  | `documents_ready_at` | `timestamptz` | NULL |  |  | `DocumentsReadyAt` |
|  | `due_at_utc` | `timestamptz` | NULL |  |  | `DueAtUtc` |
|  | `first_submitted_at_utc` | `timestamptz` | NULL |  |  | `FirstSubmittedAtUtc` |
|  | `form_data` | `jsonb` | NOT NULL |  |  | `FormData` |
|  | `notes` | `text` | NULL |  |  | `Notes` |
|  | `procedure_id` | `uuid` | NOT NULL |  | REF → `procedures.id` | → quan hệ |
|  | `procedure_title` | `varchar(500)` | NOT NULL |  |  | `ProcedureTitle` |
| UK | `receipt_number` | `varchar(64)` | NULL |  |  | `ReceiptNumber` |
|  | `received_at` | `timestamptz` | NULL |  |  | `ReceivedAt` |
|  | `resubmit_count` | `integer` | NOT NULL | 0 |  | `ResubmitCount` |
|  | `status` | `varchar(50)` | NOT NULL | 'DRAFT' |  | `Status` |
|  | `submitted_at` | `timestamptz` | NULL |  |  | `SubmittedAt` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |
|  | `user_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `ward_code` | `varchar(50)` | NULL |  | REF → `wards.code` | → quan hệ |

Ràng buộc:

- CHECK `ck_applications_deadline`: `(deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND first_submitted_at_utc IS NOT NULL AND deadline_policy_snapshot IS NOT NULL AND deadline_policy_code IS NOT NULL AND deadline_policy_version IS NOT NULL AND deadline_calendar_code IS NOT NULL AND deadline_calendar_version IS NOT NULL) OR (deadline_status IN ('UNKNOWN','NOT_APPLICABLE') AND due_at_utc IS NULL AND deadline_paused_at_utc IS NULL)`
- CHECK `ck_applications_status`: `status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','OFFICIALLY_RECEIVED','APPROVED','CANCELLED','REJECTED')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `application_assignment_history` (0..*) — FK application_id — `PE3`
- `applications` (1) → `application_checklists` (0..*) — FK application_id — `PE4`
- `applications` (1) → `application_comments` (0..*) — FK application_id — `PE5`
- `applications` (1) → `application_status_history` (0..*) — FK application_id — `PE7`
- `applications` (1) → `application_versions` (0..*) — FK application_id — `PE8`
- `applications` (1) → `workflow_outbox` (0..*) — REF (JSON) payload — `PE43`
- `applications` (0..1) → `user_submissions` (0..*) — REF application_id — `PE47`
- `applications` (1) → `application_forms` (0..*) — REF application_id — `PE49`
- `applications` (1) → `supporting_documents` (0..*) — REF application_id — `PE50`
- `applications` (1) → `generated_documents` (0..*) — REF application_id — `PE52`
- `applications` (1) → `approved_qrs` (0..1) — REF application_id — `PE54`
- `applications` (0..1) → `notifications` (0..*) — REF application_id — `PE64`
- `applications` (1) → `citizen_feedback` (0..1) — REF ApplicationId — `PE69`
- `users` (1) → `applications` (0..*) — REF user_id — `PE31`
- `users` (0..1) → `applications` (0..*) — REF assigned_officer_id — `PE32`
- `wards` (0..1) → `applications` (0..*) — REF ward_code — `PE33`
- `procedures` (1) → `applications` (0..*) — REF procedure_id — `PE34`
- `procedure_categories` (0..1) → `applications` (0..*) — REF category_id — `PE35`

#### `application_status_history`

Lịch sử chuyển trạng thái hồ sơ. Entity C#: `ApplicationStatusHistory`. ID trong draw.io: `P_application_status_history` (Physical), `L_application_status_history` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| FK | `application_id` | `uuid` | NOT NULL |  | FK → `applications.id` | → quan hệ |
|  | `changed_by` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL | CURRENT_TIMESTAMP |  | `CreatedAt` |
|  | `from_status` | `varchar(50)` | NULL |  |  | `FromStatus` |
|  | `reason` | `text` | NULL |  |  | `Reason` |
|  | `to_status` | `varchar(50)` | NOT NULL |  |  | `ToStatus` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `application_status_history` (0..*) — FK application_id — `PE7`
- `users` (1) → `application_status_history` (0..*) — REF changed_by — `PE38`

#### `application_versions`

Snapshot mỗi lần công dân nộp/nộp lại hồ sơ (version 1, 2, …). Entity C#: `ApplicationVersion`. ID trong draw.io: `P_application_versions` (Physical), `L_application_versions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK,U2 | `id` | `uuid` | NOT NULL |  |  | `Id` |
| FK,U1,U2 | `application_id` | `uuid` | NOT NULL |  | FK → `applications.id` | → quan hệ |
|  | `snapshot_data` | `jsonb` | NOT NULL |  |  | `SnapshotData` |
|  | `submitted_at` | `timestamptz` | NOT NULL |  |  | `SubmittedAt` |
|  | `submitted_by` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
| U1 | `version_number` | `integer` | NOT NULL |  |  | `VersionNumber` |

Ràng buộc:

- `U1` = UNIQUE(application_id, version_number)
- `U2` = UNIQUE(application_id, id) (alternate key, dùng cho FK kép của `application_comments`)
- CHECK `ck_application_versions_number`: `version_number > 0`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `application_versions` (1) → `application_comments` (0..*) — FK application_id, application_version_id — `PE6`
- `applications` (1) → `application_versions` (0..*) — FK application_id — `PE8`
- `users` (1) → `application_versions` (0..*) — REF submitted_by — `PE36`

#### `workflow_outbox`

Outbox sự kiện chờ đẩy sang RabbitMQ (cùng transaction với thay đổi hồ sơ). Entity C#: `WorkflowOutbox`. ID trong draw.io: `P_workflow_outbox` (Physical), `L_workflow_outbox` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `attempts` | `integer` | NOT NULL |  |  | `Attempts` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `last_error` | `varchar(100)` | NULL |  |  | `LastError` |
|  | `next_attempt_at` | `timestamptz` | NOT NULL |  |  | `NextAttemptAt` |
|  | `payload` | `jsonb` | NOT NULL |  | REF → `applications.id (JSON)` | `Payload` |
|  | `published_at` | `timestamptz` | NULL |  |  | `PublishedAt` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `workflow_outbox` (1) → `notification_inbox` (0..1) — REF Id — `PE66`
- `applications` (1) → `workflow_outbox` (0..*) — REF (JSON) payload — `PE43`

### DocumentForm DB (`DocumentDbContext`)

#### `document.application_forms`

Dữ liệu form JSON của hồ sơ (lưu riêng ở DocumentForm). Entity C#: `ApplicationForm`. ID trong draw.io: `P_application_forms` (Physical), `L_application_forms` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | uuid_generate_v4() |  | `Id` |
|  | `application_id` | `uuid` | NOT NULL |  | REF → `applications.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `varchar(100)` | NULL |  |  | `CreatedBy` |
|  | `deleted_at` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `form_data` | `jsonb` | NOT NULL |  |  | `FormData` |
|  | `is_deleted` | `boolean` | NOT NULL | false |  | `IsDeleted` |
|  | `updated_at` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `varchar(100)` | NULL |  |  | `UpdatedBy` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `application_forms` (0..*) — REF application_id — `PE49`

#### `document.approved_qrs`

Mã QR tra cứu nhanh, tối đa 1 mã mỗi hồ sơ (sinh khi hồ sơ sẵn sàng). Entity C#: `ApprovedQr`. ID trong draw.io: `P_approved_qrs` (Physical), `L_approved_qrs` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | uuid_generate_v4() |  | `Id` |
| UK | `application_id` | `uuid` | NOT NULL |  | REF → `applications.id` | → quan hệ |
|  | `generated_at` | `timestamptz` | NOT NULL |  |  | `GeneratedAt` |
|  | `qr_code_value` | `varchar(2048)` | NOT NULL |  |  | `QrCodeValue` |
|  | `qr_image_png` | `bytea` | NOT NULL |  |  | `QrImagePng` |
|  | `submission_id` | `uuid` | NOT NULL |  | REF → `user_submissions.id` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `approved_qrs` (0..1) — REF application_id — `PE54`
- `user_submissions` (1) → `approved_qrs` (0..*) — REF submission_id — `PE55`

#### `document.form_templates`

Mẫu tờ khai DOCX (mã, tiêu đề). Entity C#: `FormTemplate`. ID trong draw.io: `P_form_templates` (Physical), `L_form_templates` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
| UK | `code` | `varchar(50)` | NOT NULL |  |  | `Code` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `varchar(100)` | NULL |  |  | `CreatedBy` |
|  | `deleted_at` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `file_docx_url` | `text` | NULL |  |  | `FileDocxUrl` |
|  | `is_active` | `boolean` | NOT NULL | true |  | `IsActive` |
|  | `is_deleted` | `boolean` | NOT NULL | false |  | `IsDeleted` |
|  | `title` | `varchar(255)` | NOT NULL |  |  | `Title` |
|  | `updated_at` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `varchar(100)` | NULL |  |  | `UpdatedBy` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `form_templates` (1) → `form_template_versions` (0..*) — FK template_id — `PE9`
- `form_templates` (0..*) → `procedures` (0..*) — REF (JSON) form_definitions — `PE30`
- `form_templates` (0..1) → `application_checklists` (0..*) — REF form_template_id — `PE42`
- `form_templates` (1) → `user_submissions` (0..*) — REF template_id — `PE46`

#### `document.form_template_versions`

Phiên bản của mẫu: file gốc, JSON schema các trường, mapping placeholder. Entity C#: `FormTemplateVersion`. ID trong draw.io: `P_form_template_versions` (Physical), `L_form_template_versions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `created_at_utc` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `text` | NULL |  |  | `CreatedBy` |
|  | `deleted_at_utc` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `is_deleted` | `boolean` | NOT NULL |  |  | `IsDeleted` |
|  | `mapping_definition` | `jsonb` | NOT NULL |  |  | `MappingDefinition` |
|  | `original_blob_url` | `varchar(2048)` | NOT NULL |  |  | `OriginalBlobUrl` |
|  | `original_sha256` | `varchar(64)` | NOT NULL |  |  | `OriginalSha256` |
|  | `schema_definition` | `jsonb` | NOT NULL |  |  | `SchemaDefinition` |
| FK,U1 | `template_id` | `uuid` | NOT NULL |  | FK → `form_templates.id` | → quan hệ |
|  | `updated_at_utc` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `text` | NULL |  |  | `UpdatedBy` |
| U1 | `version_number` | `integer` | NOT NULL |  |  | `VersionNumber` |

Ràng buộc:

- `U1` = UNIQUE(template_id, version_number)

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `form_template_versions` (0..1) → `user_submissions` (0..*) — FK template_version_id — `PE10`
- `form_templates` (1) → `form_template_versions` (0..*) — FK template_id — `PE9`

#### `document.generated_documents`

File PDF hệ thống sinh ra từ hồ sơ/tờ khai. Entity C#: `GeneratedDocument`. ID trong draw.io: `P_generated_documents` (Physical), `L_generated_documents` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | uuid_generate_v4() |  | `Id` |
|  | `application_id` | `uuid` | NOT NULL |  | REF → `applications.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `varchar(100)` | NULL |  |  | `CreatedBy` |
|  | `deleted_at` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `document_type` | `varchar(50)` | NOT NULL |  |  | `DocumentType` |
|  | `is_deleted` | `boolean` | NOT NULL | false |  | `IsDeleted` |
|  | `pdf_blob_url` | `text` | NOT NULL |  |  | `PdfBlobUrl` |
|  | `submission_id` | `uuid` | NULL |  | REF → `user_submissions.id` | → quan hệ |
|  | `updated_at` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `varchar(100)` | NULL |  |  | `UpdatedBy` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `generated_documents` (0..*) — REF application_id — `PE52`
- `user_submissions` (0..1) → `generated_documents` (0..*) — REF submission_id — `PE53`

#### `document.supporting_documents`

Giấy tờ minh chứng công dân upload (Blob), gắn với hồ sơ và mục checklist. Entity C#: `SupportingDocument`. ID trong draw.io: `P_supporting_documents` (Physical), `L_supporting_documents` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `application_id` | `uuid` | NOT NULL |  | REF → `applications.id` | → quan hệ |
|  | `blob_url` | `text` | NOT NULL |  |  | `BlobUrl` |
|  | `checklist_id` | `uuid` | NULL |  | REF → `application_checklists.id` | → quan hệ |
|  | `content_type` | `varchar(100)` | NULL |  |  | `ContentType` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `varchar(100)` | NULL |  |  | `CreatedBy` |
|  | `deleted_at` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `document_copy_type` | `varchar(30)` | NULL |  |  | `DocumentCopyType` |
|  | `file_name` | `varchar(255)` | NOT NULL |  |  | `FileName` |
|  | `file_size_bytes` | `bigint` | NULL |  |  | `FileSizeBytes` |
|  | `is_deleted` | `boolean` | NOT NULL | false |  | `IsDeleted` |
|  | `quantity` | `integer` | NOT NULL | 1 |  | `Quantity` |
|  | `submission_type` | `varchar(30)` | NULL |  |  | `SubmissionType` |
|  | `updated_at` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `varchar(100)` | NULL |  |  | `UpdatedBy` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `supporting_documents` (0..*) — REF application_id — `PE50`
- `application_checklists` (0..1) → `supporting_documents` (0..*) — REF checklist_id — `PE51`

#### `document.user_submissions`

Tờ khai công dân đã điền (online hoặc upload DOCX), gắn với hồ sơ và mục checklist. Entity C#: `UserSubmission`. ID trong draw.io: `P_user_submissions` (Physical), `L_user_submissions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | uuid_generate_v4() |  | `Id` |
|  | `applicant_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
| U1 | `application_id` | `uuid` | NULL |  | REF → `applications.id` | → quan hệ |
|  | `blob_url` | `varchar(2048)` | NOT NULL |  |  | `BlobUrl` |
| U1 | `checklist_id` | `uuid` | NULL |  | REF → `application_checklists.id` | → quan hệ |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `created_by` | `varchar(100)` | NULL |  |  | `CreatedBy` |
|  | `deleted_at` | `timestamptz` | NULL |  |  | `DeletedAtUtc` |
|  | `file_name` | `varchar(512)` | NOT NULL |  |  | `FileName` |
|  | `file_size_bytes` | `bigint` | NOT NULL |  |  | `FileSizeBytes` |
|  | `form_data` | `jsonb` | NULL |  |  | `FormData` |
|  | `is_deleted` | `boolean` | NOT NULL | false |  | `IsDeleted` |
|  | `officer_comment` | `varchar(2000)` | NULL |  |  | `OfficerComment` |
|  | `reviewed_at` | `timestamptz` | NULL |  |  | `ReviewedAt` |
|  | `reviewed_by_officer_id` | `uuid` | NULL |  | REF → `users.id` | → quan hệ |
|  | `status` | `varchar(50)` | NOT NULL |  |  | `Status` |
|  | `submitted_at` | `timestamptz` | NULL |  |  | `SubmittedAt` |
|  | `template_id` | `uuid` | NOT NULL |  | REF → `form_templates.id` | → quan hệ |
| FK | `template_version_id` | `uuid` | NULL |  | FK → `form_template_versions.id` | → quan hệ |
|  | `updated_at` | `timestamptz` | NULL |  |  | `UpdatedAtUtc` |
|  | `updated_by` | `varchar(100)` | NULL |  |  | `UpdatedBy` |

Ràng buộc:

- `U1` = UNIQUE(application_id, checklist_id) WHERE `application_id IS NOT NULL AND is_deleted = false`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `user_submissions` (0..1) → `generated_documents` (0..*) — REF submission_id — `PE53`
- `user_submissions` (1) → `approved_qrs` (0..*) — REF submission_id — `PE55`
- `form_template_versions` (0..1) → `user_submissions` (0..*) — FK template_version_id — `PE10`
- `users` (1) → `user_submissions` (0..*) — REF applicant_id — `PE44`
- `users` (0..1) → `user_submissions` (0..*) — REF reviewed_by_officer_id — `PE45`
- `form_templates` (1) → `user_submissions` (0..*) — REF template_id — `PE46`
- `applications` (0..1) → `user_submissions` (0..*) — REF application_id — `PE47`
- `application_checklists` (0..1) → `user_submissions` (0..*) — REF checklist_id — `PE48`

### AIOCR DB (`ChatDbContext`)

#### `chat_messages`

Tin nhắn trong phiên chat; `citations` (JSON) lưu các đoạn kiến thức được trích dẫn. Entity C#: `ChatMessage`. ID trong draw.io: `P_chat_messages` (Physical), `L_chat_messages` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | gen_random_uuid() |  | `Id` |
|  | `citations` | `jsonb` | NULL |  | REF → `knowledge_chunks.id (JSON)` | `Citations` |
|  | `content` | `varchar(20000)` | NOT NULL |  |  | `Content` |
|  | `created_at` | `timestamptz` | NOT NULL | CURRENT_TIMESTAMP |  | `CreatedAt` |
|  | `role` | `varchar(20)` | NOT NULL |  |  | `Role` |
| FK | `session_id` | `uuid` | NOT NULL |  | FK → `chat_sessions.id` | → quan hệ |

Ràng buộc:

- CHECK `ck_chat_message_role`: `role IN ('User','Assistant')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `chat_sessions` (1) → `chat_messages` (0..*) — FK session_id — `PE0`
- `knowledge_chunks` (0..*) → `chat_messages` (0..*) — REF (JSON) citations — `PE61`

#### `chat_sessions`

Phiên chat của người dùng với trợ lý AI. Entity C#: `ChatSession`. ID trong draw.io: `P_chat_sessions` (Physical), `L_chat_sessions` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | gen_random_uuid() |  | `Id` |
|  | `created_at` | `timestamptz` | NOT NULL | CURRENT_TIMESTAMP |  | `CreatedAt` |
|  | `user_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `ward_code` | `varchar(50)` | NOT NULL |  | REF → `wards.code` | → quan hệ |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `chat_sessions` (1) → `chat_messages` (0..*) — FK session_id — `PE0`
- `users` (1) → `chat_sessions` (0..*) — REF user_id — `PE56`
- `wards` (1) → `chat_sessions` (0..*) — REF ward_code — `PE57`

#### `knowledge_chunks`

Đoạn đã chia nhỏ của tài liệu kiến thức, kèm vector `embedding`. Entity C#: `KnowledgeChunk`. ID trong draw.io: `P_knowledge_chunks` (Physical), `L_knowledge_chunks` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `char_count` | `integer` | NOT NULL |  |  | `CharCount` |
| U1 | `chunk_index` | `integer` | NOT NULL |  |  | `ChunkIndex` |
|  | `content` | `text` | NOT NULL |  |  | `Content` |
|  | `content_sha256` | `varchar(64)` | NOT NULL |  |  | `ContentSha256` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
| FK,U1 | `document_id` | `uuid` | NOT NULL |  | FK → `knowledge_documents.id` | → quan hệ |
|  | `embedded_at` | `timestamptz` | NULL |  |  | `EmbeddedAt` |
|  | `embedding` | `real[]` | NULL |  |  | `Embedding` |
|  | `embedding_model` | `varchar(120)` | NULL |  |  | `EmbeddingModel` |
|  | `heading` | `varchar(500)` | NULL |  |  | `Heading` |

Ràng buộc:

- `U1` = UNIQUE(document_id, chunk_index)

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `knowledge_chunks` (0..*) → `chat_messages` (0..*) — REF (JSON) citations — `PE61`
- `knowledge_documents` (1) → `knowledge_chunks` (0..*) — FK document_id — `PE1`

#### `knowledge_documents`

Tài liệu kiến thức cho RAG (từ thủ tục, văn bản tay, FAQ, PDF). Entity C#: `KnowledgeDocument`. ID trong draw.io: `P_knowledge_documents` (Physical), `L_knowledge_documents` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `chunk_count` | `integer` | NOT NULL |  |  | `ChunkCount` |
|  | `chunked_at` | `timestamptz` | NULL |  |  | `ChunkedAt` |
|  | `chunking_version` | `varchar(50)` | NOT NULL |  |  | `ChunkingVersion` |
|  | `content` | `text` | NOT NULL |  |  | `Content` |
|  | `content_sha256` | `varchar(64)` | NOT NULL |  |  | `ContentSha256` |
|  | `created_at` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `created_by` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `embedded_at` | `timestamptz` | NULL |  |  | `EmbeddedAt` |
|  | `embedding_attempts` | `integer` | NOT NULL |  |  | `EmbeddingAttempts` |
|  | `embedding_error` | `varchar(100)` | NULL |  |  | `EmbeddingError` |
|  | `embedding_model` | `varchar(120)` | NULL |  |  | `EmbeddingModel` |
|  | `is_active` | `boolean` | NOT NULL |  |  | `IsActive` |
|  | `next_embedding_at` | `timestamptz` | NULL |  |  | `NextEmbeddingAt` |
| UK | `procedure_id` | `uuid` | NULL |  | REF → `procedures.id` | → quan hệ |
|  | `revision` | `uuid` | NOT NULL |  |  | `Revision` |
|  | `source_reference` | `varchar(500)` | NULL |  |  | `SourceReference` |
|  | `source_type` | `varchar(20)` | NOT NULL |  |  | `SourceType` |
|  | `source_version` | `varchar(100)` | NULL |  |  | `SourceVersion` |
|  | `status` | `varchar(20)` | NOT NULL |  |  | `Status` |
|  | `title` | `varchar(500)` | NOT NULL |  |  | `Title` |
|  | `updated_at` | `timestamptz` | NOT NULL |  |  | `UpdatedAt` |
|  | `updated_by` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |

Ràng buộc:

- `UK` trên `procedure_id` là UNIQUE có điều kiện: WHERE `source_type = 'PROCEDURE'`
- CHECK `ck_knowledge_documents_procedure`: `source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL`
- CHECK `ck_knowledge_documents_source_type`: `source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')`
- CHECK `ck_knowledge_documents_status`: `status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `knowledge_documents` (1) → `knowledge_chunks` (0..*) — FK document_id — `PE1`
- `users` (1) → `knowledge_documents` (0..*) — REF created_by — `PE58`
- `users` (1) → `knowledge_documents` (0..*) — REF updated_by — `PE59`
- `procedures` (0..1) → `knowledge_documents` (0..1) — REF procedure_id — `PE60`

### Notification DB (`NotificationDbContext`)

#### `notifications`

Thông báo cho từng người nhận theo kênh IN_APP/EMAIL/SMS. Entity C#: `Notification`. ID trong draw.io: `P_notifications` (Physical), `L_notifications` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `id` | `uuid` | NOT NULL | gen_random_uuid() |  | `Id` |
|  | `action_url` | `varchar(500)` | NULL |  |  | `ActionUrl` |
|  | `application_id` | `uuid` | NULL |  | REF → `applications.id` | → quan hệ |
| U1 | `channel` | `varchar(30)` | NOT NULL | 'IN_APP' |  | `Channel` |
|  | `content` | `text` | NOT NULL |  |  | `Content` |
|  | `created_at` | `timestamptz` | NOT NULL | now() |  | `CreatedAt` |
|  | `is_read` | `boolean` | NOT NULL | false |  | `IsRead` |
|  | `read_at` | `timestamptz` | NULL |  |  | `ReadAt` |
| U1 | `recipient_user_id` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `recipient_ward_code` | `varchar(50)` | NULL |  | REF → `wards.code` | → quan hệ |
| U1 | `source_event_id` | `uuid` | NULL |  | REF → `notification_inbox.Id` | → quan hệ |
|  | `title` | `varchar(255)` | NOT NULL |  |  | `Title` |
|  | `type` | `varchar(50)` | NOT NULL |  |  | `Type` |

Ràng buộc:

- `U1` = UNIQUE(source_event_id, recipient_user_id, channel)
- CHECK `ck_notifications_channel`: `channel IN ('IN_APP','SMS','EMAIL')`
- CHECK `ck_notifications_read`: `(is_read AND read_at IS NOT NULL) OR (NOT is_read AND read_at IS NULL)`
- CHECK `ck_notifications_type`: `type IN ('APPLICATION_STATUS_CHANGED','INLINE_COMMENT_ADDED','ASSIGNED_OFFICER','SYSTEM_ALERT')`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `notifications` (1) → `notification_deliveries` (0..*) — FK NotificationId — `PE2`
- `users` (1) → `notifications` (0..*) — REF recipient_user_id — `PE62`
- `wards` (0..1) → `notifications` (0..*) — REF recipient_ward_code — `PE63`
- `applications` (0..1) → `notifications` (0..*) — REF application_id — `PE64`
- `notification_inbox` (0..1) → `notifications` (0..*) — REF source_event_id — `PE65`

#### `notification_delivery_audits`

Nhật ký thao tác quản trị trên delivery (replay, đối soát SMS). Entity C#: `DeliveryAdminAudit`. ID trong draw.io: `P_notification_delivery_audits` (Physical), `L_notification_delivery_audits` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `Id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `Action` | `varchar(40)` | NOT NULL |  |  | `Action` |
|  | `ActorId` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `AfterMessageId` | `varchar(100)` | NULL |  |  | `AfterMessageId` |
|  | `AfterRevision` | `uuid` | NOT NULL |  |  | `AfterRevision` |
|  | `AfterState` | `varchar(30)` | NOT NULL |  |  | `AfterState` |
|  | `BeforeAttempts` | `integer` | NOT NULL |  |  | `BeforeAttempts` |
|  | `BeforeMessageId` | `varchar(100)` | NULL |  |  | `BeforeMessageId` |
|  | `BeforeRevision` | `uuid` | NOT NULL |  |  | `BeforeRevision` |
|  | `BeforeState` | `varchar(30)` | NOT NULL |  |  | `BeforeState` |
|  | `CreatedAt` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `DeliveryId` | `uuid` | NOT NULL |  | REF → `notification_deliveries.Id` | → quan hệ |
|  | `Reason` | `varchar(500)` | NOT NULL |  |  | `Reason` |
|  | `Reference` | `varchar(200)` | NULL |  |  | `Reference` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `notification_deliveries` (1) → `notification_delivery_audits` (0..*) — REF DeliveryId — `PE67`
- `users` (1) → `notification_delivery_audits` (0..*) — REF ActorId — `PE68`

#### `notification_deliveries`

Trạng thái gửi từng kênh của thông báo (số lần thử, provider, lỗi). Entity C#: `NotificationDelivery`. ID trong draw.io: `P_notification_deliveries` (Physical), `L_notification_deliveries` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `Id` | `uuid` | NOT NULL |  |  | `Id` |
|  | `Attempts` | `integer` | NOT NULL |  |  | `Attempts` |
| U1 | `Channel` | `varchar(30)` | NOT NULL |  |  | `Channel` |
|  | `ErrorCode` | `varchar(100)` | NULL |  |  | `ErrorCode` |
|  | `external_message_id` | `varchar(100)` | NULL |  |  | `ExternalMessageId` |
|  | `is_sandbox` | `boolean` | NULL |  |  | `IsSandbox` |
|  | `NextAttemptAt` | `timestamptz` | NOT NULL |  |  | `NextAttemptAt` |
| FK,U1 | `NotificationId` | `uuid` | NOT NULL |  | FK → `notifications.id` | → quan hệ |
|  | `provider` | `varchar(30)` | NULL |  |  | `Provider` |
|  | `revision` | `uuid` | NOT NULL | gen_random_uuid() |  | `Revision` |
|  | `SentAt` | `timestamptz` | NULL |  |  | `SentAt` |
|  | `State` | `varchar(30)` | NOT NULL |  |  | `State` |
|  | `updated_at` | `timestamptz` | NOT NULL | now() |  | `UpdatedAt` |

Ràng buộc:

- `U1` = UNIQUE(NotificationId, Channel)

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `notification_deliveries` (1) → `notification_delivery_audits` (0..*) — REF DeliveryId — `PE67`
- `notifications` (1) → `notification_deliveries` (0..*) — FK NotificationId — `PE2`

#### `notification_inbox`

Sự kiện nhận từ RabbitMQ (Id = EventId của outbox) để chống xử lý trùng. Entity C#: `NotificationInbox`. ID trong draw.io: `P_notification_inbox` (Physical), `L_notification_inbox` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `Id` | `uuid` | NOT NULL |  | REF → `workflow_outbox.id` | `Id` |
|  | `Attempts` | `integer` | NOT NULL |  |  | `Attempts` |
|  | `CreatedAt` | `timestamptz` | NOT NULL |  |  | `CreatedAt` |
|  | `ErrorCode` | `varchar(100)` | NULL |  |  | `ErrorCode` |
|  | `NextAttemptAt` | `timestamptz` | NOT NULL |  |  | `NextAttemptAt` |
|  | `Payload` | `jsonb` | NOT NULL |  |  | `Payload` |
|  | `State` | `varchar(30)` | NOT NULL |  |  | `State` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `notification_inbox` (0..1) → `notifications` (0..*) — REF source_event_id — `PE65`
- `workflow_outbox` (1) → `notification_inbox` (0..1) — REF Id — `PE66`

### Analytics DB (`AnalyticsDbContext`)

#### `analytics.citizen_feedback`

Đánh giá 1–5 sao của công dân, tối đa 1 đánh giá mỗi hồ sơ. Entity C#: `CitizenFeedback`. ID trong draw.io: `P_citizen_feedback` (Physical), `L_citizen_feedback` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `Id` | `uuid` | NOT NULL |  |  | `Id` |
| UK | `ApplicationId` | `uuid` | NOT NULL |  | REF → `applications.id` | → quan hệ |
|  | `CitizenId` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
|  | `Comment` | `varchar(2000)` | NULL |  |  | `Comment` |
|  | `CreatedAtUtc` | `timestamptz` | NOT NULL |  |  | `CreatedAtUtc` |
|  | `ProcedureId` | `uuid` | NOT NULL |  | REF → `procedures.id` | → quan hệ |
|  | `Rating` | `integer` | NOT NULL |  |  | `Rating` |
|  | `WardCode` | `varchar(50)` | NOT NULL |  | REF → `wards.code` | → quan hệ |

Ràng buộc:

- CHECK `ck_feedback_rating`: `"Rating" BETWEEN 1 AND 5`

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `applications` (1) → `citizen_feedback` (0..1) — REF ApplicationId — `PE69`
- `users` (1) → `citizen_feedback` (0..*) — REF CitizenId — `PE70`
- `procedures` (1) → `citizen_feedback` (0..*) — REF ProcedureId — `PE71`
- `wards` (1) → `citizen_feedback` (0..*) — REF WardCode — `PE72`

#### `analytics.metric_refreshes`

Mốc làm mới số liệu thống kê theo ngày (watermark). Entity C#: `MetricRefresh`. ID trong draw.io: `P_metric_refreshes` (Physical), `L_metric_refreshes` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `MetricDate` | `date` | NOT NULL |  |  | `MetricDate` |
|  | `RefreshedAtUtc` | `timestamptz` | NOT NULL |  |  | `RefreshedAtUtc` |
|  | `SourceAtUtc` | `timestamptz` | NOT NULL |  |  | `SourceAtUtc` |

#### `analytics.daily_performance_metrics`

Thống kê hiệu suất theo ngày × phường × cán bộ × thủ tục. Entity C#: `OfficerMetric`. ID trong draw.io: `P_daily_performance_metrics` (Physical), `L_daily_performance_metrics` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `MetricDate` | `date` | NOT NULL |  |  | `MetricDate` |
| PK | `WardCode` | `varchar(50)` | NOT NULL |  | REF → `wards.code` | → quan hệ |
| PK | `OfficerId` | `uuid` | NOT NULL |  | REF → `users.id` | → quan hệ |
| PK | `ProcedureId` | `uuid` | NOT NULL |  | REF → `procedures.id` | → quan hệ |
|  | `DurationSampleCount` | `integer` | NOT NULL |  |  | `DurationSampleCount` |
|  | `ReadyCount` | `integer` | NOT NULL |  |  | `ReadyCount` |
|  | `RevisionRequestedCount` | `integer` | NOT NULL |  |  | `RevisionRequestedCount` |
|  | `TotalProcessingMinutes` | `float8` | NOT NULL |  |  | `TotalProcessingMinutes` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `users` (1) → `daily_performance_metrics` (0..*) — REF OfficerId — `PE73`
- `procedures` (1) → `daily_performance_metrics` (0..*) — REF ProcedureId — `PE74`
- `wards` (1) → `daily_performance_metrics` (0..*) — REF WardCode — `PE75`

#### `analytics.application_procedure_metrics`

Thống kê theo ngày × phường × thủ tục (số hồ sơ sẵn sàng, tiếp nhận, nộp lại…). Entity C#: `ProcedureMetric`. ID trong draw.io: `P_application_procedure_metrics` (Physical), `L_application_procedure_metrics` (Logical).

| Khóa | Cột | Kiểu | NULL | Mặc định | Tham chiếu tới | Thuộc tính Logical |
|---|---|---|---|---|---|---|
| PK | `MetricDate` | `date` | NOT NULL |  |  | `MetricDate` |
| PK | `WardCode` | `varchar(50)` | NOT NULL |  | REF → `wards.code` | → quan hệ |
| PK | `ProcedureId` | `uuid` | NOT NULL |  | REF → `procedures.id` | → quan hệ |
|  | `CancelledCount` | `integer` | NOT NULL |  |  | `CancelledCount` |
|  | `LegacyApprovedCount` | `integer` | NOT NULL |  |  | `LegacyApprovedCount` |
|  | `LegacyRejectedCount` | `integer` | NOT NULL |  |  | `LegacyRejectedCount` |
|  | `ReadyCount` | `integer` | NOT NULL |  |  | `ReadyCount` |
|  | `ReceivedCount` | `integer` | NOT NULL |  |  | `ReceivedCount` |
|  | `ResubmittedCount` | `integer` | NOT NULL |  |  | `ResubmittedCount` |
|  | `RevisionRequestedCount` | `integer` | NOT NULL |  |  | `RevisionRequestedCount` |

Quan hệ (bảng cha (bản số) → bảng con (bản số) — nhãn — ID cạnh):

- `procedures` (1) → `application_procedure_metrics` (0..*) — REF ProcedureId — `PE76`
- `wards` (1) → `application_procedure_metrics` (0..*) — REF WardCode — `PE77`

## 6. Danh sách quan hệ trên Physical

### 6.1 22 FOREIGN KEY thật (đường liền)

| # | Bảng con.cột | → Bảng cha.cột | Bản số (cha – con) | ON DELETE | ID cạnh |
|--:|---|---|---|---|---|
| 1 | `chat_messages.(session_id)` | `chat_sessions.(id)` | 1 – 0..* | CASCADE | `PE0` |
| 2 | `knowledge_chunks.(document_id)` | `knowledge_documents.(id)` | 1 – 0..* | CASCADE | `PE1` |
| 3 | `notification_deliveries.(NotificationId)` | `notifications.(id)` | 1 – 0..* | CASCADE | `PE2` |
| 4 | `application_assignment_history.(application_id)` | `applications.(id)` | 1 – 0..* | CASCADE | `PE3` |
| 5 | `application_checklists.(application_id)` | `applications.(id)` | 1 – 0..* | CASCADE | `PE4` |
| 6 | `application_comments.(application_id)` | `applications.(id)` | 1 – 0..* | CASCADE | `PE5` |
| 7 | `application_comments.(application_id, application_version_id)` | `application_versions.(application_id, id)` | 1 – 0..* | RESTRICT | `PE6` |
| 8 | `application_status_history.(application_id)` | `applications.(id)` | 1 – 0..* | CASCADE | `PE7` |
| 9 | `application_versions.(application_id)` | `applications.(id)` | 1 – 0..* | CASCADE | `PE8` |
| 10 | `form_template_versions.(template_id)` | `form_templates.(id)` | 1 – 0..* | RESTRICT | `PE9` |
| 11 | `user_submissions.(template_version_id)` | `form_template_versions.(id)` | 0..1 – 0..* | RESTRICT | `PE10` |
| 12 | `refresh_tokens.(user_id)` | `users.(id)` | 1 – 0..* | CASCADE | `PE11` |
| 13 | `role_permissions.(permission_id)` | `permissions.(id)` | 1 – 0..* | CASCADE | `PE12` |
| 14 | `role_permissions.(role_id)` | `roles.(id)` | 1 – 0..* | CASCADE | `PE13` |
| 15 | `users.(ward_id)` | `wards.(id)` | 0..1 – 0..* | RESTRICT | `PE14` |
| 16 | `user_otps.(user_id)` | `users.(id)` | 1 – 0..* | CASCADE | `PE15` |
| 17 | `user_profiles.(user_id)` | `users.(id)` | 1 – 0..1 | CASCADE | `PE16` |
| 18 | `user_roles.(role_id)` | `roles.(id)` | 1 – 0..* | CASCADE | `PE17` |
| 19 | `user_roles.(user_id)` | `users.(id)` | 1 – 0..* | CASCADE | `PE18` |
| 20 | `welcome_emails.(user_id)` | `users.(id)` | 1 – 0..1 | CASCADE | `PE19` |
| 21 | `procedures.(category_id)` | `procedure_categories.(id)` | 1 – 0..* | RESTRICT | `PE20` |
| 22 | `procedure_versions.(procedure_id)` | `procedures.(id)` | 1 – 0..* | RESTRICT | `PE21` |

ON DELETE: CASCADE = xóa cha thì xóa luôn con; RESTRICT = không cho xóa cha khi còn con; NOACTION = mặc định của PostgreSQL (tương tự RESTRICT, kiểm tra cuối câu lệnh).

### 6.2 56 REF – tham chiếu không có FOREIGN KEY (đường nét đứt)

| # | Bảng.cột lưu ID | → Trỏ tới | Loại | Bản số (cha – con) | Cùng DB? | ID cạnh |
|--:|---|---|---|---|---|---|
| 1 | `rbac_audit_logs.actor_user_id` | `users.id` | col | 1 – 0..* | cùng DB | `PE22` |
| 2 | `rbac_audit_logs.target_user_id` | `users.id` | col | 0..1 – 0..* | cùng DB | `PE23` |
| 3 | `rbac_audit_logs.role_id` | `roles.id` | col | 0..1 – 0..* | cùng DB | `PE24` |
| 4 | `rbac_audit_logs.permission_id` | `permissions.id` | col | 0..1 – 0..* | cùng DB | `PE25` |
| 5 | `users.assigned_categories` | `procedure_categories.id` | array | 0..* – 0..* | khác DB | `PE26` |
| 6 | `procedure_drafts.created_by` | `users.id` | col | 1 – 0..* | khác DB | `PE27` |
| 7 | `procedure_drafts.reviewed_by` | `users.id` | col | 0..1 – 0..* | khác DB | `PE28` |
| 8 | `procedure_drafts.published_procedure_id` | `procedures.id` | col | 0..1 – 0..* | cùng DB | `PE29` |
| 9 | `procedures.form_definitions` | `form_templates.id` | json | 0..* – 0..* | khác DB | `PE30` |
| 10 | `applications.user_id` | `users.id` | col | 1 – 0..* | khác DB | `PE31` |
| 11 | `applications.assigned_officer_id` | `users.id` | col | 0..1 – 0..* | khác DB | `PE32` |
| 12 | `applications.ward_code` | `wards.code` | col | 0..1 – 0..* | khác DB | `PE33` |
| 13 | `applications.procedure_id` | `procedures.id` | col | 1 – 0..* | khác DB | `PE34` |
| 14 | `applications.category_id` | `procedure_categories.id` | col | 0..1 – 0..* | khác DB | `PE35` |
| 15 | `application_versions.submitted_by` | `users.id` | col | 1 – 0..* | khác DB | `PE36` |
| 16 | `application_comments.officer_id` | `users.id` | col | 1 – 0..* | khác DB | `PE37` |
| 17 | `application_status_history.changed_by` | `users.id` | col | 1 – 0..* | khác DB | `PE38` |
| 18 | `application_assignment_history.actor_id` | `users.id` | col | 1 – 0..* | khác DB | `PE39` |
| 19 | `application_assignment_history.from_officer_id` | `users.id` | col | 0..1 – 0..* | khác DB | `PE40` |
| 20 | `application_assignment_history.to_officer_id` | `users.id` | col | 0..1 – 0..* | khác DB | `PE41` |
| 21 | `application_checklists.form_template_id` | `form_templates.id` | col | 0..1 – 0..* | khác DB | `PE42` |
| 22 | `workflow_outbox.payload` | `applications.id` | json | 1 – 0..* | cùng DB | `PE43` |
| 23 | `user_submissions.applicant_id` | `users.id` | col | 1 – 0..* | khác DB | `PE44` |
| 24 | `user_submissions.reviewed_by_officer_id` | `users.id` | col | 0..1 – 0..* | khác DB | `PE45` |
| 25 | `user_submissions.template_id` | `form_templates.id` | col | 1 – 0..* | cùng DB | `PE46` |
| 26 | `user_submissions.application_id` | `applications.id` | col | 0..1 – 0..* | khác DB | `PE47` |
| 27 | `user_submissions.checklist_id` | `application_checklists.id` | col | 0..1 – 0..* | khác DB | `PE48` |
| 28 | `application_forms.application_id` | `applications.id` | col | 1 – 0..* | khác DB | `PE49` |
| 29 | `supporting_documents.application_id` | `applications.id` | col | 1 – 0..* | khác DB | `PE50` |
| 30 | `supporting_documents.checklist_id` | `application_checklists.id` | col | 0..1 – 0..* | khác DB | `PE51` |
| 31 | `generated_documents.application_id` | `applications.id` | col | 1 – 0..* | khác DB | `PE52` |
| 32 | `generated_documents.submission_id` | `user_submissions.id` | col | 0..1 – 0..* | cùng DB | `PE53` |
| 33 | `approved_qrs.application_id` | `applications.id` | col | 1 – 0..1 | khác DB | `PE54` |
| 34 | `approved_qrs.submission_id` | `user_submissions.id` | col | 1 – 0..* | cùng DB | `PE55` |
| 35 | `chat_sessions.user_id` | `users.id` | col | 1 – 0..* | khác DB | `PE56` |
| 36 | `chat_sessions.ward_code` | `wards.code` | col | 1 – 0..* | khác DB | `PE57` |
| 37 | `knowledge_documents.created_by` | `users.id` | col | 1 – 0..* | khác DB | `PE58` |
| 38 | `knowledge_documents.updated_by` | `users.id` | col | 1 – 0..* | khác DB | `PE59` |
| 39 | `knowledge_documents.procedure_id` | `procedures.id` | col | 0..1 – 0..1 | khác DB | `PE60` |
| 40 | `chat_messages.citations` | `knowledge_chunks.id` | json | 0..* – 0..* | cùng DB | `PE61` |
| 41 | `notifications.recipient_user_id` | `users.id` | col | 1 – 0..* | khác DB | `PE62` |
| 42 | `notifications.recipient_ward_code` | `wards.code` | col | 0..1 – 0..* | khác DB | `PE63` |
| 43 | `notifications.application_id` | `applications.id` | col | 0..1 – 0..* | khác DB | `PE64` |
| 44 | `notifications.source_event_id` | `notification_inbox.Id` | col | 0..1 – 0..* | cùng DB | `PE65` |
| 45 | `notification_inbox.Id` | `workflow_outbox.id` | col | 1 – 0..1 | khác DB | `PE66` |
| 46 | `notification_delivery_audits.DeliveryId` | `notification_deliveries.Id` | col | 1 – 0..* | cùng DB | `PE67` |
| 47 | `notification_delivery_audits.ActorId` | `users.id` | col | 1 – 0..* | khác DB | `PE68` |
| 48 | `citizen_feedback.ApplicationId` | `applications.id` | col | 1 – 0..1 | khác DB | `PE69` |
| 49 | `citizen_feedback.CitizenId` | `users.id` | col | 1 – 0..* | khác DB | `PE70` |
| 50 | `citizen_feedback.ProcedureId` | `procedures.id` | col | 1 – 0..* | khác DB | `PE71` |
| 51 | `citizen_feedback.WardCode` | `wards.code` | col | 1 – 0..* | khác DB | `PE72` |
| 52 | `daily_performance_metrics.OfficerId` | `users.id` | col | 1 – 0..* | khác DB | `PE73` |
| 53 | `daily_performance_metrics.ProcedureId` | `procedures.id` | col | 1 – 0..* | khác DB | `PE74` |
| 54 | `daily_performance_metrics.WardCode` | `wards.code` | col | 1 – 0..* | khác DB | `PE75` |
| 55 | `application_procedure_metrics.ProcedureId` | `procedures.id` | col | 1 – 0..* | khác DB | `PE76` |
| 56 | `application_procedure_metrics.WardCode` | `wards.code` | col | 1 – 0..* | khác DB | `PE77` |

REF "cùng DB" là quan hệ mà code cố ý không tạo FK (ví dụ log/audit cần giữ lại khi bản ghi gốc bị xóa, hoặc ID nằm trong JSON).

## 7. Cách chỉnh sửa trong draw.io

### 7.1 Quy ước ID (xem bằng: chọn hình → chuột phải **Edit Data…** hoặc Ctrl+M)

| Tiền tố | Là gì | Ví dụ |
|---|---|---|
| `C_<Tên>` | Thực thể Conceptual | `C_Application` |
| `CE<n>` | Quan hệ Conceptual (theo thứ tự mục 3.2) | `CE10` |
| `CGtop`, `CG0…CG4`, `GJ` | Cây generalization; `GJ` là điểm gộp ẩn 4×4 px | |
| `L_<bảng>` | Thực thể Logical | `L_applications` |
| `LE<n>` | Quan hệ Logical | |
| `P_<bảng>` | Bảng Physical | `P_users` |
| `P_<bảng>_r<i>` / `_r<i>_<j>` | Dòng thứ i / ô thứ j của bảng (j: 0 khóa, 1 tên cột, 2 kiểu, 3 NULL) | `P_users_r0_1` = ô "id" |
| `PE<n>` | Quan hệ Physical | |
| `t<n>`, `lg<n>`, `le<n>` | Tiêu đề, khung chú giải, đường mẫu trong chú giải | |

Tìm nhanh một bảng/thực thể: **Ctrl+F** gõ tên (draw.io tìm theo chữ hiển thị).

### 7.2 Thao tác thường gặp

| Việc | Cách làm |
|---|---|
| Di chuyển bảng | Kéo tiêu đề bảng. Các đường nối có điểm gấp cố định nên sẽ lệch: chọn đường → **Arrange → Waypoints → Clear** (hoặc Format → Edge style → Orthogonal) để draw.io vẽ lại. |
| Thêm cột (Physical) | Chọn một dòng có sẵn → Ctrl+D (Duplicate) → sửa 4 ô: khóa, tên, kiểu, NULL. Bảng tự giãn. |
| Thêm thuộc tính (Logical) | Như trên, bảng Logical chỉ có 1 ô mỗi dòng. |
| Thêm quan hệ | Kéo từ cạnh bảng cha sang bảng con. Ở Format panel → **Line start** chọn ký hiệu phía cha (`ERmandOne` hoặc `ERzeroToOne`), **Line end** chọn ký hiệu phía con (thường `ERzeroToMany`). Physical: nét đứt nếu là REF (Format → Pattern → dashed). Ghi nhãn `FK cột` / `REF cột` hoặc động từ. |
| Đổi bản số | Chọn đường → Format → Line start / Line end. Nhớ: đầu nào là ký hiệu của bảng ở đầu đó. |
| Đổi chữ nhãn | Double-click lên nhãn trên đường. |
| Xuất ảnh/PDF | File → Export as → PNG/SVG/PDF; chọn "All pages" nếu muốn đủ 3 trang. |

Giữ phong cách: nền trắng, viền đen, chữ đen, không tô màu.

## 8. Khi schema thay đổi – checklist cập nhật

| Thay đổi trong code | Physical | Logical | Conceptual | Tài liệu này |
|---|---|---|---|---|
| Thêm/xóa/đổi tên **cột thường** | Sửa dòng trong bảng | Sửa thuộc tính (tên property C#) | Không đổi | Mục 5 |
| Thêm **FK** cùng DB | Thêm cột `FK` + đường liền | Thêm đường quan hệ (không thêm thuộc tính) | Thêm quan hệ nếu có ý nghĩa nghiệp vụ | Mục 5, 6.1 |
| Thêm cột lưu ID của service khác | Thêm cột + đường nét đứt `REF` | Thêm đường quan hệ | Có thể thêm quan hệ | Mục 5, 6.2 |
| Đổi NULL ↔ NOT NULL ở cột FK/REF | Đổi chữ NULL + ký hiệu phía cha (`ERzeroToOne` ↔ `ERmandOne`) | Đổi ký hiệu tương ứng | Đổi nếu có | Mục 5, 6 |
| Thêm UNIQUE cho cột FK | Thêm `UK` + đổi ký hiệu phía con thành `ERzeroToOne` | Đổi ký hiệu | Đổi nếu có | Mục 5, 6 |
| Thêm **bảng** mới | Thêm bảng vào đúng DB (ghi `[xxx DB]`) | Thêm thực thể (trừ bảng trung gian N–N thuần) | Thêm nếu là khái niệm nghiệp vụ, bỏ qua nếu là bảng kỹ thuật | Mục 1, 3–6 |
| Thêm role mới | – | – | Thêm loại con dưới `User` | Mục 3 |

Cách kiểm tra nhanh sau khi sửa: đếm lại số bảng/cột/FK ở mục 1 so với migration mới nhất (`dotnet ef migrations list` theo từng DbContext) hoặc so với các file `*ModelSnapshot.cs`.

## 9. Giả định và điểm cần xác nhận

| # | Nội dung | Lý do / cần làm |
|--:|---|---|
| 1 | `procedure_drafts.created_by`, `reviewed_by` (varchar) được coi là ID người dùng → REF tới `users` | Code gán giá trị `actor`; cần xác nhận actor là user id chứ không phải username |
| 2 | `procedures.form_definitions` (JSON) chứa ID mẫu → REF N–N tới `form_templates` | Suy ra từ `FormDefinitionSchema`; nên kiểm tra field ID mẫu trong schema JSON |
| 3 | `notifications.source_event_id` → `notification_inbox.Id`, và `notification_inbox.Id` → `workflow_outbox.id` | `NotificationInboxWorker` gán `SourceEventId = e.EventId` |
| 4 | `workflow_outbox.payload` (JSON) chứa `ApplicationId` → REF tới `applications` | Theo payload sự kiện APPLICATION_* |
| 5 | `chat_messages.citations` (JSON) chứa ID `knowledge_chunks` → REF N–N | Theo TASK-29 |
| 6 | Conceptual: *Manager reassigns Assignment Record* và *IT Admin manages Role* | Dựa trên quyền (`workflow.assign.manage`, API RBAC), không phải cột riêng |
| 7 | Cột/trạng thái legacy (`approved_at`, `APPROVED`, `REJECTED`, `Legacy*Count`) vẫn vẽ vì còn trong DB | Không có nghĩa là luồng duyệt/từ chối hành chính còn dùng |
| 8 | 6 bảng FUTURE (`ai_request_logs`, `ocr_scan_logs`, `audit_logs`, `system_settings`, `service_health_logs`, `system_backup_logs`) **không vẽ** | Chưa có trong source |
| 9 | CHECK constraint không vẽ trên sơ đồ | Đã liệt kê đầy đủ ở mục 5 |

