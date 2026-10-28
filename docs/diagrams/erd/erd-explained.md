# Giải thích ERD WardMate (bản dễ hiểu)

Tài liệu này giải thích các sơ đồ ERD trong thư mục `docs/diagrams/erd` cho người không đọc code: thầy cô chấm báo cáo, thành viên frontend, người mới vào dự án. Số liệu đối chiếu EF Core model ngày 24/10/2026, kiểm tra lại ngày 26/10/2026 không đổi.

## 1. ERD là gì, đọc thế nào

Hãy coi cơ sở dữ liệu như một tủ hồ sơ. Mỗi **bảng** là một loại sổ (sổ tài khoản, sổ hồ sơ, sổ thông báo...), mỗi **dòng** là một trang ghi một đối tượng, mỗi **cột** là một ô thông tin trên trang đó. ERD là bản đồ cho biết có những loại sổ nào và sổ nào trỏ sang sổ nào.

| Ký hiệu trên hình | Ý nghĩa |
|---|---|
| Hộp có tên bảng | Một bảng (một loại sổ) |
| **PK** (khóa chính) | Mã định danh duy nhất của mỗi dòng, ví dụ `id` |
| **FK** (khóa ngoại), **nét liền** | Ràng buộc thật trong database: dòng con bắt buộc trỏ tới một dòng cha đang tồn tại |
| **REF**, **nét đứt** | Tham chiếu logic: bảng lưu mã của bảng ở service khác, nhưng database không kiểm tra được (xem mục 2) |
| Đầu "chân chim" (nhiều nhánh) | Phía "nhiều" |
| Đầu một gạch / hai gạch | Phía "một" (bắt buộc) |
| Đầu có vòng tròn | "Có thể không có" (0) |

Ví dụ: `procedure_categories ||──o{ procedures` đọc là "một lĩnh vực có không, một hoặc nhiều thủ tục; mỗi thủ tục thuộc đúng một lĩnh vực".

## 2. Bức tranh lớn: 6 service, mỗi service một kho dữ liệu riêng

WardMate chia thành 6 service. Mỗi service **chỉ được đọc/ghi kho dữ liệu của chính nó** (kiến trúc database-per-service). Trên hình, màu tiêu đề bảng cho biết bảng thuộc service nào.

| Service | DbContext | Giữ dữ liệu gì | Số bảng |
|---|---|---|---|
| IAM | IamDbContext | Tài khoản, vai trò, quyền, phường, OTP | 11 |
| ProcedureCatalog | ProcedureDbContext | Lĩnh vực, thủ tục, phiên bản, bản nháp từ PDF | 4 |
| ApplicationWorkflow | WorkflowDbContext | Hồ sơ của công dân và toàn bộ quá trình xử lý | 7 |
| DocumentForm | DocumentDbContext (schema `document`) | Mẫu tờ khai, bản khai, giấy tờ đính kèm, PDF, QR | 7 |
| AIOCR | ChatDbContext | Hội thoại chatbot, Knowledge Base | 4 |
| AnalyticsSystem | NotificationDbContext + AnalyticsDbContext (schema `analytics`) | Thông báo và số liệu thống kê, đánh giá | 4 + 4 |

**Tổng (bản chính xác):** 41 bảng, 410 cột, 22 FK, 46 REF.

**Vì sao giữa các service chỉ có nét đứt (REF) mà không có FK?** Vì mỗi service có kho riêng, database của service này không "nhìn thấy" bảng của service kia. Ví dụ, `applications.user_id` lưu mã người dùng nhưng bảng `users` nằm ở IAM. Tính đúng đắn của mã được đảm bảo bằng code: service gọi API của service khác để kiểm tra, hoặc chép sẵn thông tin cần dùng (snapshot) vào bảng của mình. Đổi lại, mỗi service triển khai, sửa và mở rộng độc lập.

## 3. Đi theo một hồ sơ: dữ liệu chạy qua các bảng thế nào

Cách dễ hiểu nhất là theo chân một công dân nộp hồ sơ.

