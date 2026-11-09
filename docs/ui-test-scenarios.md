# Bộ kịch bản test giao diện WardMate (02/11/2026)

Người viết: Backend (Claude). Người dùng: chủ dự án và FE, để test trên giao diện thật xem chức năng nào đã chạy, chức năng nào lỗi, chức năng nào FE chưa có màn hình.

- Danh sách API lấy trực tiếp từ controller trong source ngày 02/11/2026: **224 API công khai** (IAM 46, ProcedureCatalog 28, Workflow 70, DocumentForm 31, AIOCR 20, Analytics 29), cộng 1 SignalR hub và 6 API nội bộ. Cập nhật 08/11: thêm 25 API lịch làm/đơn nghỉ Word (KB-13) và 15 API quản trị hệ thống (KB-20).
- Hợp đồng chi tiết (body, DTO, mã lỗi) xem `docs/api-guide.md`; quy tắc nghiệp vụ xem `docs/system-constraints.md`.
- Mục 4 là **ma trận phủ API**: mỗi API đều thuộc ít nhất một kịch bản. Chạy hết KB-00 → KB-20 là đã gọi qua toàn bộ API.
- Tài liệu này không chứa URL nội bộ, mật khẩu hay token. Tài khoản test do chủ dự án cấp riêng.

---

## 1. Cách test và ghi kết quả

### 1.1 Cách chạy
1. Test qua giao diện FE trỏ tới Gateway Azure. Mở DevTools → tab **Network** để xem route, mã HTTP và body lỗi.
2. Chạy **đúng thứ tự KB-00 → KB-19**: kịch bản sau dùng dữ liệu kịch bản trước tạo ra (hồ sơ, ca làm, cán bộ...).
3. Lần gọi đầu sau một lúc không dùng có thể mất 5–15 giây (service khởi động lại). Chờ tối đa 30 giây rồi mới tính là lỗi.
4. Sau khi đổi quyền, khóa tài khoản hoặc đổi mật khẩu: chờ khoảng 30 giây hoặc đăng nhập lại (quyền được cache 30 giây).

### 1.2 Cách ghi kết quả từng bước
Mỗi bước điền một trong bốn giá trị vào cột **KQ**:

| Ký hiệu | Nghĩa |
|---|---|
| **Đ** | Đạt: giao diện chạy đúng, API trả đúng mã trong cột "Kỳ vọng" |
| **L** | Lỗi: API trả sai mã / sai dữ liệu / giao diện hiển thị sai. Ghi phiếu lỗi theo mục 5 |
| **UI** | FE chưa có màn hình hoặc nút cho bước này (BE có API, FE chưa làm) |
| **B** | Bị chặn: bước trước lỗi nên không làm tiếp được |

Cuối mỗi kịch bản, đếm số Đ/L/UI/B để biết **đã làm gì, chưa làm gì, còn thiếu gì**.

### 1.3 Quy ước viết tắt
- `{appA}`, `{appB}`...: ID hồ sơ tạo ra trong lúc test. `{subA}`: submissionId của đơn trong hồ sơ A. `{docA}`: ID giấy tờ minh chứng.
- Route ghi không có tiền tố Gateway. Ví dụ `GET /api/v1/procedures` nghĩa là `GET {gateway}/api/v1/procedures`.
- "Kỳ vọng" ghi mã HTTP thành công và mã lỗi chính cần thử.

---

## 2. Chuẩn bị dữ liệu (làm một lần trước khi test)

### 2.1 Tài khoản cần có

| Mã | Vai trò | Phường | Ghi chú |
|---|---|---|---|
| ADMIN | IT_ADMIN | — | Có `iam.*`, `workflow.calendar.manage`, `notifications.deliveries.*`, `knowledge.manage` |
| PM | PROCEDURE_MANAGER | — | Có `procedure.*`, `knowledge.manage` |
| MGR-A | MANAGER | Phường A | Có `workflow.dispatch.*`, `workflow.staffing.*`, `workflow.assign.manage` |
| OFF-A1 | FRONT_DESK_OFFICER | Phường A | Cán bộ thụ lý chính; gán lĩnh vực của thủ tục test |
| OFF-A2 | FRONT_DESK_OFFICER | Phường A | Cán bộ thứ hai: nhận chuyển giao, làm chuyên viên tham vấn; cùng lĩnh vực |
| OFF-B1 | FRONT_DESK_OFFICER | Phường B | Dùng để test chặn khác phường |
| CIT-1 | CITIZEN | — | Công dân chính, có Email thật để nhận OTP |
| CIT-2 | CITIZEN | — | Công dân thứ hai, dùng để test chặn xem hồ sơ người khác |

ADMIN, PM, MGR-A, OFF-A1, OFF-A2 và OFF-B1 có thể được **tạo ngay trong KB-03**. Chỉ cần sẵn ADMIN.

### 2.2 Dữ liệu cần có
- 1 thủ tục **đang hoạt động** ở phường A, có checklist gồm ít nhất: 1 mục điền **mẫu DOCX** và 1 mục **ảnh/PDF giấy tờ** (ví dụ CCCD). Có thể tạo trong KB-04.
- File mẫu: 1 PDF thủ tục có lớp chữ (để bóc tách), 1 DOCX mẫu đơn, ảnh CCCD mặt trước/sau (dùng **ảnh mẫu, không dùng CCCD thật**), 1 PDF/ảnh giấy tờ minh chứng.
- **OFF-A1 và OFF-A2 phải có ca làm đang diễn ra** (KB-07) thì Manager mới giao được hồ sơ và tạo được tham vấn.

### 2.3 Lưu ý về AI
- Gemini gói miễn phí dùng chung hạn mức cho cả hệ thống. **Mỗi API AI chỉ gọi thật 1–2 lần** trong đợt test.
- Playwright/test tự động phải **mock** API AI, không gọi thật.

---

## 3. Kịch bản test

### KB-00 — Hạ tầng (BE/Antigravity, không cần giao diện)

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Gọi health của Gateway | `GET /health` | 200 `Healthy` | Đ (02/11, Azure) |
| 2 | Gọi `/health/ready` của 6 service (theo runbook) | `GET /health/ready` (mỗi service) | 200; `database` reachable; Workflow/Analytics có `rabbitmq` connected | Đ (02/11, Azure: 6/6 200; Analytics có cả `notifications-database` và `analytics-database`) |
| 3 | Gọi một API bảo vệ không token | `GET /api/v1/officer/applications` | 401 (chứng tỏ Gateway định tuyến đúng) | Đ (02/11, Azure) |

### KB-01 — Khách chưa đăng nhập

**Vai trò:** không đăng nhập. **Mục tiêu:** trang công khai, tra cứu thủ tục, chatbot khách.

| # | Bước trên giao diện | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Mở trang chủ, xem khối "Thủ tục nổi bật" | `GET /api/v1/procedures/featured` | 200, tối đa 5 thủ tục | Đ (02/11) |
| 2 | Mở bộ lọc lĩnh vực | `GET /api/v1/procedures/categories` | 200, danh sách lĩnh vực | Đ (02/11) |
| 3 | Tìm kiếm thủ tục theo từ khóa, lọc lĩnh vực, sang trang 2 | `GET /api/v1/procedures` | 200, có phân trang | Đ (02/11) |
| 4 | Mở chi tiết một thủ tục (checklist, phí, thời hạn) | `GET /api/v1/procedures/{id}` | 200; id sai → 404 | Đ (02/11) |
| 5 | Bấm xem văn bản nguồn PDF của thủ tục (nếu có) | `GET /api/v1/procedures/{id}/source` | 200 file PDF, hoặc 404 nếu thủ tục không có nguồn | Đ (02/11) |
| 6 | Danh sách phường công khai. Đăng ký **không** chọn phường; công dân chọn phường **khi tạo hồ sơ** (`wardCode` trong `POST /api/v1/applications`, KB-06). Khi chưa đăng nhập: mở thẳng URL API trên trình duyệt | `GET /api/v1/wards` | 200 `[{id,code,name}]`, có TEST_A, TEST_B | UI (02/11: FE chưa có ô chọn phường khi tạo hồ sơ. BE đã bắt buộc: thiếu `wardCode` → 400 "Phải chọn phường/xã tiếp nhận."; mã không tồn tại → 400 `application.invalid_ward`. Việc của FE, ghi lại để làm sau đợt test) |
| 7 | Hỏi chatbot 1 câu khi chưa đăng nhập | `POST /api/v1/ai/chat/completions` | 200 có câu trả lời. Lần đầu có thể 503 `App port is not ready` vì AIOCR đang khởi động lạnh (0 replica) | Đ (02/11: lần đầu 503 do khởi động lạnh, FE vẫn hiện trả lời) |
| 8 | Khách hỏi quá **10 câu trong ngày** trên cùng IP (`Assistant:DailyGuestRequestsPerIp`, mặc định 10; dùng mock nếu có) | `POST /api/v1/ai/chat/completions` | Câu thứ 11 → 429 `ai.guest_quota_exceeded`; FE hiện thông báo mời đăng nhập | Bỏ qua (02/11: tốn hạn mức Gemini, chờ FE có mock. Lần thử cũ: câu 6 bị trình duyệt hủy `(canceled)`, cần FE kiểm tra) |

### KB-02 — Đăng ký, đăng nhập, phiên, hồ sơ cá nhân, mật khẩu

**Vai trò:** CIT-1 (công dân mới).

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Đăng ký tài khoản công dân mới (nếu CIT-1 đã seed bằng SQL thì dùng tài khoản thứ ba). Đăng ký chỉ gồm tên đăng nhập, email, mật khẩu, họ tên — không có CCCD, không chọn phường | `POST /api/v1/auth/register` | 201/200; trùng tên đăng nhập/email → 409; mật khẩu yếu (dưới 8 ký tự, thiếu chữ hoa hoặc ký tự đặc biệt) → 400 | Đ (02/11). 1a mật khẩu yếu: FE chặn ngay trên form nên không gửi request, không thấy 400 — chấp nhận. 1b đăng ký tài khoản mới: Đ. 1c đăng ký trùng email của CIT-1: 409 Đ |
| 2 | Kiểm tra hộp thư: có email chào mừng | (worker IAM) | Có email trong vài phút (nếu Email bật) | Đ (02/11: có email chào mừng) |
| 3 | Đăng nhập | `POST /api/v1/auth/login` | 200 access token; web nhận cookie refresh HttpOnly. Sai mật khẩu → 401 | Đ (02/11). 3a sai mật khẩu → 401 Đ. 3b đăng nhập đúng → 200 Đ |
| 4 | FE nạp menu theo quyền | `GET /api/v1/users/access-context` | 200 có roles, permissions, wardCode | Đ, không áp dụng FE (02/11: FE không gọi API này. API chủ yếu để các service BE kiểm tra trạng thái/quyền; FE lấy roles/permissions từ `GET /users/me`) |
| 5 | Mở trang "Tài khoản của tôi" | `GET /api/v1/users/me` | 200 | Đ (02/11) |
| 6 | Xem hồ sơ cá nhân (lần đầu có thể chưa có) | `GET /api/v1/users/me/profile` | 200 hoặc 404 khi chưa tạo | Đ (02/11: FE không gọi API này mà lấy hồ sơ từ trường `profile` của `GET /users/me` — hợp lệ) |
| 7 | Tạo hồ sơ cá nhân (địa chỉ, ngày sinh...) | `POST /api/v1/users/me/profile` | 201/200; tạo lần hai → 409 | Đ, không áp dụng (02/11: FE không có nút tạo; hồ sơ được tạo sẵn từ họ tên khi đăng ký, FE dùng luôn bước 8. POST chỉ cần khi tài khoản chưa có hồ sơ, ví dụ sau khi xóa) |
| 8 | Sửa hồ sơ cá nhân | `PUT /api/v1/users/me/profile` | 200 | Đ (02/11) |
| 9 | Xóa hồ sơ cá nhân rồi tạo lại (để các bước sau có dữ liệu) | `DELETE /api/v1/users/me/profile` | 204/200 | UI tùy chọn (02/11: FE chưa có nút xóa thông tin cá nhân. API cho người dùng tự xóa dữ liệu cá nhân mà vẫn giữ tài khoản — xem FE-03) |
| 10 | Để access token hết hạn (hoặc xóa token trong bộ nhớ), thao tác tiếp | `POST /api/v1/auth/refresh-token` | 200 token mới; FE tự gọi, không đá ra đăng nhập | Để sau (02/11) — xem FE-05. Cách test: đăng nhập, bật Preserve log, để yên 16 phút (token sống 15 phút), bấm sang trang có API → kỳ vọng 401 → `refresh-token` 200 → gọi lại 200, không bị đá ra. Kiểm tra cookie `refreshToken` có HttpOnly. Cookie mặc định `SameSite=Strict` nên nếu FE gọi khác domain thì refresh có thể 401 → cần `RefreshCookie__SameSite=None` trên IAM hoặc gọi cùng domain |
| 11 | Đổi mật khẩu khi đang đăng nhập | `POST /api/v1/users/me/change-password` | 200/204; mật khẩu cũ sai → 400; sau đó phiên cũ bị thu hồi | Đ (02/11). 11a sai mật khẩu cũ → 400 Đ. 11b đổi đúng → Đ |
| 12 | Đăng xuất | `POST /api/v1/auth/revoke-token` | 200/204; refresh sau đó → 401 | Đ (02/11) |
| 13 | Quên mật khẩu: nhập email, chọn kênh Email | `POST /api/v1/auth/password-otp/request` | 200/202; có email OTP 6 số. Chọn kênh SMS → 400 | Đ (02/11). OTP 6 số tới Gmail của CIT-1. Gửi tới email không có trong hệ thống: vẫn 202, không có thư — đúng thiết kế chống dò tài khoản, FE cần hiện thông báo trung tính (FE-04) |
| 14 | Nhập OTP và mật khẩu mới | `POST /api/v1/auth/password-otp/verify-and-change` | 200; OTP sai 5 lần → bị khóa thử; OTP hết hạn sau 5 phút | Đ (02/11). Sai OTP → 400 Đ; đúng OTP → 200, đăng nhập bằng mật khẩu mới Đ |
| 15 | Kiểm tra không còn màn hình xác minh số điện thoại | — | Không có nút/màn hình SMS | Đ (02/11: không còn màn hình/nút SMS) |

