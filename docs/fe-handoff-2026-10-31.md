# Bàn giao Backend → Frontend — tuần 24/10–31/10/2026

Người viết: Backend (Claude), 31/10/2026. Nguồn: `PROGRESS.md`, `docs/api-guide.md`, code hiện tại và báo cáo của Antigravity (Anti, phụ trách Git/Azure).

Tài liệu này tóm tắt những gì BE đã làm trong tuần, tình trạng các lỗi FE báo, và **việc FE cần làm**. Chi tiết từng API (body, response, mã lỗi) xem `docs/api-guide.md`; 18 API điều phối/lịch làm/tham vấn xem `docs/fe-staffing-18-apis.md`.

Quy ước cột "Azure":
- **Live**: Anti đã deploy và đã gọi thử thành công.
- **Đã deploy**: Anti báo đã cập nhật image, chưa có lần gọi thử thật với token.
- **Chưa xác nhận**: có trong source, chưa có xác nhận deploy. FE gặp lỗi lạ ở phần này thì báo BE trước khi sửa phía FE.

---

## 1. BE đã làm gì trong tuần

### 1.1 Hồ sơ, phân công, cán bộ (ApplicationWorkflow, IAM)

| Mã | Nội dung | Azure |
|---|---|---|
| WORKFLOW-ASSIGN-01 (24/10) | Phân công hồ sơ theo lĩnh vực: bộ lọc `categoryIds`, `scope=my-categories`, `assignee`, `overdue`, `sort`; cán bộ trả lại hồ sơ (`release`); Manager giao/chuyển hồ sơ (`PUT .../assignee`); lịch sử phân công | Đã deploy (Workflow 29/10) |
| FIX 24/10 | Công dân không thấy lý do nhận/giao/trả hồ sơ nội bộ (`assignmentHistory = null`, `history[].reason = null` ở các bước đó) | Đã deploy |
| WORKFLOW-RESUBMIT-01 (24/10) | `resubmitCount` luôn là số nguyên (mặc định 0), không còn `null` | Đã deploy |
| IAM-CATEGORY-01 (24/10) | Gán lĩnh vực cho cán bộ kiểm tra ID với Catalog; lỗi mới 400 `iam.invalid_categories`, 503 `iam.catalog_unavailable` | Chưa xác nhận |
| WORKFLOW-FE-1026 (29/10) | Bỏ hồ sơ nháp khỏi danh sách cán bộ; thêm `applicantName`; API `assignment-summary` cho Manager; API nhật ký hoạt động cán bộ | Live (Workflow `workflow-20261029-01`) |
| WORKFLOW-COMMENT-01 (29/10) | Cán bộ đang phụ trách rút nhận xét OPEN có lý do; trạng thái nhận xét `WITHDRAWN` | Live |
| STAFFING-WORKFLOW-01 (29/10) | 18 API: điều phối hồ sơ của Manager, ca làm, nghỉ phép, tham vấn chuyên môn | Đã deploy (Workflow `-02`). Script SQL staffing trên DB cloud chưa được BE xác nhận |
| ACCESS-CACHE-01 (30/10) | Cache quyền truy cập IAM 30 giây để giảm độ trễ | Chưa xác nhận |
| DOC-CALENDAR-SPEC-01 (30/10) | Đặc tả ngày nghỉ phường/lễ cho deadline 45 giờ | **Chưa làm**, mới có đặc tả |

### 1.2 Trợ lý AI (AIOCR)