1. **Đăng ký tài khoản.** Một dòng mới trong `users` (tên đăng nhập, email, mật khẩu đã băm) và `user_profiles` (họ tên, CCCD, ngày sinh, địa chỉ). `user_roles` gắn vai trò công dân. `welcome_emails` xếp hàng email chào mừng.
2. **Đăng nhập, bảo mật.** Mỗi phiên đăng nhập có một `refresh_tokens`. Quên mật khẩu hoặc xác minh số điện thoại tạo `user_otps`; chỉ lưu bản băm của mã OTP, không lưu mã gốc.
3. **Tra cứu thủ tục.** Công dân xem `procedures`, nhóm theo `procedure_categories`. Mỗi lần thủ tục được sửa, `procedure_versions` lưu một bản chụp để có thể xem lại hoặc khôi phục.
4. **Tạo hồ sơ.** Một dòng `applications` ở trạng thái DRAFT. Hồ sơ chép lại tên thủ tục, lĩnh vực, mã phường ngay lúc tạo. Danh mục giấy tờ cần nộp được chép thành các dòng `application_checklists`. Đây là **snapshot**: thủ tục đổi về sau thì hồ sơ cũ không bị đổi theo.
5. **Khai và đính kèm giấy tờ.** Bản khai trên web lưu ở `application_forms`. File tờ khai DOCX điền theo mẫu (`form_templates`, `form_template_versions`) lưu ở `user_submissions`. Giấy tờ minh chứng (ảnh, PDF) lưu ở `supporting_documents`, gắn với từng dòng checklist.
6. **Nộp hồ sơ.** Mỗi lần nộp hoặc nộp lại tạo một `application_versions` (bản chụp toàn bộ nội dung). Mỗi lần đổi trạng thái ghi một dòng `application_status_history`. Hạn kiểm tra 45 giờ làm việc lưu ngay trên `applications` (các cột `due_at_utc`, `deadline_status`...).
7. **Cán bộ xử lý.** Cán bộ nhận hồ sơ, hoặc được Manager giao/chuyển; mỗi lần như vậy ghi `application_assignment_history`. Nhận xét tại từng ô hoặc từng giấy tờ lưu ở `application_comments`, gắn với đúng phiên bản hồ sơ cán bộ đang xem.
8. **Giấy tờ sẵn sàng.** Khi hồ sơ đạt DOCUMENTS_READY, hệ thống xuất PDF vào `generated_documents` và mã QR vào `approved_qrs`. Tên bảng có chữ "approved" là tên cũ được giữ lại; đây **không phải** phê duyệt hành chính.
9. **Tiếp nhận tại quầy.** Cán bộ ghi nhận OFFICIALLY_RECEIVED trên `applications`, kèm số biên nhận và ngày hẹn.
10. **Thông báo.** Mỗi thay đổi quan trọng, Workflow ghi một sự kiện vào `workflow_outbox`. Analytics nhận sự kiện vào `notification_inbox`, tạo `notifications` cho từng người nhận, rồi mỗi kênh gửi (trong app, email, SMS) là một `notification_deliveries`. Thao tác gửi lại hoặc đối soát của IT Admin ghi `notification_delivery_audits`.
11. **Đánh giá và thống kê.** Công dân đánh giá 1–5 sao vào `citizen_feedback`, mỗi hồ sơ tối đa một lần. Số liệu cho Manager được tổng hợp sẵn theo ngày vào `application_procedure_metrics` và `daily_performance_metrics`; `metric_refreshes` ghi lần tổng hợp gần nhất.
12. **Hỏi chatbot.** Mỗi cuộc trò chuyện là một `chat_sessions` gồm nhiều `chat_messages`. Nguồn tri thức là `knowledge_documents`, cắt thành `knowledge_chunks` có vector embedding; câu trả lời lưu trích dẫn tới các đoạn này.

## 4. Từng nhóm bảng

### IAM: con người và quyền hạn

| Bảng | Hiểu nôm na | Quan hệ chính |
|---|---|---|
| `users` | Tài khoản đăng nhập | Thuộc một `wards` (nếu là cán bộ); cột `security_stamp` đổi khi đổi mật khẩu để đăng xuất mọi phiên cũ |
| `user_profiles` | Thông tin cá nhân | 1 user – 0..1 profile |
| `roles`, `permissions` | Vai trò (CITIZEN, FRONT_DESK_OFFICER, MANAGER, PROCEDURE_MANAGER, IT_ADMIN) và quyền chi tiết | Nối nhiều–nhiều qua `role_permissions` |
| `user_roles` | User nào giữ vai trò nào | Bảng nối users – roles |
| `wards` | Danh mục phường | Một phường có nhiều cán bộ |
| `refresh_tokens` | Phiên đăng nhập | 1 user – nhiều token |
| `user_otps` | Mã OTP (đã băm), kênh EMAIL/SMS, số lần thử, hạn dùng | 1 user – nhiều OTP |
| `welcome_emails` | Hàng đợi email chào mừng | 1 user – 0..1 |
| `rbac_audit_logs` | Nhật ký ai đổi vai trò/quyền của ai | REF tới users, roles, permissions |