### KB-03 — IT_ADMIN quản trị tài khoản, phường, RBAC

**Vai trò:** ADMIN.

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Mở danh sách phường quản trị | `GET /api/v1/accounts/wards` | 200 | Đ (02/11) |
| 2 | Tạo phường A và phường B (nếu chưa có) | `POST /api/v1/accounts/wards` | 201/200; trùng mã → 409 | Đ (02/11). 2a tạo TEST_C Đ. 2b tạo trùng mã (chữ thường) → 409 Đ |
| 3 | Tạo cán bộ một cửa OFF-A1, OFF-A2 (phường A), OFF-B1 (phường B) | `POST /api/v1/accounts/front-desk` | 201; mật khẩu yếu → 400 | Đ (02/11). 3a mật khẩu yếu: FE chặn ngay trên form, không gửi request — chấp nhận. 3b tạo `kb03_off` Đ. 3c trùng email → 409 Đ |
| 4 | Tạo Manager MGR-A (bắt buộc chọn phường A) | `POST /api/v1/accounts/manager` | 201; thiếu phường → 400 | Đ (02/11). 4a thiếu phường: FE bắt buộc chọn ngay trên form — chấp nhận. 4b tạo `kb03_mgr` phường TEST_C Đ |
| 5 | Tạo Procedure Manager PM | `POST /api/v1/accounts/procedure-manager` | 201 | Đ (02/11) |
| 6 | Danh sách tài khoản | `GET /api/v1/accounts` | 200 | Đ (02/11) |
| 7 | Mở chi tiết một tài khoản | `GET /api/v1/accounts/{userId}` | 200 | UI (02/11: FE chưa dùng API chi tiết tài khoản — xem FE-06) |
| 8 | Danh sách người dùng (trang quản trị người dùng) | `GET /api/v1/users` | 200 | UI (02/11: FE chưa dùng API này — xem FE-06) |
| 9 | Tra cứu người dùng toàn hệ thống theo tên/email/CCCD, lọc vai trò | `GET /api/v1/admin/users` | 200; MGR-A gọi → 403 | Đ (02/11). 9a tìm/lọc Đ. 9b MGR-A gọi → 403 Đ |
| 10 | Danh sách hồ sơ cá nhân người dùng | `GET /api/v1/users/profiles` | 200 | Đ (02/11) |
| 11 | Xem hồ sơ cá nhân của một người | `GET /api/v1/users/{userId}/profile` | 200/404 | Đ (02/11) |
| 12 | Tạo hồ sơ cá nhân cho cán bộ mới | `POST /api/v1/users/{userId}/profile` | 201/200 | UI (02/11: giống KB-02, FE không dùng POST mà sửa luôn bằng PUT vì tài khoản cán bộ có sẵn hồ sơ — xem FE-06) |
| 13 | Sửa hồ sơ cá nhân đó | `PUT /api/v1/users/{userId}/profile` | 200 | Đ (02/11) |
| 14 | Xóa hồ sơ cá nhân đó (rồi tạo lại nếu cần) | `DELETE /api/v1/users/{userId}/profile` | 204/200 | UI (02/11: FE không có nút admin xóa hồ sơ cá nhân — xem FE-06) |
| 15 | Chuyển OFF-A2 sang phường B rồi trả lại phường A | `PUT /api/v1/accounts/{userId}/ward` | 200/204 | Đ (02/11) |
| 16 | Khóa CIT-2; CIT-2 thử đăng nhập; rồi mở khóa | `PUT /api/v1/accounts/{userId}/status` | 200/204; tài khoản bị khóa đăng nhập → 401/403 | Đ (02/11). 16a khóa CIT-2 Đ. 16b CIT-2 đăng nhập → 401 Đ. 16c mở khóa, đăng nhập lại Đ. 16d admin tự khóa mình → 409 Đ |
| 17 | Mở trang RBAC: danh sách vai trò | `GET /api/v1/rbac/roles` | 200 | Đ (02/11) |
| 18 | Danh sách quyền | `GET /api/v1/rbac/permissions` | 200 | Đ (02/11) |
| 19 | Tạo vai trò thử `TEST_ROLE` | `POST /api/v1/rbac/roles` | 201 | Đ (02/11) |
| 20 | Xem chi tiết và sửa tên/mô tả vai trò | `GET /api/v1/rbac/roles/{roleId}`, `PUT /api/v1/rbac/roles/{roleId}` | 200 | Đ (02/11). 20a xem/sửa mô tả TEST_ROLE Đ. 20b đổi tên vai trò hệ thống: FE khóa sẵn ô tên trên giao diện — chấp nhận (BE vẫn chặn 409) |
| 21 | Xem quyền của vai trò, cấp 1 quyền, thu hồi quyền | `GET /api/v1/rbac/roles/{roleId}/permissions`, `PUT /api/v1/rbac/roles/{roleId}/permissions/{permissionId}`, `DELETE /api/v1/rbac/roles/{roleId}/permissions/{permissionId}` | 200/204 | Đ (02/11) |
| 22 | Xem vai trò của CIT-2, gán `TEST_ROLE`, gỡ ra | `GET /api/v1/rbac/users/{userId}/roles`, `PUT /api/v1/rbac/users/{userId}/roles/{roleId}`, `DELETE /api/v1/rbac/users/{userId}/roles/{roleId}` | 200/204; tự nâng quyền cho chính mình bị chặn | Đ (02/11) |
| 23 | Xóa `TEST_ROLE` | `DELETE /api/v1/rbac/roles/{roleId}` | 204; xóa vai trò hệ thống → 409/400 | Đ (02/11). 23a xóa vai trò hệ thống: FE không hiện nút xóa — chấp nhận (BE vẫn chặn 409). 23b xóa TEST_ROLE Đ |
| 24 | Xem nhật ký RBAC | `GET /api/v1/rbac/audit-logs` | 200, thấy các thao tác ở bước 19–23 | Đ (02/11) |

### KB-04 — Procedure Manager: lĩnh vực, mẫu đơn, thủ tục, bản nháp PDF

**Vai trò:** PM. Kết quả cần dùng: 1 thủ tục hoạt động có checklist (cho KB-06).

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Tạo lĩnh vực mới | `POST /api/v1/procedure-manager/categories` | 201 | Đ (05/11). 1b tạo trùng tên → 409 Đ |
| 2 | Sửa tên lĩnh vực | `PUT /api/v1/procedure-manager/categories/{id}` | 200 | Đ (05/11) |
| 3 | Tạo lĩnh vực tạm rồi xóa | `DELETE /api/v1/procedure-manager/categories/{id}` | 204; xóa lĩnh vực đang có thủ tục → 409 | Đ (05/11). 3a xóa lĩnh vực tạm 204 Đ; 3b xóa lĩnh vực đang có thủ tục → 409 `category.in_use` Đ |
| 4 | Tạo mẫu đơn DOCX (metadata) | `POST /api/v1/form-templates` | 201 | Đ (05/11) |
| 5 | Tải file DOCX của mẫu lên | `POST /api/v1/form-templates/{templateId}/upload-docx` | 200; file không phải DOCX → 400 | Đ (05/11). 5a FE chỉ cho chọn file .docx — chấp nhận (BE vẫn chặn 400); 5b tải DOCX thật Đ |
| 6 | Xem danh sách và chi tiết mẫu, tải mẫu về | `GET /api/v1/form-templates`, `GET /api/v1/form-templates/{templateId}`, `GET /api/v1/form-templates/{templateId}/download-docx` | 200 | Đ (05/11) |
| 7 | Trong màn hình checklist, chọn mẫu đơn để liên kết | `GET /api/v1/procedure-manager/document-forms` | 200 danh sách mẫu chọn được | UI (05/11). Danh sách mẫu gọi được, nhưng màn checklist thủ tục nhập tay không có chỗ liên kết mẫu (xem bước 12, FE-07) |
| 8 | Tạo thủ tục thủ công | `POST /api/v1/procedure-manager/procedures` | 201 | Đ (05/11). 8a tạo 201; 8b trùng mã → 409 `procedure.code_exists` Đ |
| 9 | Danh sách thủ tục quản trị (lọc, tìm, sắp xếp) | `GET /api/v1/procedure-manager/procedures` | 200 | Đ (05/11) |
| 10 | Mở chi tiết thủ tục quản trị | `GET /api/v1/procedure-manager/procedures/{id}` | 200 | Đ (05/11) |
| 11 | Sửa thủ tục (tạo phiên bản mới) | `PUT /api/v1/procedure-manager/procedures/{id}` | 200 | Đ (05/11). Khi sửa bắt buộc nhập số quyết định và ngày hiệu lực — đúng thiết kế (mỗi lần sửa tạo phiên bản có căn cứ) |
| 12 | Cấu hình checklist: 1 mục mẫu DOCX + 1 mục ảnh/PDF giấy tờ | `PUT /api/v1/procedure-manager/procedures/{id}/checklist` | 200 | UI (05/11). Lưu checklist được, nhưng không liên kết được mẫu đơn: BE để liên kết mẫu ở `formDefinitions` (gửi qua `PUT /procedures/{id}`), không nằm trong mục checklist (FE-07) |
| 13 | Xem lịch sử phiên bản | `GET /api/v1/procedure-manager/procedures/{id}/versions` | 200 ≥ 2 phiên bản | Đ (05/11) |
| 14 | Xem nguồn PDF của một phiên bản | `GET /api/v1/procedure-manager/procedures/{id}/versions/{versionId}/source` | 200 hoặc 404 nếu phiên bản không có nguồn | Đ (05/11). Thủ tục nhập tay không có PDF → 404, FE báo đúng |
| 15 | Rollback về phiên bản 1 rồi sửa lại cho đúng | `POST /api/v1/procedure-manager/procedures/{id}/versions/{versionNumber}/rollback` | 200 | Đ (05/11). BE không ràng buộc ngày hiệu lực của rollback phải sau phiên bản trước; phiên bản cũ giữ nguyên trong lịch sử, ngày hiệu lực chỉ để ghi nhận (BE-05) |
| 16 | Tắt rồi bật lại thủ tục | `PATCH /api/v1/procedure-manager/procedures/{id}/status` | 200; khi tắt, KB-01 không còn thấy thủ tục | Đ (05/11) |
| 17 | Xem nhanh kết quả AI đọc PDF (không lưu) | `POST /api/v1/procedure-manager/drafts/extract-preview` | 200 (có thể 10–60 giây) | Đ (05/11) |
| 18 | Tải PDF thủ tục để tạo bản nháp | `POST /api/v1/procedure-manager/drafts` | 202/201, trạng thái Queued | Đ (05/11) |
| 19 | Mở danh sách bản nháp, chờ trạng thái chuyển xong | `GET /api/v1/procedure-manager/drafts` | 200 | Đ (05/11) |
| 20 | Mở chi tiết bản nháp, đối chiếu với PDF | `GET /api/v1/procedure-manager/drafts/{id}`, `GET /api/v1/procedure-manager/drafts/{id}/source` | 200 | Đ (05/11) |
| 21 | Sửa dữ liệu bản nháp và lưu | `PUT /api/v1/procedure-manager/drafts/{id}` | 200 | Đ (05/11) |
| 22 | Nếu bóc tách lỗi: bấm thử lại | `POST /api/v1/procedure-manager/drafts/{id}/retry` | 200/202 | Không phát sinh (05/11) — bóc tách không lỗi |
| 23 | Xuất bản bản nháp thành thủ tục | `POST /api/v1/procedure-manager/drafts/{id}/publish` | 200; thiếu trường bắt buộc → 400/422 | Đ (05/11). 23a chưa xác nhận → chặn; 23b xuất bản Đ. FE bắt chọn biểu mẫu trước khi xuất bản — BE không bắt buộc `formTemplateId` (FE-08). AI bỏ sót 2/3 tờ khai ở thủ tục mẫu dichvucong (BE-04) |
| 24 | Xuất bản trực tiếp một thủ tục đã rà soát (payload đầy đủ) | `POST /api/v1/procedure-manager/procedures/publish` | 200; trùng → 409 | Đ (05/11) |
| 25 | Tạo thêm 1 bản nháp rồi hủy | `DELETE /api/v1/procedure-manager/drafts/{id}` | 204 | Đ (05/11) |

### KB-05 — Knowledge Base cho chatbot