| Mã | Nội dung | Azure |
|---|---|---|
| TASK-28/29 (24/10) | Kho tri thức (Knowledge Base) và chatbot trả lời có dẫn nguồn (RAG) | Có trong image; **RAG đang tắt** |
| AIOCR-ERR-01 (29/10) | Sửa mã lỗi: gửi chen cùng phiên → 409 `chat.busy`; mất DB → 503 `chat.database_unavailable` | Live |
| AI-USAGE-01 (30/10) | Hạn mức Gemini: 30 lượt/người/ngày, 500 lượt/hệ thống/ngày; nhật ký `ai_request_logs`; API báo cáo `GET /api/v1/ai/knowledge/usage` | Live (`aiocr-20261030-02`) |
| AI-GUEST-01 (30/10) | **Chatbot công khai, không cần đăng nhập**, có hạn mức cho khách | Live (Anti gọi thử 200 OK, 30/10) |
| 31/10 | Tạo đủ bảng AIOCR trên DB cloud → hết lỗi 503 chat và tiền kiểm | Live (Anti xác nhận) |

### 1.3 Thông báo, hạ tầng, bảo mật

| Mã | Nội dung | Azure |
|---|---|---|
| NOTIFY-ADMIN-01 (24/10) | Trang quản trị delivery lỗi cho IT_ADMIN (xem, gửi lại) | Chưa xác nhận |
| SMS-REMOVE-01 (29/10) | **Gỡ SMS hoàn toàn**: OTP chỉ qua Email; bỏ 3 API xác minh số điện thoại; bỏ API đối soát SMS | Analytics: chưa xác nhận; IAM: chưa deploy |
| IAM-EMAIL-01 (30/10) | Bật gửi Email OTP trên Azure | Live |
| JWT-SECRET-01 (30/10) | Đổi khóa ký JWT → **mọi token cũ hết hiệu lực**, người dùng đăng nhập lại | Live |
| RABBITMQ-CLOUDAMQP-01 (30/10) | Bật RabbitMQ (CloudAMQP Free) cho thông báo | Live |
| 31/10 | Bật worker thông báo trên Analytics (`Notifications__WorkersEnabled=true`) | Anti vừa chạy lệnh, chưa kiểm tra lại |
| AZURE-SCALE-01 (30/10) | Mọi service về `minReplicas = 0` để tiết kiệm → lần gọi đầu sau thời gian rảnh **chậm vài giây mỗi service** | Live. Riêng Analytics sẽ đặt lại `minReplicas = 1` |
| HEALTH-01 (30/10) | `/health/ready` kiểm tra DB và RabbitMQ (dùng cho vận hành, FE không cần gọi) | AIOCR live; service khác chưa xác nhận |

---

## 2. Tình trạng 3 lỗi FE báo ngày 30–31/10

| # | Lỗi | Tình trạng | Ai làm tiếp |
|---|---|---|---|
| 1 | Chat và tiền kiểm trả 503 `chat.database_unavailable` | **Đã xử lý.** Nguyên nhân: DB của AIOCR thiếu bảng. Không phải do thiếu biến cấu hình hay lỗi DI như FE đoán | FE thử lại |
| 2 | Tiền kiểm trả 404 `ai.document_not_found` | **Không phải lỗi BE.** `documentId` FE gửi không thuộc danh sách giấy tờ minh chứng của hồ sơ | FE sửa (mục 3.2) |
| 3 | `notification_deliveries` luôn `total: 0` | **Đang xử lý.** Worker tạo delivery chưa được bật trên Azure; Anti đã bật 31/10, đang chờ xác nhận và đặt Analytics luôn chạy | Chờ BE báo, rồi FE thử (mục 3.3) |

---

## 3. Việc FE cần làm

### 3.1 Chatbot công khai (ưu tiên cao, đã live)

- **Bỏ bắt đăng nhập** ở màn chatbot. Chỉ `POST /api/v1/ai/chat/completions` mở công khai.
- Body giữ nguyên: `{ "message": "...", "conversationId": null | "<guid>", "wardCode": "..." }`. **`wardCode` vẫn bắt buộc với khách** (chữ, số, `_`, `-`, tối đa 50 ký tự). `message` tối đa 4000 ký tự.
- **Khách:**
  - Không gửi header `Authorization`.
  - Lưu `conversationId` từ response và gửi lại ở lượt sau để AI nhớ ngữ cảnh.
  - **Tự lưu nội dung hội thoại trên trình duyệt**: API lịch sử (`GET /api/v1/ai/chat/sessions`, `.../messages`) vẫn bắt buộc đăng nhập.
