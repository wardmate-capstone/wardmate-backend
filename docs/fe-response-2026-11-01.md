# Phản hồi Backend cho 2 tài liệu FE ngày 31/10/2026

Người viết: Backend (Claude), 01/11/2026.

Hai tài liệu FE gửi:
- `BE_FEATURE_REQUEST_AI_DOSSIER_ASSISTANT.md`: trợ lý AI kiểm tra hồ sơ.
- `BE_DEVOPS_NOTIFICATION_WORKER_CONFIG.md`: bật worker thông báo.

---

## 1. Trợ lý AI kiểm tra hồ sơ (AI-OFFICER-ASSIST-01)

**Tình trạng:** BE **đã làm xong ở source** (build Release đạt, kiểm thử local 30/30 ca). **Chưa deploy** lên Azure; Anti deploy lại AIOCR, không cần migration. Hợp đồng API chi tiết xem `docs/api-guide.md`, mục "Trợ lý AI kiểm tra hồ sơ cho cán bộ".

### 1.1 API

`POST /api/v1/ai-ocr/applications/{applicationId}/dossier-assist`, gửi kèm Bearer token. Body không bắt buộc:

```json
{ "includeSubmissions": true, "includeSupportingDocuments": true }
```

BE tự lấy tờ khai DOCX và giấy tờ kèm theo của hồ sơ, trích chữ DOCX trên server (DocumentFormat.OpenXml như FE đề xuất), gửi cho Gemini rồi trả kết quả đối chiếu. **FE không cần gửi file hay ID giấy tờ.**

### 1.2 Khác với đề xuất của FE và lý do

| Đề xuất FE | BE làm | Lý do |
|---|---|---|
| Manager cũng được gọi | **Chỉ cán bộ đang thụ lý** hồ sơ `UNDER_REVIEW`. Người khác nhận 403 `ai.not_assigned_officer` | Quyền giám sát của Manager không bao gồm thao tác của cán bộ; DocumentForm cũng chỉ cho cán bộ thụ lý tải DOCX. **FE ẩn nút với Manager.** |
| `overallVerdict`: `READY_TO_RECEIVE` / `REQUIRES_ATTENTION` / `INVALID` | `overallAssessment`: `NO_ISSUES_FOUND` / `NEEDS_ATTENTION` / `INSUFFICIENT_DATA` | WardMate không có phê duyệt hành chính; AI không được kết luận hồ sơ "hợp lệ" hay "được tiếp nhận". |
| `isRecommendedAccept` | Bỏ | Cùng lý do trên. |
| `policyReference` (ví dụ "Khoản 1 Điều 32 Luật Hộ tịch 2014") | Bỏ; AI được yêu cầu không trích luật | BE chưa có nguồn luật đã kiểm chứng; AI dễ bịa điều khoản. |
| `fieldsExtracted` là object tự do | `extractedFields: [{name, value, source}]` | Mỗi thủ tục có trường khác nhau; dạng mảng ổn định hơn cho FE. |
| `crossCheckResults[].matched` (bool) | `crossChecks[].result` = `MATCH` / `MISMATCH` / `NOT_FOUND` | Phân biệt "lệch" với "không có giấy tờ để đối chiếu". |
| Bảng `dossier_ai_reviews` lưu kết quả | Không lưu kết quả; chỉ ghi nhật ký `ai_request_logs` (ai gọi, lúc nào, model nào) | Tránh lưu thêm dữ liệu cá nhân. Nhận xét thật của cán bộ vẫn được Workflow lưu khi cán bộ gửi. |
| Trường `dossierStatus` | Tên trường là `applicationStatus` | Cùng tên trường với các API khác. |

### 1.3 FE cần làm

1. **Nút "AI hỗ trợ kiểm tra hồ sơ":** chỉ hiện cho **cán bộ đang thụ lý**, khi hồ sơ ở `UNDER_REVIEW`.
2. **Thẻ kết quả:**
   - `overallAssessment` kèm `summary`.
   - Bảng `crossChecks`: khớp (MATCH), lệch (MISMATCH), không có giấy tờ (NOT_FOUND).
   - Danh sách `missingItems` và `issues`.
   - Danh sách `files`: tệp nào đã phân tích, tệp nào bị bỏ qua và lý do (`skipReason`).
3. **Gợi ý nhận xét:** nút "Chèn gợi ý" đưa `suggestedComment` **vào ô nhận xét để cán bộ sửa**, không tự gửi. `suggestedComment` có thể là `null`.
4. **Ghi chú `disclaimer`:** luôn hiển thị. Không dùng chữ "phê duyệt", "hợp lệ" hay "đạt" cho kết quả AI.
5. **Thời gian chờ:** gọi lần đầu có thể mất 10–60 giây (tải nhiều tệp, khởi động lạnh, Gemini xử lý). Hiện trạng thái đang xử lý và **khóa nút** trong lúc chờ.
6. **Mã lỗi:**
   - `422 ai.dossier_empty`: hồ sơ chưa có tệp phân tích được.
   - `409 ai.application_not_under_review`: cán bộ cần nhận hồ sơ trước.
   - `502 ai.output_truncated`: gọi lại với `includeSubmissions` hoặc `includeSupportingDocuments` = `false` để phân tích từng phần.