**Vai trò:** PM hoặc ADMIN (cần `knowledge.manage`). Mặc định RAG/embedding **đang tắt**, nên các bước 10–12 có thể trả kết quả rỗng hoặc trạng thái "tắt". Đó không phải lỗi.

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Danh sách tài liệu tri thức | `GET /api/v1/ai/knowledge/documents` | 200 | Đ (07/11) |
| 2 | Thêm tài liệu văn bản / FAQ | `POST /api/v1/ai/knowledge/documents` | 201 | Đ (07/11). 2a FAQ 201 Đ; 2b nội dung/tiêu đề quá ngắn: FE chặn — chấp nhận (BE vẫn trả 400) |
| 3 | Thêm tài liệu PDF có lớp chữ | `POST /api/v1/ai/knowledge/documents/pdf` | 201; PDF scan không có chữ → 422 | Đ (07/11). 3a PDF có chữ 201; 3b PDF scan 422 `knowledge.pdf_no_text`; 3c sai loại/quá lớn bị chặn; 3d PDF không sửa được nội dung |
| 4 | Xem chi tiết và các đoạn đã chia | `GET /api/v1/ai/knowledge/documents/{id}`, `GET /api/v1/ai/knowledge/documents/{id}/chunks` | 200 | Đ (07/11) |
| 5 | Sửa tài liệu | `PUT /api/v1/ai/knowledge/documents/{id}` | 200 | Đ (07/11) |
| 6 | Tắt rồi bật tài liệu | `POST /api/v1/ai/knowledge/documents/{id}/activation` | 200 | Đ (07/11) |
| 7 | Chia đoạn lại | `POST /api/v1/ai/knowledge/documents/{id}/rechunk` | 200 | L (07/11) → **đã sửa source (BE-06)**. Trả 503 `knowledge.database_unavailable`: đoạn mới có sẵn khóa Guid nên EF cập nhật thay vì thêm mới. Chờ deploy AIOCR rồi test lại bước 7 (và bước 5 khi sửa **nội dung**) |
| 8 | Đồng bộ 1 thủ tục từ Catalog vào tri thức | `POST /api/v1/ai/knowledge/procedures/{procedureId}/sync` | 200 | Đ (07/11). 8a đồng bộ `KB04-KS` 200; 8b sửa tài liệu PROCEDURE → 409 `knowledge.source_not_editable` |
| 9 | Đồng bộ toàn bộ thủ tục | `POST /api/v1/ai/knowledge/procedures/sync` | 200 | Đ (07/11) |
| 10 | Xem trạng thái chỉ mục vector | `GET /api/v1/ai/knowledge/index-status` | 200 (có thể báo đang tắt) | Đ (07/11) |
| 11 | Yêu cầu tạo lại vector cho 1 tài liệu | `POST /api/v1/ai/knowledge/documents/{id}/reindex` | 200; embedding tắt → 503 `knowledge.embedding_disabled` | Đ (07/11). FE hiện "Vector đang tắt" thay vì gọi API — chấp nhận (embedding tắt theo quyết định chủ dự án) |
| 12 | Thử tìm kiếm tri thức | `POST /api/v1/ai/knowledge/search` | 200 (rỗng nếu chưa có vector); embedding tắt → 503 `knowledge.embedding_disabled` | Đ (07/11). 503 `knowledge.embedding_disabled` đúng khi embedding tắt |
| 13 | Xem thống kê lượt dùng AI theo ngày | `GET /api/v1/ai/knowledge/usage` | 200, có số lượt theo operation | UI (07/11). FE chưa có màn thống kê lượt dùng AI (FE-10) |

### KB-06 — Công dân tạo và nộp hồ sơ (luồng chính)

**Vai trò:** CIT-1. Kết quả cần dùng: hồ sơ `{appA}` (đi hết luồng), `{appB}` (để hủy), `{appC}` (cho luồng DocumentForm cũ, KB-11), `{appD}` (cho chuyển giao, KB-12). Tạo 4 hồ sơ, `{appA}` làm đầy đủ các bước; `{appC}` và `{appD}` chỉ cần nộp được.

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Chọn thủ tục ở KB-04, chọn phường A, bấm "Tạo hồ sơ" | `POST /api/v1/applications` | 201, trạng thái `DRAFT`, có checklist snapshot | |
| 2 | Đọc CCCD bằng AI (ảnh mặt trước và sau) để điền sẵn thông tin | `POST /api/v1/ai-ocr/cccd/extract` | 200; ảnh không đọc được → trường null; quá hạn mức → 429 | |
| 3 | Mở mục checklist có mẫu đơn: xem mẫu, tải mẫu | `GET /api/v1/form-templates/{templateId}`, `GET /api/v1/form-templates/{templateId}/download-docx` | 200 | |
| 4 | Điền đơn online và lưu nháp | `POST /api/v1/citizen/submissions/draft` | 201/200, có `submissionId` = `{subA}` | |
| 5 | Sửa đơn nháp | `PUT /api/v1/citizen/submissions/{submissionId}/draft` | 200 | |
| 6 | (Cách thứ hai) Tải lên DOCX đã điền offline cho một hồ sơ khác | `POST /api/v1/citizen/submissions/upload-filled-docx` | 201/200; thiếu ApplicationId/ChecklistId → 400 | |
| 7 | Chốt đơn | `POST /api/v1/citizen/submissions/{submissionId}/submit` | 200 | |
| 8 | Xem danh sách đơn của tôi, chi tiết, tải lại DOCX | `GET /api/v1/citizen/submissions`, `GET /api/v1/citizen/submissions/{submissionId}`, `GET /api/v1/citizen/submissions/{submissionId}/download-docx` | 200 | |
| 9 | Tải ảnh/PDF giấy tờ minh chứng cho mục checklist còn lại | `POST /api/v1/applications/{applicationId}/supporting-documents` | 201; file quá lớn / sai định dạng → 400/413 | |
| 10 | Xem danh sách giấy tờ, xem trước, tải về | `GET /api/v1/applications/{applicationId}/supporting-documents`, `GET /api/v1/supporting-documents/{documentId}/preview`, `GET /api/v1/supporting-documents/{documentId}/download` | 200 | |
| 11 | Tải nhầm 1 file rồi xóa | `DELETE /api/v1/supporting-documents/{documentId}` | 204; chỉ xóa được khi hồ sơ `DRAFT`/`NEED_REVISION` | |
| 12 | Bấm "Kiểm tra giấy tờ bằng AI" cho 1 giấy tờ | `POST /api/v1/ai-ocr/documents/verify` | 200 kết quả tham khảo; documentId sai → 404 | |
| 13 | Đánh dấu từng mục checklist đã hoàn thành | `PATCH /api/v1/applications/{id}/checklists/{checklistId}` | 200 | |
| 14 | Xem tổng hợp đơn + giấy tờ của hồ sơ trước khi nộp | `GET /api/v1/applications/{applicationId}/contents` | 200 | |
| 15 | Bấm "Nộp hồ sơ" khi còn thiếu 1 mục bắt buộc | `POST /api/v1/applications/{id}/submit` | 409/422 nêu mục còn thiếu | |
| 16 | Hoàn tất mục thiếu rồi nộp lại | `POST /api/v1/applications/{id}/submit` | 200, trạng thái `SUBMITTED`, có hạn xử lý | |
| 17 | Mở "Hồ sơ của tôi", mở chi tiết hồ sơ | `GET /api/v1/applications`, `GET /api/v1/applications/{id}` | 200; có trạng thái và hạn | |
| 18 | Tạo `{appB}` rồi bấm "Hủy hồ sơ" khi còn DRAFT | `POST /api/v1/applications/{id}/cancel` | 200 `CANCELLED`; hủy hồ sơ đã nộp → 409 | |
| 19 | Mở chatbot khi đã đăng nhập, hỏi 1 câu, xem lịch sử | `POST /api/v1/ai/chat/completions`, `GET /api/v1/ai/chat/sessions`, `GET /api/v1/ai/chat/sessions/{sessionId}/messages` | 200; có phiên chat được lưu | |
| 20 | CIT-2 mở link hồ sơ của CIT-1 | `GET /api/v1/applications/{id}` | 404/403, không lộ dữ liệu | |

### KB-07 — Manager: cán bộ, lĩnh vực, ca làm (chuẩn bị điều phối)

**Vai trò:** MGR-A. Phải làm **trước KB-08**.

| # | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | Mở danh sách cán bộ phường A | `GET /api/v1/manager/officers` | 200, chỉ thấy cán bộ phường A | |
| 2 | Gán lĩnh vực cho OFF-A1, OFF-A2 (đúng lĩnh vực thủ tục test) | `PUT /api/v1/manager/officers/{userId}/categories` | 204; sửa OFF-B1 → 404 | |
| 3 | Xem thông tin phân công của 1 cán bộ | `GET /api/v1/manager/officers/{userId}/assignment-context` | 200 | |
| 4 | Tạo ca làm cho OFF-A1 và OFF-A2 bao trùm thời gian test (bắt đầu sau 1–2 phút, dài 8 giờ) | `POST /api/v1/staffing/shifts` | 200; trùng ca → 409; giờ quá khứ → 400 | |
| 5 | Xem lịch ca làm của phường | `GET /api/v1/staffing/shifts` | 200 | |

### KB-08 — Điều phối và cán bộ thụ lý hồ sơ

**Vai trò:** MGR-A, OFF-A1. Dùng `{appA}`.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | MGR-A | Mở trang điều phối hồ sơ phường | `GET /api/v1/manager/applications` | 200, thấy `{appA}` chưa giao | |
| 2 | MGR-A | Xem thống kê phân công (ai đang giữ bao nhiêu hồ sơ) | `GET /api/v1/manager/applications/assignment-summary` | 200 | |
| 3 | MGR-A | Mở `{appA}` → danh sách cán bộ có thể giao | `GET /api/v1/manager/applications/{id}/assignment-candidates` | 200; OFF-A1 `eligible=true` | |
| 4 | MGR-A | Giao `{appA}` cho OFF-A1 | `POST /api/v1/manager/applications/{id}/assignments` | 200; `expectedAssignmentVersion` cũ → 409; cán bộ không có ca → 422 | |
| 5 | MGR-A | Xem lịch sử phân công | `GET /api/v1/manager/applications/{id}/assignments` | 200 | |
| 6 | OFF-A1 | Mở hàng đợi hồ sơ chờ nhận | `GET /api/v1/officer/applications/pending` | 200 | |
| 7 | OFF-A1 | Mở "Hồ sơ của tôi" | `GET /api/v1/officer/applications` | 200 thấy `{appA}` `UNDER_REVIEW` | |
| 8 | OFF-A1 | Mở chi tiết `{appA}` | `GET /api/v1/officer/applications/{id}` | 200; OFF-B1 mở → 404 | |
| 9 | OFF-A1 | Xem danh sách đơn của hồ sơ, tải DOCX | `GET /api/v1/officer/submissions/by-application/{applicationId}`, `GET /api/v1/officer/submissions/{submissionId}/download-docx` | 200 | |
| 10 | OFF-A1 | Ghi chú trực tiếp vào DOCX và tải lên | `PUT /api/v1/officer/submissions/{submissionId}/annotate-docx` | 200; hồ sơ không còn `UNDER_REVIEW` → 409 | |
| 11 | OFF-A1 | Xem tổng hợp + tải giấy tờ minh chứng | `GET /api/v1/applications/{applicationId}/contents`, `GET /api/v1/supporting-documents/{documentId}/download` | 200 | |
| 12 | OFF-A1 | Bấm "Trợ lý AI kiểm tra hồ sơ" | `POST /api/v1/ai-ocr/applications/{applicationId}/dossier-assist` | 200 có `overallAssessment`, `crossChecks`, `disclaimer`; MGR-A gọi → 403 `ai.not_assigned_officer` | |
| 13 | OFF-A1 | Bấm "Chèn gợi ý" → nội dung vào ô nhận xét (không tự gửi) | — (giao diện) | Ô nhận xét có `suggestedComment` để sửa | |
| 14 | OFF-A1 | Thêm 2 nhận xét trên 2 mục | `POST /api/v1/officer/applications/{id}/comments` | 200 | |
| 15 | OFF-A1 | Rút 1 nhận xét ghi nhầm (có lý do) | `POST /api/v1/officer/applications/{id}/comments/{commentId}/withdraw` | 200 | |
| 16 | OFF-A1 | Bấm "Yêu cầu bổ sung" | `POST /api/v1/officer/applications/{id}/request-revision` | 200, `NEED_REVISION`, hạn xử lý tạm dừng; không có nhận xét OPEN → 409 | |
| 17 | OFF-A1 | Xem nhật ký thao tác của tôi | `GET /api/v1/officer/activity` | 200 có các thao tác vừa làm | |

### KB-09 — Công dân bổ sung và nộp lại

**Vai trò:** CIT-1, sau đó OFF-A1. Dùng `{appA}`.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | CIT-1 | Nhận thông báo "Yêu cầu bổ sung" (xem KB-16) và mở hồ sơ | `GET /api/v1/applications/{id}` | 200 `NEED_REVISION` | |
| 2 | CIT-1 | Xem nhận xét của cán bộ | `GET /api/v1/applications/{id}/comments` | 200; nhận xét đã rút hiển thị là "đã rút" hoặc ẩn | |
| 3 | CIT-1 | Sửa đơn, thay giấy tờ theo nhận xét | `PUT /api/v1/citizen/submissions/{submissionId}/draft`, `POST /api/v1/applications/{applicationId}/supporting-documents` | 200/201 | |
| 4 | CIT-1 | Bấm "Nộp lại" | `POST /api/v1/applications/{id}/resubmit` | 200, về `UNDER_REVIEW` với OFF-A1; hạn tiếp tục chạy, không làm mới 45 giờ | |
| 5 | CIT-1 | Xem lịch sử phiên bản nộp | `GET /api/v1/applications/{id}/versions` | 200 ≥ 2 phiên bản | |
| 6 | CIT-1/OFF-A1 | So sánh phiên bản 1 và 2 | `GET /api/v1/applications/{id}/diff` | 200 danh sách trường thay đổi | |

### KB-10 — Xác nhận giấy tờ sẵn sàng, PDF, QR, tiếp nhận tại quầy, đánh giá