- **Người đã đăng nhập:**
  - Gửi token như cũ.
  - **Làm mới token trước khi gửi.** Token hết hạn hoặc sai trên API này **không bị 401** mà bị coi là khách, nên lượt chat sẽ không vào lịch sử của tài khoản và bị tính vào hạn mức khách.
- Các chức năng khác **vẫn bắt buộc đăng nhập**: OCR CCCD, tiền kiểm giấy tờ, Knowledge Base.
- **Hạn mức của khách** (mặc định, BE có thể chỉnh): 5 lượt/phút/IP, 10 lượt/ngày/IP, 200 lượt/ngày cho toàn bộ khách. Nhiều người dùng chung mạng (cùng IP) sẽ dùng chung hạn mức.

### 3.2 Xử lý mã lỗi AI (phân biệt theo trường `code` của ProblemDetails)

| HTTP | `code` | Ý nghĩa | FE hiển thị |
|---|---|---|---|
| 429 | `ai.guest_quota_exceeded` | Khách (theo IP) hết lượt hôm nay | "Bạn đã hết lượt hỏi hôm nay. Đăng nhập để hỏi tiếp." + nút Đăng nhập |
| 429 | `ai.guest_limit_reached` | Toàn bộ khách đã hết lượt hôm nay | "Hệ thống đã hết lượt cho khách hôm nay. Đăng nhập hoặc thử lại ngày mai." |
| 429 | `ai.quota_exceeded` | Người dùng đăng nhập hết 30 lượt/ngày | "Bạn đã dùng hết lượt hôm nay, thử lại ngày mai." |
| 429 | `ai.daily_limit_reached` | Toàn hệ thống hết lượt AI hôm nay | "Trợ lý AI tạm hết lượt hôm nay." |
| 429 | (không có `code`) | Gửi quá nhanh (giới hạn theo phút) | "Vui lòng chờ một chút rồi gửi lại." Không tự gửi lại liên tục |
| 409 | `chat.busy` | Đang gửi một câu khác trong cùng phiên | Khóa nút Gửi khi đang chờ trả lời |
| 502 | `ai.output_truncated` | Câu trả lời quá dài bị cắt | "Câu hỏi quá rộng, hãy hỏi cụ thể hơn." |
| 429 | `ai.provider_busy` | (từ 01/11, sau khi deploy) Google Gemini quá tải hoặc hết hạn mức tạm thời; header `Retry-After` = số giây nên chờ | Hiện đếm ngược theo `Retry-After`, sau đó mới cho gửi lại |
| 503 | `ai.provider_unavailable` | Gemini lỗi (BE đã tự thử lại 1 lần) | "Trợ lý AI tạm thời không phản hồi." |
| 503 | `chat.database_unavailable` | AIOCR không truy cập được DB | Như trên. **Báo BE**, kèm `traceId` |

Nếu FE giữ màn hình "hướng dẫn chung" khi gặp 503, phải ghi rõ **đây không phải câu trả lời của AI**.

### 3.3 Tiền kiểm giấy tờ bằng AI (sửa lỗi 404)

- API: `POST /api/v1/ai-ocr/documents/verify`, body `{ "applicationId": "<guid>", "documentId": "<guid>", "expectedType": null }`, bắt buộc đăng nhập.
- **`documentId` phải là trường `id`** lấy từ `GET /api/v1/applications/{applicationId}/supporting-documents`.
  - Không dùng `submissionId` (tờ khai DOCX) hoặc ID của PDF đã sinh.
  - `applicationId` phải đúng hồ sơ chứa giấy tờ đó.
