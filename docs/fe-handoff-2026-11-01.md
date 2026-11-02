# Bàn giao Backend → Frontend — 01/11/2026

Người viết: Backend (Claude), 01/11/2026. Tài liệu này bổ sung cho `docs/fe-handoff-2026-10-31.md` (bàn giao tuần) và `docs/fe-response-2026-11-01.md` (trả lời 2 tài liệu FE gửi ngày 31/10). Chi tiết hợp đồng từng API xem `docs/api-guide.md`.

## 1. Đã có trên Azure (01/11/2026)

Hôm nay toàn bộ service đã được đưa lên bản build từ code mới nhất. `/health/ready` của cả 6 service đều trả `200`.

| Service | Image | Thay đổi FE cần biết |
|---|---|---|
| AIOCR | aiocr-20261101-01 | **API mới: trợ lý AI kiểm tra hồ sơ cho cán bộ.** Khi Gemini quá tải, BE trả `429 ai.provider_busy` kèm header `Retry-After` |
| Analytics | analytics-20261101-01 | **Worker thông báo đã bật và chạy liên tục.** Kênh SMS đã gỡ |
| Workflow | workflow-20261101-01 | Có 4 API lịch nghỉ (do BE lịch làm việc phụ trách), 18 API điều phối/lịch làm/tham vấn, và tự kết nối lại RabbitMQ |
| DocumentForm | documentform-20261101-01 | Không đổi API; chỉ thêm health và cache quyền |
| ProcedureCatalog | catalog-20261101-01 | Không đổi API; chỉ thêm health |
| IAM | iam-20261030-01 | OTP chỉ gửi qua Email; không còn API xác minh số điện thoại |
| Gateway | gateway-20261030-02 | Không đổi |

## 2. Việc FE cần làm và cần kiểm thử

### 2.1 Trợ lý AI kiểm tra hồ sơ (ưu tiên cao)
- **API:** `POST /api/v1/ai-ocr/applications/{applicationId}/dossier-assist`. Body không bắt buộc: `{ "includeSubmissions": true, "includeSupportingDocuments": true }`.
- **Khi nào hiện nút:** chỉ với **cán bộ đang thụ lý** hồ sơ đang ở `UNDER_REVIEW`. Ẩn nút với MANAGER và các cán bộ khác (họ sẽ nhận 403 `ai.not_assigned_officer`).
- **Thẻ kết quả gồm:**
  - `overallAssessment` (`NO_ISSUES_FOUND` / `NEEDS_ATTENTION` / `INSUFFICIENT_DATA`) và `summary`.
  - Bảng `crossChecks`: `MATCH` = khớp, `MISMATCH` = lệch, `NOT_FOUND` = không có giấy tờ để đối chiếu.
  - Danh sách `missingItems` và `issues`.
  - Danh sách `files`, kèm `skipReason` cho tệp bị bỏ qua.
  - `disclaimer` luôn hiển thị.
- **Nút "Chèn gợi ý":** đưa `suggestedComment` vào ô nhận xét để cán bộ sửa. Không tự gửi. Trường này có thể là `null`.
- **Không dùng** chữ "phê duyệt", "hợp lệ" hay "đạt" cho kết quả AI.
- **Thời gian chờ:** có thể mất 10–60 giây. Hiện trạng thái đang xử lý và khóa nút trong lúc chờ.
- **Mã lỗi cần xử lý:**
  - 409 `ai.application_not_under_review`
  - 422 `ai.dossier_empty`
  - 429 `ai.quota_exceeded`, 429 `ai.daily_limit_reached`
  - 429 `ai.provider_busy` (xem mục 2.2)
  - 502 `ai.output_truncated`: gọi lại, chỉ phân tích một phần hồ sơ
  - 503 `ai.provider_unavailable`
- **Kiểm thử:** một cán bộ đang thụ lý hồ sơ có tờ khai DOCX và ảnh CCCD bấm nút. Kỳ vọng nhận 200. Đây là lần đầu chạy với Gemini thật, nên báo BE ngay nếu kết quả trả về sai hoặc vô nghĩa.

### 2.2 Gemini quá tải (áp dụng cho chat, CCCD, tiền kiểm, dossier-assist)
- Nhận **429 `ai.provider_busy`** thì đọc header **`Retry-After`** (đơn vị giây, từ 1 đến 120), hiện đếm ngược, hết thời gian mới cho gửi lại.
- Trước đây trường hợp này trả 503 `ai.provider_unavailable`.
- **Playwright không gọi API AI thật**, phải mock. Mọi tài khoản dùng chung hạn mức Gemini miễn phí; gọi thật trong test sẽ làm hết lượt của cả hệ thống.

### 2.3 Thông báo
1. Nộp một hồ sơ mới, hoặc nhận/chuyển một hồ sơ.
2. Khoảng 10 giây sau, kiểm tra:
   - trang thông báo của người nhận;
   - trang **Phát thông báo** dưới tài khoản IT_ADMIN.
3. **Kỳ vọng:** có dòng kênh `IN_APP` với trạng thái `Pending` hoặc `Sent`. Kênh `EMAIL` chỉ xuất hiện nếu Analytics bật Email.
4. **Trạng thái delivery thật** là `Pending`, `Dispatching`, `Sent`, `Unknown`, `Dead`, `Suppressed`. Không có `DELIVERED` hay `FAILED`.
5. **SMS:** bỏ nút đối soát SMS. Gửi lại một delivery SMS cũ sẽ nhận 409 `notifications.channel_removed`. Request gửi lại không còn trường `acknowledgeSmsCost`.

### 2.4 Lịch nghỉ phường / lễ toàn quốc (của BE lịch làm việc)
- 4 API dưới `/api/v1/staffing/closures` đã có trong image Workflow mới.
- Hợp đồng chi tiết và quyền truy cập xem `docs/fe-staffing-18-apis.md` (nay gồm 22 API).
- **Vấn đề cần báo BE lịch làm việc** (không phải Claude): lỗi nghiệp vụ, dữ liệu sai, hoặc kết quả tính lại hạn xử lý không đúng.

### 2.5 Gỡ SMS ở IAM
- Bỏ màn hình xác minh số điện thoại.
- OTP quên mật khẩu chỉ cho chọn Email. Chọn kênh khác sẽ nhận 400.

## 3. Chung
- **Khởi động lạnh:** hầu hết service về 0 replica khi không có người dùng, nên lần gọi đầu có thể mất 5–15 giây. Đặt timeout phía FE ≥ 30 giây.
- **Báo lỗi cho BE:** gửi kèm **route, mã HTTP, `code`, `traceId` và thời điểm gọi**. Không gửi mật khẩu hay token.
- **Chưa có ai kiểm thử bằng tài khoản thật** (có token) cho các phần trên. FE là bên nghiệm thu đầu tiên.