**Vai trò:** OFF-A1, CIT-1, khách. Dùng `{appA}`.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | OFF-A1 | Bấm "Xác nhận giấy tờ sẵn sàng" (không dùng chữ "phê duyệt") | `POST /api/v1/officer/applications/{id}/confirm-readiness` | 200 `DOCUMENTS_READY`; còn nhận xét OPEN → 409 `application.open_comments` | |
| 2 | CIT-1 | Bấm "Xuất PDF" cho đơn `{subA}` | `POST /api/v1/applications/{applicationId}/generate-pdf` | 200 có `id` PDF; hồ sơ chưa sẵn sàng → 409 `document.application_not_ready` | |
| 3 | CIT-1 | Xem danh sách PDF, tải PDF | `GET /api/v1/applications/{applicationId}/generated-documents`, `GET /api/v1/generated-documents/{documentId}/download` | 200; PDF không chứa ảnh QR (DOC-QR-04) | |
| 4 | CIT-1 | Xem thông tin mã QR, tải ảnh QR | `GET /api/v1/applications/{applicationId}/approved-qr`, `GET /api/v1/applications/{applicationId}/approved-qr/image` | 200 / ảnh PNG, kể cả khi chưa xuất PDF (DOC-QR-05, 09/11); `submissionId` có thể `null` | |
| 5 | Khách | Quét QR bằng điện thoại (không đăng nhập) | `GET /api/v1/applications/{id}` → chuyển hướng `GET /api/v1/public/qr-lookup` | Trang tra cứu công khai, không lộ dữ liệu cá nhân | |
| 6 | Khách | Tra cứu bằng mã token trong QR | `GET /api/v1/public/qr-lookup/{**token}` | 200; token sai → 404 | |
| 7 | Khách | Bấm "Xem PDF" trên trang tra cứu | `GET /api/v1/public/applications/{applicationId}/pdf-redirect` | Chuyển tới PDF | |
| 8 | OFF-A1 | Công dân mang bản gốc tới quầy → bấm "Tiếp nhận" (nhập số biên nhận, ngày hẹn) | `POST /api/v1/officer/applications/{id}/officially-received` | 200 `OFFICIALLY_RECEIVED` | |
| 9 | CIT-1 | Đánh giá 1–5 sao cho hồ sơ | `POST /api/v1/citizen/feedback/{applicationId}` | 201; đánh giá lần hai → 409; hồ sơ chưa xong → 409 | |
| 10 | CIT-1 | Xem lại đánh giá đã gửi | `GET /api/v1/citizen/feedback/{applicationId}` | 200 | |

### KB-11 — Route cũ của DocumentForm (tương thích)

FE mới nên dùng route Workflow ở KB-08/KB-10. Kịch bản này chỉ để xác nhận route cũ vẫn chạy nếu màn hình cũ còn dùng. Dùng `{appC}` (đã giao cho OFF-A1 như KB-08 bước 1–4).

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | OFF-A1 | Thêm 1 nhận xét, rồi bấm "Yêu cầu bổ sung" từ màn hình đơn | `POST /api/v1/officer/submissions/{submissionId}/request-revision` | 200 `{submissionId, message}`; hồ sơ `NEED_REVISION` | |
| 2 | CIT-1 | Nộp lại `{appC}` | `POST /api/v1/applications/{id}/resubmit` | 200 | |
| 3 | OFF-A1 | Bấm "Xác nhận sẵn sàng" từ màn hình đơn | `POST /api/v1/officer/submissions/{submissionId}/confirm-readiness` | 200; hồ sơ `DOCUMENTS_READY` (toàn hồ sơ, không riêng 1 đơn) | |

### KB-12 — Trả lại, chuyển giao, Manager đổi người thụ lý

Dùng `{appD}`.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | OFF-A2 | Tự nhận `{appD}` từ hàng đợi | `POST /api/v1/officer/applications/{id}/assign` | 200 `UNDER_REVIEW`; nhận hồ sơ do chính mình nộp → 403 | |
| 2 | OFF-A2 | Trả lại hồ sơ về hàng đợi (có lý do) | `POST /api/v1/officer/applications/{id}/release` | 200 về `SUBMITTED` | |
| 3 | MGR-A | Giao `{appD}` cho OFF-A2 bằng màn hình "Đổi người thụ lý" | `PUT /api/v1/manager/applications/{id}/assignee` | 200; thiếu/sai `expectedAssignmentVersion` → 400/409 | |
| 4 | MGR-A | Chuyển `{appD}` từ OFF-A2 sang OFF-A1 | `POST /api/v1/manager/applications/{id}/assignments` | 200; lịch sử phân công có 2 dòng | |
| 5 | OFF-A2 | Mở lại `{appD}` sau khi bị chuyển | `GET /api/v1/officer/applications/{id}` | 404 hoặc chỉ đọc; không thao tác được | |
| 6 | MGR-A | Thử bấm "Xác nhận sẵn sàng" trên `{appD}` | `POST /api/v1/officer/applications/{id}/confirm-readiness` | 403 (Manager chỉ giám sát) | |

### KB-13 — Lịch làm, lịch tháng, đổi ca và đơn nghỉ Word

**Cập nhật 08/11:** viết lại theo đợt WF-WS (25 API mới, xem `docs/Lịch làm việc Front Desk và đơn nghỉ Word.md`). Trước khi test, gọi bước 1. Nếu nhận **404**, bản Workflow trên Azure chưa có đợt WF-WS: ghi **B** cho các bước dùng API mới.

`{month}` là ngày đầu tháng, ví dụ `2026-12-01`. Giờ gửi kèm offset `+07:00`. Các bước 1–26 không cần hồ sơ (không thuộc luồng chính).

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | OFF-A1 | Mở màn nghỉ phép, đọc cấu hình mẫu Word | `GET /api/v1/staffing/capabilities` | 200 `wordRequired`, `templateConfigured`, `maxDocxBytes` | Đ (09/11) |
| 2 | MGR-A | Thêm 2 ca sáng 08:00–11:00 và chiều 13:00–16:00 cho OFF-A1 cùng một ngày tương lai | `POST /api/v1/staffing/shifts` (2 lần) | 200 cả hai; ca trùng giờ cùng người → 409; ca quá khứ hoặc dài hơn 24 giờ → 400 | Đ (09/11). 2a Đ; 2b trùng giờ, 2c ca quá khứ/dài hơn 24 giờ: FE chặn — chấp nhận |
| 3 | MGR-A, OFF-A1 | Xem lịch ca trong tuần | `GET /api/v1/staffing/shifts` | 200; Manager thấy cả phường, OFF-A1 chỉ thấy ca của mình | Đ (09/11) |
| 4 | MGR-A | Tạo 1 ca thử rồi hủy (có lý do) | `POST /api/v1/staffing/shifts`, `POST /api/v1/staffing/shifts/{id}/cancel` | 200; ca có `cancelledAtUtc` | Đ (09/11) |
| 5 | MGR-A | Mở và lưu mẫu tuần: thứ Hai (`day=1`) 2 ca cho OFF-A1 | `GET /api/v1/staffing/week-pattern`, `PUT /api/v1/staffing/week-pattern` | 200, `version` tăng; gửi `expectedVersion` cũ → 409 | Đ (09/11). 5a lưu, 5b `expectedVersion` cũ → 409 Đ |
| 6 | MGR-A | Xem trước lịch tháng sau từ mẫu tuần; thử thêm `copyFrom` tháng này | `GET /api/v1/staffing/schedules/{month}/preview` | 200 `slots`, `conflicts`, `closures`, `baseFingerprint`; chưa tạo ca thật | Đ (09/11) |
| 7 | MGR-A | Lưu nháp lịch tháng | `PUT /api/v1/staffing/schedules/{month}/draft` | 200 có revision nháp; `baseFingerprint` cũ → 409 | Đ (09/11) |
| 8 | MGR-A | Công bố lịch tháng. Thử trước: sau khi lưu nháp, thêm 1 ca lẻ trong tháng đó rồi công bố nháp cũ | `POST /api/v1/staffing/schedules/{month}/publish` | Nháp cũ → 409; tải lại, lưu nháp mới, công bố → 200; ca trong tháng thành ca chính thức | Đ (09/11). 8a nháp cũ → 409; 8b công bố nháp mới 200 |
| 9 | MGR-A | Xem lịch tháng và lịch sử công bố | `GET /api/v1/staffing/schedules/{month}`, `GET /api/v1/staffing/schedules/{month}/history` | 200; lịch sử có revision `PUBLISHED` | Đ (09/11) |
| 10 | OFF-A1 | Mở lịch tháng của mình, bấm xác nhận | `GET /api/v1/staffing/schedules/{month}`, `POST /api/v1/staffing/schedules/{month}/confirm` | 200 có biên nhận; lịch đã đổi (fingerprint cũ) → 409, tải lại rồi xác nhận | Đ (09/11) |
| 11 | OFF-A1 | Đề nghị đổi giờ 1 ca tương lai | `POST /api/v1/staffing/shift-change-requests` | 200 `PENDING`; ca gốc vẫn hiệu lực trong lúc chờ | Đ (09/11) |
| 12 | MGR-A | Xem đề nghị và duyệt | `GET /api/v1/staffing/shift-change-requests`, `POST /api/v1/staffing/shift-change-requests/{id}/decision` | 200 `APPROVED`; ca đổi sang giờ mới; OFF-A1 nhận thông báo | Đ (09/11) |
| 13 | OFF-A1 | Bấm "Xin nghỉ": lấy mẫu đơn hiện hành, tải xem | `GET /api/v1/staffing/leave-templates/current`, `GET /api/v1/staffing/leave-templates/{id}/docx` | 200, file mở được bằng Word. Chưa cấu hình mẫu (`templateConfigured=false`) → ghi **B** cho bước 13–18 | Đ (09/11) |
| 14 | OFF-A1 | Tạo nháp đơn nghỉ 1 ngày tuần sau | `POST /api/v1/staffing/leave-drafts` (header `Idempotency-Key`) | 200 có `id`, `version`, `documentRevisionId` | Đ (09/11) |
| 15 | OFF-A1 | Mở "Nháp của tôi", mở lại nháp, tải bản Word của nháp | `GET /api/v1/staffing/leave-drafts`, `GET /api/v1/staffing/leave-drafts/{id}`, `GET /api/v1/staffing/leave-drafts/{id}/docx` | 200; MGR-A mở nháp của OFF-A1 → 403/404 | Đ (09/11). 15a Đ; 15b MGR-A mở nháp của OFF-A1 bị chặn |
| 16 | OFF-A1 | Sửa ngày/lý do, điền và lưu Word | `PATCH /api/v1/staffing/leave-drafts/{id}`, `PUT /api/v1/staffing/leave-drafts/{id}/docx` (multipart) | 200 `version` mới; file không phải DOCX → 415; lớn hơn 10 MiB → 413 | Đ (09/11). 16a lưu Word Đ; 16b không phải DOCX → 415; 16c >10 MB → 413/FE chặn |
| 17 | OFF-A1 | Nộp đơn; bấm nộp 2 lần liên tiếp | `POST /api/v1/staffing/leave-drafts/{id}/submit` (header `Idempotency-Key`) | 200 `leaveId`, `PENDING`; lần 2 cùng key trả **cùng** `leaveId` | Đ (09/11) |
| 18 | OFF-A1 | Tạo thêm 1 nháp rồi xóa | `DELETE /api/v1/staffing/leave-drafts/{id}?expectedVersion=…` | 200, biến khỏi "Nháp của tôi" | Đ (09/11) |
| 19 | OFF-A1 | Gửi đơn nghỉ trực tiếp (không qua Word) cho một ngày khác | `POST /api/v1/staffing/leaves` | 200 `PENDING`; nếu bắt buộc Word → 409 `staffing.word_required`; trùng khoảng nghỉ → 409 | Đ (09/11) |
| 20 | OFF-A1, MGR-A | Xem danh sách đơn nghỉ | `GET /api/v1/staffing/leaves` | 200; cán bộ chỉ thấy của mình, Manager thấy cả phường | Đ (09/11) |
| 21 | MGR-A | Mở chi tiết đơn ở bước 17, tải bản Word đã nộp | `GET /api/v1/staffing/leaves/{id}`, `GET /api/v1/staffing/leaves/{id}/submitted-docx` | 200, `hasSubmittedDocx=true`; OFF-B1 (phường khác) mở → 403/404 | Đ (09/11). 21a tải Word đã nộp; 21b OFF-B1 bị chặn |
| 22 | MGR-A | Xem ca và hồ sơ bị ảnh hưởng nếu duyệt | `GET /api/v1/staffing/leaves/{id}/shift-impact`, `GET /api/v1/staffing/leaves/{id}/impact` | 200; `shift-impact` liệt kê phần ca bị giao; `impact` rỗng nếu OFF-A1 chưa giữ hồ sơ | Đ (09/11) |
| 23 | MGR-A | Duyệt đơn | `POST /api/v1/staffing/leaves/{id}/decision` | 200 `APPROVED`; `expectedVersion` cũ → 409; OFF-A1 nhận thông báo | Đ (09/11) |
| 24 | OFF-A1 | Tự duyệt đơn của mình | `POST /api/v1/staffing/leaves/{id}/decision` | 403 (cán bộ chỉ được hủy đơn `PENDING` của mình) | Đ (09/11) |
| 25 | OFF-A1 | Tự hủy đơn `PENDING` ở bước 19 | `POST /api/v1/staffing/leaves/{id}/decision` (`CANCELLED`) | 200 | Đ (09/11) |
| 26 | MGR-A | Xem lịch sử quyết định của đơn | `GET /api/v1/staffing/leaves/{id}/history` | 200 | Đ (09/11) |
| 27 | MGR-A | *(luồng chính, làm sau)* Giao hồ sơ cho cán bộ đang nghỉ đã duyệt | `GET /api/v1/manager/applications/{id}/assignment-candidates` | Cán bộ đó `eligible=false` kèm lý do | B (09/11). Thuộc luồng chính — xem 6.3 |

### KB-14 — Lịch nghỉ phường và ngày lễ toàn quốc

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | MGR-A | Xem lịch nghỉ từ hôm nay đến cuối năm | `GET /api/v1/staffing/closures` | 200 | Đ (09/11) |
| 2 | MGR-A | Khai báo phường A nghỉ 1 ngày trong tương lai | `POST /api/v1/staffing/closures` | 200 có `rescheduledApplications`; trùng ngày → 409 `staffing.closure_exists`; ngày đã qua → 400 | Đ (09/11). 2a tạo 15/12 Đ; 2b trùng ngày 409, 2c ngày đã qua 400, 2d thiếu lý do 400 Đ |
| 3 | ADMIN | Khai báo 1 ngày lễ toàn quốc | `POST /api/v1/staffing/closures/national` | 200 `wardCode=null`; MGR-A gọi → 403 | Đ (09/11). 3a ADMIN tạo lễ 16/12 Đ; 3b MGR-A bị chặn Đ |
| 4 | OFF-A1 | Xem lịch nghỉ | `GET /api/v1/staffing/closures` | 200 thấy cả ngày phường và ngày lễ | Đ (09/11) |
| 5 | CIT-1/OFF-A1 | Mở hồ sơ đang xử lý, kiểm tra hạn xử lý | `GET /api/v1/applications/{id}` | Hạn lùi đúng số ngày nghỉ (nếu hồ sơ bị ảnh hưởng) | B (09/11). Cần hồ sơ thật (luồng chính) — xem 6.3 |
| 6 | MGR-A, ADMIN | Hủy ngày nghỉ phường và ngày lễ vừa tạo | `POST /api/v1/staffing/closures/{id}/cancel` | 200; MGR-A hủy ngày lễ toàn quốc → 403 | Đ (09/11). 6a MGR-A hủy lễ toàn quốc bị chặn; 6b hủy ngày phường; 6c hủy lại → 409; 6d ADMIN hủy lễ Đ |