- Chỉ gọi sau khi upload giấy tờ đã trả **201**.
- Tiền kiểm chỉ đọc **PDF, PNG, JPEG**. Không gửi file DOCX; **không làm fallback sang submissions**.
- **Ai được gọi:**
  - Công dân: hồ sơ của chính mình. Nên chỉ hiện nút khi hồ sơ còn sửa được.
  - Cán bộ: chỉ **cán bộ đang được phân công** hồ sơ. **Ẩn nút với MANAGER** (Manager không có quyền này).
- Response 200: `{ isValid, documentTypeDetected, qualityIssues[], confidence, checklistId, expectedType }`. Đây là **gợi ý hỗ trợ**, không phải kết luận hành chính.
- **Lỗi thường gặp:**

  | HTTP | `code` | Ý nghĩa |
  |---|---|---|
  | 400 | `ai.invalid_input` | Thiếu `applicationId` hoặc `documentId` |
  | 404 | `ai.document_not_found` | `documentId` không thuộc hồ sơ |
  | 401/403/404 | `ai.document_access_failed` | DocumentForm từ chối quyền truy cập |
  | 503 | `ai.document_access_failed` | DocumentForm không phản hồi |

- **Vẫn 404 sau khi sửa:** gửi BE `applicationId`, `documentId` và thời điểm gọi.

### 3.4 Thông báo (sau khi BE báo đã xong)

1. Nộp một hồ sơ mới, hoặc phân công/chuyển một hồ sơ.
2. Mở trang thông báo của người nhận.
   - Analytics đang có thể tắt khi rảnh, nên lần đầu có thể trống: **chờ khoảng 30 giây rồi tải lại**.
   - Khi Analytics đã được đặt luôn chạy, thông báo sẽ đến sau vài giây.
3. Chỉ kiểm tra bằng thao tác mới. Thao tác trước khi bật worker có thể không tạo thông báo.
4. **Vẫn trống:** gửi BE tài khoản người nhận (không gửi mật khẩu), `applicationId`, loại thao tác và thời điểm.

Lưu ý: không có sự kiện `ApplicationSubmittedEvent` hay `ApplicationStatusChangedEvent`. BE chỉ có `WorkflowEvent` với `Kind` = `STATUS` hoặc `APPLICATION_ASSIGNED`. FE chỉ đọc API thông báo, không phụ thuộc tên sự kiện.

### 3.5 Gỡ SMS (làm ngay, không chờ deploy)

- Bỏ màn **xác minh số điện thoại**: 3 API `/api/v1/users/me/phone-verification[/request|/confirm]` sẽ không còn.
- Quên/đổi mật khẩu: **chỉ cho chọn Email**. Gửi kênh khác Email → 400.
- Trang quản trị delivery:
  - Bỏ nút **đối soát SMS**.
  - Gửi lại không còn trường `acknowledgeSmsCost`.
  - Delivery SMS cũ trả 409 `notifications.channel_removed` khi gửi lại.

### 3.6 Hồ sơ và phân công (Workflow)

- **`applicantName`** (danh sách và chi tiết): có thể `null` với hồ sơ nộp trước 29/10. FE hiển thị tên dự phòng, ví dụ "—".
- **Danh sách cán bộ** không còn hồ sơ nháp: `status=DRAFT` trả mảng rỗng. Bỏ bộ lọc DRAFT phía cán bộ.
- **`resubmitCount`** luôn là số. Bỏ xử lý `null`.
- **Manager:** màn tổng quan phân công dùng `GET /api/v1/manager/applications/assignment-summary` → `{ unassignedSubmitted, overdue, officers[{ officerId, activeCount, overdueCount }] }`.
- **Nhật ký hoạt động:** `GET /api/v1/officer/activity?page&pageSize&from&to&action&officerId`. Chỉ Manager được truyền `officerId` của người khác (nếu không: 403). `page × pageSize ≤ 5000`.
- **Rút nhận xét:** `POST /api/v1/officer/applications/{id}/comments/{commentId}/withdraw`, body `{ "reason": "5–500 ký tự" }`.
  - Chỉ hiện nút cho **cán bộ đang phụ trách**, khi hồ sơ `UNDER_REVIEW` và nhận xét `OPEN`.
  - Hiển thị trạng thái `WITHDRAWN` với `withdrawnBy`, `withdrawnAt`, `withdrawReason`. Công dân không thấy nhận xét đã rút.
  - Dùng khi cán bộ mới bị chặn bởi 409 `application.open_comments` do nhận xét của cán bộ cũ.