### ProcedureCatalog: thủ tục hành chính

| Bảng | Hiểu nôm na | Quan hệ chính |
|---|---|---|
| `procedure_categories` | Lĩnh vực (hộ tịch, cư trú...) | 1 lĩnh vực – nhiều thủ tục |
| `procedures` | Thủ tục đang áp dụng: tên, cơ quan, phí, thời hạn, nội dung, checklist, mẫu tờ khai (lưu dạng JSON) | Thuộc một lĩnh vực |
| `procedure_versions` | Bản chụp mỗi phiên bản thủ tục | 1 thủ tục – nhiều phiên bản |
| `procedure_drafts` | Bản nháp do AI đọc từ PDF, chờ người duyệt; có cột lease/attempts cho worker xử lý nền | REF tới thủ tục được xuất bản |

### ApplicationWorkflow: hồ sơ và quá trình xử lý

`applications` là bảng trung tâm của cả hệ thống; 6 bảng còn lại đều là "con" của nó.

| Bảng | Hiểu nôm na |
|---|---|
| `applications` | Một hồ sơ: ai nộp, thủ tục nào, phường nào, trạng thái, cán bộ phụ trách, hạn xử lý, số biên nhận |
| `application_checklists` | Danh sách giấy tờ cần nộp (snapshot từ thủ tục) và tình trạng từng mục |
| `application_versions` | Bản chụp mỗi lần nộp/nộp lại |
| `application_comments` | Nhận xét của cán bộ, gắn với một phiên bản cụ thể |
| `application_status_history` | Nhật ký đổi trạng thái (từ → đến, ai, lý do) |
| `application_assignment_history` | Nhật ký nhận/trả/giao/chuyển hồ sơ |
| `workflow_outbox` | Hộp thư đi chứa sự kiện chờ gửi sang RabbitMQ |

Vòng đời trạng thái: DRAFT → SUBMITTED → UNDER_REVIEW → NEED_REVISION (nộp lại) → DOCUMENTS_READY → OFFICIALLY_RECEIVED; công dân có thể CANCELLED khi ở DRAFT hoặc NEED_REVISION.

Riêng `application_comments` có FK kép (`application_id` + `application_version_id`) tới `application_versions`: database bảo đảm nhận xét luôn trỏ tới phiên bản **của chính hồ sơ đó**, không thể gắn nhầm sang hồ sơ khác.

### DocumentForm: giấy tờ và tờ khai

| Bảng | Hiểu nôm na |
|---|---|
| `form_templates` | Mẫu tờ khai (DOCX) |
| `form_template_versions` | Từng phiên bản của mẫu, kèm định nghĩa các ô cần điền |
| `user_submissions` | Tờ khai công dân đã điền theo một phiên bản mẫu |
| `application_forms` | Dữ liệu e-form điền trên web |
| `supporting_documents` | Giấy tờ minh chứng tải lên, gắn với mục checklist |
| `generated_documents` | PDF hệ thống xuất ra |
| `approved_qrs` | Mã QR tra cứu, mỗi hồ sơ tối đa một mã |

Các bảng ở đây có `is_deleted`/`deleted_at`: xóa mềm, đánh dấu đã xóa chứ không xóa hẳn, để giữ dấu vết.

### AIOCR: trợ lý AI

| Bảng | Hiểu nôm na |
|---|---|
| `chat_sessions` / `chat_messages` | Cuộc trò chuyện và từng tin nhắn; tin trả lời có cột `citations` |
| `knowledge_documents` | Tài liệu nguồn: thủ tục đồng bộ sang, văn bản, FAQ, PDF |
| `knowledge_chunks` | Các đoạn nhỏ của tài liệu kèm vector embedding để tìm kiếm theo nghĩa |

### AnalyticsSystem: thông báo và thống kê