### KB-15 — Tham vấn chuyên môn

Chuẩn bị: một hồ sơ mới `{appE}` đang `UNDER_REVIEW` với OFF-A1 (làm lại KB-06 + KB-08 bước 1–4). OFF-A2 có ca làm và cùng lĩnh vực.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | MGR-A | Tạo yêu cầu tham vấn OFF-A2 trên `{appE}` | `POST /api/v1/applications/{applicationId}/consultations` | 200 `OPEN`; chọn chính OFF-A1 → 422/400 | |
| 2 | OFF-A2 | Mở hộp thư tham vấn | `GET /api/v1/staffing/consultations` | 200 thấy yêu cầu | |
| 3 | OFF-A2 | Mở tham vấn của hồ sơ; FE kiểm tra quyền đọc tài liệu | `GET /api/v1/applications/{applicationId}/consultations`, `GET /api/v1/applications/{applicationId}/consultations/document-access` | 200 `allowed=true` | |
| 4 | OFF-A2 | Trả lời tham vấn | `POST /api/v1/applications/{applicationId}/consultations/{id}/answer` | 200 `ANSWERED`; `expectedVersion` cũ → 409 | |
| 5 | OFF-A1 | Xem câu trả lời trong hồ sơ | `GET /api/v1/applications/{applicationId}/consultations` | 200 | |
| 6 | MGR-A | Đóng tham vấn | `POST /api/v1/applications/{applicationId}/consultations/{id}/close` | 200 `CLOSED` | |
| 7 | OFF-A2 | Mở lại quyền đọc tài liệu sau khi đóng | `GET /api/v1/applications/{applicationId}/consultations/document-access` | 403/404 | |
| 8 | OFF-A2 | Bấm trợ lý AI trên `{appE}` | `POST /api/v1/ai-ocr/applications/{applicationId}/dossier-assist` | 403 `ai.not_assigned_officer` (giới hạn đã biết) | |

### KB-16 — Thông báo realtime và quản trị delivery

Kết nối SignalR `/hubs/notifications` kèm access token. Kiểm tra song song với KB-06 → KB-10.

| # | Vai trò | Bước | API / Hub | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | CIT-1 | Đăng nhập, FE kết nối hub | Hub `/hubs/notifications` | Kết nối thành công; token sai → bị từ chối | Đ (08/11). 1b đăng xuất → kết nối hub đóng Đ |
| 2 | CIT-1 | Mở chuông thông báo | Hub `GetNotifications(page, pageSize)` | Danh sách thông báo | Đ (08/11). Rỗng vì CIT-1 chưa có thông báo |
| 3 | CIT-1 | Xem số chưa đọc | Hub `GetUnreadCount()` | Số nguyên | Đ (08/11) |
| 4 | OFF-A1 | Thực hiện 1 thao tác đổi trạng thái `{appA}` (KB-08/KB-10) | — | Sau ~10 giây CIT-1 nhận sự kiện `ReceiveNotification` và `UpdateUnreadCount` không cần tải lại trang | Đ (08/11). Thông báo tạo bằng SQL (không chạy luồng chính); CIT-1 nhận realtime, không cần tải lại |
| 5 | OFF-A1 | Được Manager giao hồ sơ (KB-08 bước 4) | — | OFF-A1 nhận thông báo phân công | Đ (08/11). Thông báo phạm vi phường `TEST_A` tạo bằng SQL; OFF-A1 nhận realtime |
| 6 | CIT-1 | Bấm vào 1 thông báo | Hub `MarkRead(notificationId)` | Số chưa đọc giảm | Đ (08/11) |
| 7 | CIT-1 | Kiểm tra hộp thư Email (nếu Analytics bật Email) | — | Có email trạng thái hồ sơ | B (08/11). Không có thư: delivery EMAIL thành `Blocked` — `Email:Enabled` đang tắt hoặc thiếu Host/Username/Password/From trên Analytics Azure (cấu hình, không phải lỗi code). Chờ Anti bật Email (mật khẩu ứng dụng Gmail mới) rồi test lại |
| 8 | ADMIN | Mở trang "Phát thông báo": danh sách delivery | `GET /api/v1/admin/notification-deliveries` | 200. Mặc định **chỉ** hiện delivery cần xử lý (`Dead`, `Blocked`, `Unknown`, `Dispatching`); muốn thấy `Sent` phải lọc `state=Sent` | Đ (08/11). 8a thấy 2 dòng `Dead` + 1 dòng `Blocked` (từ bước 7); 8b lọc `Sent` Đ |
| 9 | ADMIN | Mở chi tiết và lịch sử xử lý 1 delivery | `GET /api/v1/admin/notification-deliveries/{id}`, `GET /api/v1/admin/notification-deliveries/{id}/audit` | 200 | Đ (08/11) |
| 10 | ADMIN | Gửi lại 1 delivery `Dead`/`Blocked` (chỉ 2 trạng thái này được gửi lại) | `POST /api/v1/admin/notification-deliveries/{id}/replay` | 202, delivery về `Pending` rồi được gửi; delivery SMS cũ → 409 `notifications.channel_removed`; delivery khác `Dead`/`Blocked` → 409 `notifications.invalid_state` | B (08/11). 10a gửi lại EMAIL → 409 (do Email tắt, kỳ vọng `notifications.channel_disabled`) — test lại sau khi bật Email; 10b FE ẩn nút gửi lại SMS — chấp nhận (BE vẫn trả 409 `channel_removed`) |
| 11 | ADMIN | Kiểm tra giao diện không còn nút "Đối soát SMS" | — | Không có nút | Đ (08/11) |

### KB-17 — Thống kê Manager và job tổng hợp

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | MGR-A | Mở dashboard: thẻ tổng quan | `GET /api/v1/analytics/overview` | 200, chỉ số liệu phường A; đọc `coverage`, `asOfUtc` | Đ (09/11). 1a số liệu 0, giao diện không lỗi; 1b khoảng ngày sai và 1c quá 366 ngày bị chặn |
| 2 | MGR-A | Biểu đồ xu hướng theo ngày | `GET /api/v1/analytics/trends` | 200 | Đ (09/11), dữ liệu rỗng |
| 3 | MGR-A | Bảng theo thủ tục | `GET /api/v1/analytics/procedures` | 200 | Đ (09/11), dữ liệu rỗng |
| 4 | MGR-A | Bảng theo cán bộ | `GET /api/v1/analytics/officers` | 200 | Đ (09/11), dữ liệu rỗng |
| 5 | MGR-A | Tổng hợp và danh sách đánh giá | `GET /api/v1/analytics/feedback/summary`, `GET /api/v1/analytics/feedback` | 200; thấy đánh giá ở KB-10 | Đ (09/11), rỗng vì chưa có đánh giá — test lại sau luồng chính (6.3) |
| 6 | OFF-A1 | Thử mở dashboard | `GET /api/v1/analytics/overview` | 403 | Đ (09/11) |
| 7 | ADMIN | Chạy lại tổng hợp số liệu cho một khoảng ngày | `POST /api/v1/analytics/jobs/rebuild` | 202/200 có `jobId` | Đ (09/11). 202 có `jobId` |
| 8 | ADMIN | Theo dõi trạng thái job | `GET /api/v1/analytics/jobs/{jobId}` | 200 đến khi xong | Đ (09/11). Job chạy xong; `jobId` lạ → 404 |

### KB-18 — Phân quyền và phạm vi (test âm)

Mỗi dòng: đăng nhập vai trò ghi bên trái, gọi chức năng không được phép. **Kỳ vọng chung:** FE ẩn nút; nếu gọi thẳng thì nhận 401/403/404, không lộ dữ liệu.

| # | Vai trò | Thao tác | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | Không token | Mở trang hồ sơ của tôi | `GET /api/v1/applications` | 401 | Đ (09/11). Chưa đăng nhập thì FE không hiện mục "Hồ sơ của tôi" |
| 2 | CIT-1 | Mở hàng đợi cán bộ | `GET /api/v1/officer/applications/pending` | 403 | Đ (09/11) |
| 3 | CIT-1 | Xem danh sách đơn của hồ sơ theo route cán bộ | `GET /api/v1/officer/submissions/by-application/{applicationId}` | 403 | Đ (09/11) |
| 4 | CIT-2 | Tải giấy tờ minh chứng của CIT-1 | `GET /api/v1/supporting-documents/{documentId}/download` | 403/404 | B (09/11). Cần giấy tờ thật của CIT-1 (luồng chính) — xem 6.3 |
| 5 | OFF-B1 | Mở hồ sơ phường A | `GET /api/v1/officer/applications/{id}` | 404 | B (09/11). Cần hồ sơ phường A (luồng chính) — xem 6.3 |
| 6 | OFF-A2 | Thêm nhận xét vào hồ sơ đang do OFF-A1 thụ lý | `POST /api/v1/officer/applications/{id}/comments` | 403/404 | B (09/11). Cần hồ sơ đang thụ lý (luồng chính) — xem 6.3 |
| 7 | MGR-A | Tạo tài khoản Manager khác | `POST /api/v1/accounts/manager` | 403 | Đ (09/11) |
| 8 | MGR-A | Mở trang RBAC | `GET /api/v1/rbac/roles` | 403 | Đ (09/11) |
| 9 | PM | Mở dashboard Manager | `GET /api/v1/analytics/overview` | 403 | Đ (09/11) |
| 10 | CIT-1 | Mở trang quản trị Knowledge Base | `GET /api/v1/ai/knowledge/documents` | 403 | Đ (09/11) |
| 11 | ADMIN khóa OFF-A1 | OFF-A1 thao tác tiếp trong 30–60 giây | Bất kỳ API Workflow | Sau tối đa ~30 giây: 401 `auth.session_revoked`, FE đưa về đăng nhập. Mở khóa lại sau test | Đ (09/11). Đã mở khóa lại OFF-A1 |

### KB-19 — Lỗi AI và hạn mức (nên dùng mock)

| # | Bước | API | Kỳ vọng FE | KQ |
|---|---|---|---|---|
| 1 | Giả lập 429 `ai.provider_busy` kèm `Retry-After: 30` | Bất kỳ API AI | Hiện đếm ngược 30 giây, khóa nút gửi đến hết giờ | |
| 2 | Giả lập 429 `ai.quota_exceeded` / `ai.daily_limit_reached` | Bất kỳ API AI | Báo hết lượt hôm nay | |
| 3 | Giả lập 503 `ai.provider_unavailable` | Bất kỳ API AI | Báo tạm thời không dùng được, cho thử lại | |
| 4 | Giả lập 502 `ai.output_truncated` | `dossier-assist` | Báo chỉ phân tích được một phần, cho gọi lại | |
| 5 | Gọi `dossier-assist` cho hồ sơ không còn `UNDER_REVIEW` | `dossier-assist` | 409 `ai.application_not_under_review` | |
| 6 | Gọi `dossier-assist` cho hồ sơ không có tệp | `dossier-assist` | 422 `ai.dossier_empty` | |

---

### KB-20 — Quản trị hệ thống IT_ADMIN: cấu hình, nhật ký, sức khỏe, sao lưu

**Thêm 08/11** (TASK-34/35: 15 API trong AnalyticsSystem, xem `docs/fe-handoff-task34-35-36-management-portal.md`). Vai trò: ADMIN. Không cần hồ sơ.

Nếu một phần trả 503 `system.administration_disabled`, `system.health_disabled`, `system.backups_disabled`, `system.backup_runner_unavailable` hoặc `system.backup_storage_unavailable` thì phần đó **chưa bật trên Azure**: ghi **B**, không phải lỗi FE.