- **Giao/chuyển/trả hồ sơ:**
  - Luôn gửi `expectedAssignmentVersion` lấy từ dữ liệu vừa đọc.
  - Gặp 409 `workflow.assignment_conflict`: tải lại hồ sơ, không tự gửi lại.
- **18 API điều phối, ca làm, nghỉ phép, tham vấn:** theo `docs/fe-staffing-18-apis.md`. Nếu gặp 500 hoặc lỗi bảng không tồn tại, báo BE: có thể script SQL staffing chưa chạy trên cloud.

### 3.7 Hành vi chung FE cần biết

- **Khởi động lạnh:** service về 0 replica khi rảnh, nên request đầu có thể mất **5–15 giây** (qua nhiều service còn lâu hơn).
  - Đặt timeout phía FE **≥ 30 giây** cho lần gọi đầu và hiện trạng thái đang tải.
  - Không tự gửi lại liên tục: dễ chạm giới hạn 429.
- **Token:** khóa JWT đã đổi ngày 30/10. Gặp 401 thì đưa người dùng về đăng nhập.
- **Cache quyền (khi ACCESS-CACHE-01 được deploy):** đổi mật khẩu, khóa tài khoản, đổi quyền hoặc phường có thể mất **tối đa khoảng 30 giây** mới có hiệu lực ở các service ngoài IAM. Đây là hành vi đúng, không phải lỗi.
- **ProblemDetails:** giữ dạng `{ status, title, code, traceId }`. Khi báo lỗi cho BE luôn gửi kèm **`traceId`, route và thời điểm**.

### 3.8 Thuật ngữ và nội dung giao diện (bắt buộc sửa)

- **Trạng thái:**
  - WardMate **không có** `Approved`, `Completed` và **không có bước phê duyệt hành chính**.
  - `DOCUMENTS_READY` = giấy tờ đã đủ và sẵn sàng, **không phải phê duyệt**.
  - `OFFICIALLY_RECEIVED` = hồ sơ đã được **tiếp nhận tại quầy**.
- Thay các câu kiểu "trước khi phê duyệt" bằng "trước khi xác nhận giấy tờ sẵn sàng".
- **Chatbot và tiền kiểm AI** chỉ hỗ trợ tham khảo; giao diện không trình bày như kết luận chính thức của phường.

---

## 4. Giới hạn hiện tại FE nên biết

- RAG (trả lời có dẫn nguồn) **đang tắt**. `citations` thường rỗng và `grounded = false`.
- Ngày nghỉ phường và lễ chưa được tính vào deadline 45 giờ; mới có đặc tả.
- Hạn mức khách theo IP lưu trong bộ nhớ, nên có thể đặt lại khi service khởi động lại.
- Hội thoại của khách được lưu trên server nhưng chưa có cơ chế tự xóa.
- Một số mục ở bảng 1 còn "Chưa xác nhận". FE gặp lỗi ở các chức năng đó thì báo BE trước khi đổi code FE.

## 5. Việc BE còn mở (để FE biết khi nào thử lại)

- Bật worker thông báo và đặt Analytics `minReplicas = 1` (Anti), rồi báo FE thử mục 3.4.
- Deploy IAM bản gỡ SMS (Anti).
- Xác nhận deploy ACCESS-CACHE-01, HEALTH-01 và NOTIFY-ADMIN-01 cho các service còn lại (Anti).
- Ngày nghỉ phường và lễ toàn quốc theo `docs/be-calendar-closures-spec.md` (BE staffing).