| Bảng | Hiểu nôm na |
|---|---|
| `notification_inbox` | Hộp thư đến nhận sự kiện từ Workflow; khóa chính là mã sự kiện nên một sự kiện không bị ghi hai lần |
| `notifications` | Thông báo gửi tới một người hoặc cả phường |
| `notification_deliveries` | Từng lần gửi theo kênh (IN_APP, EMAIL, SMS): trạng thái, số lần thử, mã tin của nhà mạng |
| `notification_delivery_audits` | Nhật ký thao tác quản trị trên một lần gửi |
| `citizen_feedback` | Đánh giá của công dân |
| `application_procedure_metrics` | Số hồ sơ theo ngày × thủ tục × phường |
| `daily_performance_metrics` | Hiệu suất cán bộ theo ngày × thủ tục × phường |
| `metric_refreshes` | Lần tổng hợp số liệu gần nhất |

Một số bảng nhóm này đặt tên cột kiểu PascalCase (`NotificationId`, `CreatedAt`). Đó là tên thật trong database, giữ nguyên trên sơ đồ Physical.

## 5. Ba mức sơ đồ khác nhau thế nào

| Trang | Dành cho | Thể hiện |
|---|---|---|
| **Conceptual** | Người đọc nghiệp vụ | Các khái niệm (Tài khoản, Hồ sơ, Thủ tục...) và quan hệ giữa chúng; không có cột |
| **Logical** | Phân tích thiết kế | Đầy đủ bảng, thuộc tính, PK/FK, bản số quan hệ; chưa có kiểu dữ liệu |
| **Physical** | Lập trình, quản trị DB | Đúng như database PostgreSQL: schema, kiểu dữ liệu, NOT NULL/NULL |

## 6. Hai bản ERD: bản chính xác và bản báo cáo

- **Bản chính xác** (`wardmate-erd-en.drawio`, `wardmate-erd-interactive.html`): 41 bảng đang có trong code. Đánh dấu thêm 6 bảng **FUTURE** bằng viền nét đứt.
- **Bản báo cáo** (`wardmate-erd-report-en.drawio`, thư mục `report/`): vẽ 6 bảng đó như bảng thường, tổng 47 bảng, 23 FK, 49 REF.

| Bảng dự kiến | Service | Mục đích |
|---|---|---|
| `ai_request_logs` | AIOCR | Nhật ký mỗi lần gọi AI (gắn với `chat_sessions`) |
| `ocr_scan_logs` | AIOCR | Nhật ký mỗi lần quét OCR |
| `audit_logs` | Analytics | Nhật ký thao tác chung của hệ thống |
| `system_settings` | Analytics | Cấu hình hệ thống |
| `service_health_logs` | Analytics | Theo dõi tình trạng các service |
| `system_backup_logs` | Analytics | Nhật ký sao lưu |

**Lưu ý trung thực:** 6 bảng này **chưa có** entity, migration hay bảng trong database. Kiểu dữ liệu của chúng là đề xuất. Khi báo cáo cần nói về tình trạng triển khai, dùng số của bản chính xác.

## 7. Câu hỏi thường gặp

- **Sao `applications` lại chép tên thủ tục, tên lĩnh vực?** Để hồ sơ giữ đúng thông tin tại thời điểm nộp và không phải gọi sang service khác mỗi lần hiển thị (snapshot).
- **Sao có cả `application_versions` lẫn `application_status_history`?** Versions lưu *nội dung* mỗi lần nộp. Status history lưu *việc đổi trạng thái*. Hai thứ khác nhau.
- **Xóa người dùng thì sao?** Trong IAM, FK có `ON DELETE CASCADE` nên token, OTP, profile, vai trò của user bị xóa theo. Hồ sơ ở Workflow không bị ảnh hưởng vì chỉ là REF.
- **`ON DELETE RESTRICT` nghĩa là gì?** Không cho xóa dòng cha khi còn dòng con, ví dụ không xóa được lĩnh vực còn thủ tục.
- **Có bảng "phê duyệt" không?** Không. Hệ thống hỗ trợ chuẩn bị và kiểm tra giấy tờ; DOCUMENTS_READY nghĩa là giấy tờ sẵn sàng, việc tiếp nhận chính thức diễn ra tại quầy. Cột `LegacyApprovedCount`/`LegacyRejectedCount` trong thống kê chỉ để giữ số liệu cũ.

## Nguồn

- `model-inventory.json`, `schema/*.sql`: xuất từ EF Core model.
- Sơ đồ: [README.md](README.md), [report/README.md](report/README.md).
- Nghiệp vụ: `docs/api-guide.md`, `docs/system-constraints.md`.