| # | Vai trò | Bước | API | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | ADMIN | Mở bảng cấu hình, xem chi tiết `analytics.lookbackDays` | `GET /api/v1/system/settings`, `GET /api/v1/system/settings/{key}` | 200, có `version` | Đ (09/11) |
| 2 | ADMIN | Sửa giá trị thành 14, nhập lý do | `PUT /api/v1/system/settings/{key}` | 200, `version` tăng; giá trị ngoài 1–366 hoặc lý do dưới 3 ký tự → 400 `system.invalid_setting`; `expectedVersion` cũ → 409 `system.setting_conflict`; key lạ → 404 `system.setting_not_found` | L (09/11). 2a lưu giá trị 14 → **500** (traceId `00-90ead3a8d489d9849f314116a4556201-…`) — BE2-01. 2b, 2c bị chặn 400 Đ. 2d, 2e không test được vì 2a lỗi |
| 3 | ADMIN | Xem nhật ký thay đổi cấu hình, mở 1 dòng | `GET /api/v1/system/audit-logs`, `GET /api/v1/system/audit-logs/{id}` | 200; thấy thao tác ở bước 2 kèm giá trị cũ/mới và lý do | Đ (09/11). Chưa có dòng nào vì 2a lỗi |
| 4 | ADMIN | Xem nhật ký nghiệp vụ, lọc nguồn `iam`, mở chi tiết JSON | `GET /api/v1/system/audit-events?source=iam`, `GET /api/v1/system/audit-events/{id}` | 200 (có thể rỗng nếu service nguồn chưa gửi sự kiện); id lạ → 404 `system.audit_not_found` | Đ (09/11). 4a, 4b Đ |
| 5 | ADMIN | Mở bảng sức khỏe, xem chi tiết 1 service | `GET /api/v1/system/health/services`, `GET /api/v1/system/health/services/{serviceName}` | 200, mỗi service có trạng thái, mã HTTP, thời gian phản hồi; tên lạ → 404 `system.service_not_found` | Đ (09/11). 5a, 5b Đ |
| 6 | ADMIN | Bấm "Kiểm tra ngay", vài giây sau tải lại | `POST /api/v1/system/health/check-now` | **202** `status=Queued`; sau đó thời gian kiểm tra được cập nhật | Đ (09/11) |
| 7 | ADMIN | Xem lịch sử sức khỏe theo service | `GET /api/v1/system/health/history?service=iam` | 200 | Đ (09/11) |
| 8 | ADMIN | Tạo bản sao lưu cho 1 service, có lý do | `POST /api/v1/system/backups` | **202** `Queued`; tạo lần 2 khi đang chờ → 409 `system.backup_in_progress`; service chưa cấu hình → 404 `system.backup_target_not_found` | L (09/11). 8a tạo bản sao lưu → **500** (traceId `00-8c8f750304b03efce5adc891a7a8deeb-…`) — BE2-02. Không phải 503: đã qua kiểm tra bật backup, runner và Blob |
| 9 | ADMIN | Xem danh sách, lọc trạng thái, mở chi tiết | `GET /api/v1/system/backups`, `GET /api/v1/system/backups/{id}` | 200; trạng thái chuyển `Running` → `Succeeded` (hoặc `Failed` kèm mã lỗi) | B (09/11). Không có bản sao lưu vì 8a lỗi |
| 10 | ADMIN | Xác minh bản `Succeeded` | `POST /api/v1/system/backups/{id}/verify` | 200 `Verified`; xác minh bản đang `Queued`/`Running` → 409 `system.backup_in_progress` | B (09/11). Như bước 9 |
| 11 | MGR-A | Gọi trang quản trị hệ thống | `GET /api/v1/system/settings` | 403; FE ẩn menu | Đ (09/11) |

## 4. Ma trận phủ API (224 API công khai)

Dùng bảng này để đánh dấu **API nào đã được gọi thành công trên giao diện**. Cột "KB" là kịch bản dùng API đó. Một API được tính "đã có" khi ít nhất một bước trong các kịch bản đó đạt.

### 4.1 IAM (46)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | POST | `/api/v1/auth/register` | KB-02 | |
| 2 | POST | `/api/v1/auth/login` | KB-02 | |
| 3 | POST | `/api/v1/auth/refresh-token` | KB-02 | |
| 4 | POST | `/api/v1/auth/revoke-token` | KB-02 | |
| 5 | POST | `/api/v1/auth/password-otp/request` | KB-02 | |
| 6 | POST | `/api/v1/auth/password-otp/verify-and-change` | KB-02 | |
| 7 | GET | `/api/v1/users/me` | KB-02 | |
| 8 | POST | `/api/v1/users/me/change-password` | KB-02 | |
| 9 | GET | `/api/v1/users/access-context` | KB-02 | |
| 10 | GET | `/api/v1/users/me/profile` | KB-02 | |
| 11 | POST | `/api/v1/users/me/profile` | KB-02 | |
| 12 | PUT | `/api/v1/users/me/profile` | KB-02 | |
| 13 | DELETE | `/api/v1/users/me/profile` | KB-02 | |
| 14 | GET | `/api/v1/wards` | KB-01 | |
| 15 | GET | `/api/v1/users` | KB-03 | |
| 16 | GET | `/api/v1/users/profiles` | KB-03 | |
| 17 | GET | `/api/v1/users/{userId}/profile` | KB-03 | |
| 18 | POST | `/api/v1/users/{userId}/profile` | KB-03 | |
| 19 | PUT | `/api/v1/users/{userId}/profile` | KB-03 | |
| 20 | DELETE | `/api/v1/users/{userId}/profile` | KB-03 | |
| 21 | GET | `/api/v1/accounts` | KB-03 | |
| 22 | GET | `/api/v1/accounts/{userId}` | KB-03 | |
| 23 | PUT | `/api/v1/accounts/{userId}/status` | KB-03, KB-18 | |
| 24 | POST | `/api/v1/accounts/front-desk` | KB-03 | |
| 25 | POST | `/api/v1/accounts/manager` | KB-03, KB-18 | |
| 26 | POST | `/api/v1/accounts/procedure-manager` | KB-03 | |
| 27 | GET | `/api/v1/accounts/wards` | KB-03 | |
| 28 | POST | `/api/v1/accounts/wards` | KB-03 | |
| 29 | PUT | `/api/v1/accounts/{userId}/ward` | KB-03 | |
| 30 | GET | `/api/v1/admin/users` | KB-03 | |
| 31 | GET | `/api/v1/manager/officers` | KB-07 | |
| 32 | PUT | `/api/v1/manager/officers/{userId}/categories` | KB-07 | |
| 33 | GET | `/api/v1/manager/officers/{userId}/assignment-context` | KB-07 | |
| 34 | GET | `/api/v1/rbac/roles` | KB-03, KB-18 | |
| 35 | GET | `/api/v1/rbac/roles/{roleId}` | KB-03 | |
| 36 | POST | `/api/v1/rbac/roles` | KB-03 | |
| 37 | PUT | `/api/v1/rbac/roles/{roleId}` | KB-03 | |
| 38 | DELETE | `/api/v1/rbac/roles/{roleId}` | KB-03 | |
| 39 | GET | `/api/v1/rbac/permissions` | KB-03 | |
| 40 | GET | `/api/v1/rbac/roles/{roleId}/permissions` | KB-03 | |
| 41 | PUT | `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}` | KB-03 | |
| 42 | DELETE | `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}` | KB-03 | |
| 43 | GET | `/api/v1/rbac/users/{userId}/roles` | KB-03 | |
| 44 | PUT | `/api/v1/rbac/users/{userId}/roles/{roleId}` | KB-03 | |
| 45 | DELETE | `/api/v1/rbac/users/{userId}/roles/{roleId}` | KB-03 | |
| 46 | GET | `/api/v1/rbac/audit-logs` | KB-03 | |

### 4.2 ProcedureCatalog (28)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | GET | `/api/v1/procedures` | KB-01 | |
| 2 | GET | `/api/v1/procedures/featured` | KB-01 | |
| 3 | GET | `/api/v1/procedures/categories` | KB-01 | |
| 4 | GET | `/api/v1/procedures/{id}` | KB-01 | |
| 5 | GET | `/api/v1/procedures/{id}/source` | KB-01 | |
| 6 | POST | `/api/v1/procedure-manager/categories` | KB-04 | |
| 7 | PUT | `/api/v1/procedure-manager/categories/{id}` | KB-04 | |
| 8 | DELETE | `/api/v1/procedure-manager/categories/{id}` | KB-04 | |
| 9 | GET | `/api/v1/procedure-manager/document-forms` | KB-04 | |
| 10 | POST | `/api/v1/procedure-manager/procedures` | KB-04 | |
| 11 | GET | `/api/v1/procedure-manager/procedures` | KB-04 | |
| 12 | GET | `/api/v1/procedure-manager/procedures/{id}` | KB-04 | |
| 13 | PUT | `/api/v1/procedure-manager/procedures/{id}` | KB-04 | |
| 14 | PUT | `/api/v1/procedure-manager/procedures/{id}/checklist` | KB-04 | |
| 15 | PATCH | `/api/v1/procedure-manager/procedures/{id}/status` | KB-04 | |
| 16 | GET | `/api/v1/procedure-manager/procedures/{id}/versions` | KB-04 | |
| 17 | GET | `/api/v1/procedure-manager/procedures/{id}/versions/{versionId}/source` | KB-04 | |
| 18 | POST | `/api/v1/procedure-manager/procedures/{id}/versions/{versionNumber}/rollback` | KB-04 | |
| 19 | POST | `/api/v1/procedure-manager/procedures/publish` | KB-04 | |
| 20 | POST | `/api/v1/procedure-manager/drafts/extract-preview` | KB-04 | |
| 21 | POST | `/api/v1/procedure-manager/drafts` | KB-04 | |
| 22 | GET | `/api/v1/procedure-manager/drafts` | KB-04 | |
| 23 | GET | `/api/v1/procedure-manager/drafts/{id}` | KB-04 | |
| 24 | PUT | `/api/v1/procedure-manager/drafts/{id}` | KB-04 | |
| 25 | POST | `/api/v1/procedure-manager/drafts/{id}/retry` | KB-04 | |
| 26 | POST | `/api/v1/procedure-manager/drafts/{id}/publish` | KB-04 | |
| 27 | GET | `/api/v1/procedure-manager/drafts/{id}/source` | KB-04 | |
| 28 | DELETE | `/api/v1/procedure-manager/drafts/{id}` | KB-04 | |

### 4.3 ApplicationWorkflow (70)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | POST | `/api/v1/applications` | KB-06 | |
| 2 | GET | `/api/v1/applications` | KB-06, KB-18 | |
| 3 | GET | `/api/v1/applications/{id}` | KB-06, KB-09, KB-10, KB-14 | |
| 4 | PATCH | `/api/v1/applications/{id}/checklists/{checklistId}` | KB-06 | |
| 5 | POST | `/api/v1/applications/{id}/submit` | KB-06 | |
| 6 | POST | `/api/v1/applications/{id}/cancel` | KB-06 | |
| 7 | POST | `/api/v1/applications/{id}/resubmit` | KB-09, KB-11 | |
| 8 | GET | `/api/v1/applications/{id}/comments` | KB-09 | |
| 9 | GET | `/api/v1/applications/{id}/versions` | KB-09 | |
| 10 | GET | `/api/v1/applications/{id}/diff` | KB-09 | |
| 11 | GET | `/api/v1/officer/applications` | KB-08 | |
| 12 | GET | `/api/v1/officer/applications/pending` | KB-08, KB-18 | |
| 13 | GET | `/api/v1/officer/applications/{id}` | KB-08, KB-12, KB-18 | |
| 14 | POST | `/api/v1/officer/applications/{id}/assign` | KB-12 | |
| 15 | POST | `/api/v1/officer/applications/{id}/release` | KB-12 | |
| 16 | POST | `/api/v1/officer/applications/{id}/comments` | KB-08, KB-18 | |
| 17 | POST | `/api/v1/officer/applications/{id}/comments/{commentId}/withdraw` | KB-08 | |
| 18 | POST | `/api/v1/officer/applications/{id}/request-revision` | KB-08 | |
| 19 | POST | `/api/v1/officer/applications/{id}/confirm-readiness` | KB-10, KB-12 | |
| 20 | POST | `/api/v1/officer/applications/{id}/officially-received` | KB-10 | |
| 21 | GET | `/api/v1/officer/activity` | KB-08 | |
| 22 | GET | `/api/v1/manager/applications` | KB-08 | |
| 23 | GET | `/api/v1/manager/applications/assignment-summary` | KB-08 | |
| 24 | PUT | `/api/v1/manager/applications/{id}/assignee` | KB-12 | |
| 25 | GET | `/api/v1/manager/applications/{id}/assignment-candidates` | KB-08, KB-13 | |
| 26 | GET | `/api/v1/manager/applications/{id}/assignments` | KB-08 | |
| 27 | POST | `/api/v1/manager/applications/{id}/assignments` | KB-08, KB-12 | |
| 28 | GET | `/api/v1/staffing/shifts` | KB-07 | |
| 29 | POST | `/api/v1/staffing/shifts` | KB-07, KB-13 | |
| 30 | POST | `/api/v1/staffing/shifts/{id}/cancel` | KB-13 | |
| 31 | GET | `/api/v1/staffing/leaves` | KB-13 | |
| 32 | POST | `/api/v1/staffing/leaves` | KB-13 | |
| 33 | POST | `/api/v1/staffing/leaves/{id}/decision` | KB-13 | |
| 34 | GET | `/api/v1/staffing/leaves/{id}/impact` | KB-13 | |
| 35 | GET | `/api/v1/staffing/leaves/{id}/history` | KB-13 | |
| 36 | GET | `/api/v1/staffing/closures` | KB-14 | |
| 37 | POST | `/api/v1/staffing/closures` | KB-14 | |
| 38 | POST | `/api/v1/staffing/closures/national` | KB-14 | |
| 39 | POST | `/api/v1/staffing/closures/{id}/cancel` | KB-14 | |
| 40 | GET | `/api/v1/staffing/consultations` | KB-15 | |
| 41 | GET | `/api/v1/applications/{applicationId}/consultations` | KB-15 | |
| 42 | POST | `/api/v1/applications/{applicationId}/consultations` | KB-15 | |
| 43 | GET | `/api/v1/applications/{applicationId}/consultations/document-access` | KB-15 | |
| 44 | POST | `/api/v1/applications/{applicationId}/consultations/{id}/answer` | KB-15 | |
| 45 | POST | `/api/v1/applications/{applicationId}/consultations/{id}/close` | KB-15 | |
| 46 | GET | `/api/v1/staffing/capabilities` | KB-13 | |
| 47 | GET | `/api/v1/staffing/leave-templates/current` | KB-13 | |
| 48 | GET | `/api/v1/staffing/leave-templates/{id}/docx` | KB-13 | |
| 49 | POST | `/api/v1/staffing/leave-drafts` | KB-13 | |
| 50 | GET | `/api/v1/staffing/leave-drafts` | KB-13 | |
| 51 | GET | `/api/v1/staffing/leave-drafts/{id}` | KB-13 | |
| 52 | PATCH | `/api/v1/staffing/leave-drafts/{id}` | KB-13 | |
| 53 | DELETE | `/api/v1/staffing/leave-drafts/{id}` | KB-13 | |
| 54 | GET | `/api/v1/staffing/leave-drafts/{id}/docx` | KB-13 | |
| 55 | PUT | `/api/v1/staffing/leave-drafts/{id}/docx` | KB-13 | |
| 56 | POST | `/api/v1/staffing/leave-drafts/{id}/submit` | KB-13 | |
| 57 | GET | `/api/v1/staffing/leaves/{id}` | KB-13 | |
| 58 | GET | `/api/v1/staffing/leaves/{id}/submitted-docx` | KB-13 | |
| 59 | GET | `/api/v1/staffing/leaves/{id}/shift-impact` | KB-13 | |
| 60 | GET | `/api/v1/staffing/schedules/{month}` | KB-13 | |
| 61 | GET | `/api/v1/staffing/week-pattern` | KB-13 | |
| 62 | PUT | `/api/v1/staffing/week-pattern` | KB-13 | |
| 63 | GET | `/api/v1/staffing/schedules/{month}/preview` | KB-13 | |
| 64 | PUT | `/api/v1/staffing/schedules/{month}/draft` | KB-13 | |
| 65 | POST | `/api/v1/staffing/schedules/{month}/publish` | KB-13 | |
| 66 | GET | `/api/v1/staffing/schedules/{month}/history` | KB-13 | |
| 67 | POST | `/api/v1/staffing/schedules/{month}/confirm` | KB-13 | |
| 68 | GET | `/api/v1/staffing/shift-change-requests` | KB-13 | |
| 69 | POST | `/api/v1/staffing/shift-change-requests` | KB-13 | |
| 70 | POST | `/api/v1/staffing/shift-change-requests/{id}/decision` | KB-13 | |