7. **Giới hạn mỗi lần gọi:** 8 tệp; ảnh/PDF tối đa 5 tệp và tổng 12 MiB. Tệp vượt giới hạn bị bỏ qua chứ không làm lỗi cả request.

### 1.4 Lỗi 503 `ai.provider_unavailable` (mục 6 tài liệu FE)

| Đề xuất FE | BE làm |
|---|---|
| Polly thử lại 3 lần, chờ 2s/4s/8s | Google trả 429 → BE **trả ngay 429 `ai.provider_busy`** kèm header **`Retry-After`** (số giây nên chờ, 1–120). Google lỗi 5xx → BE **tự thử lại 1 lần**. Không giữ request chờ 14 giây vì vừa làm nghẽn server, vừa tiếp tục làm vượt hạn mức phút của Google. |
| Chuyển `Retry-After` về FE | Đã làm, áp dụng cho **mọi API AI**: chat, CCCD, tiền kiểm, dossier-assist. |
| Bật billing hoặc dùng nhiều API key | **Không làm.** Dự án chỉ dùng dịch vụ miễn phí. Xoay vòng nhiều key để vượt hạn mức cũng trái điều khoản của Google. |
| Đổi model sang `gemini-2.5-flash` / `1.5-pro` | **Không đổi.** Model là cấu hình vận hành do chủ dự án quyết định. |

**FE cần làm:**
- Gặp `429 ai.provider_busy`: đọc header `Retry-After`, hiện đếm ngược, hết giờ mới cho gửi lại.
- **Không chạy Playwright gọi API AI thật liên tục**, vì sẽ làm hết hạn mức chung của cả hệ thống (mặc định 500 lượt/ngày) và hạn mức phút của Google. Kiểm thử tự động nên mock API AI.

### 1.5 Thay tờ khai DOCX (mục 5 tài liệu FE)

Đồng ý giữ `PUT /api/v1/citizen/submissions/{submissionId}/draft` để ghi đè; BE không thêm API DELETE.

### 1.6 Lưu ý dữ liệu cá nhân

Trợ lý gửi nội dung hồ sơ, kể cả số định danh, tới Google Gemini **gói miễn phí**. Theo điều khoản của Google, dữ liệu gửi qua gói miễn phí có thể được dùng để cải thiện sản phẩm. Chủ dự án đã chấp nhận rủi ro này ngày 01/11/2026. FE **không thêm** dữ liệu cá nhân nào khác vào request.

---

## 2. Worker thông báo (tài liệu DevOps)

**Tình trạng:** không cần sửa code. Anti đã bật `Notifications__WorkersEnabled=true` ngày 31/10 và đang đặt Analytics `minReplicas = 1`. BE sẽ báo khi xong để FE thử lại.

### 2.1 Đính chính tài liệu FE

| Tài liệu FE ghi | Đúng theo code |
|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings__NotificationsDatabase` (đã cấu hình) |
| `EventBus__RabbitMQ__HostName` / `UserName` / `Password` | `RabbitMq__Enabled`, `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__User`, `RabbitMq__Password`, `RabbitMq__VirtualHost`, `RabbitMq__UseTls` (đã cấu hình CloudAMQP). Đặt theo tên FE ghi sẽ không có tác dụng. |
| `--max-replicas 3` | Nên để **1**: job thống kê theo lịch chạy trên từng replica, nhiều replica sẽ chạy trùng. |
| Kênh `IN_APP` hoặc `EMAIL` | Đúng. `IN_APP` luôn có; `EMAIL` chỉ có khi Analytics bật `Email__Enabled=true`. |
| Trạng thái `PENDING` / `DELIVERED` / `FAILED` | Trạng thái thật: `Pending`, `Dispatching`, `Sent`, `Unknown`, `Dead`, `Suppressed`. FE map nhãn hiển thị theo các giá trị này. |
| Bảng "lập tức" có dòng mới | Sau khi bật `minReplicas = 1`, dòng mới thường xuất hiện **sau vài giây**: sự kiện đi qua outbox của Workflow, rồi RabbitMQ, rồi inbox của Analytics. |

### 2.2 Kịch bản kiểm thử (sau khi BE báo xong)

1. Công dân nộp hồ sơ mới, hoặc cán bộ nhận/chuyển hồ sơ.
2. Chờ khoảng 10 giây, tải lại trang **Phát thông báo** bằng tài khoản IT_ADMIN.
3. Kỳ vọng: có dòng kênh `IN_APP` với trạng thái `Pending` hoặc `Sent`.
4. Vẫn trống: gửi BE thời điểm thao tác, `applicationId` và `traceId` (nếu có).