### 4.4 DocumentForm (31)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | GET | `/api/v1/form-templates` | KB-04 | |
| 2 | GET | `/api/v1/form-templates/{templateId}` | KB-04, KB-06 | |
| 3 | POST | `/api/v1/form-templates` | KB-04 | |
| 4 | POST | `/api/v1/form-templates/{templateId}/upload-docx` | KB-04 | |
| 5 | GET | `/api/v1/form-templates/{templateId}/download-docx` | KB-04, KB-06 | |
| 6 | GET | `/api/v1/citizen/submissions` | KB-06 | |
| 7 | GET | `/api/v1/citizen/submissions/{submissionId}` | KB-06 | |
| 8 | POST | `/api/v1/citizen/submissions/draft` | KB-06 | |
| 9 | POST | `/api/v1/citizen/submissions/upload-filled-docx` | KB-06 | |
| 10 | PUT | `/api/v1/citizen/submissions/{submissionId}/draft` | KB-06, KB-09 | |
| 11 | POST | `/api/v1/citizen/submissions/{submissionId}/submit` | KB-06 | |
| 12 | GET | `/api/v1/citizen/submissions/{submissionId}/download-docx` | KB-06 | |
| 13 | POST | `/api/v1/applications/{applicationId}/supporting-documents` | KB-06, KB-09 | |
| 14 | GET | `/api/v1/applications/{applicationId}/supporting-documents` | KB-06 | |
| 15 | DELETE | `/api/v1/supporting-documents/{documentId}` | KB-06 | |
| 16 | GET | `/api/v1/supporting-documents/{documentId}/download` | KB-06, KB-08, KB-18 | |
| 17 | GET | `/api/v1/supporting-documents/{documentId}/preview` | KB-06 | |
| 18 | GET | `/api/v1/applications/{applicationId}/contents` | KB-06, KB-08 | |
| 19 | GET | `/api/v1/officer/submissions/by-application/{applicationId}` | KB-08, KB-18 | |
| 20 | GET | `/api/v1/officer/submissions/{submissionId}/download-docx` | KB-08 | |
| 21 | PUT | `/api/v1/officer/submissions/{submissionId}/annotate-docx` | KB-08 | |
| 22 | POST | `/api/v1/officer/submissions/{submissionId}/request-revision` | KB-11 | |
| 23 | POST | `/api/v1/officer/submissions/{submissionId}/confirm-readiness` | KB-11 | |
| 24 | POST | `/api/v1/applications/{applicationId}/generate-pdf` | KB-10 | |
| 25 | GET | `/api/v1/applications/{applicationId}/generated-documents` | KB-10 | |
| 26 | GET | `/api/v1/generated-documents/{documentId}/download` | KB-10 | |
| 27 | GET | `/api/v1/applications/{applicationId}/approved-qr` | KB-10 | |
| 28 | GET | `/api/v1/applications/{applicationId}/approved-qr/image` | KB-10 | |
| 29 | GET | `/api/v1/public/qr-lookup` | KB-10 | |
| 30 | GET | `/api/v1/public/qr-lookup/{**token}` | KB-10 | |
| 31 | GET | `/api/v1/public/applications/{applicationId}/pdf-redirect` | KB-10 | |

### 4.5 AIOCR (20)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | POST | `/api/v1/ai-ocr/cccd/extract` | KB-06 | |
| 2 | POST | `/api/v1/ai-ocr/documents/verify` | KB-06 | |
| 3 | POST | `/api/v1/ai-ocr/applications/{applicationId}/dossier-assist` | KB-08, KB-15, KB-19 | |
| 4 | POST | `/api/v1/ai/chat/completions` | KB-01, KB-06 | |
| 5 | GET | `/api/v1/ai/chat/sessions` | KB-06 | |
| 6 | GET | `/api/v1/ai/chat/sessions/{sessionId}/messages` | KB-06 | |
| 7 | GET | `/api/v1/ai/knowledge/documents` | KB-05, KB-18 | |
| 8 | GET | `/api/v1/ai/knowledge/documents/{id}` | KB-05 | |
| 9 | GET | `/api/v1/ai/knowledge/documents/{id}/chunks` | KB-05 | |
| 10 | POST | `/api/v1/ai/knowledge/documents` | KB-05 | |
| 11 | POST | `/api/v1/ai/knowledge/documents/pdf` | KB-05 | |
| 12 | PUT | `/api/v1/ai/knowledge/documents/{id}` | KB-05 | |
| 13 | POST | `/api/v1/ai/knowledge/documents/{id}/activation` | KB-05 | |
| 14 | POST | `/api/v1/ai/knowledge/documents/{id}/rechunk` | KB-05 | |
| 15 | POST | `/api/v1/ai/knowledge/documents/{id}/reindex` | KB-05 | |
| 16 | GET | `/api/v1/ai/knowledge/index-status` | KB-05 | |
| 17 | POST | `/api/v1/ai/knowledge/search` | KB-05 | |
| 18 | POST | `/api/v1/ai/knowledge/procedures/{procedureId}/sync` | KB-05 | |
| 19 | POST | `/api/v1/ai/knowledge/procedures/sync` | KB-05 | |
| 20 | GET | `/api/v1/ai/knowledge/usage` | KB-05 | |

### 4.6 AnalyticsSystem (29 + hub)

| # | Method | Route | KB | Đã test |
|---|---|---|---|---|
| 1 | GET | `/api/v1/analytics/overview` | KB-17, KB-18 | |
| 2 | GET | `/api/v1/analytics/trends` | KB-17 | |
| 3 | GET | `/api/v1/analytics/procedures` | KB-17 | |
| 4 | GET | `/api/v1/analytics/officers` | KB-17 | |
| 5 | GET | `/api/v1/analytics/feedback/summary` | KB-17 | |
| 6 | GET | `/api/v1/analytics/feedback` | KB-17 | |
| 7 | POST | `/api/v1/analytics/jobs/rebuild` | KB-17 | |
| 8 | GET | `/api/v1/analytics/jobs/{jobId}` | KB-17 | |
| 9 | POST | `/api/v1/citizen/feedback/{applicationId}` | KB-10 | |
| 10 | GET | `/api/v1/citizen/feedback/{applicationId}` | KB-10 | |
| 11 | GET | `/api/v1/admin/notification-deliveries` | KB-16 | |
| 12 | GET | `/api/v1/admin/notification-deliveries/{id}` | KB-16 | |
| 13 | GET | `/api/v1/admin/notification-deliveries/{id}/audit` | KB-16 | |
| 14 | POST | `/api/v1/admin/notification-deliveries/{id}/replay` | KB-16 | |
| 15 | GET | `/api/v1/system/settings` | KB-20 | |
| 16 | GET | `/api/v1/system/settings/{key}` | KB-20 | |
| 17 | PUT | `/api/v1/system/settings/{key}` | KB-20 | |
| 18 | GET | `/api/v1/system/audit-logs` | KB-20 | |
| 19 | GET | `/api/v1/system/audit-logs/{id}` | KB-20 | |
| 20 | GET | `/api/v1/system/audit-events` | KB-20 | |
| 21 | GET | `/api/v1/system/audit-events/{id}` | KB-20 | |
| 22 | GET | `/api/v1/system/health/services` | KB-20 | |
| 23 | GET | `/api/v1/system/health/services/{serviceName}` | KB-20 | |
| 24 | POST | `/api/v1/system/health/check-now` | KB-20 | |
| 25 | GET | `/api/v1/system/health/history` | KB-20 | |
| 26 | POST | `/api/v1/system/backups` | KB-20 | |
| 27 | GET | `/api/v1/system/backups` | KB-20 | |
| 28 | GET | `/api/v1/system/backups/{id}` | KB-20 | |
| 29 | POST | `/api/v1/system/backups/{id}/verify` | KB-20 | |
| Hub | WS | `/hubs/notifications`: `GetNotifications`, `GetUnreadCount`, `MarkRead`; server đẩy `ReceiveNotification`, `UpdateUnreadCount` | KB-16 | |

### 4.7 API nội bộ (FE không gọi; được kiểm tra gián tiếp)

| Method | Route | Service | Được dùng khi | Kiểm tra gián tiếp ở |
|---|---|---|---|---|
| POST | `/internal/v1/notification-recipients` | IAM | Analytics tìm người nhận thông báo | KB-16 bước 4–5 |
| GET | `/internal/notifications/v1/applications/{applicationId}/events/{eventId}/email-context` | Workflow | Analytics dựng nội dung Email | KB-16 bước 7 |
| GET | `/internal/analytics/v1/daily` | Workflow | Job tổng hợp số liệu theo ngày | KB-17 bước 7–8 |
| GET | `/internal/analytics/v1/deadlines` | Workflow | Thống kê hạn xử lý | KB-17 bước 1–4 |
| POST | `/internal/v1/procedure-extractions` | AIOCR | Catalog gửi PDF cho AI bóc tách | KB-04 bước 17–22 |
| POST | `/internal/audit/events` | Analytics | Các service gửi sự kiện kiểm toán nghiệp vụ về trung tâm | KB-20 bước 4 |

Ngoài ra mỗi service có `GET /`, `GET /health`, `GET /health/ready` cho vận hành (KB-00).

---

## 5. Mẫu phiếu báo lỗi gửi BE

Mỗi lỗi một phiếu. **Không gửi mật khẩu, token hay dữ liệu cá nhân thật.**

```
Mã bước: KB-08 #12
Vai trò / tài khoản test: OFF-A1
Thời điểm gọi (giờ VN): 2026-11-02 14:35
Route: POST /api/v1/ai-ocr/applications/{id}/dossier-assist
Mã HTTP nhận được: 500
code trong body: (ví dụ ai.provider_unavailable)
traceId: (lấy trong body ProblemDetails)
Kỳ vọng: 200
Mô tả ngắn / ảnh chụp màn hình:
```

Lỗi về **lịch nghỉ, ca làm, nghỉ phép, tham vấn, điều phối** (KB-07, KB-12 → KB-15) gửi BE lịch làm việc (Codex). Các lỗi còn lại gửi BE chính (Claude).

## 6. Tổng hợp kết quả (điền sau khi test)

| KB | Tên | Số bước | Đ | L | UI | B | Ghi chú |
|---|---|---|---|---|---|---|---|
| KB-00 | Hạ tầng | 3 | 3 | 0 | 0 | 0 | 02/11 trên Azure |
| KB-01 | Khách | 8 | 6 | 0 | 1 | 0 | Bước 6: FE thiếu chọn phường (FE-01). Bước 8 bỏ qua (chờ mock) |
| KB-02 | Tài khoản, phiên | 15 | 13 | 0 | 1 | 0 | Bước 9: xóa hồ sơ cá nhân là tùy chọn (FE-03). Bước 10 để sau (FE-05) |
| KB-03 | Quản trị IT_ADMIN | 24 | 20 | 0 | 4 | 0 | Bước 7, 8, 12, 14: FE chưa dùng các API phụ (FE-06) |
| KB-04 | Thủ tục | 25 | 22 | 0 | 2 | 0 | 05/11. Bước 7, 12: không liên kết được mẫu đơn từ màn checklist (FE-07). Bước 22 không phát sinh. Góp ý nghiệp vụ ghi ở 6.2 (BE-01..BE-05) |
| KB-05 | Knowledge Base | 13 | 11 | 1 | 1 | 0 | 07/11. Bước 7 lỗi BE, đã sửa source (BE-06), chờ deploy. Bước 13: FE chưa có màn thống kê (FE-10) |
| KB-06 | Công dân nộp hồ sơ | 20 | | | | | |
| KB-07 | Manager chuẩn bị | 5 | | | | | |
| KB-08 | Điều phối, thụ lý | 17 | | | | | |
| KB-09 | Bổ sung, nộp lại | 6 | | | | | |
| KB-10 | Sẵn sàng, PDF, QR, tiếp nhận | 10 | | | | | |
| KB-11 | Route cũ DocumentForm | 3 | | | | | |
| KB-12 | Trả lại, chuyển giao | 6 | | | | | |
| KB-13 | Lịch làm, lịch tháng, đơn nghỉ Word | 27 | 26 | 0 | 0 | 1 | 09/11. Phần Codex làm, không có lỗi. Bước 27 thuộc luồng chính |
| KB-14 | Lịch nghỉ phường, lễ | 6 | 5 | 0 | 0 | 1 | 09/11. Phần Codex làm, không có lỗi. Bước 5 cần hồ sơ (luồng chính) |
| KB-15 | Tham vấn | 8 | | | | | |
| KB-16 | Thông báo | 11 | 9 | 0 | 0 | 2 | 08/11, thông báo tạo bằng SQL (chưa chạy luồng chính). Bước 7, 10a chặn do Email tắt trên Azure — chờ Anti bật Email |
| KB-17 | Thống kê | 8 | 8 | 0 | 0 | 0 | 09/11, chưa có hồ sơ nên số liệu rỗng; kiểm tra quyền, lọc ngày, giao diện rỗng và job tổng hợp |
| KB-18 | Phân quyền (test âm) | 11 | 8 | 0 | 0 | 3 | 09/11. FE ẩn hầu hết chức năng không được phép. Bước 4–6 cần hồ sơ (luồng chính) |
| KB-19 | Lỗi AI (mock) | 6 | 0 | 0 | 0 | 6 | 09/11 hoãn: FE chưa deploy phần xử lý lỗi AI. Bước 1–3 giả lập bằng script Console khi FE deploy; bước 4–6 cần luồng chính |
| KB-20 | Quản trị hệ thống | 11 | 7 | 2 | 0 | 2 | 09/11. Phần Codex làm. Bước 2a và 8a trả 500 khi ghi DB (BE2-01, BE2-02); bước 9, 10 bị chặn theo |

### 6.1 Việc FE cần làm sau đợt test

| Mã | Từ bước | Việc | Ưu tiên |
|---|---|---|---|
| FE-01 | KB-01 #6 | Thêm ô **chọn phường tiếp nhận** vào form tạo hồ sơ, lấy danh sách từ `GET /api/v1/wards`, gửi `wardCode` trong `POST /api/v1/applications`. BE đã bắt buộc: thiếu → 400 "Phải chọn phường/xã tiếp nhận.", sai mã → 400 `application.invalid_ward`. Không có ô này thì công dân không tạo được hồ sơ | Cao — chặn KB-06 |
| FE-02 | KB-01 #8 | Kiểm tra vì sao request chatbot bị trình duyệt hủy `(canceled)` khi hỏi liên tiếp (gửi câu mới khi câu trước chưa xong, AbortController, timeout quá ngắn). Hiện thông báo 429 `ai.guest_quota_exceeded` thành lời mời đăng nhập | Trung bình |
| FE-03 | KB-02 #9 | (Tùy chọn) Nút "Xóa thông tin cá nhân" gọi `DELETE /api/v1/users/me/profile`: hỏi xác nhận trước; sau khi xóa `profile` trong `/users/me` là `null` nên phải hiện form **tạo** (`POST /api/v1/users/me/profile`) thay cho form sửa | Thấp |
| FE-04 | KB-02 #13 | Màn "Quên mật khẩu": **hiện nguyên thông báo trung tính của BE** ("Nếu tài khoản hợp lệ…, mã OTP sẽ được gửi qua Email…"), **không** hiện "Đã gửi mã thành công" và không đoán email có tồn tại hay không. BE luôn trả 202 dù email có trong hệ thống hay không (chống dò tài khoản). Thêm gợi ý: kiểm tra cả thư Spam; chờ 60 giây mới xin lại mã (xin sớm hơn vẫn 202 nhưng không có thư mới); mã hết hạn sau 5 phút; sai 5 lần phải xin mã mới. Có thể khóa nút "Gửi lại mã" 60 giây | Trung bình |
| FE-05 | KB-02 #10 | Test lại việc tự làm mới token sau 15 phút (hướng dẫn ở KQ bước 10). Nếu `refresh-token` bị 401 do cookie không được gửi, báo BE/Anti để chỉnh `RefreshCookie__SameSite` | Cao — nếu lỗi, người dùng bị đăng xuất sau 15 phút |
| FE-06 | KB-03 #7, #8, #12, #14 | (Tùy chọn) Các API quản trị FE chưa dùng: `GET /api/v1/accounts/{userId}` (chi tiết tài khoản), `GET /api/v1/users` (danh sách người dùng), `POST /api/v1/users/{userId}/profile` (tạo hồ sơ cá nhân khi tài khoản chưa có), `DELETE /api/v1/users/{userId}/profile` (admin xóa hồ sơ cá nhân). Không chặn luồng chính; chỉ cần nếu muốn đủ chức năng quản trị | Thấp |
| FE-07 | KB-04 #7, #12 | Màn sửa thủ tục nhập tay cần phần **"Biểu mẫu, tờ khai"**: chọn mẫu từ `GET /api/v1/procedure-manager/document-forms`, lưu vào `formDefinitions` (`formTemplateId`, `formCode`, `formName`, `formType` = `DOCX_TEMPLATE`) qua `PUT /api/v1/procedure-manager/procedures/{id}` (kèm số quyết định, ngày hiệu lực). `PUT .../checklist` chỉ lưu danh sách giấy tờ, **không** có chỗ liên kết mẫu | Cao — KB-06 cần thủ tục có mẫu DOCX; tạm dùng thủ tục `KB04-AI` |
| FE-08 | KB-04 #23 | FE đang bắt chọn mẫu trước khi xuất bản bản nháp; BE cho phép `formTemplateId` = null (liên kết sau). Nên cho xuất bản khi chưa có mẫu, kèm cảnh báo "Biểu mẫu chưa có file" | Thấp |
| FE-09 | KB-04 góp ý | Trên màn bản nháp AI, với mỗi biểu mẫu AI đọc được mà chưa có mẫu: nút **"Tạo phôi và tải DOCX"** gọi lần lượt `POST /api/v1/form-templates` → `POST /api/v1/form-templates/{id}/upload-docx`, rồi gán `formTemplateId` vào bản nháp (`PUT .../drafts/{id}`). Dùng API sẵn có, BE không cần sửa | Trung bình |
| FE-10 | KB-05 #13 | (Tùy chọn) Màn thống kê lượt dùng AI cho PM/ADMIN: `GET /api/v1/ai/knowledge/usage?from=yyyy-MM-dd&to=yyyy-MM-dd` (mặc định 7 ngày gần nhất, theo ngày UTC+07 và loại thao tác, kèm hạn mức). Giúp theo dõi hạn mức Gemini miễn phí | Thấp |

### 6.2 Góp ý nghiệp vụ cho BE (chờ chủ dự án quyết)

| Mã | Từ bước | Hiện trạng trong code | Đề xuất |
|---|---|---|---|
| BE-01 | KB-04 góp ý | `feeSummary` và `processingTimeSummary` bắt buộc (`ProcedureInputValidator`, `DraftPayloadRequirements`). Danh sách `results` được để rỗng | Cho phép trống hoặc thống nhất ghi "Không quy định"/"Không thu phí". Nếu bỏ bắt buộc phải kiểm tra cột DB và cập nhật `system-constraints.md` |
| BE-02 | KB-04 #14, góp ý | Thủ tục nhập tay chỉ nhận `originalPdfUrl` là đường dẫn HTTPS; PDF chỉ được lưu qua luồng bản nháp, nên thủ tục nhập tay không có PDF nguồn | Thêm API tải PDF lên cho thủ tục đã có (lưu Blob như bản nháp, ghi vào phiên bản mới) |
| BE-03 | KB-04 góp ý | Bản nháp AI luôn để `categoryId = 0` cho cán bộ chọn; lĩnh vực phải tạo trước bằng tay | Khi xuất bản, cho phép gửi tên lĩnh vực mới: chưa có thì tạo và gán luôn (cần quyết: trùng tên khác dấu/hoa thường xử lý thế nào) |
| BE-04 | KB-04 #23 | Prompt bóc tách (`AzureProcedureDocumentExtractor`) không dặn rõ phải lấy **mọi** tên file trong cột "Mẫu đơn, tờ khai". Thử với thủ tục đăng ký biến động đất đai: AI chỉ lấy Mẫu số 25, bỏ sót tờ khai lệ phí trước bạ (01.LPTB) và tờ khai thuế TNCN (01.TK-SDDPNN) | **Đã sửa source 05/11** (prompt + cảnh báo đếm tệp `.doc/.docx`), chờ deploy AIOCR rồi test lại KB-04 #17 bằng chính PDF này |
| BE-05 | KB-04 #15 | Rollback nhận số quyết định và ngày hiệu lực mới, **không** so với ngày của phiên bản trước. Phiên bản cũ giữ nguyên trong lịch sử. Nội dung được áp dụng ngay, ngày hiệu lực chỉ để ghi nhận | Nếu cần, chặn ngày hiệu lực lùi so với phiên bản đang dùng, hoặc hiện cảnh báo ở FE |
| BE-06 | KB-05 #7 | **Lỗi BE.** Chia đoạn lại (và sửa nội dung tài liệu, đồng bộ lại thủ tục đã đổi) tạo đoạn mới có sẵn khóa Guid; EF coi là dòng cũ, chạy UPDATE 0 dòng → 503 `knowledge.database_unavailable` | **Đã sửa source 07/11** (`KnowledgeBaseService`: thêm rõ các đoạn mới vào context). Chờ deploy AIOCR, test lại KB-05 #5 (sửa nội dung), #7 |
| BE2-01 (Codex) | KB-20 #2a | **Lỗi BE, chưa rõ nguyên nhân.** `PUT /api/v1/system/settings/analytics.lookbackDays` trả 500 (traceId `00-90ead3a8d489d9849f314116a4556201-804120d81728bcea-00`). Đọc cấu hình và nhật ký vẫn 200, kiểm tra 400 vẫn chạy → lỗi xảy ra ở đoạn ghi DB trong `Infrastructure/SystemAdministration.cs` `Update` (transaction + `pg_advisory_xact_lock` + ghi `system_settings` và `system_audit_logs`). Bảng `analytics.system_settings` có trigger `wardmate_audit_capture` (migration `20261102100005_TransactionalAuditSourceAnalyticsDbContext`) ghi vào `audit_transport.outbox`; PROGRESS TASK-34 ghi migration audit chưa chạy trên Azure | Codex xem log Analytics theo traceId. Kiểm tra DB Analytics Azure: `analytics.__AnalyticsMigrationsHistory` có đủ 7 migration; có schema/bảng `audit_transport.outbox`, hàm `audit_transport.capture`; trigger trên `analytics.system_settings`. Nếu thiếu thì chạy SQL migration còn thiếu (qua Anti), không sửa code |
| BE2-02 (Codex) | KB-20 #8a | **Lỗi BE, chưa rõ nguyên nhân.** `POST /api/v1/system/backups` trả 500 (traceId `00-8c8f750304b03efce5adc891a7a8deeb-1cc80412eca74584-00`). Đã qua các kiểm tra `SystemBackups:Enabled`, `RunnerProvisioned`, Blob và target → lỗi ở đoạn ghi DB trong `Infrastructure/SystemBackups.cs` `Create` (transaction + `pg_advisory_xact_lock` + đọc/ghi `system_backup_logs`) | Codex xem log theo traceId; kiểm tra bảng `analytics.system_backup_logs` (migration `20261103070217_SystemOperationsHistory`) đã có trên Azure và khớp model. Cùng kiểu lỗi BE2-01 nên có thể chung nguyên nhân (thiếu migration) |


### 6.3 Test lại cuối đợt

Chủ dự án quyết định (09/11): **chỉ test lại sau khi đã chạy xong mọi KB và đã sửa hết lỗi**, không test lại từng phần giữa đợt. Danh sách dồn lại:

| KB / bước | Lý do test lại | Điều kiện |
|---|---|---|
| KB-01 #6 | FE-01 ô chọn phường | FE xong FE-01 |
| KB-01 #8 | 429 chatbot khách | Hạ tạm `Assistant__DailyGuestRequestsPerIp` xuống 2 rồi trả về 10 |
| KB-02 #10 | Tự làm mới token (FE-05) | FE xong FE-05 |
| KB-04 #7, #12 | Liên kết mẫu đơn (FE-07) | FE xong FE-07 |
| KB-04 #17 | BE-04 bóc đủ biểu mẫu | Đã deploy `aiocr-20261108-01` |
| KB-05 #5, #7 (và #8 UPDATED) | BE-06 chia đoạn lại | Đã deploy `aiocr-20261108-01` |
| KB-16 #7, #10a | Gửi email thông báo | Đã bật Email Analytics 09/11 |
| KB-18 #4, #5, #6 | Phân quyền trên hồ sơ/giấy tờ thật | Sau khi chạy luồng chính |
| KB-17 #1–#5 | Số liệu và đánh giá thật | Sau khi chạy luồng chính |
| KB-20 #2, #3, #8, #9, #10 | Sau khi sửa BE2-01, BE2-02 | Codex/Anti xử lý xong |
| KB-13 #27 | Giao hồ sơ cho cán bộ đang nghỉ | Sau khi chạy luồng chính |
| KB-14 #5 | Hạn xử lý hồ sơ lùi theo ngày nghỉ | Sau khi chạy luồng chính |
| KB-19 #1–#6 | FE chưa deploy phần xử lý lỗi AI; bước 4–6 cần hồ sơ | FE deploy, luồng chính |
| Các bước UI/L phát sinh sau | Ghi thêm vào đây khi test các KB còn lại | — |

**Cách đọc:** cột **UI** = việc FE còn thiếu; cột **L** = lỗi cần BE/FE sửa; API trong mục 4 chưa có dấu "Đã test" sau khi chạy hết = chức năng chưa được dùng trên giao diện.

## 7. Giới hạn của tài liệu

- KB-13 (viết lại) và KB-20 (mới) ngày 08/11: route đã đối chiếu với controller; kỳ vọng chi tiết của KB-13 lấy từ `docs/Lịch làm việc Front Desk và đơn nghỉ Word.md` (bàn giao của BE lịch làm việc), của KB-20 đối chiếu mã lỗi trong `SystemAdministration`, `SystemHealth`, `SystemBackups`. Chưa xác nhận các API này đã có trên Azure.
- Kỳ vọng mã HTTP lấy từ source và `api-guide.md`; một số mã lỗi phụ (ví dụ 200 hay 204 cho thao tác xóa) có thể khác nhẹ. Khi thấy khác, đối chiếu `api-guide.md` trước khi ghi lỗi.
- Các ghi chú "giới hạn đã biết" (ví dụ chuyên viên tham vấn không gọi được AI, RAG đang tắt) **không phải lỗi**.
- Tài liệu chưa được chạy thử trên Azure. Người viết chỉ đối chiếu với source; kết quả thật do FE và chủ dự án ghi.
