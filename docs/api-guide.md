> Cập nhật 2026-10-14: chủ dự án đã yêu cầu gỡ bộ test và script kiểm thử khỏi repository. Các kết quả kiểm thử bên dưới là lịch sử; không còn chạy được các script cũ. Dùng Swagger và `docs/api-guide.md` để kiểm tra thủ công.

> Cập nhật 16/10/2026 — Luồng hiện hành là kiểm tra giấy tờ → DOCUMENTS_READY, không có approve/reject mới. Các mục ghi lịch sử task cũ không còn là hợp đồng của hai endpoint đã bỏ. Xem mục confirm-readiness bên dưới; chỉ áp dụng sau khi deploy đồng bộ Workflow, DocumentForm, Analytics và migrate Workflow/IAM.


# WardMate — Chức năng và hướng dẫn sử dụng API theo Service

## TASK-31–33 — Analytics và đánh giá công dân (21/10/2026)

Phần này mô tả code local mới; không xác nhận đã deploy Azure. AnalyticsSystem vẫn chứa
Notifications, bổ sung schema `analytics` với DbContext/migration riêng. Không truy vấn DB
Workflow/IAM từ Analytics. Swagger: `/api/analytics-system/swagger/index.html` qua Gateway,
hoặc `http://localhost:5006/swagger/index.html` khi chạy local bật Swagger.

### Cách hiểu số liệu trước khi tích hợp FE

- Số liệu **sự kiện theo ngày**, múi giờ `Asia/Ho_Chi_Minh` (UTC+7). Khoảng ngày gồm cả From/To.
  Không phải snapshot số hồ sơ đang nằm trong từng trạng thái. Không cộng Received + Ready
  để suy ra số hồ sơ duy nhất: một hồ sơ có thể được tiếp nhận và xác nhận trong cùng ngày.
- `receivedCount`: hồ sơ chuyển DRAFT → SUBMITTED lần đầu trong ngày;
  `resubmittedCount`: số lượt NEED_REVISION → SUBMITTED;
  `revisionRequestedCount`: số lượt yêu cầu bổ sung; `readyCount`: hồ sơ đạt DOCUMENTS_READY.
  `cancelledCount`: hồ sơ hủy. `legacyApprovedCount`/`legacyRejectedCount` chỉ phản ánh lịch sử cũ,
  không cộng vào Ready và không khôi phục API phê duyệt cũ.
- Cán bộ được ghi nhận bằng người thực hiện sự kiện (`ChangedBy`), không phải người đang được giao
  hồ sơ tại thời điểm chạy job. `averageElapsedMinutes` tính từ lần nộp đầu đến DOCUMENTS_READY,
  bao gồm thời gian công dân bổ sung và chờ; **không phải số phút cán bộ trực tiếp làm việc**.
  Bình quân có trọng số theo `durationSampleCount`, thiếu mẫu trả null.
- Workflow chưa có deadline chuẩn hóa: `onTimeRate` và `lateRate` trả null,
  `timelinessStatus=UNAVAILABLE_NO_WORKFLOW_DEADLINE`. FE hiển thị “Chưa có dữ liệu hạn xử lý”,
  không hiển thị 0%/100%. Không suy deadline từ ProcessingTimeSummary hoặc ApprovedAt cũ.
- `coverage.missingDates` cho biết ngày chưa từng tổng hợp thành công; biểu đồ có điểm 0 cho ngày
  đó nhưng FE phải đánh dấu thiếu dữ liệu. `oldestSourceAtUtc`/`latestRefreshAtUtc` cho biết độ mới.
  Nếu job lỗi, số liệu cũ được giữ lại. Ngày hiện tại có thể chưa đầy đủ đến cuối ngày.
- ID cán bộ/thủ tục là ID liên service. FE lấy tên qua API mà người dùng có quyền; backend không
  đọc chéo database để lấy tên. Dữ liệu lịch sử thiếu ward được lưu nhóm rỗng và không lộ cho Manager.

### Quyền và bộ lọc chung

Dashboard dùng Bearer JWT IAM và role **MANAGER**, chỉ xem phường hiện tại lấy từ IAM
`access-context` mỗi request. `PROCEDURE_MANAGER` không thay thế MANAGER. Không có tham số ward
để tự mở rộng quyền, không tin ward/role cũ trong token. IT_ADMIN chỉ có quyền job bên dưới,
không tự được đọc dashboard hoặc nhận xét của người dân.

Các API GET dashboard nhận `from=2026-10-01&to=2026-10-21`, tùy chọn `procedureId`.
Ngày bắt buộc, tối đa 366 ngày, From >= 2000-01-01, To không vượt hôm nay UTC+7.
Các danh sách có `page` (1–100000), `pageSize` (1–100, mặc định 20).
Ví dụ: `GET /api/v1/analytics/overview?from=2026-10-01&to=2026-10-21`.

Lỗi chung: 400 ngày/GUID/phân trang sai; 401 thiếu/hết hạn token hoặc tài khoản không khả dụng;
403 thiếu role/phường; 503 IAM không truy cập được. Sửa query, đăng nhập lại hoặc thử lại sau,
không bỏ kiểm tra quyền khi IAM lỗi. Lỗi nghiệp vụ trả ProblemDetails có `code`, `traceId`;
lỗi model binding trả ValidationProblemDetails, có thể không có `code`. Challenge JWT 401 có thể
không có JSON body, FE không được giả định mọi lỗi đều có body.

| Method / route | Ai gọi, lúc nào và FE dùng thế nào | Kết quả thành công / cách khắc phục sai dùng |
|---|---|---|
| GET `/api/v1/analytics/overview` | Manager mở các thẻ thống kê | 200 `{totals,coverage,onTimeRate,lateRate,timelinessStatus}`; kiểm tra coverage trước khi ghi “không có hồ sơ” |
| GET `/api/v1/analytics/trends` | Manager mở biểu đồ theo ngày | 200 `{items:[{date,totals}],coverage}`; khoảng ngày không phải UTC, tránh tự dịch thêm 7 giờ |
| GET `/api/v1/analytics/procedures` | Manager xem bảng theo thủ tục | 200 `{items:[{procedureId,totals}],page,pageSize,totalCount,coverage}`; sort Received giảm rồi ID để phân trang ổn định |
| GET `/api/v1/analytics/officers` | Manager xem bảng hiệu suất cán bộ | 200 `{items:[{officerId,readyCount,revisionRequestedCount,durationSampleCount,averageElapsedMinutes}],page,pageSize,totalCount,coverage}`; không gọi thời gian này là thời gian lao động trực tiếp |
| GET `/api/v1/analytics/feedback/summary` | Manager mở biểu đồ hài lòng | 200 `{totalCount,averageRating,satisfactionRate,ratingCounts}`; số sao 1–5 luôn đủ nhóm; không có đánh giá thì average/rate null |
| GET `/api/v1/analytics/feedback` | Manager đọc nhận xét thuộc phường | 200 danh sách phân trang FeedbackDto, sắp ngày mới nhất rồi ID; không trả citizenId, không có API công khai toàn bộ nhận xét |

Điểm trung bình = tổng sao / số lượt. `satisfactionRate` = 100 × số đánh giá 4–5 sao / số lượt,
khác với averageRating/5. Bộ lọc ngày feedback dùng **ngày gửi đánh giá**, không phải ngày nộp hồ sơ.

### Công dân gửi và xem đánh giá

Role **REGISTERED_CITIZEN**, danh tính lấy từ token đã xác minh và IAM hiện tại; không nhận applicantId
hoặc citizenId từ body.

1. `POST /api/v1/citizen/feedback/{applicationId}` — người dân bấm “Gửi đánh giá” sau khi hồ sơ
   đạt DOCUMENTS_READY hoặc OFFICIALLY_RECEIVED. JSON: `{"rating":5,"comment":"Hướng dẫn rõ ràng"}`.
   Rating là số nguyên 1–5; comment tùy chọn tối đa 2000 ký tự, trim, chuỗi trắng lưu null.
   Backend gọi Workflow bằng token người dùng và kiểm tra chủ hồ sơ, trạng thái, procedure/ward.
   Thành công 201 + Location, body `{id,applicationId,rating,comment,createdAtUtc}`.
   400 sai sao/comment/GUID; 404 hồ sơ không thuộc bạn/không tồn tại; 409 `feedback.not_ready`
   nếu không ở DOCUMENTS_READY/OFFICIALLY_RECEIVED; 409 `feedback.already_exists` nếu đã gửi (kể cả gửi đồng thời).
   503 nguồn Workflow lỗi/thiếu ward hợp lệ. Chưa gửi lại khi 409 trùng; gọi GET bên dưới.
2. `GET /api/v1/citizen/feedback/{applicationId}` — khi mở lại màn hình hồ sơ, lấy đánh giá của
   chính người đăng nhập. 200 FeedbackDto; 404 nếu chưa có hoặc thuộc người khác. GET đọc bản ghi
   của chủ sở hữu đã lưu, không cần gọi Workflow lại; vẫn xác minh IAM mỗi request.

Mỗi hồ sơ chỉ một đánh giá, hiện không có sửa/xóa. Đây là đánh giá trải nghiệm chuẩn bị giấy tờ,
không phải đánh giá kết quả giải quyết hành chính chính thức tại phường. FE hiển thị comment dạng
text đã escape, không render HTML từ người dùng. Nếu gặp 503 sau POST, có thể GET trước để biết
đánh giá đã lưu hay chưa thay vì tự gửi nhiều lần.

### Job tổng hợp và hợp đồng nội bộ

- `POST /api/v1/analytics/jobs/rebuild` — **IT_ADMIN** dùng khi backfill hoặc tính lại dữ liệu lịch sử.
  JSON `{"from":"2026-10-01","to":"2026-10-21"}`. 202 `{jobId}` chỉ nghĩa đã xếp hàng, chưa hoàn tất.
  400 khoảng ngày sai; 403 không phải IT_ADMIN; 503 `analytics.jobs_disabled` nếu job chưa bật.
- `GET /api/v1/analytics/jobs/{jobId}` — IT_ADMIN theo dõi job vừa tạo. 200 `{jobId,state,createdAtUtc}`.
  `Succeeded` mới là kết thúc thành công; `Scheduled` có thể đang chờ retry; `Failed` cần xem log vận hành
  rồi sửa kết nối/nguồn và chạy lại. 400 ID không hợp lệ; 404 job không tồn tại hoặc không phải job Analytics;
  503 nếu job tắt. Không trả tham số, stack trace hay secrets của Hangfire. Không mở Hangfire Dashboard công khai.
- `GET /internal/analytics/v1/daily?date=yyyy-MM-dd` — chỉ worker Analytics gọi trực tiếp Workflow,
  header `X-Analytics-Key` khớp `Analytics:ServiceKey` ít nhất 32 byte. Không dùng token công dân,
  không xuất Swagger công khai. 200 snapshot contractVersion=1 gồm các nhóm procedure/officer;
  không chứa formData, tên công dân, comment hay file. 401 key sai/chưa cấu hình, 400 ngày sai,
  503 nếu ngày vượt 100000 bản ghi lịch sử cần chia nhỏ nguồn. Không retry bằng cách bỏ key.

Job mặc định cron `15 1 * * *` (01:15 UTC+7), tính lại 7 ngày gần nhất kể cả hôm nay;
Hangfire retry tối đa 3 lần. Mỗi ngày thay thế hai bảng metric và watermark trong **một transaction**,
khóa advisory PostgreSQL ngăn hai worker ghi đè kết quả cũ. Có ngày thành công trước khi một ngày khác
lỗi: xem coverage; chạy lại cả khoảng an toàn. Dữ liệu được nhập/sửa lùi ngày ngoài lookback cần rebuild.
Không có cross-service FK, không coi job chạy là xác nhận dữ liệu đầy đủ nếu nguồn thiếu lịch sử.

### Cấu hình, migration và kiểm tra local

1. Analytics dùng `ConnectionStrings:AnalyticsDatabase`, hoặc fallback `NotificationsDatabase` của cùng
   service. Không dùng connection string của Workflow/IAM. Migration mới
   `20261021031538_AnalyticsMetricsAndCitizenFeedback` tạo schema analytics, ba bảng nghiệp vụ và
   `metric_refreshes`; lịch sử riêng `analytics.__AnalyticsMigrationsHistory`.
2. Apply EF với `--context AnalyticsDbContext`, Infrastructure project và API startup project;
   có thể dùng `Database:AutoMigrate=true` ở local. Schema Hangfire `analytics_jobs` được storage tạo
   khi bật job. Tài khoản DB cần quyền tạo schema/bảng lúc khởi tạo. Không tự apply Azure trong task này.
3. Cấu hình secret `ANALYTICS_SERVICE_KEY` giống nhau ở Workflow/Analytics, `ANALYTICS_JOBS_ENABLED=true`,
   `ANALYTICS_LOOKBACK_DAYS=7`, `ANALYTICS_CRON="15 1 * * *"`, JWT chung IAM và mật khẩu Notifications DB.
   `Workflow:BaseUrl` phải trỏ Workflow thật. Docker Compose hỗ trợ profile `analytics` dùng DB của
   AnalyticsSystem hiện có; không khởi chạy lại service khác ngoài nhu cầu tích hợp.
4. Process Hangfire phải chạy liên tục để thực hiện cron; môi trường scale-to-zero không bảo đảm lịch.
   Xem [tài liệu Hangfire](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html).
5. Local: build Release toàn solution, chạy `python scripts/verify-analytics-local.py` với Docker khả dụng.
   Script mới của TASK-31–33 dùng DB/container/cổng tạm, dữ liệu giả và IAM mô phỏng; không khôi phục
   các test project cũ. Workflow, Analytics, PostgreSQL, Hangfire và Gateway đều chạy thật.
   Kiểm tra thêm trên IAM thật trước bàn giao production; test mô phỏng không thay thế bước này.

FE kiểm tra theo thứ tự: IT_ADMIN chạy rebuild → đợi job Succeeded → Manager mở overview/trends/bảng
→ công dân gửi đánh giá hồ sơ của mình → Manager xem điểm và nhận xét. Thử thêm khác phường, sai chủ,
role bị thu hồi, rating 0/6, gửi trùng, IAM/Workflow mất kết nối và ngày chưa tổng hợp.

Tra cứu các điều kiện dữ liệu, quyền, trạng thái và giới hạn file tại
[Sổ tay ràng buộc hệ thống](system-constraints.md). Cập nhật hai tài liệu cùng nhau
khi thay đổi hành vi API hoặc quy tắc nghiệp vụ.
### Cập nhật 20/10/2026: Danh sách hồ sơ Cán bộ & Tra cứu QR công khai (TASK-OFFICER-LIST-QR-LOOKUP)

- **Danh sách hồ sơ cán bộ một cửa:** GET `/api/v1/officer/applications`
  - *Mục đích*: Cho phép cán bộ một cửa (`FRONT_DESK_OFFICER`) truy vấn toàn bộ hồ sơ trong phường được giao, bao gồm tất cả trạng thái (`DOCUMENTS_READY`, `READY_TO_SUBMIT`, `NEED_REVISION`, `APPROVED`, `REJECTED`, `CANCELLED`). Tự động loại trừ hồ sơ do chính cán bộ nộp (đảm bảo tính khách quan).
  - *Tham số query*: `status` (có thể truyền đơn lẻ hoặc danh sách phân cách bởi dấu phẩy như `DOCUMENTS_READY,NEED_REVISION`), `procedureId` (Guid), `applicationCode` (tìm kiếm theo mã đơn), `submittedFrom`/`submittedTo` (khoảng ngày nộp), `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100).
  - *Phản hồi thành công*: `200 OK` kèm `PagedResult<OfficerApplicationSummaryDto>`.
  - *Lỗi thường gặp*: 401 Unauthorized nếu thiếu token hoặc token hết hạn; 403 Forbidden nếu tài khoản không có quyền FRONT_DESK_OFFICER hoặc chưa được phân công phường công tác.

- **Tra cứu hồ sơ qua mã QR công khai:** GET `/api/v1/public/qr-lookup` và GET `/api/v1/public/qr-lookup/{**token}`
  - *Mục đích*: Cho phép người dân, cán bộ hoặc cơ quan kiểm tra quét mã QR in trên giấy tờ/tờ khai kết xuất để kiểm tra tính hợp lệ và xác thực của hồ sơ. Endpoint này hoàn toàn công khai (`AllowAnonymous`), không yêu cầu đăng nhập.
  - *Cách gọi*:
    + Qua query param: `GET /api/v1/public/qr-lookup?applicationId={id}` hoặc `GET /api/v1/public/qr-lookup?token={token}`
    + Qua path param: `GET /api/v1/public/qr-lookup/{token_hoặc_url}`
  - *Phản hồi thành công*: `200 OK` kèm `QrLookupResultDto` (`applicationId`, `submissionId`, `qrCodeValue`, `status`, `isValid`, `message`, `generatedAt`).
  - *Lỗi thường gặp*: 400 Validation nếu không truyền `applicationId` hoặc `token`; 404 NotFound nếu không tìm thấy mã QR đã được phê duyệt trong hệ thống.

### Cập nhật kiểm tra liên service — 15/10/2026 (FLOW-REVIEW-001)

- **Tạo/sửa/xuất bản thủ tục:** POST `/api/v1/procedure-manager/procedures`,
  PUT `/api/v1/procedure-manager/procedures/{id}`, POST
  `/api/v1/procedure-manager/procedures/publish` và POST
  `/api/v1/procedure-manager/drafts/{id}/publish` dùng kiểm tra chung.
  Checklist tối đa 500 mục; mã duy nhất/tối đa 255/không dư khoảng trắng.
  CaseCode duy nhất/tối đa 100; checklist/form chỉ tham chiếu case có thật.
  FormCode/FormName tối đa 255, template không nhận GUID toàn số 0.
  Cùng FormCode được dùng ở hai case riêng, nhưng không lặp trong cùng case hoặc
  trùng form dùng chung. Sai dữ liệu trả 400 `validation.failed`; sửa payload rồi gửi lại.
  Bản nháp vẫn có thể lưu dữ liệu chưa hoàn chỉnh để tiếp tục đối soát; publish mới
  yêu cầu vượt qua các kiểm tra này. Success DTO/route/quyền không thay đổi.
- **Sửa checklist riêng:** PUT `/api/v1/procedure-manager/procedures/{id}/checklist`
  giữ body `{expectedUpdatedAt, decisionNumber, effectiveDate, items}`.
  Case hợp lệ vẫn được chấp nhận; timestamp cũ vẫn trả 409, phải tải lại trước khi lưu.
- **Nộp lại hồ sơ:** POST `/api/v1/applications/{id}/resubmit`, body gồm
  `expectedVersionNumber`, `formData`, `checklists`. Checklist không được chứa null
  hoặc ID trùng. BE kiểm tra payload trước khi gọi DocumentForm; sai trả 400 validation,
  thành công vẫn 200. Thiếu giấy tờ thật vẫn trả 422, không sửa thành COMPLETED để né lỗi.
- **Xác minh giấy tờ:** các API đánh dấu hoàn tất/nộp/nộp lại trả 503
  `application.documents_unavailable` nếu service tài liệu trả mảng/phần tử null hoặc ID
  rỗng/trùng. FE cho phép thử lại, báo traceId cho BE; không coi đây là thiếu file
  để bắt người dân upload lại toàn bộ.
- **Đọc giấy tờ/PDF:** GET `/api/v1/applications/{applicationId}/supporting-documents`,
  GET `/api/v1/supporting-documents/{documentId}/download`, GET
  `/api/v1/applications/{applicationId}/generated-documents`, GET
  `/api/v1/generated-documents/{documentId}/download`, cùng POST
  `/api/v1/applications/{applicationId}/generate-pdf` nay kiểm tra chính chủ trước.
  Tài khoản có thêm role FRONT_DESK_OFFICER vẫn đọc/xuất tài liệu hồ sơ mình nộp.
  Nếu không phải chủ hồ sơ, phải là cán bộ được giao đúng phường; ngoài phạm vi trả 404.
  PDF yêu cầu hồ sơ DOCUMENTS_READY (chưa sẵn sàng trả 409); không cấp quyền tự thẩm định.

Các thay đổi này không cần migration. Build lại ProcedureCatalog, ApplicationWorkflow
và DocumentForm để kiểm tra; không cần đổi JWT/role hoặc sửa dữ liệu database để né lỗi.

#### Kiểm tra Docker trước khi bàn giao Azure

1. Đăng nhập bằng các tài khoản thử: citizen, Front Desk phường A, Front Desk phường B,
   Procedure Manager. Dùng dữ liệu giả và giữ ID tạo ra để dọn dữ liệu thử theo nghiệp vụ.
2. Bóc tách PDF, lưu/đối soát draft, chọn category thật và kiểm tra checklist/cases/template.
   Thử case không tồn tại/mã checklist trùng: publish phải 400. Sửa hợp lệ rồi publish.
3. Citizen tạo hồ sơ chọn phường A và case. Xác nhận chỉ có checklist chung + case đã chọn.
   Sửa checklist thủ tục sau đó: checklist hồ sơ cũ phải giữ nguyên.
4. Thử hoàn tất/nộp khi chưa có file, sai template, sai số lượng hoặc loại bản sao:
   phải bị chặn. Điền/nộp form đúng template, upload giấy tờ thật rồi nộp hồ sơ.
5. FD phường B không được nhận/xem hồ sơ A. FD A tiếp nhận; cán bộ khác chưa được giao
   không được annotate/confirm-readiness. Tạo comment OPEN rồi yêu cầu bổ sung.
6. Citizen sửa form/giấy tờ và nộp lại đúng version. Version cũ phải 409;
   bộ checklist chứa null phải 400. Kiểm tra history, snapshot và diff.
7. Cán bộ nhận lại và xác nhận sẵn sàng khi không còn OPEN. Thử sửa checklist/upload/xóa/annotate
   sau xác nhận: phải bị chặn. Không còn API từ chối hồ sơ.
8. Xuất PDF từ submission đã lưu: kiểm tra nội dung thật và không lộ Word comment.
   Khi Gotenberg không dùng được phải trả lỗi, không trả PDF giả. Kiểm tra tài khoản
   citizen đồng thời có role Front Desk vẫn đọc được tài liệu hồ sơ mình nộp.
9. Kết nối SignalR, thực hiện thay đổi trạng thái và kiểm tra thông báo đúng người/phường.
   Ngắt/kết nối lại rồi tải REST danh sách để kiểm tra không mất dữ liệu. Kiểm tra
   inbox/outbox/delivery khi RabbitMQ bị gián đoạn trên môi trường thử riêng.

Kết quả kiểm tra tạm bằng handler/HTTP giả lập không thay thế các bước Docker trên.
Chưa xác nhận khóa nguyên tử request sửa file đang chạy đồng thời với quyết định duyệt
giữa hai service; không đánh dấu kiểm thử cạnh tranh phân tán là đã đạt.

Cập nhật ngày **07/10/2026**. Tài liệu chia theo **Service → Nhóm chức năng → Từng API**, mô tả các
API nghiệp vụ hiện có, các endpoint hệ thống và đường dẫn Gateway. Không phải xác nhận tình trạng deploy.

**Cách đọc:** Mỗi API có mục đích, quyền, cách dùng, kết quả và lỗi thường gặp trên các dòng riêng. Public
nghĩa là không cần đăng nhập. Bearer là access token lấy sau đăng nhập. Thay `{id}` bằng ID thật từ API danh
sách, không thay bằng tên người hay tên thủ tục.

**Lỗi chung:** API có bảo vệ trả 401 khi token không hợp lệ (refresh hoặc đăng nhập lại), 403 khi thiếu quyền
(kiểm tra/cấp đúng quyền). 200/201 là thành công, 202 là đã nhận để xử lý, 204 là thành công không có JSON
body. Các mục lỗi tập trung tình huống riêng của từng API, không liệt kê mọi lỗi hạ tầng có thể xảy ra.

**Tra cứu:** 1. IAM · 2. Procedure Catalog · 3. Document & Form · 4. AIOCR · 5. Gateway và hệ thống. Phần
**Phụ lục kỹ thuật** cuối file giữ các cổng, DTO, ví dụ JSON/fetch và cấu hình cần thiết. Các dẫn chiếu “mục
3.1/4.3/6.1” trong hướng dẫn là mục thuộc phụ lục.

## 1. IAM Service (Identity & Access Management)

**Chịu trách nhiệm:** Đăng ký, đăng nhập, quản lý phiên, hồ sơ cá nhân, khóa/mở tài khoản và phân quyền RBAC.

### Nhóm 1.1 — Xác thực & Quản lý phiên (`/api/v1/auth`)

Yêu cầu chung: cả bốn POST đều cần `X-CSRF-Protection: 1`. FE dùng `credentials: "include"` cho cookie đăng
nhập/refresh/đăng xuất. Role mặc định khi đăng ký là **REGISTERED_CITIZEN**. Thời hạn token lấy từ
response/cấu hình, không hardcode số phút/ngày vào FE.

#### API 01 — Đăng ký tài khoản công dân

`POST /api/v1/auth/register`

**Quyền / Header:** Public + CSRF

**Mục đích:** Tạo tài khoản mới cho người chưa có tài khoản, đồng thời tạo hồ sơ tên người dùng và gán vai trò
công dân. Không dùng API này để tự tạo tài khoản quản trị.

**Cách dùng:** Không cần đăng nhập. Gửi `username`, `email`, `password`, `fullName` và header
`X-CSRF-Protection: 1`. Ví dụ người dùng điền bốn ô trên màn hình Đăng ký rồi bấm gửi.

**Kết quả:** Nhận 201 và thông tin tài khoản vừa tạo. Sau đó gọi API 02 để đăng nhập; đăng ký chưa cấp phiên
đăng nhập.

**Trường hợp sai và cách xử lý:**

- Thiếu tên, email sai hoặc mật khẩu không đủ 8 ký tự/chữ hoa/ký tự đặc biệt → 400, sửa trường được báo.
- Trùng username/email → 409, dùng thông tin khác hoặc đăng nhập tài khoản đã có.
- Thiếu header CSRF hoặc Origin không được phép → 403, sửa cách gọi/cấu hình, không phải đổi mật khẩu.

#### API 02 — Đăng nhập

`POST /api/v1/auth/login`

**Quyền / Header:** Public + CSRF

**Mục đích:** Mở phiên làm việc để sử dụng các chức năng cần quyền.

**Cách dùng:** Gửi `usernameOrEmail` và `password`, kèm `X-CSRF-Protection: 1`. FE phải bật
`credentials: 'include'` để trình duyệt nhận cookie. Không cần Bearer trước khi đăng nhập.

**Kết quả:** Nhận 200, accessToken và thời gian hết hạn. Trình duyệt giữ refresh token trong cookie HttpOnly;
FE dùng accessToken cho các API cần đăng nhập.

**Trường hợp sai và cách xử lý:**

- Sai thông tin hoặc tài khoản không còn khả dụng → 401; kiểm tra thông tin/tình trạng tài khoản.
- 403 CSRF → kiểm tra header/Origin.
- Không thấy refreshToken trong JSON là bình thường; không cố đọc cookie HttpOnly bằng JavaScript.

#### API 03 — Gia hạn phiên đăng nhập

`POST /api/v1/auth/refresh-token`

**Quyền / Header:** Public + Cookie + CSRF

**Mục đích:** Lấy access token mới khi access token cũ hết hạn mà người dùng chưa cần nhập lại mật khẩu.

**Cách dùng:** Gọi từ cùng host đã đăng nhập, gửi cookie bằng `credentials: 'include'` và header CSRF. **Không
gửi body AccessToken/RefreshToken**. FE chỉ cho một yêu cầu refresh chạy tại một thời điểm.

**Kết quả:** Nhận 200 với access token mới; cookie refresh cũ được thay bằng cookie mới. Dùng token mới để thử
lại yêu cầu trước đó một lần.

**Trường hợp sai và cách xử lý:**

- Không có cookie, cookie hết hạn, đã dùng hoặc bị thu hồi → 401, yêu cầu đăng nhập lại.
- Đăng nhập localhost rồi refresh bằng 127.0.0.1 có thể mất cookie; dùng nhất quán một hostname.
- Gửi nhiều lần đồng thời có thể làm một lần thất bại vì token chỉ được dùng một lần.

#### API 04 — Đăng xuất phiên hiện tại

`POST /api/v1/auth/revoke-token`

**Quyền / Header:** Bearer + Cookie + CSRF

**Mục đích:** Hủy khả năng gia hạn của phiên đang sử dụng khi người dùng bấm Đăng xuất.

**Cách dùng:** Gửi Bearer, header CSRF và cookie; không body. Sau khi xử lý, FE xóa access token đang giữ và
đưa người dùng về màn hình đăng nhập.

**Kết quả:** Nhận 204, cookie được xóa. Không có cookie refresh vẫn có thể nhận 204; không cần xem đây là lỗi.

**Trường hợp sai và cách xử lý:**

- Access token không hợp lệ → 401; thiếu header/Origin sai → 403.
- Chỉ xóa token trên FE mà không gọi API sẽ không thu hồi refresh trên server.
- API này không phải nút đăng xuất mọi thiết bị và không đảm bảo access token đã phát hành lập tức mất hiệu
  lực tại mọi service.


### Nhóm 1.2 — Người dùng & Hồ sơ cá nhân (`/api/v1/users`)

Hồ sơ gồm họ tên, số định danh, điện thoại, ngày sinh, giới tính, địa chỉ thường trú và tạm trú. Đăng ký đã
tạo hồ sơ; sửa bằng PUT, không POST lại. Xóa hồ sơ không xóa tài khoản.

#### API 05 — Xem tôi là ai và có quyền gì

`GET /api/v1/users/me`

**Quyền / Header:** Bearer + `iam.profile.read`

**Mục đích:** Hiển thị người đang đăng nhập, hồ sơ, vai trò và quyền; FE dùng để dựng menu phù hợp.

**Cách dùng:** Gửi Bearer của chính người dùng có `iam.profile.read`, không cần userId hay body.

**Kết quả:** Nhận 200 với tài khoản, profile nếu có, danh sách roles/permissions.

**Trường hợp sai và cách xử lý:**

- Dùng token người khác sẽ nhận thông tin của người đó, không phải người đang hiện trên giao diện.
- Token sai → 401; thiếu quyền → 403.
- Profile không có sau khi xóa hồ sơ không đồng nghĩa tài khoản đã bị xóa.

#### API 06 — Xem hồ sơ của mình

`GET /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.read`

**Mục đích:** Điền dữ liệu hiện tại vào màn hình Hồ sơ cá nhân.

**Cách dùng:** Gửi Bearer có `iam.profile.read`, không body; hệ thống tự nhận biết người dùng từ token.

**Kết quả:** Nhận 200 với dữ liệu hồ sơ, không phải toàn bộ thông tin vai trò như API 05.

**Trường hợp sai và cách xử lý:**

- 404 nghĩa là chưa có/đã xóa hồ sơ — chuyển sang tạo hồ sơ bằng API 07.
- Không gọi lại GET mãi để chờ dữ liệu tự xuất hiện.

#### API 07 — Tạo lại hồ sơ của mình khi chưa có

`POST /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Tạo hồ sơ cho tài khoản hiện chưa có hồ sơ.

**Cách dùng:** Gửi Bearer có `iam.profile.write` và các trường hồ sơ; fullName bắt buộc.

**Kết quả:** Nhận 201 và hồ sơ mới.

**Trường hợp sai và cách xử lý:**

- Sau đăng ký đã có hồ sơ, gọi POST sẽ nhận 409; muốn sửa thì dùng API 08.
- Dữ liệu sai như ngày sinh tương lai → 400.
- Số định danh trùng người khác → xung đột; kiểm tra lại, không đổi ngẫu nhiên để vượt kiểm tra.

#### API 08 — Sửa hồ sơ của mình

`PUT /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Lưu thông tin cá nhân sau khi chỉnh sửa.

**Cách dùng:** Có `iam.profile.write`; lấy hồ sơ bằng API 06, chỉnh rồi gửi toàn bộ trường muốn giữ lại.

**Kết quả:** Nhận 200 với hồ sơ đã cập nhật.

**Trường hợp sai và cách xử lý:**

- Chỉ gửi tên và bỏ địa chỉ có thể làm địa chỉ cũ bị xóa về null: đây là thay cả hồ sơ, không phải chỉ sửa các
  trường gửi lên.
- 404 → tạo hồ sơ trước; 400 → sửa trường sai; 409 → kiểm tra trùng số định danh hoặc tải lại nếu hồ sơ đã
  thay đổi.

#### API 09 — Xóa hồ sơ của mình

`DELETE /api/v1/users/me/profile`

**Quyền / Header:** Bearer + `iam.profile.write`

**Mục đích:** Gỡ thông tin hồ sơ cá nhân, không xóa tài khoản đăng nhập.

**Cách dùng:** Có `iam.profile.write`, gọi không body sau khi người dùng chọn xóa.

**Kết quả:** Nhận 204; lần xem hồ sơ sau có thể trả 404. Có thể tạo lại bằng API 07.

**Trường hợp sai và cách xử lý:**

- Dùng API này với ý định khóa/xóa tài khoản sẽ không đạt mục đích.
- Nếu hồ sơ không còn tồn tại, xử lý phản hồi không tìm thấy thay vì thông báo hệ thống hỏng.


### Nhóm 1.3 — Quản trị tài khoản & Hồ sơ người dùng

Dùng cho IT_ADMIN có iam.manage hoặc MANAGER đã được gán phường. Tài khoản quản lý việc đăng nhập; hồ sơ quản lý thông tin cá nhân. Chọn
đúng userId trước khi thao tác.

#### API 14 — Danh sách tài khoản

`GET /api/v1/accounts`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Quản trị tìm tài khoản để xem trạng thái, sửa hồ sơ hoặc cấp vai trò.

**Cách dùng:** Trong phạm vi quản lý; gọi `?page=1&pageSize=20`. Dùng id trong từng dòng cho các API tài khoản/hồ
sơ/RBAC.

**Kết quả:** Nhận 200 gồm items và tổng số tài khoản `total`.

**Trường hợp sai và cách xử lý:**

- pageSize vượt 100 hoặc trang không hợp lệ → 400.
- Trang rỗng không có nghĩa toàn hệ thống không có tài khoản; kiểm tra page và total.
- API này chưa có tham số tìm theo tên được công bố.

#### API 15 — Xem một tài khoản

`GET /api/v1/accounts/{userId}`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Xem username, email, trạng thái hoạt động và ngày tạo trước khi quản trị thao tác.

**Cách dùng:** Trong phạm vi quản lý, điền id lấy từ API 14; không body.

**Kết quả:** Nhận 200 với thông tin tài khoản.

**Trường hợp sai và cách xử lý:**

- userId không tồn tại → 404, tải lại danh sách.
- Không tìm họ tên/địa chỉ trong response này; dùng API 10 để xem hồ sơ.

#### API 16 — Khóa hoặc mở lại tài khoản

`PUT /api/v1/accounts/{userId}/status`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Ngừng cho một tài khoản sử dụng hệ thống hoặc cho phép hoạt động trở lại.

**Cách dùng:** Trong phạm vi quản lý; gửi `{ "isActive": false }` để khóa, true để mở. Không có trường thời hạn
khóa tự động.

**Kết quả:** Nhận 204. Khi khóa, refresh token bị thu hồi; khi mở lại, người dùng đăng nhập lại để có phiên
mới.

**Trường hợp sai và cách xử lý:**

- Bỏ isActive → 400; tài khoản không có → 404; tự khóa mình hoặc khóa IT_ADMIN hoạt động cuối cùng → 409.
- Không dùng thao tác này thay cho xóa hồ sơ; và không khẳng định JWT đã cấp mất hiệu lực ngay ở mọi service
  chỉ kiểm tra JWT.

#### API 60 — Danh sách hồ sơ người dùng

**Method & Đường dẫn:** `GET /api/v1/users/profiles?page=1&pageSize=20`

**Mục đích:** Hiển thị danh sách thông tin cá nhân để quản lý mở/xem/sửa hồ sơ theo userId.
Khác danh sách tài khoản, API này chỉ trả hồ sơ đang tồn tại; người chưa có/đã xóa profile không xuất hiện.

**Ai sử dụng:** IT_ADMIN có quyền iam.manage xem toàn hệ thống. Manager đã được gán phường chỉ xem
Front Desk cùng phường theo phạm vi quản lý hiện hành; không xem công dân hoặc cán bộ phường khác.
Front Desk có role đặc quyền khác ngoài FRONT_DESK_OFFICER/REGISTERED_CITIZEN không thuộc phạm vi Manager.

**Cách gọi:** Gửi `Authorization: Bearer <accessToken>`, không body. page mặc định 1 (tối đa 1.000.000),
pageSize mặc định 20 (tối đa 100). Muốn lấy hết thì gọi lần lượt các trang đến khi đủ total.
Không truyền wardId/userId để tự mở rộng quyền. Gateway dùng cùng route tại cổng 5000; IAM local cổng 5001.

**Kết quả:** 200 với `{items, page, pageSize, total}`, sắp xếp fullName rồi userId để phân trang ổn định.
Mỗi item gồm userId, fullName, identityNumber, phoneNumber, dateOfBirth, gender, permanentAddress,
temporaryAddress, updatedAt. Trường null bị bỏ theo cấu hình JSON của IAM. updatedAt là UTC.
Lấy userId để gọi `GET /api/v1/users/{userId}/profile`; không nhầm profile với tài khoản đăng nhập.
Hồ sơ tài khoản bị khóa vẫn xuất hiện nếu thuộc phạm vi; total là số profile được phép xem, không phải số user.

**Lỗi và cách xử lý:**

- 400 khi page/pageSize sai hoặc không phải số → sửa query theo errors tiếng Việt.
- 401 khi token sai/hết hạn → refresh qua IAM hoặc đăng nhập lại.
- 403 khi không có quyền, Manager chưa có phường hoặc tài khoản quản lý bị khóa → nhờ Admin kiểm tra.
- items rỗng vẫn trả 200; kiểm tra trang và hồ sơ trong phạm vi, không xem đây là lỗi hệ thống.

#### API 10 — Quản trị xem hồ sơ một người

`GET /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Người quản lý xem hồ sơ tài khoản được chọn trong màn hình quản trị.

**Cách dùng:** Lấy userId từ danh sách tài khoản API 14, điền vào URL và gửi Bearer quản trị.

**Kết quả:** Nhận 200 với hồ sơ của đúng userId đã chọn.

**Trường hợp sai và cách xử lý:**

- Nhập username thay UUID không gọi được route hợp lệ.
- 404 → kiểm tra tài khoản hoặc hồ sơ có tồn tại; không tự coi đó là lỗi đăng nhập.

#### API 11 — Quản trị tạo hồ sơ cho một người

`POST /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Bổ sung hồ sơ còn thiếu cho một tài khoản đã tồn tại; không tạo tài khoản mới.

**Cách dùng:** Trong phạm vi quản lý, chọn đúng userId và gửi dữ liệu hồ sơ có fullName.

**Kết quả:** Nhận 201 với hồ sơ được tạo cho người đó.

**Trường hợp sai và cách xử lý:**

- userId không có → 404; đã có hồ sơ → 409, dùng API 12; dữ liệu sai → 400.
- Kiểm tra người được chọn để tránh tạo thông tin cho nhầm tài khoản.

#### API 12 — Quản trị sửa hồ sơ một người

`PUT /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Sửa thông tin hồ sơ cho tài khoản được quản trị chọn.

**Cách dùng:** Trong phạm vi quản lý; đọc bằng API 10 trước rồi gửi đầy đủ hồ sơ đã chỉnh.

**Kết quả:** Nhận 200 với dữ liệu mới.

**Trường hợp sai và cách xử lý:**

- Bỏ trường tùy chọn sẽ xóa giá trị cũ; không dùng PUT như thao tác sửa riêng một ô.
- 404 → kiểm tra user/hồ sơ; 400/409 → xem lỗi trường hoặc trùng số định danh và sửa lại.

#### API 13 — Quản trị xóa hồ sơ một người

`DELETE /api/v1/users/{userId}/profile`

**Quyền / Header:** Bearer: Admin toàn hệ thống / Manager trong phường

**Mục đích:** Xóa hồ sơ được chọn trong quản trị, giữ lại tài khoản.

**Cách dùng:** Trong phạm vi quản lý, kiểm tra userId rồi gọi không body.

**Kết quả:** Nhận 204; tài khoản không bị khóa bởi thao tác này.

**Trường hợp sai và cách xử lý:**

- Chọn nhầm userId sẽ tác động nhầm hồ sơ; cần xác nhận đúng người trên giao diện.
- Muốn ngăn người đó đăng nhập thì dùng API 16, không dùng xóa hồ sơ.



**Phạm vi bắt buộc:** Manager chỉ thấy/quản lý Front Desk cùng phường công tác, không phải
địa chỉ thường trú. Backend lấy phường từ database. Không có phường thì 403; user ngoài phạm
vi trả 404 ở API tài khoản/hồ sơ. Front Desk kiêm role quản trị/role tùy chỉnh được bảo vệ,
Manager không quản lý tài khoản đó. Admin quản lý toàn bộ, kể cả tài khoản chưa gán phường.

### Nhóm 1.3b — Danh sách người dùng, phường và tạo Front Desk

Admin là IT_ADMIN có iam.manage. Manager cần role MANAGER đang hoạt động và phường công tác
được Admin gán. Một phường có nhiều Front Desk. Không cấp iam.manage toàn cục cho Manager.

#### API 54 — Danh sách người dùng và thông tin hồ sơ

`GET /api/v1/users`

**Quyền:** Admin xem tất cả; Manager chỉ xem Front Desk cùng phường.

**Mục đích:** Dựng màn hình quản lý nhân sự có tài khoản, hồ sơ, phường và vai trò.
API accounts là bản gọn về tài khoản; API users này có thêm thông tin người dùng.

**Cách dùng:** Gọi `?page=1&pageSize=20`, pageSize từ 1 đến 100. Backend tự giới hạn phạm vi;
không nhận wardId để Manager tùy ý chọn xem phường khác. Gọi nhiều trang để lấy toàn bộ
người dùng trong phạm vi, không tải không giới hạn một lần.

**Kết quả:** 200 `{items,page,pageSize,total}`. Mỗi item gồm id, username, email, isActive,
wardId?, wardName?, profile? (cấu trúc hồ sơ ở mục 3.1), roles là mảng `{id,roleName}`.
Không trả passwordHash hoặc refresh token. total cũng chỉ tính trong phạm vi được phép.

**Trường hợp sai:** Phân trang sai → 400; thiếu quyền/chưa gán phường → 403.
Danh sách rỗng có thể do phường chưa có Front Desk. roles của API này là object có ID;
roles trong GET users/me vẫn là mảng tên. Dùng role ID từ dữ liệu, không đoán ID theo thứ tự.

#### API 55 — Tạo tài khoản Front Desk

`POST /api/v1/accounts/front-desk`

**Quyền:** Admin hoặc Manager trong phường.

**Mục đích:** Tạo tài khoản đăng nhập, hồ sơ và role FRONT_DESK_OFFICER trong một giao dịch.
Manager không phải đăng ký tài khoản công dân rồi nhờ Admin nâng quyền cho từng cán bộ.

**Cách dùng:** Gửi username, email, password, fullName, wardId?. Manager bỏ wardId để server
tự lấy phường của mình; Admin phải chọn wardId hợp lệ. Quy tắc mật khẩu giống đăng ký:
ít nhất 8 ký tự, có chữ hoa và ký tự đặc biệt, tối đa 72 byte UTF-8.

**Kết quả:** 201 ManagedUserDto giống một item của API 54, Location tới accounts/{id}.
Tài khoản mới active, có hồ sơ và role Front Desk; không tự đăng nhập hoặc trả token.

**Trường hợp sai:** Dữ liệu sai → 400; username/email trùng → 409; Manager truyền phường
khác → 403; Admin không chọn phường hoặc phường không tồn tại → 404. Không có trường cho
Manager tùy ý chọn role quản trị trong body. Không ghi mật khẩu thực vào tài liệu/mã FE.

#### API 56 — Xem danh sách phường công tác

`GET /api/v1/accounts/wards`

**Quyền:** Admin hoặc Manager đã có phường.

**Mục đích:** Lấy ID phường cho màn hình tạo cán bộ/gán phường.

**Cách dùng:** Gửi Bearer, không body. Admin nhận toàn bộ phường; Manager chỉ nhận phường mình.

**Kết quả:** 200 mảng `{id,code,name}`. Đây là phường công tác, không suy ra từ địa chỉ hồ sơ.

**Trường hợp sai:** 403 nếu Manager chưa có phường. Admin nhận mảng rỗng thì tạo phường trước.

#### API 57 — Admin tạo phường

`POST /api/v1/accounts/wards`

**Quyền:** Chỉ Admin.

**Mục đích:** Khởi tạo đơn vị quản lý để gán Manager và Front Desk.

**Cách dùng:** Gửi ví dụ `{ "code": "DEMO_A", "name": "Phường minh họa A" }`.
Code tối đa 50, chỉ chữ ASCII/số/gạch ngang/gạch dưới, chuẩn hóa chữ hoa; name tối đa 255.

**Kết quả:** 201 `{id,code,name}`. Giữ ID để gán tài khoản ở API 58.

**Trường hợp sai:** Thiếu/sai trường → 400; mã trùng → 409; Manager gọi → 403.
Tạo phường không tự tạo tài khoản Manager.

#### API 58 — Admin gán hoặc chuyển phường công tác

`PUT /api/v1/accounts/{userId}/ward`

**Quyền:** Chỉ Admin. Manager không tự chuyển phường hoặc đưa người ngoài vào phường mình.

**Mục đích:** Xác lập phạm vi quản lý bằng quan hệ dữ liệu cụ thể.

**Cách dùng:** Gửi `{ "wardId": "<UUID từ API 56>" }`. Gửi wardId null để gỡ phường.
Body rỗng `{}` cũng có wardId null, vì vậy FE phải gửi rõ lựa chọn trước khi lưu.

**Kết quả:** 204, cập nhật phường và thu hồi refresh token của tài khoản được chuyển.
Phạm vi IAM được kiểm tra lại từ DB ngay cả với access token cũ; user/me trả wardId mới.

**Trường hợp sai:** User/phường không có → 404; Manager gọi → 403. Gỡ phường khỏi Manager
khiến Manager mất quyền quản lý cán bộ cho đến khi được gán lại. Không tự chuyển toàn bộ
Front Desk theo khi chuyển Manager; Admin phải xử lý từng tài khoản cần chuyển.

**Luồng sử dụng:** Admin tạo phường → gán role MANAGER bằng RBAC và gán wardId cho Manager
→ Manager tạo Front Desk (tự thuộc phường mình) → GET users/accounts để quản lý.
Manager có thể PUT/DELETE role FRONT_DESK_OFFICER của người cùng phường theo API 27/28;
không được cấp role khác. Catalog chỉ dùng lọc, không gắn cứng vào tài khoản.

### Nhóm 1.4 — Phân quyền vai trò RBAC (`/api/v1/rbac`)

RBAC Admin: tài khoản đang hoạt động có **IT_ADMIN và iam.manage**. Vai trò là nhóm người dùng; quyền là hành
động được phép. Tạo role mới không tự bổ sung chức năng backend hay vượt các policy cố định.

#### API 17 — Danh sách vai trò

`GET /api/v1/rbac/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Hiển thị các vai trò để quản trị chọn khi cấp quyền.

**Cách dùng:** Gọi `?page=1&pageSize=20`, gửi Bearer quản trị.

**Kết quả:** Nhận 200 với danh sách role và tổng số. Giữ id, không dùng thứ tự dòng làm roleId.

**Trường hợp sai và cách xử lý:**

- Phân trang sai → 400; trang rỗng → kiểm tra trang/tổng.
- Không tự mặc định IT_ADMIN luôn là một số ID cố định trên mọi môi trường.

#### API 18 — Xem chi tiết vai trò

`GET /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Xem tên, mô tả, có phải vai trò hệ thống không và các quyền đang được gán.

**Cách dùng:** Điền roleId từ API 17, không body.

**Kết quả:** Nhận 200 với thông tin vai trò.

**Trường hợp sai và cách xử lý:**

- Không tồn tại → 404; truyền tên role thay số không đúng route. isSystem=true là dấu hiệu vai trò được bảo
  vệ, không phải được phép xóa.

#### API 19 — Tạo vai trò tùy chỉnh

`POST /api/v1/rbac/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Tạo nhóm quyền mới khi hệ thống cần thêm nhóm người dùng.

**Cách dùng:** Gửi ví dụ `{ "roleName": "CUSTOM_OFFICER", "description": "Nhóm cán bộ bổ sung" }`, sau đó
dùng API 24 để gán quyền và API 27 để gán người.

**Kết quả:** Nhận 201 với vai trò mới. Chỉ tạo role không tự cấp nó cho ai và không tự thêm chức năng backend.

**Trường hợp sai và cách xử lý:**

- Tên có dấu/khoảng trắng, bắt đầu bằng số hoặc quá dài → 400; trùng tên → 409.
- Role tùy chỉnh không tự vượt policy cố định PROCEDURE_MANAGER/IT_ADMIN của Procedure.

#### API 20 — Sửa vai trò

`PUT /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Cập nhật tên/mô tả vai trò, không phải sửa danh sách quyền.

**Cách dùng:** Gửi roleName và description mới cho roleId đã chọn.

**Kết quả:** Nhận 200 với role đã sửa.

**Trường hợp sai và cách xử lý:**

- Role không có → 404; tên sai → 400; trùng tên hoặc đổi tên role hệ thống → 409.
- Muốn đổi quyền thì gọi API 24/25.
- Mô tả role hệ thống có thể chỉnh khi giữ nguyên tên được bảo vệ.

#### API 21 — Xóa vai trò tùy chỉnh

`DELETE /api/v1/rbac/roles/{roleId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Bỏ vai trò tùy chỉnh không còn dùng.

**Cách dùng:** Kiểm tra role không phải hệ thống và không còn được gán cho người dùng, rồi gọi không body.

**Kết quả:** Nhận 204; cập nhật lại danh sách role trên giao diện.

**Trường hợp sai và cách xử lý:**

- Role hệ thống hoặc còn người dùng được gán → 409; gỡ vai trò khỏi người dùng trước nếu thật sự cần xóa.
- Role không tồn tại → 404.
- Không xóa tài khoản người dùng để xử lý lỗi này.

#### API 22 — Xem danh sách quyền có sẵn

`GET /api/v1/rbac/permissions`

**Quyền / Header:** RBAC Admin

**Mục đích:** Lấy các quyền mà backend hiện biết để dựng danh sách chọn quyền.

**Cách dùng:** Gửi Bearer quản trị, không body, không cần roleId.

**Kết quả:** Nhận 200 với mã, tên, module và id của từng quyền.

**Trường hợp sai và cách xử lý:**

- Không thấy quyền mong muốn không có nghĩa gọi lỗi; quyền đó có thể chưa được triển khai/seed.
- Không tự gửi một permissionId đoán vào API gán quyền; lấy ID từ danh sách này.

#### API 23 — Xem quyền của một vai trò

`GET /api/v1/rbac/roles/{roleId}/permissions`

**Quyền / Header:** RBAC Admin

**Mục đích:** Đánh dấu các quyền role đang có trong màn hình phân quyền.

**Cách dùng:** Chọn role từ API 17, gọi với roleId, không body.

**Kết quả:** Nhận 200 với mảng quyền; mảng rỗng nghĩa role chưa có quyền.

**Trường hợp sai và cách xử lý:**

- Role không còn tồn tại → 404, tải lại danh sách.
- Không coi mảng rỗng là lý do cấp tất cả quyền tự động.

#### API 24 — Gán một quyền cho vai trò

`PUT /api/v1/rbac/roles/{roleId}/permissions/{permissionId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Cho người thuộc vai trò được hưởng thêm một quyền của IAM.

**Cách dùng:** Lấy roleId từ API 17, permissionId từ API 22; điền cả hai trong URL, không body.

**Kết quả:** Nhận 204; tải lại API 23 để thấy quyền vừa gán. Gán lại liên kết đã có không tạo quyền trùng.

**Trường hợp sai và cách xử lý:**

- Role/quyền không có → 404.
- Gán đúng quyền cho nhầm role vẫn có thể thành công và tác động nhiều người; kiểm tra tên role và quyền trước
  khi gửi.

#### API 25 — Gỡ một quyền khỏi vai trò

`DELETE /api/v1/rbac/roles/{roleId}/permissions/{permissionId}`

**Quyền / Header:** RBAC Admin

**Mục đích:** Ngừng cấp một quyền thông qua vai trò này; không xóa định nghĩa quyền.

**Cách dùng:** Chọn liên kết từ API 23, gọi không body.

**Kết quả:** Nhận 204. Người dùng còn role khác cấp cùng quyền thì vẫn có thể giữ quyền đó.

**Trường hợp sai và cách xử lý:**

- Không có role/quyền → 404; gỡ quyền quản trị hệ thống của IT_ADMIN → 409 vì được bảo vệ.
- Đừng kết luận gỡ thất bại chỉ vì một người vẫn còn quyền qua vai trò khác.

#### API 26 — Xem các vai trò của một tài khoản

`GET /api/v1/rbac/users/{userId}/roles`

**Quyền / Header:** RBAC Admin

**Mục đích:** Xem một người đang là công dân, cán bộ hay quản trị trước khi điều chỉnh.

**Cách dùng:** Lấy userId từ API 14 và gọi không body.

**Kết quả:** Nhận 200 với mảng role; một người có thể có nhiều role.

**Trường hợp sai và cách xử lý:**

- userId không tồn tại → 404.
- Không dùng email thay userId; không suy ra vai trò chỉ từ tên tài khoản.

#### API 27 — Gán vai trò cho tài khoản

`PUT /api/v1/rbac/users/{userId}/roles/{roleId}`

**Quyền / Header:** RBAC Admin hoặc Manager cấp/gỡ riêng FRONT_DESK_OFFICER cùng phường

**Mục đích:** Ví dụ cấp PROCEDURE_MANAGER cho người phụ trách thủ tục.

**Cách dùng:** Lấy hai ID từ API 14 và 17, gọi không body; sau đó người được cấp lấy token mới.

**Kết quả:** Nhận 204; API 26 có role vừa thêm. Không tự gỡ các role cũ.

**Trường hợp sai và cách xử lý:**

- Người/role không tồn tại → 404.
- Đã cấp mà Procedure vẫn báo 403 có thể vì JWT cũ chưa chứa role mới; refresh hoặc đăng nhập lại.
- Đừng cấp IT_ADMIN chỉ để né lỗi thiếu role quản lý thủ tục.


Manager không được gán IT_ADMIN, MANAGER hoặc role khác. Chỉ cấp/gỡ FRONT_DESK_OFFICER
cho tài khoản cùng phường chưa có role đặc quyền; vượt phạm vi trả 403. Sau khi gỡ role,
người đó không còn xuất hiện trong danh sách Front Desk của Manager. Admin có thể đổi
phường; Manager không tự chuyển người sang phường mình.

#### API 28 — Gỡ vai trò khỏi tài khoản

`DELETE /api/v1/rbac/users/{userId}/roles/{roleId}`

**Quyền / Header:** RBAC Admin hoặc Manager cấp/gỡ riêng FRONT_DESK_OFFICER cùng phường

**Mục đích:** Thu lại nhóm quyền người đó không còn được giao.

**Cách dùng:** Xem API 26, chọn đúng role cần gỡ, gọi không body.

**Kết quả:** Nhận 204; các role khác của người dùng vẫn giữ nguyên.

**Trường hợp sai và cách xử lý:**

- Người/role không tồn tại → 404; gỡ IT_ADMIN hoạt động cuối cùng → 409.
- Service chỉ đọc JWT có thể còn nhận role trong token cũ đến khi token hết hạn; không coi thay đổi DB là bảo
  đảm thu hồi tức thì ở mọi nơi.


Manager không được gán IT_ADMIN, MANAGER hoặc role khác. Chỉ cấp/gỡ FRONT_DESK_OFFICER
cho tài khoản cùng phường chưa có role đặc quyền; vượt phạm vi trả 403. Sau khi gỡ role,
người đó không còn xuất hiện trong danh sách Front Desk của Manager. Admin có thể đổi
phường; Manager không tự chuyển người sang phường mình.

#### API 29 — Xem nhật ký phân quyền

`GET /api/v1/rbac/audit-logs`

**Quyền / Header:** RBAC Admin

**Mục đích:** Tra cứu ai đã thao tác gì lên role/quyền/tài khoản trong luồng RBAC.

**Cách dùng:** Gọi `?page=1&pageSize=20`, mở từng dòng để xem actor, action, đối tượng và thời gian.

**Kết quả:** Nhận 200 với danh sách và tổng số; details là chuỗi JSON cần parse nếu muốn trình bày thành các
trường.

**Trường hợp sai và cách xử lý:**

- Phân trang không hợp lệ → 400.
- Đây không phải lịch sử mọi hành động trong toàn hệ thống, cũng không có thao tác sửa/xóa log được công bố.


## 2. Procedure Catalog Service (Danh mục & Quản lý thủ tục)

**Chịu trách nhiệm:** Tra cứu thủ tục cho công dân; quản lý nội dung, trạng thái và lịch sử; lưu PDF gốc, bản
nháp đối soát và xuất bản.

### Nhóm 2.1 — Cổng công khai cho Công dân (`/api/v1/procedures`)

Không cần token. Chỉ trả thủ tục đang hoạt động (`isActive=true`); cờ này không thay thế việc xác minh hiệu
lực pháp lý của nội dung.

#### API 30 — Công dân tìm và xem danh sách thủ tục

`GET /api/v1/procedures`

**Quyền / Header:** Public

**Mục đích:** Hiển thị danh sách trên cổng công dân và tìm theo mã/tên/lĩnh vực/cấp thực hiện.

**Cách dùng:** Ví dụ `?keyword=ket%20hon&pageNumber=1&pageSize=10`. Dùng id từ kết quả để mở chi tiết qua API
31.

**Kết quả:** Nhận 200 với danh sách gọn và thông tin phân trang. Chỉ trả thủ tục đang hoạt động; không trả
toàn bộ nội dung chi tiết.

**Trường hợp sai và cách xử lý:**

- pageSize trên 50 hoặc bộ lọc sai → 400.
- Truyền isActive=false cũng không xem được thủ tục đã tắt.
- Không thấy kết quả → thử bỏ bộ lọc/kiểm tra trang; không kết luận thủ tục đã bị xóa.

#### API 31 — Xem chi tiết thủ tục công khai

`GET /api/v1/procedures/{id}`

**Quyền / Header:** Public

**Mục đích:** Dựng trang chi tiết gồm các trường hợp, bước thực hiện, giấy tờ, biểu mẫu và căn cứ pháp lý.

**Cách dùng:** Lấy UUID từ API 30 rồi gọi; đây không phải route tra theo mã thủ tục dạng 1.116789.

**Kết quả:** Nhận 200 với đầy đủ dữ liệu để dựng trang. Mỗi case có thể có các bước và giấy tờ khác nhau.

**Trường hợp sai và cách xử lý:**

- Nhập mã thủ tục thay UUID không đúng route; thủ tục không có/đã tắt → 404.
- Không chỉ hiển thị case đầu tiên rồi bỏ những trường hợp còn lại.
- Link PDF riêng tư cần lấy qua API 32.

#### API 32 — Mở PDF gốc cho công dân

`GET /api/v1/procedures/{id}/source`

**Quyền / Header:** Public

**Mục đích:** Cho người xem đối chiếu trang thủ tục với văn bản PDF gốc đã lưu qua luồng draft.

**Cách dùng:** Gọi khi người dùng bấm Xem PDF, lấy `url` trong response rồi mở URL đó.

**Kết quả:** Nhận 200 với link tạm dùng trong 600 giây. Không phải response bytes PDF trực tiếp.

**Trường hợp sai và cách xử lý:**

- Thủ tục bị tắt hoặc không có draft Published khớp nguồn → 404; chỉ điền OriginalPdfUrl thủ công chưa đủ tạo
  liên kết này.
- Blob chưa cấu hình → 503.
- Link cũ hết hạn → gọi lại API, không lưu cố định link tạm vào DB/FE.


#### API 59 — Danh sách lĩnh vực thủ tục để chọn bộ lọc

`GET /api/v1/procedures/categories`

**Quyền:** Public; Front Desk cũng dùng API này, không cần role Procedure Manager.

**Mục đích:** Cung cấp các lĩnh vực như Hộ tịch, Đất đai để giao diện dựng ô lọc thủ tục.

**Cách dùng:** Gọi không body; lấy id rồi gọi GET procedures?categoryId={id}.
Cán bộ có thể đổi lĩnh vực đang xem; chưa phân công cứng một hay nhiều lĩnh vực cho tài khoản.

**Kết quả:** 200 mảng `{id,categoryName,description?}`, sắp theo tên rồi ID. Trả các danh mục
đã có, kể cả danh mục chưa có thủ tục active; không trả nội dung thủ tục hoặc dữ liệu nhân sự.

**Trường hợp sai:** Không gửi categoryName thay categoryId vào API lọc. Danh mục có thể tồn tại
nhưng danh sách public rỗng nếu không có thủ tục đang hoạt động. Mỗi lần lọc hiện nhận một
categoryId; không truyền danh sách `1,2` vào tham số số nguyên. Front Desk được tự chọn mọi
lĩnh vực, không đồng nghĩa có quyền sửa thủ tục; việc sửa vẫn theo policy ProcedureManager.

### Nhóm 2.2 — Quản lý thủ tục (`/api/v1/procedure-manager/procedures`)

Cần token có **PROCEDURE_MANAGER hoặc IT_ADMIN**. Role MANAGER đơn thuần không đáp ứng policy này. Version là
số nguyên 1, 2, 3…; reason khi đổi trạng thái là tùy chọn.

#### API 33 — Quản trị xem toàn bộ thủ tục

`GET /api/v1/procedure-manager/procedures`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Quản lý cả thủ tục đang mở và đã tắt, xem số version của mỗi thủ tục.

**Cách dùng:** Ví dụ `?isActive=false&pageNumber=1&pageSize=20` để xem thủ tục đã tắt; bỏ isActive để xem cả
hai.

**Kết quả:** Nhận 200 với trạng thái, số version và phân trang.

**Trường hợp sai và cách xử lý:**

- pageSize trên 100/bộ lọc sai → 400; dùng token công dân hoặc role MANAGER đơn thuần → 403.
- Dùng API public thay API này sẽ thiếu các thủ tục đã tắt.

#### API 34 — Tạo thủ tục mới bằng cách nhập dữ liệu

`POST /api/v1/procedure-manager/procedures`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Cán bộ tạo thủ tục mới khi đã có dữ liệu đầy đủ, không cần đi qua upload PDF.

**Cách dùng:** Gửi payload mục 4.3, chọn categoryId đã tồn tại và mã thủ tục chưa có.

**Kết quả:** Nhận 201 với thủ tục active và version 1. Dữ liệu có thể xuất hiện ở cổng công dân ngay.

**Trường hợp sai và cách xử lý:**

- Trùng mã → 409; tên quá ngắn/thiếu dữ liệu/danh mục không tồn tại → lỗi validation/nghiệp vụ, sửa theo
  response.
- Không gửi dữ liệu đang đối soát vào API này nếu chưa muốn công khai; dùng draft để chuẩn bị trước.

#### API 35 — Sửa thủ tục và lưu lịch sử

`PUT /api/v1/procedure-manager/procedures/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Cập nhật thủ tục đã có, giữ lại trạng thái trước sửa để tra cứu lịch sử.

**Cách dùng:** Điền id; gửi toàn bộ dữ liệu mới theo mục 4.3 cùng decisionNumber/effectiveDate ở cấp ngoài
cùng.

**Kết quả:** Nhận 200 với thủ tục mới; snapshot trạng thái cũ được lưu trong version tiếp theo.

**Trường hợp sai và cách xử lý:**

- ID không có → 404; dữ liệu sai → 400; mã trùng/xung đột → 409.
- Không chỉ gửi một trường như PATCH.
- Ngày hiệu lực không tự hẹn giờ cập nhật; thay đổi được áp dụng khi lệnh thành công.

#### API 36 — Mở hoặc ngừng hiển thị/tiếp nhận thủ tục

`PATCH /api/v1/procedure-manager/procedures/{id}/status`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Đánh dấu thủ tục đang được sử dụng hay đã đóng mà không xóa dữ liệu.

**Cách dùng:** Gửi `{ "isActive": false, "reason": "Tạm ngừng áp dụng" }`; true để mở lại.

**Kết quả:** Nhận 200 với trạng thái mới. Public không còn trả thủ tục inactive; quản trị vẫn nhìn thấy.

**Trường hợp sai và cách xử lý:**

- Thiếu isActive → 400; id sai → 404.
- API này không tự xử lý/hủy các hồ sơ công dân đã nộp, không nên mô tả cho người dùng rằng toàn bộ hồ sơ liên
  quan đã bị hủy.

#### API 37 — Xem lịch sử phiên bản thủ tục

`GET /api/v1/procedure-manager/procedures/{id}/versions`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Đối chiếu nội dung trước và sau các lần thay đổi quy định.

**Cách dùng:** Gửi id thủ tục, chọn version từ danh sách và đọc snapshotData.

**Kết quả:** Nhận 200 với các version theo thứ tự giảm dần; snapshotData là object dữ liệu, không phải chuỗi
cần parse thêm.

**Trường hợp sai và cách xử lý:**

- Không có thủ tục → 404.
- Không coi version lớn nhất luôn là nội dung hiện tại: các lần cập nhật lưu snapshot bản trước sửa.
- API chỉ đọc lịch sử, không tự phục hồi một phiên bản.

#### API 38 — Xuất bản dữ liệu đã đối soát theo mã thủ tục

`POST /api/v1/procedure-manager/procedures/publish`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Nhận dữ liệu đã được cán bộ kiểm tra; tự phân biệt thêm mới hay cập nhật theo procedureCode.

**Cách dùng:** Gửi toàn bộ ReviewedProcedureInput theo mục 4.3. Nếu đang sử dụng draft đã lưu, ưu tiên API 45
để đồng thời hoàn tất draft và liên kết PDF.

**Kết quả:** Nhận 200 ở cả hai trường hợp: mã mới tạo thủ tục/version 1; mã cũ lưu lịch sử rồi cập nhật. Thủ
tục cũ giữ trạng thái hoạt động đang có.

**Trường hợp sai và cách xử lý:**

- Mã gõ nhầm nhưng trùng thủ tục khác có thể cập nhật nhầm thủ tục đó.
- Gửi lặp có thể sinh thêm version, nên tránh bấm nhiều lần; khi timeout kiểm tra kết quả trước khi gửi lại.
- Chưa đủ dữ liệu → 400; xung đột → 409.
- Publish thủ tục đang tắt không tự mở lại.


### Nhóm 2.3 — PDF & Bản nháp đối soát (`/api/v1/procedure-manager/drafts`)

Luồng chính: upload → đọc bản nháp/PDF → chỉnh và lưu → xác nhận xuất bản. Preview chỉ đọc thử, không lưu.
revision là mã chống ghi đè dữ liệu cũ; dùng giá trị mới nhất. AI/OCR tắt vẫn có thể đọc PDF có lớp chữ, nhưng
dữ liệu nghiệp vụ cần được hoàn thiện và đối soát.

#### API 39 — Đọc thử chữ từ PDF

`POST /api/v1/procedure-manager/drafts/extract-preview`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Kiểm tra PDF có đọc được chữ không, lấy text/gợi ý để hỗ trợ nhập liệu trước khi lưu.

**Cách dùng:** Chọn PDF ở multipart field `file`, gửi Bearer. Không cần Blob nhưng Procedure phải gọi được
AIOCR.

**Kết quả:** Nhận 200 với extractedText, payload và warnings. Không tạo draft, không lưu file, không trả
draftId. AI tắt thì có text nhưng payload cần điền thủ công.

**Trường hợp sai và cách xử lý:**

- File không đúng/ quá lớn → 400 hoặc 413; kết nối chưa cấu hình → 503; không đọc được/dịch vụ phụ thuộc lỗi →
  502; quá thời gian → 504.
- PDF toàn hình có thể không có text khi OCR tắt.
- Nhận 200 không có nghĩa nội dung đã đủ để publish; phải xem warnings.

#### API 40 — Upload PDF và tạo bản nháp có lưu trữ

`POST /api/v1/procedure-manager/drafts`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Giữ PDF gốc cùng bản nháp để cán bộ đối soát và công dân xem nguồn sau xuất bản.

**Cách dùng:** Gửi multipart `file` là PDF tối đa 20 MiB; cần Blob đã cấu hình. Giữ id trong response để gọi
API 42.

**Kết quả:** Nhận 202 với draft. Nếu trích xuất được bật, theo dõi xử lý tiếp; nếu tắt, chuyển sang đối soát
thủ công. Chưa có thủ tục công khai ở bước này.

**Trường hợp sai và cách xử lý:**

- Upload sai loại/quá lớn → 400/413; Blob chưa cấu hình → 503.
- 202 không có nghĩa AI đã xử lý xong.
- Upload lại cùng file có thể tạo bản nháp khác; kiểm tra draft trước khi thử lại vì lỗi mạng.

#### API 41 — Danh sách bản nháp

`GET /api/v1/procedure-manager/drafts`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Tìm các PDF đang chờ xử lý, cần đối soát, thất bại hoặc đã xuất bản.

**Cách dùng:** Gọi `?page=1&pageSize=10`, chọn id để mở bản nháp.

**Kết quả:** Nhận 200 với mảng thông tin ngắn gồm trạng thái và failureCode nếu có; chưa có toàn bộ
text/payload.

**Trường hợp sai và cách xử lý:**

- pageSize quá 50/trang sai → 400.
- Response là mảng trực tiếp, không có items/totalCount như danh sách thủ tục; FE đọc nhầm cấu trúc sẽ hiển
  thị sai dù API thành công.

#### API 42 — Mở bản nháp để đối soát

`GET /api/v1/procedure-manager/drafts/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Lấy text PDF, các cảnh báo và nội dung đang chỉnh cho màn hình đối soát.

**Cách dùng:** Dùng draftId từ API 40/41. Nếu đang Queued/Processing, kiểm tra lại định kỳ hợp lý; khi
NeedsReview thì cho chỉnh dữ liệu.

**Kết quả:** Nhận 200 với payload và revision mới nhất. Giữ revision để gửi khi lưu/xuất bản.

**Trường hợp sai và cách xử lý:**

- Nhầm procedureId với draftId → có thể 404.
- Failed không phải đã xuất bản; đọc failureCode/warnings.
- Không ghi đè nội dung người dùng đang gõ chỉ vì một lần polling trả về dữ liệu cũ.

#### API 43 — Lưu dữ liệu đã chỉnh của bản nháp

`PUT /api/v1/procedure-manager/drafts/{id}`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Lưu phần cán bộ đã đọc và sửa trước khi xuất bản.

**Cách dùng:** Gửi `{revision,payload}`; revision lấy từ lần đọc/lưu mới nhất, payload là object thông tin
thủ tục đã chỉnh. Dùng với draft NeedsReview hoặc Failed.

**Kết quả:** Nhận 200 và draft/revision mới; vẫn chưa công khai thủ tục. Lưu revision trả về cho thao tác tiếp
theo.

**Trường hợp sai và cách xử lý:**

- Dùng revision cũ hoặc draft đang xử lý/đã xuất bản → 409; tải lại và đối chiếu, không tự ghi đè.
- Payload quá lớn/sai dạng → 400/413.
- Lưu draft thành công chưa bảo đảm các trường đủ điều kiện xuất bản.

#### API 44 — Thử trích xuất lại bản nháp bị lỗi

`POST /api/v1/procedure-manager/drafts/{id}/retry`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Chạy lại xử lý sau khi đã sửa nguyên nhân lỗi như cấu hình dịch vụ.

**Cách dùng:** Chỉ dùng draft Failed; gửi `{revision}` mới nhất và cần bật xử lý trích xuất.

**Kết quả:** Nhận 200 với draft về trạng thái xếp hàng; dùng API 42 theo dõi tiếp.

**Trường hợp sai và cách xử lý:**

- Draft không Failed/revision cũ → 409; tính năng trích xuất tắt → 503; id không có → 404.
- Không gọi retry cho draft đang chạy và không nhầm retry với bước sửa thủ công.

#### API 45 — Xác nhận bản nháp và xuất bản chính thức

`POST /api/v1/procedure-manager/drafts/{id}/publish`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Hoàn tất đối soát, đưa nội dung vào danh mục chính thức cùng PDF gốc và lịch sử phiên bản.

**Cách dùng:** Đọc PDF, sửa và lưu qua API 43 trước; lấy revision mới, gửi
`{ "revision": "<revision thực tế>", "confirmed": true }` khi cán bộ xác nhận.

**Kết quả:** Nhận 200 với chi tiết thủ tục; draft chuyển Published. Việc ghi draft/thủ tục/version nằm trong
transaction.

**Trường hợp sai và cách xử lý:**

- Chưa xác nhận/dữ liệu thiếu → 400; revision cũ, draft chưa sẵn sàng hoặc đã Published → 409; thiếu draft →
  404.
- Không lấy URL tùy ý trong payload để thay nguồn: backend dùng PDF của draft.
- Không gửi lại để tạo version tiếp theo; dùng luồng cập nhật phù hợp.

#### API 46 — Mở PDF gốc của bản nháp

`GET /api/v1/procedure-manager/drafts/{id}/source`

**Quyền / Header:** Bearer: PROCEDURE_MANAGER / IT_ADMIN

**Mục đích:** Hiển thị PDF bên cạnh dữ liệu nhập để cán bộ kiểm tra từng trường.

**Cách dùng:** Gọi với draftId, lấy url trả về để hiển thị PDF/mở tab.

**Kết quả:** Nhận 200 với URL có thời hạn 600 giây; không cần chờ bản nháp được xuất bản.

**Trường hợp sai và cách xử lý:**

- id sai → 404; lưu trữ chưa cấu hình → 503.
- URL hết hạn thì xin URL mới; không đưa link bản nháp cho công dân thay API 32 và không lưu SAS làm liên kết
  vĩnh viễn.


### Nhóm 2.3 — Hoàn thiện quản trị thủ tục theo yêu cầu FE (06/10/2026)

Tám API dưới đây yêu cầu Bearer token có role PROCEDURE_MANAGER hoặc IT_ADMIN theo policy ProcedureManager.
Role MANAGER quản lý phường không tự có quyền quản lý thủ tục. Token sai/hết hạn trả 401; thiếu role trả 403.
Các lỗi nghiệp vụ trả ProblemDetails tiếng Việt với code, traceId và errors khi có lỗi trường dữ liệu.
Đây là API đã triển khai trong source; cần build/deploy bản mới để xuất hiện trên Swagger Azure.

#### API 66 — Quản lý xem chi tiết cả thủ tục đã tắt

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/procedures/{id}`

**Mục đích:** Cán bộ mở form chỉnh sửa thủ tục, kể cả isActive=false, không cần bật công khai trước.

**Cách dùng:** Lấy id từ danh sách quản trị, không body. FE quản trị chuyển sang endpoint này thay vì
GET public `/api/v1/procedures/{id}`.

**Kết quả:** 200 ProcedureDetailDto đầy đủ contentPayload, checklistSchema, formDefinitions, trạng thái,
OriginalPdfUrl/PdfFileName nếu có. API public vẫn 404 với thủ tục đã tắt.

**Lỗi và xử lý:** 404 khi không có thủ tục → tải lại danh sách. Không dùng URL PDF thô để đọc private Blob;
dùng API source thích hợp. API này không thay đổi trạng thái công khai.

#### API 67 — Xóa bản nháp PDF không dùng nữa

**Method & Đường dẫn:** `DELETE /api/v1/procedure-manager/drafts/{id}`

**Mục đích:** Loại bỏ file tải nhầm hoặc bản nháp không sử dụng, đồng thời dọn PDF trên Blob.

**Cách dùng:** Không body. Cho phép NeedsReview, Failed, Queued; trạng thái Deleting được phép thử xóa lại.
FE nên yêu cầu người dùng xác nhận vì thao tác thành công không khôi phục bản nháp được.

**Kết quả:** 204, không body; PDF và bản ghi đã xóa. FE bỏ dòng khỏi danh sách.
Backend ghi nhận Deleting trước khi xóa Blob; trạng thái này ngăn sửa, publish và worker lấy lại bản nháp.

**Lỗi và xử lý:**

- 404: bản nháp không còn tồn tại, kể cả đã xóa trước đó → tải lại danh sách.
- 409 draft.delete_conflict: đang Processing, đã Published hoặc PDF còn được thủ tục/lịch sử tham chiếu.
  Không xóa PDF của thủ tục đã xuất bản; với Processing chờ xử lý kết thúc rồi thử lại nếu cần.
- 503 draft.storage_unavailable: Blob chưa cấu hình hoặc thao tác Blob lỗi → sửa cấu hình/kết nối rồi gọi
  DELETE lại. Nếu đã chuyển Deleting thì bản ghi vẫn tồn tại để retry, không cho sửa/xuất bản.
- Database lỗi sau khi Blob đã xóa: lần gọi DELETE tiếp theo có thể hoàn tất dọn bản ghi; chưa có worker
  tự retry xóa, FE/cán bộ phải gọi lại. Không có transaction phân tán giữa PostgreSQL và Blob.

#### API 68 — Thêm danh mục thủ tục

**Method & Đường dẫn:** `POST /api/v1/procedure-manager/categories`

**Mục đích:** Thêm lĩnh vực để chọn khi tạo/sửa thủ tục hoặc lọc danh sách.

**Cách dùng:** Gửi JSON:

```json
{ "categoryName": "Môi trường", "description": "Thủ tục thuộc lĩnh vực môi trường" }
```

Tên bắt buộc, tối đa 255 ký tự; description tùy chọn, tối đa 4000 ký tự. Backend bỏ khoảng trắng đầu/cuối.

**Kết quả:** 201 ProcedureCategoryDto `{id, categoryName, description}`; description null được bỏ khỏi JSON.
Dùng id mới cho categoryId. Tải lại danh sách bằng GET `/api/v1/procedures/categories`.

**Lỗi và xử lý:** 400 khi thiếu tên/quá dài; 409 category.name_exists khi trùng tên (không phân biệt hoa/thường)
→ chọn danh mục đã có hoặc nhập tên khác. Migration AlignCategoryIdentitySequence cần được áp dụng trước
để ID sinh tự động không trùng các danh mục seed 1–3.

#### API 69 — Sửa tên/mô tả danh mục

**Method & Đường dẫn:** `PUT /api/v1/procedure-manager/categories/{id}`

**Mục đích:** Chỉnh thông tin lĩnh vực; giữ nguyên id và liên kết với các thủ tục hiện có.

**Cách dùng:** Body giống API 68, gửi đầy đủ tên/mô tả mong muốn. Bỏ description hoặc gửi null/rỗng sẽ xóa mô tả.

**Kết quả:** 200 ProcedureCategoryDto. FE cập nhật nhãn danh mục trong dropdown và tải lại danh sách cần thiết.

**Lỗi và xử lý:** 400 dữ liệu sai, 404 category.not_found khi danh mục không còn tồn tại,
409 category.name_exists nếu tên đã thuộc danh mục khác. Sửa tên không tạo version thủ tục.

#### API 70 — Xóa danh mục trống

**Method & Đường dẫn:** `DELETE /api/v1/procedure-manager/categories/{id}`

**Mục đích:** Dọn lĩnh vực không còn dùng.

**Cách dùng:** Không body. Phải chuyển mọi thủ tục đang liên kết sang danh mục khác trước, kể cả thủ tục inactive.

**Kết quả:** 204 không body. FE bỏ danh mục khỏi danh sách.

**Lỗi và xử lý:** 404 nếu không tồn tại; 409 category.in_use kèm thông báo không thể xóa danh mục đang có
thủ tục liên kết. Lịch sử snapshot vẫn giữ categoryId cũ: nếu chỉ còn tham chiếu lịch sử thì được xóa,
nhưng rollback về danh mục đã xóa sẽ bị chặn 409 và cần đối soát/cập nhật thủ công.

#### API 71 — Khôi phục nội dung từ phiên bản lịch sử

**Method & Đường dẫn:** `POST /api/v1/procedure-manager/procedures/{id}/versions/{versionNumber}/rollback`

**Mục đích:** Khôi phục nội dung sai mà không phải nhập lại từng trường và không mất lịch sử.

**Cách dùng:** Lấy versionNumber từ API lịch sử (không dùng GUID versionId ở route này), gửi:

```json
{
  "reason": "Khôi phục nội dung trước khi chỉnh nhầm",
  "decisionNumber": "QD-2026-01",
  "effectiveDate": "2026-10-06"
}
```

Reason bắt buộc, tối đa 4000 ký tự; decisionNumber bắt buộc, tối đa 100 ký tự; effectiveDate là ngày hợp lệ.
Backend khóa thủ tục, chụp trạng thái trước rollback vào version kế tiếp rồi khôi phục dữ liệu snapshot
đã chọn trong cùng transaction. Giữ nguyên id, createdAt, isActive và lý do bật/tắt hiện tại; cập nhật updatedAt.
Số quyết định trong contentPayload được thay bằng decisionNumber của lần khôi phục này.

**Kết quả:** 200 ProcedureDetailDto. Lịch sử mới giữ snapshot TRƯỚC rollback theo quy ước version hiện có;
`snapshotData.rollback` ghi restoredFromVersion, reason, changedBy, decisionNumber, effectiveDate.
Không xóa/sửa các version cũ. FE tải lại chi tiết và lịch sử sau thành công.

**Lỗi và xử lý:**

- 400 khi body/số phiên bản sai; 404 khi thủ tục hoặc phiên bản của thủ tục không tồn tại.
- 409 procedure.invalid_snapshot khi snapshot thiếu/sai dữ liệu → đối soát và sửa thủ công.
- 409 procedure.category_not_found khi danh mục lịch sử đã bị xóa; procedure.code_exists khi mã cũ đã được
  thủ tục khác dùng → cập nhật thủ công với danh mục/mã phù hợp.
- Thủ tục đang tắt vẫn tắt sau rollback. Gọi lại là một hành động rollback mới, có version mới;
  FE không tự retry khi chưa biết lần trước thành công hay chưa.

#### API 72 — Danh sách biểu mẫu để chọn cho thủ tục

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/document-forms?page=1&pageSize=50&searchCode=CT`

**Mục đích:** Cán bộ chọn formTemplateId từ dropdown, không nhập GUID thủ công.

**Cách dùng:** Không body. Page từ 1 đến 1000000, pageSize 1–100; searchCode tùy chọn, tối đa 100 ký tự.
Procedure gọi DocumentForm qua HTTP và chỉ lấy biểu mẫu active. Cấu hình DocumentForm__BaseUrl phải trỏ
đúng service (Compose dùng http://document-form:8080/, local mặc định http://localhost:5004/).

**Kết quả:** 200 mảng `{id, formCode, formName, formType}` của trang yêu cầu; dùng id làm formTemplateId.
API này không trả metadata phân trang; khi cần thêm, FE gọi trang kế tiếp cho đến mảng rỗng.
Hợp đồng DocumentForm hiện cung cấp mẫu DOCX, nên formType là DOCX_TEMPLATE; chưa có danh sách mẫu
ONLINE_INTERACTIVE riêng. Mẫu chưa có file Word vẫn có thể xuất hiện; cần kiểm tra mẫu trước khi sử dụng.

**Lỗi và xử lý:** 400 query sai; 502 document_form.invalid_response khi dữ liệu nguồn sai;
503 khi thiếu địa chỉ hoặc DocumentForm không sẵn sàng; 504 timeout. Không hiển thị mảng rỗng giả khi service lỗi.
Không truy vấn chéo database, không đưa secret liên dịch vụ vào FE.

#### API 73 — Đọc PDF gốc của một phiên bản lịch sử

**Method & Đường dẫn:** `GET /api/v1/procedure-manager/procedures/{id}/versions/{versionId}/source`

**Mục đích:** Mở PDF của phiên bản cũ để đối chiếu, kể cả thủ tục hiện đã tắt hoặc đã thay PDF mới.

**Cách dùng:** Lấy GUID id của version từ API lịch sử (không dùng versionNumber), không body.

**Kết quả:** 200 `{url, expiresInSeconds: 600}`, Cache-Control no-store. URL chỉ có quyền đọc và hết hạn
sau 10 phút; FE xin link mới khi hết hạn, không lưu SAS vào dữ liệu thủ tục.

**Lỗi và xử lý:** 404 khi version không thuộc thủ tục, không có PDF đã upload qua draft, hoặc Blob đã mất;
503 khi cấu hình/lưu trữ chưa sẵn sàng. Hệ thống chỉ ký URL của Blob ghi nhận trong draft Published của
thủ tục đó, không tự ký URL ngoài hoặc URL tùy ý trong snapshot.

## 3. Document & Form Service — chỉnh sửa DOCX trên web

**Phân quyền từ 08/10/2026:** toàn bộ 5 API `/api/v1/form-templates` và các route con yêu cầu
Bearer JWT có role `PROCEDURE_MANAGER`; toàn bộ 6 API `/api/v1/citizen/submissions` và các route con
đòi role `REGISTERED_CITIZEN`. Không tự cấp quyền cho IT_ADMIN. Request/body và status thành công
ở bảng dưới giữ nguyên. Mọi API trong hai bảng có thể trả **401** (thiếu/sai/hết hạn token: đăng nhập lại)
hoặc **403** (sai role: dùng tài khoản đúng quyền). Lỗi auth là ProblemDetails có code/traceId.

`applicantId` vẫn có trong query/multipart/JSON nhưng phải bằng claim `sub` (UUID khác rỗng) trong JWT
đã xác thực; không dùng UUID giả khi test với token thật. Sai applicantId trả403 trước khi đọc/ghi đơn.
Swagger: đăng nhập IAM lấy access token → Authorize → dán token; đổi token khi chuyển cụm API.
DocumentForm cấu hình `Jwt__Key` (>=32 byte), `Jwt__Issuer`, `Jwt__Audience` giống IAM; Docker Compose
đọc khóa từ `JWT_KEY`. Không có khóa mặc định; thiếu cấu hình thì startup từ chối chạy.

Với yêu cầu khóa cả cụm FormTemplates, người dân không gọi được GET mẫu/download mẫu ở cụm này.
Luồng FE mở mẫu lần đầu và client nội bộ Procedure gọi FormTemplates phải được thiết kế lại quyền đọc
hoặc endpoint citizen riêng trước khi tích hợp luồng đó. Không đưa token quản lý vào FE người dân.
Health vẫn không yêu cầu token. Đã deploy phân quyền lên Azure ngày 08/10/2026 (image roles-20261008-01); Swagger Bearer và request không token401 đã được xác minh.


Luồng hiện tại (07/10/2026): admin upload mẫu DOCX nguyên bản; FE tải binary vào editor Word trên web;
người dân sửa trong editor; FE export DOCX và gửi lại khi lưu nháp. Backend lưu nguyên byte, không sinh
trang khai báo bổ sung, không yêu cầu schema, mapping hay placeholder. File người dân nằm ở blob riêng.
Mẫu gốc không bị ghi đè khi người dân lưu. Việc hiển thị và bảo toàn bố cục khi chỉnh sửa phụ thuộc editor FE.

DocumentForm quản lý **một đơn**, không quản lý toàn bộ thủ tục/giấy tờ. Procedure giữ templateId trong
thành phần hồ sơ; Workflow có thể tham chiếu submissionId khi hoàn thành đơn. Không truy vấn chéo database.

### 3.1. Nhóm mẫu DOCX (admin quản lý, người dân đọc)

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `GET /api/v1/form-templates` | Admin lấy danh sách; query page=1,pageSize=20,isActive=true,searchCode tùy chọn. | 200 danh sách phân trang | 400 query không hợp lệ: sửa phân trang. |
| `POST /api/v1/form-templates` | Admin tạo mẫu; JSON `{ "code":"DON_01", "title":"Đơn mẫu" }`. | 201, lưu `id` làm templateId | 400 thiếu dữ liệu; 409 trùng code: dùng mẫu đã có hoặc code khác. |
| `GET /api/v1/form-templates/{templateId}` | FE gọi khi người dân chọn “Điền đơn”. | 200 metadata, `onlineReady=true` khi active và có file; không còn schemaDefinition/templateVersionId | 404: kiểm tra ID; onlineReady=false: admin upload DOCX hoặc kiểm tra trạng thái. |
| `POST /api/v1/form-templates/{templateId}/upload-docx` | Admin thêm/thay mẫu chính thức; multipart field `file`, tối đa 20 MiB. | 200 `{templateId,blobUrl}`; blob mới, bytes nguyên bản | 400 rỗng/hỏng/sai đuôi/quá lớn: chọn DOCX hợp lệ; 404 sai templateId. |
| `GET /api/v1/form-templates/{templateId}/download-docx` | FE fetch binary rồi import vào editor khi bắt đầu đơn mới; admin đối chiếu mẫu. | 200 binary DOCX nguyên bản | 404 thiếu mẫu/file: kiểm tra ID hoặc upload lại. |

### 3.2. Nhóm đơn của người dân

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `POST /api/v1/citizen/submissions/draft` | FE lần đầu lưu: multipart `templateId`, `applicantId`, `file` (DOCX export từ editor). | 201 `{submissionId,status:"Draft",fileName,fileSizeBytes,updatedAt,message}` | 400 file/ID không hợp lệ; 404 mẫu chưa upload/không có; 409 mẫu inactive; 415 gửi JSON: đổi multipart; 503 Blob lỗi: thử lại. |
| `PUT /api/v1/citizen/submissions/{submissionId}/draft` | FE lưu tiếp: multipart `applicantId`, `file`. Không đổi templateId. | 200 cùng DTO lưu nháp; file mới được lưu nguyên byte | 400 đơn đã chốt/file lỗi; 403 sai chủ; 404 thiếu đơn/mẫu; 409 xung đột: tải lại trước khi lưu; 503 Blob lỗi. |
| `GET /api/v1/citizen/submissions?applicantId=...` | FE màn hình “Đơn của tôi”; thêm status,page,pageSize nếu cần. | 200 phân trang metadata | 403 applicantId thiếu/không khớp JWT; 400 page/pageSize/status sai: sửa query. |
| `GET /api/v1/citizen/submissions/{submissionId}?applicantId=...` | FE mở lại đơn, đọc status trước khi mở editor. | 200 metadata (id,templateId,applicantId,fileName,fileSizeBytes,status,…); không formData/schema | 403 sai chủ; 404 không tồn tại: kiểm tra ID. |
| `GET /api/v1/citizen/submissions/{submissionId}/download-docx?applicantId=...` | FE mở **bản đã sửa**, kể cả sau khi admin đổi mẫu; có thể dùng để tải/in. | 200 binary đúng file đã lưu gần nhất | 403 sai chủ; 404 thiếu đơn/blob: kiểm tra ID hoặc khôi phục file. |
| `POST /api/v1/citizen/submissions/{submissionId}/submit` | Sau khi lưu thành công và người dân xác nhận hoàn thành; JSON `{ "applicantId":"UUID" }`. | 200 status=Submitted, khóa sửa nháp | 400 đã chốt; 403 sai chủ; 404 thiếu đơn/file; 409 xung đột: mở lại kiểm tra. |

File phải là DOCX hợp lệ không macro, tối đa 20 MiB; ZIP tối đa 5.000 entries/100 MiB giải nén.
Backend không thể suy ra tất cả chỗ trống đã điền từ DOCX tùy ý. `submit` xác nhận của người dân,
không phải kiểm tra required fields hay nộp toàn bộ hồ sơ hành chính. FE cần lưu thành công trước khi submit.

Đã bỏ API `PUT .../online-config`; luồng JSON formData cũ không còn được nhận. Các cột lịch sử
schema/version trong database được giữ để không mất dữ liệu cũ; không cần migration cho thay đổi này.

Lỗi nhóm Citizen dùng ProblemDetails (`status,title,detail,code,traceId`); model binding dùng
ValidationProblemDetails. Nhóm template giữ `{code,message}` cho lỗi nghiệp vụ. FE đọc HTTP status trước.
Hiện applicantId vẫn theo hợp đồng tích hợp sẵn có; controller chưa tự xác thực JWT/Authorize.
Danh tính này cần đến từ lớp xác thực tin cậy của hệ thống, không coi việc có UUID là xác thực.

### 3.3. Nhóm Giấy tờ Minh chứng (TASK-17 — Supporting Documents)

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `POST /api/v1/applications/{applicationId}/supporting-documents` | Người dân/cán bộ đính kèm file minh chứng (CCCD, giấy khai sinh...); multipart `file` + optional `checklistId`. | 201 `{id, applicationId, checklistId, fileName, blobUrl, fileSizeBytes, contentType, createdAtUtc}` | 400 file rỗng, > 20MB, đuôi file không hỗ trợ (.pdf, .jpg, .jpeg, .png, .doc, .docx); 503 Blob upload lỗi. |
| `GET /api/v1/applications/{applicationId}/supporting-documents` | FE hiển thị danh sách toàn bộ giấy tờ minh chứng đã tải lên của hồ sơ. | 200 danh sách `SupportingDocumentDto` | 401 chưa đăng nhập; 400 applicationId không hợp lệ. |
| `DELETE /api/v1/supporting-documents/{documentId}` | Người dân/cán bộ xóa 1 giấy tờ minh chứng (soft-delete). | 204 No Content | 404 không tìm thấy file minh chứng; 401 chưa đăng nhập. |
| `GET /api/v1/supporting-documents/{documentId}/download` | FE tải về hoặc mở xem file minh chứng trực tiếp từ Azure Blob. | 200 binary stream (`application/octet-stream` hoặc MIME gốc) | 404 không tìm thấy file hoặc blob bị xóa; 401 chưa đăng nhập. |

### 3.4. Nhóm Văn bản kết xuất PDF (TASK-18 — Generated Documents)

**Validation bắt buộc:** Cả công dân và cán bộ chỉ được xuất PDF khi Workflow DOCUMENTS_READY và submission ReadyForDownload, đúng phạm vi truy cập. Đây là giấy tờ mang đến phường.

PDF xuất ra **không chứa comment của cán bộ**: backend dùng `DocxCommentStripper` (OpenXML SDK) để xóa toàn bộ Word comments (`CommentRangeStart`, `CommentRangeEnd`, `CommentReference`, `WordprocessingCommentsPart`) khỏi DOCX trước khi gửi sang Gotenberg. Người dân in PDF sạch để nộp giấy tờ.

| API | Ai gọi, lúc nào và request | Thành công | Lỗi thường gặp và cách xử lý |
|---|---|---|---|
| `POST /api/v1/applications/{applicationId}/generate-pdf` | **Người dân** kích hoạt sau khi hồ sơ được cán bộ duyệt (`Status=ReadyForDownload`). Multipart: bắt buộc `submissionId` đã lưu và thuộc hồ sơ; `documentType` mặc định `ORIGINAL_SUBMISSION`. Không truyền file thay thế. | 200 `{id, applicationId, documentType, pdfBlobUrl, createdAtUtc}` | 400 `document.submission_not_ready` — hồ sơ chưa được duyệt; 403 không phải chủ hồ sơ; 404 không tìm thấy submission; 503 Blob/Render lỗi. |
| `GET /api/v1/applications/{applicationId}/generated-documents` | FE lấy danh sách PDF đã sinh cho hồ sơ. | 200 danh sách `GeneratedDocumentDto` | 401 chưa đăng nhập; 400 applicationId không hợp lệ. |
| `GET /api/v1/generated-documents/{documentId}/download` | Người dân/cán bộ tải file PDF giấy tờ chuẩn bị để in hoặc đóng dấu. | 200 binary stream `application/pdf` | 404 không tìm thấy document hoặc blob bị xóa; 400 hồ sơ chưa được duyệt (người dân). |

### 3.5. Nhóm Cán bộ xét xác nhận giấy tờ sẵn sàng (Officer Review Flow)

**Ai sử dụng:** Cán bộ phòng tiếp nhận (`FRONT_DESK_OFFICER`), Quản lý (`MANAGER`), Quản lý thủ tục (`PROCEDURE_MANAGER`), IT Admin (`IT_ADMIN`). Tất cả đều cần JWT Bearer token hợp lệ với đúng role.

**Luồng đầy đủ:**
1. Người dân điền DOCX trên web editor → lưu nháp → nộp hồ sơ (`Status: Submitted`)
2. Cán bộ tải DOCX về (`GET .../download-docx`) → mở trên web editor → thêm comment/ghi chú
3. Cán bộ lưu DOCX có comment về backend (`PUT .../annotate-docx`) — `Status` **không thay đổi**
4a. Nếu cần sửa: cán bộ gọi `POST .../request-revision` → `Status: RevisionRequested`  
    → Người dân tải DOCX về, **thấy comment của cán bộ** → sửa → lưu lại → nộp lại  
4b. Nếu OK: cán bộ gọi `POST .../confirm-readiness` → `Status: ReadyForDownload`
5. Người dân gọi `generate-pdf` → BE strip comment → PDF sạch → in & nộp

**Base path:** `/api/v1/officer/submissions`  
**Auth:** Bearer + role officer (xem danh sách trên)

| API | Mô tả | Request | Thành công | Lỗi thường gặp |
|---|---|---|---|---|
| `GET /{submissionId}/download-docx` | Cán bộ tải DOCX hiện tại của hồ sơ (có thể đã có comment nếu đã annotate trước). Dùng để mở trong web editor, đọc nội dung người dân điền, thêm comment. | — | 200 binary DOCX stream | 403 không phải officer; 404 không tìm thấy hồ sơ; 400 blob bị xóa. |
| `PUT /{submissionId}/annotate-docx` | Cán bộ lưu lại DOCX **có comment** từ web editor. DOCX mới thay thế bản hiện tại trong Blob. `Status` giữ nguyên (`Submitted` hoặc `RevisionRequested`). Người dân sẽ thấy comment khi mở lại. | `multipart/form-data`: trường `file` (DOCX ≤ 20MB) | 200 `{submissionId, status, fileName, fileSizeBytes, updatedAt, message}` | 403 không phải officer; 404 hồ sơ không tồn tại; 400 file rỗng/không hợp lệ/không phải DOCX; 400 status không phải `Submitted`/`RevisionRequested`; 503 Blob upload lỗi. |
| `POST /{submissionId}/request-revision` | Cán bộ trả hồ sơ lại và yêu cầu người dân sửa. Thường gọi **sau** `annotate-docx` để người dân biết cần mở lại file có comment. `Status: Submitted → RevisionRequested`. | JSON: `{ "comment": "Cần bổ sung giấy khai sinh." }` | 200 `{submissionId, status, ...}` | 403 không phải officer; 404 hồ sơ không tồn tại; 400 status không phải `Submitted`; 400 comment rỗng. |
| `POST /{submissionId}/confirm-readiness` | Cán bộ phê xác nhận giấy tờ sẵn sàng. `Status: Submitted → ReadyForDownload`. Sau bước này người dân mới được gọi `generate-pdf`. | JSON: `{}` (officerId lấy tự động từ JWT `sub` claim) | 200 `{submissionId, message: "Workflow đã xác nhận giấy tờ sẵn sàng và đồng bộ đơn.", ...}` | 403 không phải officer; 404 hồ sơ không tồn tại; 400 status không phải `Submitted`. |

**Lưu ý quan trọng cho FE:**
- **Thứ tự annotate → request-revision:** Cán bộ nên `PUT annotate-docx` trước rồi mới `POST request-revision`. Có thể gọi `request-revision` mà không annotate nếu chỉ cần comment text qua trường `comment`.
- **Người dân download DOCX sau revision:** Citizen dùng `GET /api/v1/citizen/submissions/{id}/download-docx` (endpoint của citizen) để tải về DOCX có comment của cán bộ — đây là cùng một file blob vừa được officer annotate.
- **PDF không có comment:** Comment của cán bộ chỉ tồn tại trong DOCX. Khi người dân `generate-pdf`, backend tự động xóa trước khi render. Comment không cần phải được xóa thủ công.


## 4. AIOCR Service (Trích xuất dữ liệu PDF)

**Chịu trách nhiệm:** Đọc chữ PDF và thực hiện các bước AI/OCR khi được cấu hình. Service Procedure gọi nội
bộ; FE dùng API preview/draft của Procedure.

### Nhóm 4.1 — Trích xuất PDF nội bộ

Request là bytes PDF trực tiếp, không phải multipart. Service key chỉ dùng phía backend.

#### API 53 — Service Procedure nhờ AIOCR trích xuất PDF

`POST /internal/v1/procedure-extractions`

**Quyền / Header:** `X-Service-Key`

**Mục đích:** Kết nối hai service: Procedure gửi file, AIOCR đọc chữ và có thể gọi AI/OCR tùy cấu hình. Người
dùng FE không gọi API này trực tiếp.

**Cách dùng:** Backend gửi bytes PDF với Content-Type application/pdf và X-Service-Key đã cấu hình. Không dùng
multipart và không thay service key bằng access token người dùng.

**Kết quả:** Nhận 200 với text, payload, warnings. Đọc chữ PDF, OCR ảnh và AI hiểu nội dung là các bước riêng;
AI/OCR tắt vẫn có thể đọc được PDF có lớp chữ.

**Trường hợp sai và cách xử lý:**

- Key sai → 401; tính năng/key chưa cấu hình → 503; PDF không hợp lệ → 400; quá giới hạn → 413; xử lý phụ
  thuộc lỗi/timeout → 502/504.
- Không đưa service key vào mã FE.
- Payload trống nhưng có text khi AI tắt không phải bằng chứng trích xuất text thất bại.


## 5. YARP Gateway & Endpoints hệ thống chung

**Chịu trách nhiệm:** Gateway tiếp nhận và chuyển tiếp request đến service phù hợp; các service tự thực thi
quyền nghiệp vụ. Health/root phục vụ nhận biết tiến trình, không phải API nghiệp vụ.

### Nhóm 5.1 — Kiểm tra service và Swagger

- **Method & Đường dẫn:** `GET /health`
  - **Service:** Gateway và cả 6 service
  - **Mục đích & Cách sử dụng:** Mở tại đúng base URL để kiểm tra ứng dụng phản hồi; thông thường 200 với
    `Healthy`.
  - **Lưu ý khi kết quả không như mong đợi:** Chưa kiểm tra đầy đủ PostgreSQL/Blob/AI; không dùng Healthy để
    kết luận mọi API sẵn sàng. Không kết nối được thì kiểm tra tiến trình/port.

- **Method & Đường dẫn:** `GET /`
  - **Service:** 6 service, không gồm Gateway
  - **Mục đích & Cách sử dụng:** Trả tên microservice; DocumentForm có thêm version/status.
  - **Lưu ý khi kết quả không như mong đợi:** 200 không chứng minh database hoạt động. Root Gateway không có
    endpoint này.

- **Method & Đường dẫn:** `GET /swagger/index.html`
  - **Service:** IAM, Procedure, DocumentForm khi Development
  - **Mục đích & Cách sử dụng:** Giao diện thử API: Try it out → điền dữ liệu → Execute; Authorize nếu cần
    token.
  - **Lưu ý khi kết quả không như mong đợi:** 404 có thể vì sai service hoặc không bật Development, không nhất
    thiết lỗi đăng nhập.

- **Method & Đường dẫn:** `GET /swagger/v1/swagger.json`
  - **Service:** IAM, Procedure, DocumentForm khi Development
  - **Mục đích & Cách sử dụng:** Bản mô tả OpenAPI để công cụ đọc hợp đồng API.
  - **Lưu ý khi kết quả không như mong đợi:** Không phải dữ liệu nghiệp vụ; các service còn lại chưa có
    Swagger tương ứng.


ApplicationWorkflow đã có API hồ sơ (xem mục TASK-11 bên dưới); AnalyticsSystem mới có root/health. Tổng cộng có 6 root và 7
health endpoint theo host.

### Nhóm 5.2 — Các tuyến chuyển tiếp của Gateway (local: cổng 5000)

`ANY` nghĩa Gateway chuyển tiếp method của request; không có nghĩa service đích chấp nhận mọi method. `{**}`
thể hiện phần đường dẫn còn lại.

- **Method & Đường dẫn:** `ANY /api/v1/auth/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Đăng ký/đăng nhập/refresh/logout, giữ nguyên route. Dùng tuyến này để khớp
    Path cookie refresh.

- **Method & Đường dẫn:** `ANY /api/v1/users/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Thông tin người dùng và hồ sơ, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/accounts/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Quản trị tài khoản, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/rbac/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Quản lý vai trò/quyền, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/procedures/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Danh mục công dân, chi tiết và nguồn PDF, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/v1/procedure-manager/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Quản trị thủ tục và draft, giữ nguyên route.

- **Method & Đường dẫn:** `ANY /api/document-form/{**}`
  - **Service đích:** DocumentForm
  - **Mục đích & Cách sử dụng:** Bỏ prefix `/api/document-form`. Ví dụ
    `/api/document-form/api/v1/form-templates`. Không có tuyến trực tiếp `/api/v1/form-templates` trong cấu
    hình hiện tại.

- **Method & Đường dẫn:** `ANY /api/ai-ocr/{**}`
  - **Service đích:** AIOCR
  - **Mục đích & Cách sử dụng:** Bỏ prefix `/api/ai-ocr`; API nội bộ vẫn yêu cầu service key. Không đưa key
    vào FE.

- **Method & Đường dẫn:** `ANY /api/application-workflow/{**}`
  - **Service đích:** ApplicationWorkflow
  - **Mục đích & Cách sử dụng:** Bỏ prefix tương ứng, ví dụ `/api/application-workflow/health`.

- **Method & Đường dẫn:** `ANY /api/analytics-system/{**}`
  - **Service đích:** Analytics
  - **Mục đích & Cách sử dụng:** Bỏ prefix tương ứng, ví dụ `/api/analytics-system/health`.

- **Method & Đường dẫn:** `ANY /api/iam/{**}`
  - **Service đích:** IAM
  - **Mục đích & Cách sử dụng:** Tuyến prefix cũ. Ưu tiên `/api/v1/auth/...` cho auth để trình duyệt gửi đúng
    cookie.

- **Method & Đường dẫn:** `ANY /api/procedure-catalog/{**}`
  - **Service đích:** Procedure
  - **Mục đích & Cách sử dụng:** Tuyến prefix thay thế, bỏ `/api/procedure-catalog` trước khi chuyển tiếp.


## Quy tắc cập nhật

Mỗi khi thêm, đổi hoặc xóa API, cập nhật **chính file này** trong cùng task. Xếp API vào đúng service/nhóm;
ghi quyền thật, mục đích, cách gọi, kết quả và trường hợp sai/cách khắc phục. Cập nhật DTO/ví dụ trong phụ lục
khi hợp đồng đổi. Không ghi tính năng hoặc quyền dự kiến thành chức năng đã có. Quy tắc duy trì tài liệu đã có
trong AGENTS.md.

---

# Phụ lục kỹ thuật — Cổng, DTO, JSON mẫu và cấu hình

Các mục 1–9 dưới đây là phần tra cứu chi tiết bổ trợ cho các nhóm API ở trên.

## 1. Địa chỉ và quy ước gọi API

### 1.1. Local Dev

Các cổng dưới đây lấy từ `Properties/launchSettings.json`, áp dụng khi chạy bằng launch profile tương ứng.
Docker/Azure dùng địa chỉ và port đã cấu hình cho môi trường đó.

- **Thành phần:** YARP Gateway
  - **Base URL trực tiếp:** `http://localhost:5000`
  - **Swagger UI khi bật môi trường Development:** Không có Swagger tổng hợp

- **Thành phần:** IAM
  - **Base URL trực tiếp:** `http://localhost:5001`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5001/swagger`

- **Thành phần:** Procedure Catalog
  - **Base URL trực tiếp:** `http://localhost:5002`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5002/swagger`

- **Thành phần:** Application Workflow
  - **Base URL trực tiếp:** `http://localhost:5003`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5003/swagger`

- **Thành phần:** Document Form
  - **Base URL trực tiếp:** `http://localhost:5004`
  - **Swagger UI khi bật môi trường Development:** `http://localhost:5004/swagger`

- **Thành phần:** AIOCR
  - **Base URL trực tiếp:** `http://localhost:5005`
  - **Swagger UI khi bật môi trường Development:** Chưa cấu hình

- **Thành phần:** Analytics System
  - **Base URL trực tiếp:** `http://localhost:5006`
  - **Swagger UI khi bật môi trường Development:** Chưa cấu hình


### 1.2. Gọi qua Gateway

- **Service:** IAM
  - **Đường dẫn qua Gateway:** Giữ nguyên `/api/v1/auth`, `/api/v1/users`, `/api/v1/accounts`, `/api/v1/rbac`
  - **Ví dụ:** `http://localhost:5000/api/v1/auth/login`

- **Service:** Procedure
  - **Đường dẫn qua Gateway:** Giữ nguyên `/api/v1/procedures` và `/api/v1/procedure-manager`
  - **Ví dụ:** `http://localhost:5000/api/v1/procedures?pageNumber=1`

- **Service:** Document Form
  - **Đường dẫn qua Gateway:** Thêm `/api/document-form` trước route service
  - **Ví dụ:** `http://localhost:5000/api/document-form/api/v1/form-templates`

- **Service:** AIOCR
  - **Đường dẫn qua Gateway:** Thêm `/api/ai-ocr` trước route service
  - **Ví dụ:** `/api/ai-ocr/internal/v1/procedure-extractions` — dành cho backend

- **Service:** Application Workflow
  - **Đường dẫn qua Gateway:** Thêm `/api/application-workflow`
  - **Ví dụ:** `/api/application-workflow/health`

- **Service:** Analytics
  - **Đường dẫn qua Gateway:** Thêm `/api/analytics-system`
  - **Ví dụ:** `/api/analytics-system/health`


Gateway cũng có prefix `/api/iam` và `/api/procedure-catalog`, được loại bỏ trước khi chuyển tiếp. **Với IAM,
dùng route chuẩn `/api/v1/auth/...` **, vì cookie refresh có Path `/api/v1/auth`; dùng prefix cũ có thể khiến
trình duyệt không gửi cookie đến route refresh. Gateway hiện không có route trực tiếp `/api/v1/form-templates`
.

### 1.3. Quy ước

- Route trong các bảng bên dưới là route **tại service**, chưa có prefix Gateway.
- `{id}`, `{userId}`, `{templateId}` là UUID; `{roleId}`, `{permissionId}` là số nguyên. Thay cả dấu ngoặc
  bằng giá trị thật.
- JSON request dùng `Content-Type: application/json`. Upload dùng `multipart/form-data`, trường `file`; để
  trình duyệt/Postman tự tạo boundary.
- Endpoint yêu cầu đăng nhập: `Authorization: Bearer <accessToken>`. Swagger: đăng nhập trước, lấy
  `accessToken`, nhấn **Authorize** và nhập theo mô tả security scheme.
- Ngày thuần túy có dạng `YYYY-MM-DD`; thời điểm có dạng ISO 8601, ví dụ `2026-10-04T10:00:00Z`.
- `?` sau tên trường DTO trong tài liệu nghĩa là nullable/tùy chọn; không phải một phần tên JSON. IAM bỏ
  trường null khỏi response. FE không nên mặc định các service khác cũng làm như vậy.
- `204` không có JSON body. Các mã lỗi trong bảng là lỗi điển hình; endpoint được bảo vệ còn có `401/403`, và
  có thể gặp lỗi hạ tầng `500`.

## 2. IAM: đăng ký, đăng nhập và token

### 2.1. Danh mục xác thực

**Cả 4 API POST dưới đây đều cần header `X-CSRF-Protection: 1`.** “Public” chỉ có nghĩa không cần Bearer,
không miễn kiểm tra CSRF.

- **Method:** POST
  - **Route:** `/api/v1/auth/register`
  - **Quyền / Request:** Public; `{username,email,password,fullName}`
  - **Thành công:** `201 CurrentUserDto`, Location `/api/v1/users/me`
  - **Lỗi điển hình:** `400` dữ liệu sai; `409` trùng tài khoản

- **Method:** POST
  - **Route:** `/api/v1/auth/login`
  - **Quyền / Request:** Public; `{usernameOrEmail,password}`
  - **Thành công:** `200 BrowserAuthResponse` và Set-Cookie refresh
  - **Lỗi điển hình:** `401` thông tin sai/tài khoản không hợp lệ

- **Method:** POST
  - **Route:** `/api/v1/auth/refresh-token`
  - **Quyền / Request:** Public; **không body**, gửi cookie refresh
  - **Thành công:** `200 BrowserAuthResponse` và cookie mới
  - **Lỗi điển hình:** `401` thiếu/hết hạn/đã thu hồi token

- **Method:** POST
  - **Route:** `/api/v1/auth/revoke-token`
  - **Quyền / Request:** Bearer; **không body**, gửi cookie refresh
  - **Thành công:** `204`, thu hồi refresh và xóa cookie
  - **Lỗi điển hình:** `401/403`

- **Method:** GET
  - **Route:** `/api/v1/users/me`
  - **Quyền / Request:** Bearer + `iam.profile.read`; không body
  - **Thành công:** `200 CurrentUserDto`
  - **Lỗi điển hình:** `401/403`


`BrowserAuthResponse` gồm `accessToken`, `accessTokenExpiresAt`, `refreshTokenExpiresAt`, `tokenType`
(`Bearer`). **Không có refreshToken trong JSON**. Đăng ký tạo sẵn hồ sơ và gán `REGISTERED_CITIZEN`; không tự
đăng nhập.

`CurrentUserDto` gồm `id`, `username`, `email`, `profile?` (UserProfileDto bên dưới), `roles` (mảng tên
role), `permissions` (mảng mã quyền).

Validation đăng ký:

- `username`: bắt buộc, tối đa 100, chỉ chữ ASCII, số, `_`, `.`, `-`.
- `email`: bắt buộc, email hợp lệ, tối đa 255.
- `password`: ít nhất 8 ký tự, có chữ hoa và ký tự dấu câu/ký hiệu; tối đa **72 byte UTF-8**. Không có yêu
  cầu riêng bắt buộc chữ số trong validator hiện tại.
- `fullName`: bắt buộc, tối đa 255.

### 2.2. HttpOnly, CSRF và CORS

Cookie `refreshToken` có `HttpOnly=true`, Path `/api/v1/auth`, không đặt Domain. JavaScript không đọc cookie
này. Mặc định SameSite Strict; Secure được áp dụng trừ cấu hình cho phép HTTP localhost trong Development. Khi
FE/API khác site trên môi trường thật, cần cấu hình cookie/CORS tương ứng; không chỉ đổi URL trong FE là đủ.

Nếu request có `Origin`, IAM chấp nhận khi Origin trùng scheme/host hiện tại hoặc nằm trong AllowedOrigins.
Header CSRF vẫn bắt buộc kể cả không có Origin. Forwarded Headers giúp nhận đúng HTTPS/host bên ngoài reverse
proxy. Gặp `403 iam.csrf_rejected`: kiểm tra custom header, Origin, CORS và forwarded host/proto.

Ví dụ FE (biến đăng nhập do người dùng nhập, không hardcode tài khoản):

```javascript
const apiBase = 'http://localhost:5000';
const response = await fetch(`${apiBase}/api/v1/auth/login`, {
  method: 'POST',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json', 'X-CSRF-Protection': '1' },
  body: JSON.stringify({ usernameOrEmail, password })
});
if (!response.ok) throw await response.json();
const auth = await response.json();
const me = await fetch(`${apiBase}/api/v1/users/me`, {
  headers: { Authorization: `Bearer ${auth.accessToken}` }
});

// Khi cần refresh: không truyền accessToken/refreshToken vào JSON.
const renewed = await fetch(`${apiBase}/api/v1/auth/refresh-token`, {
  method: 'POST', credentials: 'include',
  headers: { 'X-CSRF-Protection': '1' }
});
```

FE dùng `credentials: 'include'` cho login/refresh/logout, giữ cùng hostname và base URL trong suốt phiên.
Refresh token xoay vòng một lần sử dụng: gom các request refresh đồng thời thành một lần gọi. Khi refresh thất
bại 401, quay về đăng nhập. Logout gửi Bearer và cookie, sau đó xóa access token phía FE; endpoint thu hồi
refresh hiện tại, không phải thao tác đăng xuất mọi thiết bị.

Ngoại lệ RBAC: PUT/DELETE user-role cho phép MANAGER cấp/gỡ riêng FRONT_DESK_OFFICER
cùng phường. Các thao tác tạo role, sửa quyền và xem audit vẫn chỉ dành cho Admin.
CurrentUserDto bổ sung wardId nullable; token/cookie không đổi. Phạm vi quản lý luôn đọc từ DB.

## 3. IAM: hồ sơ, tài khoản và RBAC

### 3.1. Hồ sơ cá nhân và quản trị hồ sơ

`ProfileInput` và `UserProfileDto` có cùng các trường:

```json
{
  "fullName": "Người dùng kiểm thử",
  "identityNumber": null,
  "phoneNumber": null,
  "dateOfBirth": "1995-01-01",
  "gender": null,
  "permanentAddress": null,
  "temporaryAddress": null
}
```

Tên bắt buộc/tối đa 255; số định danh tối đa 20 và duy nhất nếu có; điện thoại tối đa 20; giới tính tối đa 10;
địa chỉ tối đa 4000; ngày sinh không được ở tương lai. `PUT` thay toàn bộ hồ sơ: trường tùy chọn bỏ
qua/null/trắng được xóa về null.

- **Method:** GET
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.read`; không body
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `404` chưa có hồ sơ

- **Method:** POST
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; ProfileInput
  - **Thành công:** `201 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; ProfileInput
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/users/me/profile`
  - **Quyền / Request:** `iam.profile.write`; không body
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; không body
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; ProfileInput
  - **Thành công:** `201 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** PUT
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; ProfileInput
  - **Thành công:** `200 UserProfileDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/users/{userId}/profile`
  - **Quyền / Request:** AccountManagement, đúng phạm vi; không body
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`


Đăng ký đã tạo hồ sơ nên POST hồ sơ ngay sau đăng ký sẽ xung đột; dùng PUT để sửa. Xóa hồ sơ không xóa tài
khoản.

### 3.2. Quản lý tài khoản

Cả 3 endpoint cần AccountManagement: IT_ADMIN + iam.manage quản lý toàn hệ thống; MANAGER chỉ quản lý Front Desk cùng phường.

- **Method:** GET
  - **Route:** `/api/v1/accounts`
  - **Request / Công dụng:** Query `page=1&pageSize=20`; danh sách tài khoản
  - **Thành công:** `200 AccountPage`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/accounts/{userId}`
  - **Request / Công dụng:** Không body; xem một tài khoản
  - **Thành công:** `200 AccountDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/accounts/{userId}/status`
  - **Request / Công dụng:** `{ "isActive": false }` khóa; true mở lại
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `400/404/409`


`AccountDto`: `id, username, email, isActive, createdAt, wardId?`.
`AccountPage`: `{items: AccountDto[], page, pageSize, total}`; pageSize 1–100.

Không được tự khóa tài khoản đang dùng hoặc khóa IT_ADMIN hoạt động cuối cùng. Khóa tài khoản thu hồi refresh
token; mở lại không khôi phục các refresh token đã thu hồi. Không có ngày hết hạn khóa trong request này.

### 3.3. Quản lý RBAC

Các API RBAC cần tài khoản hiện tại còn hoạt động, có role **IT_ADMIN** và quyền **iam.manage** theo dữ liệu
IAM.

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles`
  - **Request / Công dụng:** Query `page=1&pageSize=20`
  - **Thành công:** `200 RbacPage<RoleDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** Xem role
  - **Thành công:** `200 RoleDto`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/rbac/roles`
  - **Request / Công dụng:** RoleInput; tạo role tùy chỉnh
  - **Thành công:** `201 RoleDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** RoleInput; sửa role
  - **Thành công:** `200 RoleDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/roles/{roleId}`
  - **Request / Công dụng:** Xóa role tùy chỉnh
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/permissions`
  - **Request / Công dụng:** Danh sách quyền có sẵn
  - **Thành công:** `200 PermissionDto[]`
  - **Lỗi nghiệp vụ:** —

- **Method:** GET
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions`
  - **Request / Công dụng:** Quyền của role
  - **Thành công:** `200 PermissionDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}`
  - **Request / Công dụng:** Không body; gán quyền
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/roles/{roleId}/permissions/{permissionId}`
  - **Request / Công dụng:** Không body; gỡ quyền
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/users/{userId}/roles`
  - **Request / Công dụng:** Role của tài khoản
  - **Thành công:** `200 RoleDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/rbac/users/{userId}/roles/{roleId}`
  - **Request / Công dụng:** Không body; gán role
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** DELETE
  - **Route:** `/api/v1/rbac/users/{userId}/roles/{roleId}`
  - **Request / Công dụng:** Không body; gỡ role
  - **Thành công:** `204`
  - **Lỗi nghiệp vụ:** `404/409`

- **Method:** GET
  - **Route:** `/api/v1/rbac/audit-logs`
  - **Request / Công dụng:** Query `page=1&pageSize=20`; lịch sử thao tác
  - **Thành công:** `200 RbacPage<AuditDto>`
  - **Lỗi nghiệp vụ:** `400`


- RoleInput: `{ "roleName": "CUSTOM_OFFICER", "description": "Vai trò kiểm thử" }`. Tên tối đa 50, bắt đầu
  bằng chữ ASCII, chỉ chữ/số/underscore; chuẩn hóa hoa. Description tùy chọn tối đa 2000.
- RoleDto: `id, roleName, description?, isSystem, permissions: PermissionDto[]`.
- PermissionDto: `id, permissionCode, permissionName, module`.
- RbacPage: `items, page, pageSize, total`; pageSize 1–100.
- AuditDto: `id, actorUserId, action, targetUserId?, roleId?, permissionId?, details, createdAt`; `details`
  là **chuỗi JSON**, không phải object trực tiếp.

Năm role hệ thống: `REGISTERED_CITIZEN`, `FRONT_DESK_OFFICER`, `MANAGER`, `PROCEDURE_MANAGER`, `IT_ADMIN`
. Không đổi tên/xóa role hệ thống. Xóa role tùy chỉnh cần gỡ các liên kết trước. Không gỡ `iam.manage` khỏi
IT_ADMIN hoặc làm mất IT_ADMIN hoạt động cuối cùng. Gán lại liên kết đã có không tạo bản sao; gỡ liên kết
không có có thể trả 204 khi các đối tượng vẫn tồn tại.

Luồng cấp quyền: tài khoản đăng ký → IT_ADMIN lấy userId, roleId từ API danh sách → PUT gán role → tài khoản
nhận access token mới để dùng quyền ở service khác. IAM kiểm tra quyền hiện tại trong DB; Procedure dùng role
trong JWT, nên token cũ có thể chưa phản ánh thay đổi role. Chưa có API tạo/xóa Permission tùy ý hoặc API
bootstrap IT_ADMIN đầu tiên.

## 4. Procedure: tra cứu và quản trị thủ tục

### 4.1. Danh mục endpoint

Quyền **ProcedureManager** trong bảng nghĩa là Bearer chứa `PROCEDURE_MANAGER` hoặc `IT_ADMIN`. Role
`MANAGER` đơn thuần không đáp ứng policy này.

- **Method:** GET
  - **Route:** `/api/v1/procedures`
  - **Quyền / Request:** Public; bộ lọc và phân trang bên dưới
  - **Thành công:** `200 PagedResult<ProcedureSummaryDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** GET
  - **Route:** `/api/v1/procedures/{id}`
  - **Quyền / Request:** Public; chỉ thủ tục active
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `404` nếu thiếu/inactive

- **Method:** GET
  - **Route:** `/api/v1/procedures/{id}/source`
  - **Quyền / Request:** Public; lấy link PDF gốc từ bản nháp đã xuất bản
  - **Thành công:** `200 {url,expiresInSeconds}`
  - **Lỗi nghiệp vụ:** `404/503`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/procedures`
  - **Quyền / Request:** ProcedureManager; bộ lọc quản trị
  - **Thành công:** `200 PagedResult<ProcedureManagerSummaryDto>`
  - **Lỗi nghiệp vụ:** `400`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/procedures`
  - **Quyền / Request:** ProcedureManager; ProcedureInput
  - **Thành công:** `201 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `400/409`

- **Method:** PUT
  - **Route:** `/api/v1/procedure-manager/procedures/{id}`
  - **Quyền / Request:** ProcedureManager; UpdateProcedureInput
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi nghiệp vụ:** `400/404/409`

- **Method:** PATCH
  - **Route:** `/api/v1/procedure-manager/procedures/{id}/status`
  - **Quyền / Request:** ProcedureManager; `{isActive,reason?}`
  - **Thành công:** `200 ProcedureStatusDto`
  - **Lỗi nghiệp vụ:** `400/404`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/procedures/{id}/versions`
  - **Quyền / Request:** ProcedureManager; không body
  - **Thành công:** `200 ProcedureVersionDto[]`
  - **Lỗi nghiệp vụ:** `404`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/procedures/publish`
  - **Quyền / Request:** ProcedureManager; ReviewedProcedureInput
  - **Thành công:** `200 ProcedureDetailDto` khi thêm hoặc cập nhật
  - **Lỗi nghiệp vụ:** `400/409`


### 4.2. Tìm kiếm và phân trang

- **Query parameter:** `keyword`
  - **Mặc định / Ý nghĩa:** Tùy chọn, tối đa 255; tìm mã/tên, hỗ trợ không phân biệt hoa thường và dấu

- **Query parameter:** `categoryId`
  - **Mặc định / Ý nghĩa:** Tùy chọn; số nguyên dương

- **Query parameter:** `levelOfImplementation`
  - **Mặc định / Ý nghĩa:** Tùy chọn, tối đa 50; lọc cấp thực hiện. Không truyền thì ưu tiên cấp Xã/Phường,
    **không loại các cấp khác**

- **Query parameter:** `pageNumber`
  - **Mặc định / Ý nghĩa:** 1

- **Query parameter:** `pageSize`
  - **Mặc định / Ý nghĩa:** 10; tối đa 50 public, 100 manager

- **Query parameter:** `sortBy`
  - **Mặc định / Ý nghĩa:** `Title`; nhận `Title`, `ProcedureCode`, `UpdatedAt`, `CreatedAt`,
    `LevelOfImplementation`

- **Query parameter:** `isAscending`
  - **Mặc định / Ý nghĩa:** true

- **Query parameter:** `isActive`
  - **Mặc định / Ý nghĩa:** Chỉ manager: true/false; bỏ qua để lấy cả hai. Public luôn ép true

- **Query parameter:** `page`, `search`
  - **Mặc định / Ý nghĩa:** Alias tương thích; page ưu tiên hơn pageNumber; search dùng khi keyword không được
    truyền


Ví dụ public: `/api/v1/procedures?keyword=ket%20hon&pageNumber=1&pageSize=10`.
Ví dụ quản trị thủ tục đã tắt: `/api/v1/procedure-manager/procedures?isActive=false&pageNumber=1&pageSize=20`.

PagedResult có cấu trúc:

```json
{
  "items": [],
  "currentPage": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0,
  "hasPrevious": false,
  "hasNext": false
}
```

ProcedureSummaryDto:
`id, procedureCode, title, categoryName, levelOfImplementation, feeSummary, processingTimeSummary, originalPdfUrl?, updatedAt`
.
ProcedureManagerSummaryDto bổ sung `isActive, versionCount, createdAt`. Danh sách không tải
ContentPayload/Checklist/FormDefinitions; dùng API chi tiết khi người dùng mở một thủ tục.

### 4.3. Payload tạo/cập nhật/xuất bản

Ví dụ dữ liệu **minh họa**, không đại diện quy định pháp luật. Thay categoryId bằng danh mục thực tế có trong
DB:

```json
{
  "categoryId": 1,
  "procedureCode": "DEMO.001",
  "title": "Thủ tục minh họa để kiểm thử",
  "issuingAuthority": "Cơ quan ban hành minh họa",
  "executingAgency": "Cơ quan tiếp nhận minh họa",
  "levelOfImplementation": "Cấp Xã",
  "targetAudience": "Công dân Việt Nam",
  "feeSummary": "Theo văn bản gốc",
  "processingTimeSummary": "Theo văn bản gốc",
  "originalPdfUrl": null,
  "pdfFileName": null,
  "contentPayload": {
    "decisionNumber": "DEMO/QD",
    "receivingAddress": "Địa điểm tiếp nhận minh họa",
    "submissionMethods": [{
      "methodName": "Trực tiếp", "feeAmount": 0, "feeUnit": "VND",
      "estimatedDays": 1, "note": "Số liệu kiểm thử"
    }],
    "legalReferences": [{
      "documentNumber": "DEMO/QD", "documentName": "Văn bản minh họa",
      "issueDate": "2026-01-01", "authority": "Cơ quan minh họa"
    }],
    "results": ["Kết quả minh họa"],
    "cases": [{
      "caseCode": "CASE_01", "caseName": "Trường hợp thông thường",
      "steps": [{
        "stepOrder": 1, "stepName": "Nộp hồ sơ", "executor": "Công dân",
        "actionDetails": "Chuẩn bị và gửi các giấy tờ theo danh sách."
      }]
    }]
  },
  "checklistSchema": [{
    "checklistId": "DOC_01", "caseCode": "CASE_01", "submissionType": "NOP",
    "itemName": "Tờ khai minh họa", "documentCopyType": "ORIGINAL",
    "quantity": 1, "conditionNote": null, "isMandatory": true
  }],
  "formDefinitions": [{
    "formTemplateId": null, "caseCode": "CASE_01", "formCode": "DEMO_FORM",
    "formName": "Tờ khai minh họa", "formType": "DOCX_TEMPLATE",
    "quantity": 1, "isMandatory": true
  }]
}
```

Đây là cấu trúc ProcedureInput/ReviewedProcedureInput. Khi PUT cập nhật, gửi toàn bộ dữ liệu mới, bổ sung **ở
cấp ngoài cùng** `decisionNumber` và `effectiveDate`, ví dụ `"effectiveDate": "2026-10-04"`. EffectiveDate
ghi metadata phiên bản, không tự lên lịch xuất bản.

Các giới hạn chính:

- categoryId phải tồn tại; procedureCode bắt buộc/tối đa 50, duy nhất; title từ 10 đến 500.
- issuingAuthority tối đa 255; levelOfImplementation 50; targetAudience, feeSummary, processingTimeSummary
  255. Gửi đầy đủ các trường bắt buộc khi xuất bản.
- originalPdfUrl nullable, HTTPS URL tuyệt đối không có userinfo, tối đa 500; pdfFileName nullable/tối đa 255.
  URL lưu không mặc nhiên là URL công khai tải được.
- ContentPayload gồm decisionNumber, receivingAddress, submissionMethods, legalReferences, results, cases. Mỗi
  case có nhiều steps; không cần ép mọi thủ tục vào một trường hợp duy nhất.
- feeAmount/estimatedDays không âm; stepOrder dương. Checklist/form quantity dương.
- submissionType: `NOP` hoặc `XUAT_TRINH`; documentCopyType: `ORIGINAL`, `CERTIFIED_COPY`, `REGULAR_COPY`.
- formType: `ONLINE_INTERACTIVE` hoặc `DOCX_TEMPLATE`. formTemplateId là ID tham chiếu Document Form, không
  phải khóa ngoại xuyên database.

ProcedureDetailDto gồm toàn bộ trường dữ liệu trên và `id, categoryName, isActive, createdAt, updatedAt`.
ProcedureStatusDto: `id, isActive, reason?, updatedAt`.
ProcedureVersionDto:
`id, versionNumber, decisionNumber?, effectiveDate, snapshotData, createdAt, originalPdfUrl?, pdfFileName?`;
snapshotData là **object JSON**. Danh sách version trả thứ tự phiên bản giảm dần.

### 4.4. Chọn API phù hợp

- **POST tạo**: yêu cầu mã chưa có, tạo active và version 1; trùng mã trả lỗi.
- **PUT cập nhật**: cập nhật theo ID; lưu snapshot trạng thái trước sửa vào version tiếp theo. Version 1 là
  trạng thái khởi tạo nên snapshot version 2 sau lần sửa đầu có thể giống version 1.
- **POST publish**: upsert theo procedureCode, thêm mới hoặc lưu snapshot cũ rồi cập nhật trong transaction.
  Thủ tục đã có giữ trạng thái active/inactive hiện tại; dùng PATCH status nếu cần mở lại. Gọi lặp có thể sinh
  thêm version, không coi API này là idempotent.
- **PATCH status**: bật/tắt tiếp nhận bằng isActive; reason tùy chọn. Không xóa dữ liệu.
- **Nguồn PDF public**: trả SAS URL có thời hạn 600 giây, chỉ khi thủ tục active và có bản nháp Published khớp
  nguồn PDF hiện tại. PDF nhập tay chỉ có URL mà không có bản nháp tương ứng có thể trả 404. SAS đã cấp còn
  hiệu lực đến lúc hết hạn.

Hiện chưa có DELETE thủ tục. Đã bổ sung CRUD lĩnh vực và GET chi tiết quản trị (API 66, 68–70). Không dùng
public detail để xem thủ tục inactive vì sẽ nhận 404.

## 5. Procedure: PDF, bản nháp và đối soát

Các endpoint draft yêu cầu ProcedureManager; DELETE mới xem API 67, nguồn PDF lịch sử xem API 73. Backend cung cấp dữ liệu cho màn hình đối soát; không có màn
hình FE Split-view trong backend.

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/extract-preview`
  - **Request / Công dụng:** Multipart `file`; đọc thử PDF, không lưu DB/Blob
  - **Thành công:** `200 ExtractionResult`
  - **Lỗi điển hình:** `400/413/502/503/504`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts`
  - **Request / Công dụng:** Multipart `file`; lưu PDF và tạo draft
  - **Thành công:** `202 DraftDto`, Location tới draft
  - **Lỗi điển hình:** `400/413/503`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts`
  - **Request / Công dụng:** Query `page=1&pageSize=10`
  - **Thành công:** `200 DraftSummaryDto[]`
  - **Lỗi điển hình:** `400`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts/{id}`
  - **Request / Công dụng:** Lấy dữ liệu đối soát
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `404`

- **Method:** PUT
  - **Route:** `/api/v1/procedure-manager/drafts/{id}`
  - **Request / Công dụng:** `{revision,payload}`; lưu phần đã chỉnh sửa
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `400/404/409`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/retry`
  - **Request / Công dụng:** `{revision}`; chạy lại draft Failed
  - **Thành công:** `200 DraftDto`
  - **Lỗi điển hình:** `404/409/503`

- **Method:** POST
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/publish`
  - **Request / Công dụng:** `{revision,confirmed:true}`
  - **Thành công:** `200 ProcedureDetailDto`
  - **Lỗi điển hình:** `400/404/409`

- **Method:** GET
  - **Route:** `/api/v1/procedure-manager/drafts/{id}/source`
  - **Request / Công dụng:** Link PDF phục vụ đối soát
  - **Thành công:** `200 {url,expiresInSeconds:600}`
  - **Lỗi điển hình:** `404/503`


PDF phải có đuôi .pdf và nội dung PDF hợp lệ, tối đa 20 MiB. Preview cần kết nối AIOCR nhưng không cần Blob;
upload draft cần Blob. Danh sách draft trả **mảng trực tiếp**, không có totalCount; pageSize tối đa 50.

### 5.1. DTO và trạng thái

- ExtractionResult: `payload` (object dữ liệu thủ tục gợi ý), `extractedText` (text gốc), `warnings` (mảng
  chuỗi).
- DraftDto:
  `id, status, pdfFileName, payload, warnings, extractedText, failureCode?, revision, publishedProcedureId?, createdAt, updatedAt`
  .
- DraftSummaryDto:
  `id, status, pdfFileName, failureCode?, revision, publishedProcedureId?, createdAt, updatedAt`.
- status: `Queued` → `Processing` → `NeedsReview` hoặc `Failed`; sau xác nhận thành công là `Published`.
- revision là GUID kiểm soát sửa đồng thời. Luôn gửi revision mới nhất từ GET/PUT; gặp 409 cần tải lại và đối
  chiếu trước khi gửi tiếp.
- PUT payload có giới hạn 500.000 ký tự JSON và request tối đa 1 MiB. Có thể chỉnh draft NeedsReview/Failed;
  không sửa draft đang xử lý hoặc đã xuất bản.

### 5.2. Luồng đọc PDF thuận tiện

**Đọc thử không lưu:** chọn file → POST extract-preview → hiển thị extractedText và warnings → người dùng sửa
payload. Nếu AI tắt, vẫn có thể nhận text PDF nhưng payload chưa được điền tự động. Preview không sinh
draftId, không lưu PDF gốc.

**Lưu và xuất bản có PDF gốc:** POST drafts → lưu draftId → GET draft cho đến trạng thái cần đối soát → GET
source hiển thị PDF → PUT payload đã sửa kèm revision → POST draft publish kèm revision mới và confirmed=true.
Xuất bản cập nhật procedure/version và trạng thái draft trong transaction. PDF metadata được backend lấy từ
nguồn draft; không tin URL tùy ý FE gửi trong payload. Draft đã Published không xuất bản lần hai.

**Chế độ thủ công:** nếu `ProcedureDrafts:ExtractionEnabled=false`, upload đi vào NeedsReview với payload chờ
nhập tay. Có thể đọc thử PDF qua preview riêng rồi điền dữ liệu để đối soát. Chỉ có text không đồng nghĩa toàn
bộ trường nghiệp vụ đã được tự động điền chính xác.

**Chế độ tự động:** cấu hình AIOCR URL/service key và bật ExtractionEnabled. AIOCR ưu tiên trích chữ có sẵn
trong PDF; OCR chỉ dành cho trang scan/thiếu text khi được bật. AI ánh xạ text thành payload là bước riêng.
Người quản lý vẫn phải xem warnings và đối soát trước publish.

Ví dụ upload bằng FE:

```javascript
const data = new FormData();
data.append('file', selectedPdfFile);
const response = await fetch(`${apiBase}/api/v1/procedure-manager/drafts`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${accessToken}` },
  body: data
});
if (!response.ok) throw await response.json();
const draft = await response.json();
// Giữ draft.id và draft.revision để gọi các bước đối soát tiếp theo.
```

FE không nhận hoặc gửi X-Service-Key. Không lưu SAS URL lâu dài; gọi lại source khi URL hết hạn.
Source/preview được trả với chỉ dẫn không cache.

## 6. Document Form: FE tích hợp editor DOCX

Base local qua Gateway: `http://localhost:5000/api/document-form`.
Swagger: `http://localhost:5000/api/document-form/swagger/index.html`.
Các route API bên dưới nối tiếp base bằng `/api/v1/...`.

Các bước FE dưới đây mô tả luồng DOCX; quyền GET mẫu hiện giới hạn PROCEDURE_MANAGER.
Không sử dụng token admin để chạy trên màn hình công dân.

1. **Admin / Quản lý mẫu:** POST tạo code/title → upload-docx multipart → GET chi tiết thấy onlineReady=true.
2. **Chi tiết thủ tục:** service Procedure trả thành phần giấy tờ có templateId; FE hiển thị nút “Điền đơn”.
3. **Mở đơn mới:** GET template, GET template/download-docx → đọc arrayBuffer/blob → editor import DOCX.
   Đây là tải dữ liệu vào ứng dụng web, không bắt người dân tự tải file rồi mở Word desktop.
4. **Lưu nháp:** editor export DOCX → POST draft multipart; giữ submissionId từ response.
5. **Sửa tiếp:** GET submission metadata + GET submission/download-docx → import editor; PUT draft multipart khi lưu.
6. **Hoàn thành:** chờ PUT/POST lưu nháp thành công → POST submit. FE hiển thị “Hoàn thành đơn”, editor read-only.
   submissionId gắn vào thành phần hồ sơ ở Workflow; giấy tờ khác vẫn thuộc luồng riêng.

Ví dụ adapter FE (editor.importDocx/exportDocx là hàm minh họa, thay bằng API của editor đã chọn):

```javascript
const base = "http://localhost:5000/api/document-form";
async function checked(response) {
  if (!response.ok) throw new Error(await response.text());
  return response;
}
async function openOriginal(templateId, editor) {
  const response = await checked(await fetch(`${base}/api/v1/form-templates/${templateId}/download-docx`));
  await editor.importDocx(await response.arrayBuffer());
}
async function saveDraft(editor, templateId, applicantId, submissionId) {
  const data = new FormData();
  data.append("applicantId", applicantId);
  if (!submissionId) data.append("templateId", templateId);
  data.append("file", await editor.exportDocx(), "don-da-dien.docx");
  const route = submissionId ? `${submissionId}/draft` : "draft";
  const response = await checked(await fetch(`${base}/api/v1/citizen/submissions/${route}`, {
    method: submissionId ? "PUT" : "POST", body: data
  })); // Không tự đặt Content-Type: trình duyệt thêm multipart boundary.
  return response.json();
}
Bộ kiểm thử tự động trong mục này đã được gỡ theo yêu cầu chủ dự án.json
{
  "title": "Dữ liệu không hợp lệ.",
  "status": 400,
  "instance": "/api/v1/procedure-manager/procedures",
  "code": "validation.failed",
  "traceId": "example-trace",
  "errors": { "Title": ["Tên thủ tục không hợp lệ."] }
}
```

Không dựa vào nội dung câu thông báo để điều khiển FE; ưu tiên HTTP status và code. IAM dùng
`validation_failed`, Procedure dùng `validation.failed` ở luồng validation nghiệp vụ. Các lỗi thường gặp:
`iam.csrf_rejected`, `iam.profile_not_found`, `iam.profile_exists`, `iam.self_disable`, `iam.last_admin`,
`procedure.not_found`, `procedure.category_not_found`, `procedure.code_exists`.

DocumentForm hiện có lỗi nghiệp vụ dạng `{code,message}`, lỗi model validation là ValidationProblemDetails;
không phải mọi lỗi ở mọi service đều cùng cấu trúc. FE nên hỗ trợ cả `title`, `message`, `errors`, và
response không có JSON nếu lỗi từ proxy.

- **HTTP:** 400
  - **Hành động phù hợp:** Hiển thị lỗi theo trường, sửa dữ liệu

- **HTTP:** 401
  - **Hành động phù hợp:** Thử refresh một lần theo luồng IAM; thất bại thì yêu cầu đăng nhập

- **HTTP:** 403
  - **Hành động phù hợp:** Kiểm tra quyền; riêng auth kiểm tra CSRF/Origin

- **HTTP:** 404
  - **Hành động phù hợp:** Đối tượng không tồn tại hoặc bị ẩn khỏi public

- **HTTP:** 409
  - **Hành động phù hợp:** Trùng mã, trạng thái không cho phép hoặc revision cũ; tải lại trước khi sửa

- **HTTP:** 413
  - **Hành động phù hợp:** Giảm kích thước upload/request

- **HTTP:** 502/503/504
  - **Hành động phù hợp:** Kiểm tra dịch vụ phụ thuộc/cấu hình/timeout; giữ dữ liệu người dùng đang chỉnh


### 9.2. Trình tự thử trên Swagger/Postman

1. Chạy service và dependency cần dùng; mở Swagger của đúng service. Cổng trong mục 1 là cấu hình, không khẳng
định tiến trình đang chạy.
2. IAM: register → login với X-CSRF-Protection → giữ cookie trong trình duyệt/cookie jar → Authorize access
token → GET me/profile. Khi gọi bằng Postman, bật lưu/gửi cookie đúng host/path.
3. IT_ADMIN: GET accounts/roles → gán PROCEDURE_MANAGER cho tài khoản kiểm thử → lấy token mới. Không có tài
khoản IT_ADMIN thì cần người vận hành cấp tài khoản ban đầu.
4. Procedure public: GET danh sách → GET chi tiết. Procedure manager: tạo/publish payload → kiểm tra versions
→ tắt status → xác nhận public không còn trả thủ tục đó nhưng manager vẫn thấy.
5. PDF: dùng extract-preview để kiểm tra đọc chữ; dùng drafts nếu cần lưu PDF, đối soát và xuất bản có nguồn
gốc. Chỉ nhấn publish khi dữ liệu được kiểm tra đầy đủ.
6. DocumentForm: tạo template → upload DOCX nguyên bản → FE import/export DOCX → lưu file nháp → chốt đơn. Xem mục 3 và phụ lục 6 cho hợp đồng online mới.

### 9.3. Đối chiếu mã nguồn khi API thay đổi

Nguồn chính là các thư mục `Controllers` và `Program.cs` của từng API, DTO/validator tầng Application, cấu
hình YARP và security IAM. Tài liệu này không thay đổi endpoint hoặc quyền. Khi thêm API, cần bổ sung
method/route, policy, DTO, status và luồng sử dụng tại đây để FE có một đầu mối tra cứu.


### Khắc phục lỗi DocumentForm local (07/10/2026)

Compose `docker/docker-compose.yml` dùng database riêng `wardmate_documentform_db`. Nếu log có
`created_at_utc does not exist` hoặc migration báo `application_forms already exists`, container đang
trỏ vào schema cũ trong `wardmate_db`; không xóa bảng hay ghi giả lịch sử migration để bỏ qua lỗi.
Khởi tạo database riêng, áp migration chuẩn và giữ database cũ để đối chiếu. Azurite chạy ngoài container
API nên không dùng loopback của API; mặc định Docker Desktop dùng DevelopmentStorageProxyUri
`http://host.docker.internal`. Có thể override bằng AZURE_BLOB_CONNECTION_STRING.

Luồng editor mới không dùng checksum/schema cũ. Không cần cấu hình online-config trước khi tạo draft.
Tên file có dấu/khoảng trắng được giải mã URL khi tải lại từ Blob. Backend lưu/tải nguyên bytes DOCX
đã nhận, không thêm trang formData và không ghi đè mẫu khi người dân lưu đơn.
## Bổ sung RBAC — Danh mục chức năng đã deploy (07/10/2026)

Database IAM deploy đã được bổ sung 27 permission bằng migration DeployedFeaturePermissionCatalog,
đưa tổng số permission lên 30. Không xóa/sửa ba permission cũ hoặc thu hồi các role-permission đã có.
Không tạo quyền cho các chức năng chưa xác nhận deploy như Analytics, SMS, RAG hoặc xác nhận sẵn sàng toàn hồ sơ.

Admin gọi GET /api/v1/rbac/permissions để lấy danh mục, GET /api/v1/rbac/roles/{roleId}/permissions
để xem quyền của role. Dùng các API gán/gỡ permission hiện có; không đoán permissionId, lấy ID từ response.
Tên quyền mới là tiếng Việt, module là IAM, ProcedureCatalog hoặc DocumentForm.

**Quan trọng về hiệu lực:** Migration này cập nhật danh mục quyền và phân quyền mặc định trong IAM,
không tự đổi authorization của các service. IAM vẫn dùng policy hiện có và phạm vi phường ở backend.
Procedure hiện dùng role PROCEDURE_MANAGER/IT_ADMIN; gỡ một permission procedure.* chưa chặn riêng endpoint.
DocumentForm đang nhận applicantId từ request và chưa tích hợp JWT theo code được kiểm tra; các permission
DocumentForm mới chưa khắc phục vấn đề đó. Không dùng việc ẩn menu ở FE thay cho bảo vệ API.

Access token đã phát hành không được sửa nội dung. Đăng nhập lại hoặc refresh qua IAM để nhận claim
permissions cập nhật; GET /api/v1/users/me dùng để tải lại thông tin quyền cho giao diện.
Dữ liệu public như danh sách thủ tục/danh mục vẫn là public, không yêu cầu thêm permission.
Quyền document.submissions.* mô tả thao tác trên đơn của chính mình, không cấp quyền xem đơn người khác.

Danh mục bổ sung và role được gán mặc định:
- `iam.accounts.read` — Xem tài khoản và hồ sơ trong phạm vi quản lý.
  Role: IT_ADMIN, MANAGER.

- `iam.accounts.manage` — Quản lý tài khoản và hồ sơ trong phạm vi được phép.
  Role: IT_ADMIN, MANAGER.

- `iam.wards.read` — Xem phường trong phạm vi quản lý.
  Role: IT_ADMIN, MANAGER.

- `iam.wards.manage` — Tạo phường và phân công phường công tác.
  Role: IT_ADMIN.

- `iam.rbac.manage` — Quản trị vai trò và quyền toàn hệ thống.
  Role: IT_ADMIN.

- `iam.audit.read` — Xem nhật ký phân quyền.
  Role: IT_ADMIN.

- `procedure.read` — Xem toàn bộ thủ tục, kể cả đã ngừng công khai.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.create` — Tạo thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.update` — Cập nhật nội dung thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.publish` — Xuất bản dữ liệu thủ tục đã đối soát.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.status` — Bật hoặc tắt thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.versions.read` — Xem lịch sử phiên bản thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.rollback` — Khôi phục phiên bản thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.source.read` — Đọc PDF gốc của phiên bản lịch sử.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.categories.manage` — Thêm sửa xóa danh mục thủ tục.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.read` — Xem bản nháp và PDF đối soát.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.upload` — Tải PDF tạo bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.update` — Sửa dữ liệu bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.extract` — Đọc thử PDF và thử trích xuất lại.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.publish` — Xác nhận và xuất bản bản nháp.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `procedure.drafts.delete` — Xóa bản nháp chưa xuất bản.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `document.templates.read` — Tra cứu và tải biểu mẫu gốc.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.templates.manage` — Tạo biểu mẫu và tải DOCX gốc.
  Role: IT_ADMIN, PROCEDURE_MANAGER.

- `document.submissions.read` — Xem đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.write` — Tạo và sửa nháp đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.submit` — Chốt đơn điện tử của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.

- `document.submissions.download` — Tải DOCX đã điền của chính mình.
  Role: IT_ADMIN, PROCEDURE_MANAGER, MANAGER, FRONT_DESK_OFFICER, REGISTERED_CITIZEN.


## Application Workflow — TASK-11: thẩm định, yêu cầu sửa và nộp lại

Swagger local: `http://localhost:5003/swagger` khi service chạy ở Development.
Gateway chuyển tiếp nguyên route `/api/v1/applications/...` và
`/api/v1/officer/applications/...` đến Workflow.
Tất cả API dưới đây dùng `Authorization: Bearer <accessToken>` của IAM.

### Luồng sử dụng

1. Công dân tạo hồ sơ, hoàn tất checklist rồi gọi API `submit` hiện có.
2. Lần nộp đầu tạo snapshot version 1 gồm form và toàn bộ checklist/file URL.
3. Cán bộ Một cửa mở danh sách chờ và tự tiếp nhận bằng `assign`.
4. Cán bộ xem version, tạo các nhận xét cụ thể, rồi gọi `request-revision`.
5. Công dân đọc nhận xét, chỉnh dữ liệu trên FE và gửi toàn bộ dữ liệu qua `resubmit`.
6. Backend tạo version kế tiếp, đóng nhận xét của phiên bản trước, chuyển về SUBMITTED.
   Cán bộ cũ tiếp nhận lại và dùng `diff` để đối chiếu.

Phạm vi quyền: các API `/officer/...` yêu cầu role `FRONT_DESK_OFFICER`.
Cán bộ không được tự thẩm định hồ sơ của chính mình, không nhận hồ sơ đang thuộc cán bộ khác.
API đọc comments/versions/diff chỉ cho chủ hồ sơ hoặc cán bộ đang thụ lý.
IT_ADMIN/MANAGER không tự động có quyền thẩm định trong task này.
JWT role được kiểm tra tại service; task này không thêm permission vào IAM deploy.

Hiện Application chưa có phường tiếp nhận và JWT chưa cấp phạm vi phường cho luồng này.
Danh sách chờ gồm hồ sơ chưa phân công và hồ sơ của chính cán bộ, chưa chia theo phường.
Không được hiểu đây là cơ chế cách ly hồ sơ giữa các phường đã hoàn chỉnh.

### Thay đổi tương thích của API hồ sơ hiện có

- `POST /api/v1/applications`: vẫn nhận `{procedureId, caseCode?, formData}` và trả 201.
  Theo yêu cầu ApplicationCode NOT NULL, mã HS nay được cấp ngay khi tạo DRAFT.
  Mã không đổi khi submit/nộp lại; có thể có khoảng trống trong dãy mã.
- `POST /api/v1/applications/{id}/submit`: vẫn trả 200, yêu cầu DRAFT và checklist
  bắt buộc COMPLETED; nay lưu snapshot version 1 cùng transaction với lịch sử.
- `GET /api/v1/applications/{id}`: chủ hồ sơ đọc được `assignedOfficerId`,
  `resubmitCount`, `currentVersionNumber`, `notes`, `documentsReadyAt` bổ sung.
  DRAFT có currentVersionNumber = 0. Không chuyển form/checklist thành dữ liệu dùng chung.
- API PATCH checklist cũ vẫn chỉ sửa DRAFT. Khi NEED_REVISION, FE gửi dữ liệu sửa
  bằng resubmit để form và checklist được kiểm tra/lưu cùng nhau.
- `userId` hiện có chính là CitizenId trong đặc tả; `history[].changedBy` là người
  chuyển trạng thái. Giữ tên cũ để không phá hợp đồng FE.

### GET /api/v1/officer/applications/pending

**Dùng để làm gì:** danh sách công việc chờ của cán bộ, hồ sơ nộp lâu nhất đứng trước.

**Cách gọi:** không có body. Query tùy chọn `status=SUBMITTED` hoặc `UNDER_REVIEW`,
`procedureId`, `submittedFrom`, `submittedTo` (ISO 8601 có múi giờ), `page=1`, `pageSize=20`.
Không truyền status lấy cả hai. Khoảng thời gian gồm hai đầu mút; nộp lại dùng thời điểm nộp mới.

**Kết quả:** 200 `{items, page, pageSize, total}`; items là thông tin gọn, không có form/file.
Trang rỗng vẫn 200. `pageSize` 1–100, page 1–1000000.

**Dùng sai:** 400 nếu status ngoài hai giá trị, phân trang sai hoặc ngày bắt đầu sau ngày kết thúc.
401 cần đăng nhập lại; 403 cần đúng role. Không dùng API này để lấy hồ sơ đã duyệt.

### POST /api/v1/officer/applications/{id}/assign

**Dùng để làm gì:** cán bộ tự nhận xử lý một hồ sơ SUBMITTED, chuyển sang UNDER_REVIEW.

**Cách gọi:** không body; cán bộ được lấy từ `sub` của JWT, không nhận officerId từ FE.

**Kết quả:** 200 `ReviewResultDto`, gồm `application`, `assignedOfficerId`,
`resubmitCount`, `currentVersionNumber`. Ghi lịch sử chuyển trạng thái cùng transaction.

**Dùng sai:** 404 không có hồ sơ; 403 tự nhận hồ sơ của mình/thiếu role;
409 hồ sơ đã được nhận, đang trạng thái khác hoặc không có snapshot hợp lệ.
Tải lại danh sách khi 409. Hai cán bộ nhận đồng thời chỉ một người thành công.

### POST /api/v1/officer/applications/{id}/comments

**Dùng để làm gì:** đánh dấu chính xác ô nhập hoặc giấy tờ cần sửa trên version hiện tại.

**Cách gọi:** cán bộ đang thụ lý hồ sơ UNDER_REVIEW gửi:

```json
{
  "versionNumber": 1,
  "targetType": "FORM_FIELD",
  "targetId": "/formData/fullName",
  "fieldLabel": "Họ và tên",
  "commentText": "Vui lòng nhập họ tên đúng theo giấy tờ."
}
```

FORM_FIELD dùng đường dẫn JSON Pointer: `/formData/address/city`,
`/formData/children/0/name`. Ký tự `/` trong tên key viết `~1`, `~` viết `~0`.
Đích phải tồn tại trong snapshot và là giá trị lá (hoặc object/array rỗng).
CHECKLIST_ITEM dùng UUID dòng checklist từ `application.checklists[].id`, không dùng code giấy tờ.
TargetId tối đa 100 ký tự; label 255; commentText 4000, đều không rỗng.

**Kết quả:** 200 ReviewResultDto; nhận xét OPEN gắn với phiên bản hiện tại.
Gọi GET comments để lấy id/nội dung nhận xét vừa tạo.

**Dùng sai:** 400 ô/giấy tờ không tồn tại hoặc nội dung sai;
403 không phải cán bộ thụ lý; 409 trạng thái/version đã thay đổi. Tải lại versions trước khi bắt lỗi.
Không dùng version cũ để đánh dấu dữ liệu mới.

### POST /api/v1/officer/applications/{id}/request-revision

**Dùng để làm gì:** gửi yêu cầu chỉnh sửa sau khi đã tạo nhận xét chi tiết.

**Cách gọi:** cán bộ thụ lý gửi `{"reason":"Vui lòng bổ sung giấy tờ và sửa các ô đã đánh dấu."}`.
Reason bắt buộc, tối đa 4000 ký tự.

**Kết quả:** 200 ReviewResultDto, NEED_REVISION; reason lưu vào notes và lịch sử.

**Dùng sai:** 409 nếu chưa có nhận xét OPEN của version hiện tại hoặc không còn UNDER_REVIEW.
Tạo nhận xét trước. 403 nếu người gọi không thụ lý hồ sơ; 400 nếu lý do rỗng.

### POST /api/v1/applications/{id}/resubmit

**Dùng để làm gì:** chủ hồ sơ gửi dữ liệu đã sửa trong một lần nộp lại.

**Cách gọi:** chỉ ở NEED_REVISION. Gửi toàn bộ formData mới và mọi dòng checklist,
bao gồm cả dòng tùy chọn và dòng không đổi:

```json
{
  "expectedVersionNumber": 1,
  "formData": { "fullName": "Nguyễn Văn A" },
  "checklists": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "status": "COMPLETED",
      "fileUrl": "https://example.invalid/giay-to-da-sua.pdf",
      "note": "Đã thay giấy tờ"
    }
  ]
}
```

Dùng id thật của hồ sơ; ví dụ trên chỉ minh họa. Status PENDING hoặc COMPLETED.
FileUrl HTTPS tối đa 500 ký tự, hoặc null để bỏ liên kết; note tối đa 4000 ký tự.
FormData là object tối đa 64 KiB. Checklist rỗng `[]` chỉ khi hồ sơ không có checklist.
Không thể thay đổi tiêu đề, tính bắt buộc hay thêm/bỏ checklist trong API này.

**Kết quả:** 200 ReviewResultDto. ResubmitCount tăng một; snapshot version kế tiếp
được tạo; nhận xét OPEN của version trước thành RESOLVED; hồ sơ về SUBMITTED,
giữ cán bộ cũ và mã HS. RESOLVED có nghĩa đã nộp lại, chưa phải cán bộ duyệt nội dung sửa.
Toàn bộ thao tác cùng transaction, không có trường hợp lưu nửa chừng.

**Dùng sai:** 404 hồ sơ không thuộc bạn; 409 sai trạng thái hoặc expectedVersionNumber cũ;
400 thiếu/trùng checklist, form không đúng hoặc URL sai; 422 checklist bắt buộc chưa COMPLETED,
response `missingItems` chỉ ra giấy tờ cần hoàn tất. Không được chỉ gửi các ô đã sửa:
các ô bị bỏ khỏi formData sẽ được xem là bị xóa và hiển thị trong diff.

API chưa kiểm chứng nội dung file hay liên kết DocumentForm submission; COMPLETED là trạng thái
đã khai báo. Không xem đây là bằng chứng giấy tờ đã được xác thực.

### GET /api/v1/applications/{id}/comments

**Dùng để làm gì:** công dân biết cần sửa gì; cán bộ xem các nhận xét của mình trên hồ sơ.

**Cách gọi:** không body. Query `versionNumber=1`, `status=OPEN` hoặc `RESOLVED` tùy chọn.
Không truyền bộ lọc sẽ nhận toàn bộ nhận xét của hồ sơ.

**Kết quả:** 200 mảng CommentDto: id, applicationVersionId, versionNumber, officerId,
targetType, targetId, fieldLabel, commentText, status, createdAt, updatedAt.
Không có nhận xét phù hợp trả `[]`.

**Dùng sai:** 404 ngoài phạm vi truy cập hoặc version không thuộc hồ sơ;
400 số phiên bản/status sai. Nhận xét cũ vẫn còn để đối chiếu, không bị xóa khi nộp lại.

### GET /api/v1/applications/{id}/versions

**Dùng để làm gì:** lấy dữ liệu đóng băng cho màn hình xem hồ sơ và so sánh các lần nộp.

**Cách gọi:** không body; chủ hồ sơ hoặc cán bộ thụ lý. Cán bộ cần assign trước khi đọc.

**Kết quả:** 200 mảng VersionDto tăng dần theo versionNumber, gồm id, versionNumber,
submittedBy, submittedAt và snapshotData (thông tin hồ sơ, formData, checklists/file URL).
DRAFT chưa nộp trả `[]`. Snapshot không đổi khi các lần nộp sau sửa dữ liệu.

**Dùng sai:** 404 hồ sơ không tồn tại hoặc không thuộc phạm vi. Không dùng thời điểm updatedAt
của hồ sơ thay cho submittedAt của phiên bản; không gửi snapshotData vào API sửa trực tiếp.

### GET /api/v1/applications/{id}/diff?fromVersion=1&toVersion=2

**Dùng để làm gì:** xem những ô/giấy tờ đã đổi giữa hai lần nộp.

**Cách gọi:** chọn hai số phiên bản của cùng hồ sơ; toVersion lớn hơn fromVersion.
Chủ hồ sơ và cán bộ thụ lý được đọc.

**Kết quả:** 200 mảng các thay đổi: fieldKey, fieldLabel, oldValue, newValue,
hasChanged=true, associatedComments, oldExists, newExists.
Không có thay đổi trả `[]`. Diff đi sâu vào object/array của form;
checklist so theo UUID, không theo vị trí trong mảng. File URL đổi có fieldKey
`/checklists/{uuid}/fileUrl`. Giá trị bị xóa được phân biệt với giá trị null qua cờ Exists.
FieldLabel dùng nhãn nhận xét khi có, nếu không dùng đường dẫn fieldKey.
AssociatedComments chỉ gồm nhận xét thuộc hai phiên bản đang đối chiếu.

**Dùng sai:** 400 số phiên bản sai/thứ tự sai; 404 thiếu một phiên bản hoặc không có quyền đọc hồ sơ.
So sánh chuỗi JSON nguyên văn trên FE có thể tạo khác biệt giả do thứ tự key; dùng kết quả API.

### Migration và môi trường

Migration `20261008004706_ApplicationReviewVersions` tạo application_versions/application_comments,
FK ràng buộc comment thuộc đúng hồ sơ/version, unique `(application_id, version_number)` và index hàng chờ.
Hồ sơ SUBMITTED cũ (API cũ đã khóa sửa) được backfill version 1 từ dữ liệu hiện có;
DRAFT cũ được cấp mã, chưa tạo snapshot. Không tái dựng lịch sử không tồn tại.
APPROVED/CANCELLED đã là trạng thái hợp lệ trong schema, chưa có API duyệt/hủy trong TASK-11.

Connection key vẫn là `ConnectionStrings__WorkflowDatabase`. Cấu hình local hiện dùng database độc lập
`wardmate_workflow_db`; giữ tên đang dùng để không làm mất kết nối dữ liệu cũ.
Nếu môi trường mới dùng tên `wardmate_application_db` trong đặc tả, trỏ connection string đến database đó
và chạy migration của Workflow. Không trỏ vào database IAM/Procedure.
Chưa áp dụng migration trên Azure trong task này.


## FE Vercel — cấu hình truy cập API (09/10/2026)

Origin được cho phép: `https://wardmate-frontend.vercel.app` (không dấu `/` cuối).
Policy dùng chung ở Gateway, IAM, Workflow, Procedure và DocumentForm; cho phép credentials,
GET/POST/PUT/PATCH/DELETE/OPTIONS và Content-Type/Authorization/X-CSRF-Protection.
Development vẫn mặc định hỗ trợ localhost:5173 và localhost:3000.
Nếu có Cors__AllowedOrigins__* trên Azure, danh sách đó thay thế mặc định: phải thêm domain Vercel vào đó.

IAM mặc định SameSite=None, HttpOnly và Secure. FE gọi login/refresh/logout với `credentials: 'include'`
(axios: `withCredentials: true`) và `X-CSRF-Protection: 1` cho các POST auth.
API cần quyền vẫn phải có Bearer token; CORS không cấp role hoặc bỏ authorization.
Dùng một API origin nhất quán cho toàn bộ auth, ưu tiên Gateway và route `/api/v1/auth/...`.
Cookie host-only không chuyển từ IAM trực tiếp sang Gateway. Trình duyệt chặn cookie bên thứ ba
vẫn có thể chặn refresh; khi đó cần kiến trúc proxy/domain cùng site.

Azure: cập nhật/deploy lại image chứa SharedKernel cho Gateway/IAM/Workflow/Procedure/DocumentForm.
Nếu môi trường đã override cấu hình, đặt:

```text
Cors__AllowedOrigins__0=https://wardmate-frontend.vercel.app
AuthCookie__SameSite=None
AuthCookie__AllowInsecureLocalhost=false
```

Hai biến AuthCookie chỉ đặt tại IAM. Không cần thêm domain Azure API vào allowlist chỉ để Swagger cùng origin.
Local HTTP nếu chủ động bật AllowInsecureLocalhost=true phải override SameSite=Strict hoặc Lax;
không dùng cấu hình đó cho FE Vercel gọi Azure. Không có endpoint/DTO mới trong lần cập nhật này.

## Hoàn thiện Tuần 1–4 — IAM directory, permission và hồ sơ theo phường (09/10/2026)

Mục này cập nhật các giới hạn của TASK-11 phía trên: hàng đợi nay đã phân theo phường,
đã có DOCUMENTS_READY (thay luồng cũ) và kiểm permission theo từng chức năng.
Không thay đổi API DocumentForm của BE khác trong đợt này.

### GET /api/v1/manager/officers

**Mục đích:** Manager tìm cán bộ Một cửa do phường mình quản lý.

**Quyền:** tài khoản MANAGER đang hoạt động, đã gán phường, có `iam.accounts.read`.
Phạm vi được lấy từ database IAM; không tin WardCode do FE gửi để mở rộng quyền.

**Cách gọi:** không body. Ví dụ:
`/api/v1/manager/officers?wardCode=WARD_A&assignedCategory=1&page=1&pageSize=20`.
Query tùy chọn: wardCode, assignedCategory (ID số nguyên của danh mục Procedure), search, role;
page mặc định 1, pageSize mặc định 20 (tối đa 100).
AssignedCategory lọc metadata lĩnh vực, không giới hạn cán bộ chỉ được thụ lý lĩnh vực đó.

**Kết quả:** 200 `{items,page,pageSize,total}`. Mỗi item: id, username, email, fullName,
identityNumber, isActive, wardCode, roles[], assignedCategories[]. Không trả passwordHash/token.
Chỉ cán bộ thuộc phường Manager và không kiêm vai trò đặc quyền ngoài FD/Citizen được hiển thị.
Lọc sang phường khác trả danh sách rỗng; phạm vi áp dụng trước total/phân trang.

**Dùng sai:** 400 khi page/pageSize/category sai; 401 chưa đăng nhập/tài khoản bị khóa;
403 thiếu permission, không phải Manager hoặc chưa có phường. Kiểm tra tài khoản/phường với Admin.

### GET /api/v1/admin/users

**Mục đích:** IT_ADMIN tra cứu toàn bộ người dùng kể cả tài khoản bị khóa.

**Quyền:** IT_ADMIN có `iam.accounts.read`. Manager không dùng API này.

**Cách gọi:** không body; query `search` tìm chuỗi trong họ tên, email hoặc CCCD,
`role=FRONT_DESK_OFFICER`, `page`, `pageSize`; có thể thêm wardCode/assignedCategory.
Search không phân biệt hoa thường; `%` và `_` được hiểu là ký tự tìm kiếm, không là wildcard.

**Kết quả:** 200 DirectoryPage giống API cán bộ; tìm không có kết quả trả items rỗng, total=0.

**Dùng sai:** 403 thiếu quyền/không phải IT_ADMIN; 400 từ khóa quá 255 ký tự hoặc phân trang sai.
Không sử dụng API này để lấy mật khẩu hay dữ liệu token.

### PUT /api/v1/manager/officers/{userId}/categories

**Mục đích:** Manager/Admin cập nhật metadata lĩnh vực phụ trách phục vụ bộ lọc cán bộ.

**Quyền:** `iam.accounts.manage` và phạm vi quản lý hiện có; Manager chỉ sửa FD của phường mình.

**Cách gọi:** body `{"categories":[1,2]}`. Mã là ID danh mục từ Procedure Catalog.
Mảng thay thế toàn bộ metadata hiện tại; `[]` xóa danh sách lĩnh vực.
Đây là logical reference, không tạo khóa ngoại sang database Procedure và không giới hạn queue theo lĩnh vực.
Backend kiểm mã nguyên dương, không trùng, tối đa 100; chưa xác minh sự tồn tại qua service Procedure.
FE nên chọn từ API danh mục thay vì nhập ID tự do.

**Kết quả:** 204 không body; gọi lại danh sách cán bộ để thấy thay đổi.

**Dùng sai:** 400 mảng null/mã âm/trùng/quá giới hạn; 404 không có FD trong phạm vi được phép;
403 thiếu permission. Không dùng API này để chuyển phường hoặc cấp role.

### GET /api/v1/wards

**Mục đích:** cung cấp danh sách phường/xã tiếp nhận để công dân chọn khi tạo hồ sơ.

**Cách gọi:** công khai, không body. Trả 200 `[{id,code,name}]` sắp xếp theo code.
Đây là các phường đã tạo trong IAM, không tự đồng bộ danh mục hành chính quốc gia.
Danh sách rỗng cần Admin tạo phường qua API quản trị hiện có.

### GET /api/v1/users/access-context

**Mục đích:** Workflow xác minh quyền và phường hiện hành của người đang gửi request.

**Cách gọi:** Bearer token IAM; không nhận userId/ward từ query hoặc body.

**Kết quả:** 200 `{userId,wardCode,roles,permissions}`, Cache-Control no-store.
401 nếu token không hợp lệ hoặc tài khoản bị khóa/không tồn tại.
Đây là ngữ cảnh của chính người đăng nhập, không phải API tra quyền tùy ý của người khác.

### Tạo hồ sơ và hàng đợi theo phường

`POST /api/v1/applications` vẫn dùng workflow.create, nay **bắt buộc wardCode**:

```json
{
  "procedureId": "11111111-1111-1111-1111-111111111111",
  "wardCode": "WARD_A",
  "caseCode": null,
  "formData": { "fullName": "Nguyễn Văn A" }
}
```

Chọn mã thật từ GET /api/v1/wards; thay procedureId bằng ID thủ tục đang hoạt động.
Mã được trim/viết hoa. 400 application.invalid_ward nếu không tồn tại;
503 application.iam_unavailable nếu không xác minh được IAM, không bỏ qua kiểm tra.
WardCode lưu vào hồ sơ, snapshot mới, detail và summary. Không lấy phường cư trú của công dân
làm phường tiếp nhận tự động và không cho FE thay phường khi resubmit.

GET /api/v1/officer/applications/pending chỉ thấy SUBMITTED/UNDER_REVIEW đúng WardCode hiện hành,
chưa có cán bộ hoặc đã thuộc chính cán bộ đó. Bộ lọc status/procedure/date/paging/FIFO giữ nguyên.
Thiếu phường trả danh sách rỗng; cán bộ khác phường không được assign, comment, request-revision,
confirm-readiness hoặc đọc phiên bản/diff của hồ sơ không thuộc mình.

Workflow gọi IAM bằng token của request để cập nhật permissions/ward/roles trước authorization.
Chuyển phường/thu hồi quyền/khóa tài khoản có hiệu lực ở request tiếp theo, không chờ token hết hạn.
IAM không truy cập được trả 503; không quay về dùng quyền/phường cũ trong JWT.
Cấu hình Workflow `Iam__BaseUrl` (Compose: http://iam-api:8080/; chạy trực tiếp mặc định localhost:5001).

Hồ sơ cũ được giữ WardCode=null vì không đủ dữ liệu xác định phường. Các hồ sơ này bị loại khỏi queue,
DRAFT thiếu phường không được submit (409 application.ward_required). Cần kế hoạch bổ sung địa bàn
có kiểm chứng trước khi đưa hồ sơ cũ vào vận hành; migration không tự gán toàn bộ vào một phường.

### POST /api/v1/officer/applications/{id}/confirm-readiness

**Mục đích:** cán bộ xác nhận bộ giấy tờ đã được kiểm tra, sẵn sàng tải/in mang đến phường. Không xác nhận sẵn sàng hoặc từ chối giải quyết thủ tục hành chính trên WardMate.

**Ai dùng:** FRONT_DESK_OFFICER có permission `workflow.readiness.confirm`, cùng phường, đã được phân công hồ sơ; không phải chủ hồ sơ. Gửi Bearer token, không cần body.

**Khi gọi:** hồ sơ UNDER_REVIEW, có snapshot đã nộp, không còn comment OPEN trên phiên bản hiện tại, checklist bắt buộc COMPLETED. Server xác minh tài liệu bắt buộc/các mục COMPLETED qua DocumentForm (file tồn tại, đúng checklist/template, loại bản và số lượng).

**Thành công:** 200 ReviewResultDto, `application.status=DOCUMENTS_READY`, `application.documentsReadyAt` UTC; ghi history và Outbox cùng transaction. Không tạo thêm version nộp: giữ snapshot hiện tại. Không cho sửa checklist, upload/xóa/annotate hay thêm comment sau xác nhận. Người dân gọi generate-pdf với submissionId đã lưu để tải giấy tờ rồi đến phường.

**Gọi sai và cách xử lý:** 401 đăng nhập lại; 403 sai quyền/cán bộ/phường hoặc tự xác nhận; 404 không có hồ sơ trong phạm vi; 409 sai trạng thái, thiếu snapshot, `application.open_comments` hoặc `application.checklist_incomplete`. Có comment OPEN thì dùng request-revision, công dân sửa và resubmit, cán bộ nhận lại để kiểm tra. 422 `application.documents_incomplete` có missingChecklistIds: bổ sung tài liệu tương ứng; 503 `application.documents_unavailable`: kiểm tra DocumentForm/Blob rồi thử lại. Không tự đổi trạng thái trên FE. Gọi xác nhận trực tiếp lần hai trả 409; GET trạng thái trước khi thử lại sau timeout.

### Endpoint cũ đã bỏ và tương thích DocumentForm

- POST /api/v1/officer/applications/{id}/approve và /reject đã bỏ (không còn trong Swagger; không còn chuyển trạng thái mới sang APPROVED/REJECTED).
- POST /api/v1/officer/submissions/{submissionId}/confirm-readiness thay /approve. Đây là API chuyển tiếp xác nhận **toàn Application** qua Workflow, không duyệt riêng một form. Body tùy chọn `{ "officerId": "GUID người gọi" }`; không được giả danh cán bộ khác. Có Bearer token và quyền Workflow tương ứng. 200 có submissionId/message; lỗi Workflow được trả bằng ProblemDetails `document.workflow_review_failed` với HTTP status tương ứng. Hồ sơ đã DOCUMENTS_READY thì đồng bộ và trả 200, không ghi history mới.
- Generate/list/download PDF yêu cầu Workflow DOCUMENTS_READY. DocumentForm đồng bộ Submitted → ReadyForDownload khi kiểm tra truy cập. 409 `document.application_not_ready` thì hoàn tất kiểm tra hồ sơ trước; 400 `document.submission_not_ready` thì kiểm tra trạng thái form đã nộp. Không cho cán bộ bỏ qua điều kiện sẵn sàng.
- GET /api/v1/applications/{id}/confirm-readinessd-qr và /approved-qr/image giữ tên route để tương thích, nhưng QR chỉ tham chiếu bộ giấy tờ chuẩn bị; không phải quyết định, chữ ký hay con dấu hành chính. 404 QR chưa tạo: kết xuất PDF trước. Bảng document.approved_qrs giữ tên cũ, không cần đổi dữ liệu.
- Dữ liệu APPROVED/REJECTED, approved_at và lịch sử cũ được giữ nguyên; không tự gán DOCUMENTS_READY. Hồ sơ cũ chỉ đọc, không dùng làm điều kiện xuất PDF mới. Cần kế hoạch đối soát riêng nếu muốn chuyển dữ liệu cũ.
- DTO Application thay approvedAt bằng documentsReadyAt. FE đổi nút thành “Xác nhận giấy tờ sẵn sàng”, bỏ nút từ chối; thiếu giấy tờ dùng “Yêu cầu bổ sung”.
- Migration IAM chuyển các grant workflow.approve sang workflow.readiness.confirm, gỡ workflow.approve/reject. Workflow đọc quyền hiện tại từ IAM ở mỗi request; FE nên đăng nhập/refresh sau migrate IAM để cập nhật menu và claims. Role chỉ có quyền reject cũ không tự được quyền xác nhận.

### Permission đang được thực thi

IAM kiểm quyền từ DB tại request; giữ các ràng buộc phạm vi/anti-escalation có sẵn.
- Xem tài khoản/người dùng/hồ sơ quản trị: iam.accounts.read.
- Tạo FD, sửa trạng thái/hồ sơ, cấp/gỡ role FD, gán lĩnh vực: iam.accounts.manage.
- Xem phường quản trị: iam.wards.read; tạo/gán phường: iam.wards.manage.
- Quản trị role/permission: iam.rbac.manage; audit: iam.audit.read.
- Hồ sơ cá nhân vẫn iam.profile.read/iam.profile.write.

Procedure không còn cho phép role IT_ADMIN/PROCEDURE_MANAGER tự vượt kiểm tra permission.
Dùng 15 mã đã seed: procedure.read/create/update/publish/status, procedure.versions.read,
procedure.rollback, procedure.source.read, procedure.categories.manage,
procedure.drafts.read/upload/update/extract/publish/delete.
API biểu mẫu chọn cho Procedure dùng procedure.read; API public vẫn công khai.
Procedure đọc permissions từ JWT: đăng nhập/refresh sau khi thay grant; token cũ có quyền cũ đến hết hạn.

Workflow dùng 13 mã: workflow.create/read/checklist.write/submit/resubmit,
workflow.comments.read/versions.read/diff.read, workflow.queue.read/assign/comments.write,
workflow.revision.request/readiness.confirm. Mỗi endpoint cần mã tương ứng cùng phạm vi chủ hồ sơ/cán bộ.
Migration IAM cấp 8 quyền cá nhân/đọc trong phạm vi cho 5 role chuẩn; 6 quyền thẩm định chỉ cho FD.
IT_ADMIN/MANAGER không tự có quyền thẩm định toàn hệ thống.

### Triển khai thay đổi

1. Áp dụng migration IAM OfficerDirectoryCategories: assigned_categories và 14 permission Workflow.
2. Deploy IAM có access-context/wards trước khi deploy Workflow phụ thuộc hai API này.
3. Áp dụng migration Workflow WardScopedFinalDecisions (WardCode, index, trạng thái REJECTED).
4. Cấu hình Iam__BaseUrl đúng origin IAM trên môi trường đích; deploy Workflow/Procedure/Gateway mới.
5. FE bổ sung chọn phường khi tạo hồ sơ, bộ lọc cán bộ và xử lý hai trạng thái cuối.

Các bước trên là hướng dẫn bàn giao; chưa được chạy trên Azure trong phiên này.

## TASK-25 — Thông báo trong ứng dụng (AnalyticsSystem / SignalR)

### Kết nối thông báo realtime

Địa chỉ local trực tiếp: `http://localhost:5006/hubs/notifications`.
Qua YARP hiện có: `http://localhost:5000/api/analytics-system/hubs/notifications`.
Đây là SignalR Hub, không phải REST controller; Swagger không liệt kê các hub methods.

Dành cho người dùng đã đăng nhập. FE dùng **access token** IAM, không dùng refresh cookie làm token kết nối. JWT key/issuer/audience của AnalyticsSystem phải khớp IAM. Khi kết nối, backend kiểm tra tài khoản qua IAM `/api/v1/users/access-context`; IAM không truy cập được hoặc tài khoản bị khóa thì kết nối bị đóng.

- `POST /hubs/notifications/negotiate?negotiateVersion=1`: SignalR SDK tự gọi, không có body nghiệp vụ; thành công 200 với thông tin transport, thiếu/sai/hết hạn JWT trả 401.
- `GET /hubs/notifications`: SDK thiết lập WebSocket (101 khi nâng cấp thành công); SSE/Long Polling do SDK quản lý nếu cần.
- Browser WebSocket/SSE gửi JWT qua query `access_token`; backend chỉ chấp nhận cách này ở đường dẫn hub. Không ghi URL có token vào log/telemetry. Dùng HTTPS trên Azure.
- Mỗi connection được vào `user_{userId}` từ JWT. Nhiều tab/thiết bị của cùng người dùng đều nhận thông báo.
- Chỉ `FRONT_DESK_OFFICER` hoặc `MANAGER` có phường trong ngữ cảnh IAM được thêm vào `ward_{wardCode}`. Không có phương thức để client tự tham gia nhóm.
- Nhóm phường được xác định tại thời điểm kết nối; khi đổi phường/role cần ngắt và kết nối lại. Token hết hạn thì backend đóng kết nối. Task này chưa cung cấp cơ chế thu hồi ngay mọi connection đang mở khi IAM thay đổi quyền.

### Các thao tác FE gọi trên Hub

**`GetNotifications(page, pageSize)` — Lấy thông báo của chính mình**

Dùng khi mở danh sách thông báo hoặc kết nối lại sau mất mạng. Gọi `connection.invoke("GetNotifications", 1, 20)`. Trả mảng `NotificationDto`, mới nhất trước, chỉ kênh IN_APP. Không có tham số userId; không thể đọc thông báo của người khác. Trang từ 1 đến 1000000, pageSize từ 1 đến 100; sai giới hạn thì promise bị reject với thông báo tiếng Việt. Trang không có dữ liệu trả mảng rỗng.

**`GetUnreadCount()` — Lấy số thông báo chưa đọc**

Gọi khi cần đồng bộ lại huy hiệu chuông. Trả số nguyên của chính người đang kết nối. Khi connect và sau thay đổi, server còn tự gửi event `UpdateUnreadCount`. Realtime có thể ngắt hoặc các sự kiện đồng thời đến khác thứ tự; khi mở chuông/reconnect nên đọc lại số từ DB bằng phương thức này.

**`MarkRead(notificationId)` — Đánh dấu một thông báo đã đọc**

Gọi khi người dùng mở thông báo. Thành công promise hoàn tất, server cập nhật `is_read`, `read_at` UTC và phát `UpdateUnreadCount` tới các connection cùng người dùng. Gọi lại cùng id không đổi thời điểm đọc lần đầu. Id không tồn tại hoặc thuộc người khác đều trả lỗi “Không tìm thấy thông báo của bạn”; FE tải lại danh sách thay vì thử id của người khác. Hub invocation errors dùng cơ chế lỗi SignalR, không phải HTTP ProblemDetails cho từng phương thức.

### Event backend gửi tới FE

- `ReceiveNotification(notification)`: có thông báo mới đã lưu thành công vào DB. Thêm vào danh sách theo `id` (khử trùng nếu đã có).
- `UpdateUnreadCount(unreadCount)`: cập nhật số ở chuông thông báo.

`NotificationDto` gồm `id`, `recipientUserId`, `applicationId` (nullable), `title`, `content`, `type`, `channel`, `isRead`, `readAt` (nullable), `actionUrl` (nullable), `createdAt`. Thời gian là UTC. Render title/content như text, không render HTML trực tiếp. `actionUrl` là đường dẫn tương đối nội bộ FE; nó không thay thế kiểm tra quyền ở API hồ sơ.

Ví dụ với package FE `@microsoft/signalr`:

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5006/hubs/notifications", {
    accessTokenFactory: () => getCurrentAccessToken(),
  })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveNotification", item => showNotification(item));
connection.on("UpdateUnreadCount", count => updateBellCount(count));
connection.onreconnected(async () => {
  renderNotifications(await connection.invoke("GetNotifications", 1, 20));
  updateBellCount(await connection.invoke("GetUnreadCount"));
});
await connection.start();
renderNotifications(await connection.invoke("GetNotifications", 1, 20));
// Khi người dùng mở một thông báo:
// await connection.invoke("MarkRead", notificationId);
Bộ kiểm thử tự động trong mục này đã được gỡ theo yêu cầu chủ dự án.json
{
  "expectedUpdatedAt": "2026-10-14T00:00:00Z",
  "decisionNumber": "QD-123",
  "effectiveDate": "2026-10-14",
  "items": [
    { "checklistId": "DOC-1", "caseCode": null, "submissionType": "NOP",
      "itemName": "Tờ khai", "documentCopyType": "ORIGINAL", "quantity": 1,
      "conditionNote": null, "isMandatory": true }
  ]
}
```
Thay expectedUpdatedAt bằng giá trị thật từ response mới nhất. items=[] xóa toàn bộ checklist;
không dùng khi chỉ định xóa một dòng. caseCode null/rỗng là giấy tờ chung; nếu có mã phải thuộc cases.
200 trả ProcedureDetailDto, giữ lại updatedAt mới cho lần lưu tiếp. 400: dữ liệu sai, trùng mã,
mã trường hợp không tồn tại, thiếu ngày/quyết định hoặc quá 500 mục. 401/403: kiểm tra token/quyền.
404: thủ tục không tồn tại. 409 procedure.concurrent_update: người khác đã sửa, GET lại và đối chiếu,
không tự gửi đè. Mỗi lần lưu thành công chụp trạng thái cũ vào versions trong cùng transaction.
Không cập nhật bảng Application; các hồ sơ đã tạo giữ checklist riêng. Chỉ hỗ trợ quản lý
checklist của Procedure chính thức; bản nháp vẫn dùng PUT drafts/{id} và revision hiện có.

## Khai báo giấy tờ minh chứng và kiểm tra điều kiện nộp hồ sơ (Application Checklist)

Khi tạo hồ sơ (`POST /api/v1/applications`), toàn bộ yêu cầu checklist đã duyệt từ thủ tục sẽ được sao chép nguyên vẹn (snapshot) vào hồ sơ, bao gồm: thứ tự sắp xếp (`sortOrder`), trường hợp áp dụng (`caseCode`), hình thức nộp (`submissionType`: `NOP` hoặc `XUAT_TRINH`), loại bản sao (`documentCopyType`: `ORIGINAL`, `CERTIFIED_COPY`, `REGULAR_COPY`), số lượng yêu cầu (`quantity`) và ghi chú điều kiện (`conditionNote`). Nếu thủ tục gốc sau này có thay đổi checklist, hồ sơ đã tạo vẫn giữ nguyên yêu cầu đã chốt.

### 1. Khai báo giấy tờ minh chứng kèm thuộc tính
Người dân tải lên giấy tờ minh chứng cho hồ sơ thông qua `POST /api/v1/applications/{id}/supporting-documents`:
- Content-Type: `multipart/form-data`
- Các trường form data:
  - `File`: File tài liệu (PDF, PNG, JPG, ...).
  - `ChecklistId`: Guid của mục checklist trong hồ sơ cần liên kết.
  - `SubmissionType`: `NOP` (nộp bản chụp/bản điện tử) hoặc `XUAT_TRINH` (xuất trình khi đến nhận kết quả/đối chiếu).
  - `DocumentCopyType`: `ORIGINAL` (bản chính), `CERTIFIED_COPY` (bản sao chứng thực), `REGULAR_COPY` (bản chụp/sao thường).
  - `Quantity`: Số lượng bản đính kèm (mặc định là 1, tối thiểu 1).

### 2. Kiểm tra điều kiện nộp hồ sơ phía Server (Pre-submit Validation)
Khi nộp hồ sơ lần đầu (`POST /api/v1/applications/{id}/submit`) hoặc nộp lại sau bổ sung (`POST /api/v1/applications/{id}/resubmit`):
- Hệ thống gọi nội bộ sang `DocumentForm` (`GET /api/v1/applications/{id}/contents`) để xác minh thực tế:
  1. Với mục checklist là Biểu mẫu điện tử (`formTemplateId` có giá trị): Biểu mẫu phải ở trạng thái `Submitted` hoặc `ReadyForDownload`, file kết xuất DOCX phải tồn tại thực sự trong Blob Storage.
  2. Với mục checklist là Giấy tờ minh chứng đính kèm: Phải có tài liệu đính kèm liên kết đúng `ChecklistId`, file phải tồn tại thực sự trong Blob Storage, khớp loại hình thức `submissionType`, khớp loại bản sao `documentCopyType`, và tổng số lượng giấy tờ đính kèm phải đạt hoặc vượt số lượng yêu cầu `quantity`.
- Nếu thiếu giấy tờ hoặc file không tồn tại trong Blob Storage, hệ thống trả về mã `422 Unprocessable Entity` với ProblemDetails:
  - `code`: `"application.documents_incomplete"`
  - `missingChecklistIds`: Danh sách các Guid của mục checklist chưa đáp ứng điều kiện.
  Người dân không thể chỉ đánh dấu trạng thái checklist là `COMPLETED` ở client để vượt qua bước nộp hồ sơ.


## Luồng checklist – điền DOCX – nộp hồ sơ: những điểm cần làm đúng

1. Procedure Manager tạo FormTemplate và upload DOCX gốc trong DocumentForm.
   Khi cập nhật/xuất bản Procedure, khai báo `formDefinitions` với `formTemplateId`
   vừa tạo, `formCode` trùng `checklistId` cần điền đơn, `formType: DOCX_TEMPLATE`,
   `formName`, `quantity`, `isMandatory` và `caseCode` nếu có.
   API PUT checklist chỉ sửa checklist, không tự thêm liên kết FormTemplate.
   Dùng PUT toàn bộ Procedure hoặc API publish để lưu formDefinitions;
   giữ đủ các trường hiện có để tránh ghi đè mất dữ liệu.
2. Công dân tạo Application bằng POST `/api/v1/applications` với `procedureId`,
   `formData: {}`, `wardCode` hợp lệ lấy từ IAM; gửi `caseCode` nếu thủ tục có nhiều trường hợp.
   Thiếu/sai phường hoặc trường hợp trả 400. Lưu applicationId và các checklistId từ response 201.
   Hồ sơ giữ metadata checklist riêng: thứ tự, case, loại bản sao, nộp/xuất trình,
   số lượng và điều kiện. Chỉnh Procedure sau đó không sửa hồ sơ đã tạo.
3. Mở DOCX mẫu trong editor của FE hoặc Word, điền và xuất DOCX.
   POST `/api/v1/citizen/submissions/draft` (multipart) gồm File, TemplateId,
   ApplicationId, ChecklistId và ApplicantId của token. Đây là lưu file DOCX đã điền;
   API không tự tạo giao diện editor. Sai liên kết hoặc mẫu trả 400; hồ sơ khác chủ trả 404/403.
   PUT `/api/v1/citizen/submissions/{submissionId}/draft` lưu lại bản sửa.
4. POST `/api/v1/citizen/submissions/{submissionId}/submit` với JSON `applicantId`
   chốt đơn thành Submitted. Đây chưa phải nộp toàn bộ Application.
   PATCH checklist của Workflow với `status: COMPLETED` sau khi chốt đơn.
5. Với giấy tờ minh chứng, upload File và ChecklistId cùng SubmissionType,
   DocumentCopyType, Quantity vào endpoint supporting-documents của DocumentForm.
   Quantity là số bản người dân khai báo được file đại diện, không phải số trang PDF.
   Khai báo CERTIFIED_COPY không chứng minh file được chứng thực; cán bộ vẫn phải đối chiếu.
   XUAT_TRINH vẫn cần bản điện tử để nộp online theo luồng hiện tại; bản gốc đối chiếu tại quầy.
   Với biểu mẫu điện tử đúng template, một DOCX hoàn thành được dùng để in số bản yêu cầu.
6. GET `/api/v1/applications/{id}/contents` trên DocumentForm trả liên kết từng tài liệu
   qua checklistId, trạng thái, fileExists và thông tin khai báo giấy tờ.
   Nếu kho file không truy cập được, trả 503; thử lại khi Blob hoạt động.
   PATCH COMPLETED, POST submit và POST resubmit của Workflow đều kiểm tra evidence.
   422 `application.documents_incomplete` kèm missingChecklistIds: bổ sung file thật,
   đúng loại/số lượng, đúng template và chốt đơn rồi thử lại. FileUrl tự nhập không thay được evidence.
7. Cán bộ đúng phường nhận Application, ghi lỗi và yêu cầu bổ sung hoặc xác nhận sẵn sàng qua Workflow.
   Khi NEED_REVISION, công dân sửa DOCX/giấy tờ, chốt lại đơn và resubmit hồ sơ.
   Sau khi nộp/duyệt, người dân không được sửa file qua luồng ghi của DocumentForm.
8. Xuất PDF: duyệt một lần tại Workflow. Khi truy cập đơn hoặc xuất PDF,
   DocumentForm đọc trạng thái Workflow và tự đồng bộ đơn Submitted sang ReadyForDownload.
   Không cần gọi duyệt đơn riêng. Gọi POST `/api/v1/applications/{id}/generate-pdf`
   bằng multipart/form-data có SubmissionId, không đính kèm file thay thế.
   Cần Gotenberg và Blob hoạt động. Thiếu dịch vụ chuyển đổi trả 503, không trả PDF mẫu.


Trong Swagger dùng scheme HTTP Bearer, dán nguyên accessToken vào Authorize, không thêm
một tiền tố Bearer thứ hai. 401: token sai/hết hạn; 403: thiếu quyền. Không dùng ID mẫu cố định.
Các hồ sơ cũ giữ metadata null/quantity mặc định từ migration; không tự lấy cấu hình Procedure
mới để lấp lại và thay đổi yêu cầu lịch sử. Tích hợp HTTP giữa các service chưa phải giao dịch
phân tán nguyên tử; chưa đảm bảo chống mọi thao tác ghi đồng thời giữa hai service.

## Duyệt một lần tại Workflow và xuất PDF giấy tờ chuẩn bị (2026-10-14)

Mục này thay thế các hướng dẫn cũ yêu cầu duyệt đơn DocumentForm riêng.
Workflow quyết định trạng thái hồ sơ. DocumentForm đồng bộ khi truy cập (không phải worker
đẩy tức thì vào tất cả đơn): khi xác minh Workflow DOCUMENTS_READY, các đơn Submitted thuộc hồ sơ
được chuyển sang ReadyForDownload. Gọi lại an toàn; nếu mất kết nối hoặc chưa lưu được trạng thái,
trả 503 và thử lại. Không xuất PDF chỉ dựa vào trạng thái ReadyForDownload cũ trong DocumentForm.

### Cán bộ đọc hồ sơ đang thụ lý

GET `/api/v1/officer/applications/{id}` trên Workflow; không body.
Cần Bearer token, permission `workflow.read`, vai trò FRONT_DESK_OFFICER hiện tại từ IAM,
đúng phường và assignedOfficerId của hồ sơ. 200 trả ApplicationDto gồm checklist, formData,
trạng thái và lịch sử. Chưa nhận hồ sơ hoặc sai phường/cán bộ: 404; thiếu quyền: 403;
IAM không khả dụng: 503. Nhận hồ sơ qua endpoint assign trước khi mở tài liệu.

DocumentForm dùng API này để xác minh cán bộ, không tin OfficerId trong body hoặc chỉ role token.
MANAGER/PROCEDURE_MANAGER/IT_ADMIN không được mặc nhiên thụ lý đơn của công dân bằng các API cán bộ.
Công dân chỉ đọc/ghi hồ sơ của mình. Không đổi cơ chế JWT; các service vẫn phải dùng cùng issuer/audience/key.

### Xem và ghi chú tài liệu

- GET `/api/v1/officer/submissions/{submissionId}/download-docx`: cán bộ đã được phân công
  lấy DOCX của hồ sơ đúng phường; 200 file, 404 không thuộc phạm vi, 409 đơn cũ chưa liên kết Workflow.
- PUT cùng đường dẫn `/annotate-docx`: multipart File DOCX, chỉ khi Workflow UNDER_REVIEW.
  200 lưu thành công; 400 file sai; 409 sau duyệt/đóng hoặc không còn UNDER_REVIEW.
  Ghi chú Word không tự tạo lỗi OPEN trong Workflow. Dùng API comments của Workflow để ghi lỗi nghiệp vụ.
- GET `/api/v1/applications/{id}/supporting-documents` và
  GET `/api/v1/supporting-documents/{documentId}/download`: chủ hồ sơ hoặc cán bộ đang thụ lý
  được đọc. Upload và DELETE vẫn dành cho công dân, chỉ DRAFT/NEED_REVISION.

### Quyết định hồ sơ

Khuyến nghị FE gọi trực tiếp POST `/api/v1/officer/applications/{id}/confirm-readiness` trên Workflow,
không body. Quyền `workflow.readiness.confirm`, đúng người thụ lý; phải UNDER_REVIEW và không còn lỗi OPEN
ở phiên bản hiện tại. 200 trả ReviewResultDto; 403 sai người/phường; 409 trạng thái hoặc lỗi chưa xử lý.
Không cần gọi API confirm-readiness của DocumentForm sau đó.

Hai route DocumentForm cũ được giữ để tương thích đường dẫn, nhưng hành vi đã đổi:

- POST `/api/v1/officer/submissions/{submissionId}/confirm-readiness`, body `{}`: chuyển quyết định
  sang Workflow cho TOÀN BỘ hồ sơ liên kết, sau đó đồng bộ đơn. Không duyệt riêng một tờ khai.
- POST `/api/v1/officer/submissions/{submissionId}/request-revision`, body `{"comment":"Lý do"}`:
  chuyển yêu cầu bổ sung sang Workflow. Cần lỗi OPEN ở phiên bản hiện tại và quyền
  `workflow.revision.request`; người dân sửa file khi Workflow NEED_REVISION rồi chốt đơn/resubmit.
- Response thành công của hai route cũ là 200 `{submissionId, message}`. FE phải tải lại trạng thái
  hồ sơ/đơn, không dựa vào SaveSubmissionResultDto cũ. Cùng quyết định đã được Workflow áp dụng
  sẽ trả thành công khi gọi lại; không ghi thêm lịch sử lần hai. OfficerId nếu gửi phải trùng token.
- 409 `document.workflow_review_failed`: kiểm tra Workflow, lỗi OPEN và quyền thụ lý;
  503: chưa xác nhận được kết quả, GET lại trạng thái trước khi thử lại.

### Xuất và tải PDF

POST `/api/v1/applications/{id}/generate-pdf` trên DocumentForm nhận multipart:
`SubmissionId` bắt buộc, `DocumentType` tùy chọn (mặc định ORIGINAL_SUBMISSION).
Không gửi File mới: backend dùng DOCX đã lưu. Cần chủ hồ sơ hoặc cán bộ đang thụ lý,
Workflow DOCUMENTS_READY và đơn hợp lệ đã chốt. 200 trả GeneratedDocumentDto có `id` dùng để tải PDF,
và `submissionId` chỉ định chính xác đơn gốc đã kết xuất.
Mỗi lần xuất tạo bản ghi riêng; không ghi đè PDF của đơn khác trong cùng Application.

GET `/api/v1/applications/{id}/generated-documents` lấy danh sách PDF kết xuất của hồ sơ; hỗ trợ lọc theo `?submissionId={guid}` để lấy đúng file của đơn tương ứng.
Mỗi phần tử trong danh sách trả về chứa `id`, `applicationId`, `submissionId`, `documentType`, `pdfBlobUrl` và `createdAtUtc`.
GET `/api/v1/generated-documents/{documentId}/download` tải PDF. Cả hai đều kiểm tra quyền/phường
và trạng thái DOCUMENTS_READY qua Workflow. Không chia sẻ trực tiếp URL Blob private với người không có quyền.

- 409 `document.application_not_ready`: chờ xác nhận giấy tờ sẵn sàng; không cố duyệt riêng đơn để vượt điều kiện.
- 404: sai hồ sơ/tài liệu hoặc ngoài phạm vi thụ lý.
- 503 `document.pdf_render_failed`: Gotenberg thiếu cấu hình/mất kết nối/chuyển đổi lỗi;
  khôi phục dịch vụ rồi thử lại. Không còn fallback PDF mẫu.
- 503 `document.workflow_unavailable`/`document.sync_unavailable`: khôi phục kết nối và tải lại.
- Comment nội bộ bị loại khỏi bản DOCX dùng để chuyển PDF; file DOCX gốc không bị sửa.
- Bản in PDF giấy tờ chuẩn bị được tự động đóng dấu mã QR tra cứu (TASK-22, TASK-23) ở cuối văn bản căn lề phải kích thước 3.5cm x 3.5cm. Mã QR chứa URL tra cứu hồ sơ chính thức qua Gateway (`/api/v1/applications/{id}`).

### Tra cứu `submissionId` của hồ sơ cho Cán bộ một cửa (TASK-25 / Gaps 1 & 2)

Để cán bộ có thể gọi các API thao tác đơn DOCX (`download-docx`, `annotate-docx`, `request-revision`, `confirm-readiness`), cán bộ cần biết `submissionId` của đơn:

#### 1. Lấy toàn bộ đơn và tài liệu minh chứng của hồ sơ
- **Endpoint**: `GET /api/v1/applications/{applicationId}/contents`
- **Mục đích**: Lấy thông tin tất cả đơn điện tử (kèm `id` chính là `submissionId`) và giấy tờ minh chứng thuộc hồ sơ.
- **Ai dùng**: Công dân chủ hồ sơ hoặc Cán bộ một cửa đang thụ lý hồ sơ (`DocumentUser` policy).
- **Kết quả thành công (200 OK)**: `ApplicationContentsDto` chứa danh sách `submissions` và `supportingDocuments`. Trong đó `submissions[i].id` chính là `submissionId`.

#### 2. Lấy danh sách đơn điện tử của hồ sơ dành riêng cho Cán bộ
- **Endpoint**: `GET /api/v1/officer/submissions/by-application/{applicationId}`
- **Mục đích**: Cán bộ một cửa lấy danh sách chi tiết các đơn người dân đã điền cho hồ sơ để lấy `submissionId`, trạng thái, thời gian nộp và ghi chú.
- **Ai dùng**: Cán bộ một cửa (`FRONT_DESK_OFFICER`).
- **Kết quả thành công (200 OK)**: Danh sách `UserSubmissionSummaryDto[]` gồm:
  - `id`: submissionId của đơn điện tử.
  - `templateId`: Mẫu biểu DOCX gốc.
  - `applicationId`: ID hồ sơ.
  - `checklistId`: Mục thành phần hồ sơ tương ứng.
  - `applicantId`: ID công dân nộp đơn.
  - `fileName`, `fileSizeBytes`, `status`: Trạng thái đơn (`Submitted`, `ReadyForDownload`, `Draft`, v.v.).
  - `officerComment`: Ghi chú cán bộ nếu có.
- **Lỗi thường gặp & Phục hồi**:
  - `401 Unauthorized`: Chưa đăng nhập hoặc token hết hạn.
  - `403 Forbidden`: Người gọi không có role `FRONT_DESK_OFFICER` hoặc không thuộc phạm vi xử lý hồ sơ.
  - `404 Not Found`: Không tìm thấy hồ sơ tương ứng trong hệ thống Workflow.

### Tra cứu và tải Mã QR hồ sơ đã xác nhận sẵn sàng (TASK-22 / TASK-23)

Mã QR tham chiếu được tạo khi kết xuất PDF lần đầu cho hồ sơ DOCUMENTS_READY và lưu trữ trong bảng `document.approved_qrs`.

#### 1. Lấy thông tin mã QR tra cứu
- **Endpoint**: `GET /api/v1/applications/{applicationId}/approved-qr`
- **Mục đích**: Lấy URL tra cứu và metadata mã QR của hồ sơ hành chính đã được xác nhận sẵn sàng.
- **Ai dùng**: Công dân nộp hồ sơ hoặc Cán bộ một cửa đang thụ lý hồ sơ.
- **Khi nào và cách gọi**: Sau khi hồ sơ đạt trạng thái `DOCUMENTS_READY` tại Workflow. Gửi request kèm Header `Authorization: Bearer <token>`.
- **Kết quả thành công (200 OK)**:
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "applicationId": "d9cd5161-1c1e-43d6-af1a-e3411fb34af5",
    "submissionId": "8b3e27a4-8b55-4a62-a46e-27b43e94f0fa",
    "qrCodeValue": "https://wardmate-gateway.../api/v1/applications/d9cd5161-1c1e-43d6-af1a-e3411fb34af5",
    "generatedAt": "2026-10-15T15:42:30Z"
  }
  ```
- **Lỗi thường gặp & Phục hồi**:
  - `401 Unauthorized`: Token không hợp lệ hoặc hết hạn. Đăng nhập lại để lấy token mới.
  - `403 Forbidden` (`document.application_forbidden`): Người gọi không phải chủ hồ sơ hoặc không phải cán bộ được phân công.
  - `404 Not Found` (`document.qr_not_found`): Hồ sơ chưa có mã QR tra cứu (do chưa được duyệt hoặc chưa từng sinh QR).
  - `409 Conflict` (`document.application_not_ready`): Hồ sơ chưa đạt trạng thái DOCUMENTS_READY. Chờ cán bộ xác nhận giấy tờ sẵn sàng trước.

#### 2. Tải file ảnh PNG của Mã QR tra cứu
- **Endpoint**: `GET /api/v1/applications/{applicationId}/approved-qr/image`
- **Mục đích**: Tải trực tiếp file hình ảnh PNG của mã QR để hiển thị lên giao diện web, in ấn tại quầy hoặc gửi cho người dân quét tra cứu.
- **Ai dùng**: Công dân nộp hồ sơ hoặc Cán bộ một cửa.
- **Khi nào và cách gọi**: Sau khi hồ sơ đạt trạng thái `DOCUMENTS_READY`. Gửi GET request kèm Header `Authorization: Bearer <token>`.
- **Kết quả thành công (200 OK)**: File nhị phân `image/png`, tên file `qr-{applicationId}.png`.
- **Lỗi & Phục hồi**: Tương tự như endpoint lấy thông tin (401, 403, 404, 409).

### Cập nhật API Mới — 19/10/2026 (TASK-OFFICER-LIST-QR-LOOKUP)

#### 1. Danh sách Tổng hợp Toàn bộ Hồ sơ của Phường (Cán bộ một cửa)
- **Endpoint**: `GET /api/v1/officer/applications`
- **Mục đích**: Cung cấp danh sách tổng hợp toàn bộ hồ sơ thuộc phường của cán bộ đang đăng nhập, thay vì chỉ xem được danh sách chờ (`pending`). Hỗ trợ xem lịch sử các hồ sơ đã duyệt, bị từ chối, cần sửa đổi hoặc đang xử lý.
- **Ai dùng**: Cán bộ một cửa hoặc Manager có `workflow.queue.read`.
- **Khi nào và cách gọi**: Gửi `GET` request kèm Bearer Token của cán bộ.
  - Query parameters hỗ trợ:
    - `status`: Lọc theo trạng thái hồ sơ (VD: `APPROVED`, `NEED_REVISION`, `SUBMITTED`, `UNDER_REVIEW`, `REJECTED`). Hỗ trợ truyền nhiều trạng thái cách nhau bởi dấu phẩy, ví dụ: `status=DOCUMENTS_READY,NEED_REVISION` hoặc `status=SUBMITTED,UNDER_REVIEW`.
    - `procedureId`: UUID của thủ tục hành chính.
    - `applicationCode`: Tìm kiếm theo mã số hồ sơ (tìm tương đối chứa chuỗi).
    - `submittedFrom`, `submittedTo`: Khoảng thời gian nộp hồ sơ (ISO 8601).
    - `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100).
- **Kết quả thành công (200 OK)**:
  ```json
  {
    "items": [
      {
        "id": "b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9",
        "applicationCode": "HS-20261015-001",
        "procedureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "procedureTitle": "Đăng ký khai sinh",
        "status": "DOCUMENTS_READY",
        "createdAt": "2026-10-15T08:00:00Z",
        "submittedAt": "2026-10-15T08:30:00Z",
        "wardCode": "W01",
        "assignedOfficerId": null
      }
    ],
    "page": 1,
    "pageSize": 20,
    "total": 1
  }
  ```
- **Lỗi thường gặp & Phục hồi**:
  - `401 Unauthorized`: Thiếu Bearer Token hoặc token hết hạn.
  - `403 Forbidden`: Thiếu role FD/Manager hoặc permission `workflow.queue.read`; chưa gán phường thì danh sách rỗng.

#### 2. Mở Trực Tiếp File PDF khi Quét Mã QR — Endpoint Mới (23/10/2026)
- **Endpoint**: `GET /api/v1/public/applications/{applicationId}/pdf-redirect`
- **Mục đích**: Khi người dùng quét mã QR in trên giấy tờ, trình duyệt/camera sẽ redirect thẳng đến file PDF mới nhất của hồ sơ đó. Không cần đăng nhập, không hiển thị JSON — người dùng mở thẳng PDF.
- **Ai dùng**: Bất kỳ ai quét mã QR (người dân, cơ quan xác minh, cán bộ).
- **Cách hoạt động**:
  1. Camera quét QR → URL `https://gateway/api/v1/public/applications/{id}/pdf-redirect`
  2. Backend kiểm tra toàn bộ các file PDF đã kết xuất của hồ sơ:
     - **Nếu hồ sơ có 1 đơn:** Trả về SAS URL của file PDF đó.
     - **Nếu hồ sơ có từ 2 đơn trở lên:** Backend tự động gọi Gotenberg Engine (`/forms/pdfengines/merge`) để ghép nối tiếp tất cả các đơn thành **1 file PDF duy nhất trọn bộ** (loại `COMBINED_APPLICATION`, lưu vào Azure Blob và tái sử dụng cho các lần quét sau).
  3. Tạo **SAS URL ngắn hạn 15 phút** trỏ đến file PDF (hoặc bản gộp) trên Azure Blob Storage.
  4. Trả về `302 Found` redirect → trình duyệt/camera tự động mở toàn bộ file PDF.
- **Kết quả thành công (302 Found)**: Header `Location` chứa SAS URL Azure Blob. Trình duyệt tự redirect, người dùng thấy/tải PDF.
- **Lỗi thường gặp & Phục hồi**:
  - `404` (`qr.not_found`): Hồ sơ chưa có mã QR — chưa generate PDF lần nào.
  - `404` (`qr.pdf_not_found`): Hồ sơ có QR nhưng chưa có file PDF được xuất.
  - `404` (`qr.pdf_blob_missing`): File PDF không còn trên Azure Blob — liên hệ cơ quan tiếp nhận.
  - `400` (`qr.invalid_application_id`): ApplicationId là GUID rỗng.

#### 3. Tra cứu Thông Tin Hồ Sơ bằng Mã QR (legacy — trả JSON)
- **Endpoint**: 
  - `GET /api/v1/public/qr-lookup?token={token}` hoặc `GET /api/v1/public/qr-lookup?applicationId={applicationId}`
  - `GET /api/v1/public/qr-lookup/{token}`
- **Mục đích**: Tra cứu thông tin và tính hợp lệ hồ sơ qua QR — trả về JSON thay vì redirect PDF. Dùng khi FE cần hiển thị thông tin chi tiết.
- **Ai dùng**: Công chúng, người quét mã QR, ứng dụng cần dữ liệu JSON.
- **Kết quả thành công (200 OK)**:
  ```json
  {
    "applicationId": "b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9",
    "submissionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "qrCodeValue": "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/public/applications/b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9/pdf-redirect",
    "generatedAt": "2026-10-23T08:00:00Z",
    "status": "APPROVED",
    "documentName": "Tờ khai đăng ký khai sinh",
    "isValid": true,
    "message": "Hồ sơ điện tử hợp lệ, đã được cơ quan có thẩm quyền phê duyệt và đóng dấu xác thực."
  }
  ```
- **Lỗi thường gặp & Phục hồi**:
  - `400 Bad Request` (`qr.token_required`): Chưa truyền token hoặc applicationId.
  - `404 Not Found` (`qr.not_found`): Mã QR không khớp với bất kỳ hồ sơ nào trong hệ thống.

#### 3. Cơ chế tương thích ngược khi quét QR cũ (`GET /api/v1/applications/{id}`)
- **Endpoint**: `GET /api/v1/applications/{id}`
- **Mục đích**: Đối với các tài liệu PDF đã in/tải trước đây có nhúng mã QR chứa URL `/api/v1/applications/{id}`, khi người dân dùng camera điện thoại quét (không kèm Header `Authorization`), hệ thống tự động trả về `302 Found` Redirect chuyển hướng sang `GET /api/v1/public/qr-lookup?applicationId={id}` để hiển thị kết quả kiểm tra tính hợp lệ mà không báo lỗi 401/403.
- **Ai dùng**: Bất kỳ người quét mã QR cũ nào.
- **Khi có Bearer token**: API vẫn hoạt động bình thường, trả về chi tiết `ApplicationDto` cho chủ hồ sơ hoặc cán bộ thụ lý.

### Cấu hình và trình tự đối soát quy trình QR
Trình tự: tạo hồ sơ/đơn → chốt đơn → COMPLETED checklist → submit hồ sơ → cán bộ assign
→ xem/ghi chú → tạo comment OPEN và request-revision nếu cần → dân sửa/chốt/resubmit
→ cán bộ assign lại → confirm-readiness Workflow → **generate-pdf** sinh QR vào `approved_qrs` trỏ tới `/api/v1/public/applications/{id}/pdf-redirect`
→ dân/cán bộ generate-pdf (kết xuất bản PDF sạch, không dán mã QR lên mặt giấy tờ; mã QR được lưu DB phục vụ tra cứu số) → download PDF hoặc **quét QR → mở thẳng PDF** (302 redirect, không cần đăng nhập).

> **QR cũ** (sinh trước 23/10/2026) chứa URL `/api/v1/public/qr-lookup?applicationId={id}` vẫn hoạt động và trả JSON hợp lệ. Khi gọi `generate-pdf` lần tiếp theo cho cùng hồ sơ, QR sẽ tự động được regenerate sang URL `/pdf-redirect` mới.
Kiểm tra âm: cán bộ khác phường/cùng phường chưa phân công, giả OfficerId, xuất trước duyệt,
sửa sau duyệt, Workflow hoặc Gotenberg mất kết nối. Các trường hợp này không được trả file chính thức mới.




### Bàn giao triển khai luồng giấy tờ sẵn sàng — 17/10/2026

Đây là thay đổi hợp đồng API. Tạm dừng thao tác kiểm tra hồ sơ khi chuyển phiên bản: áp dụng migration Workflow `20261016154813_DocumentReadiness` và IAM `20261016154952_WorkflowReadinessPermission`, rồi đưa các bản mới Workflow, DocumentForm, AnalyticsSystem và IAM vào hoạt động đồng bộ. Không để API approve cũ phục vụ khi quyền approve đã bị gỡ. Chưa áp dụng các migration này lên Azure trong phiên sửa code.

FE đổi nút thành “Xác nhận giấy tờ sẵn sàng”, gọi POST /api/v1/officer/applications/{id}/confirm-readiness không body. Dùng trạng thái DOCUMENTS_READY và documentsReadyAt, quyền workflow.readiness.confirm. Khi hoàn tất, hiển thị hướng dẫn tải giấy tờ và đến phường; không hiển thị kết quả giải quyết thủ tục. Workflow lấy quyền hiện hành từ IAM mỗi request; đăng nhập lại/refresh giúp menu FE cập nhật claims.

Kiểm thử Docker trước khi deploy: tạo hồ sơ → điền form/đính kèm → submit → cán bộ đúng phường nhận hồ sơ → yêu cầu bổ sung → công dân sửa/nộp lại → cán bộ kiểm tra và xác nhận → công dân xuất/tải PDF. Kiểm tra thêm hồ sơ khác phường, cán bộ chưa được phân công, nhận xét OPEN, checklist thiếu, file mất và thao tác sửa sau DOCUMENTS_READY. Build thành công chưa thay thế kiểm thử tích hợp này.


## TASK-25/27 — Kiểm tra thông báo realtime và RabbitMQ (17/10/2026)

### Phạm vi và cấu hình

Không bổ sung REST endpoint trong lần này. FE dùng Hub /hubs/notifications và các method GetNotifications(page, pageSize), GetUnreadCount(), MarkRead(notificationId). ReceiveNotification và UpdateUnreadCount là callback server gọi FE. Lỗi method là SignalR invocation error, không phải ProblemDetails. JWT hết hạn cần lấy access token mới rồi reconnect.

Workflow và AnalyticsSystem phải cùng RabbitMq__Host, Port, User, Password, VirtualHost, UseTls và RabbitMq__Enabled=true. AnalyticsSystem cần Notifications__WorkersEnabled=true, ConnectionStrings__NotificationsDatabase và Iam__BaseUrl. Notifications__ServiceKey phải khớp cấu hình IAM (ít nhất 32 ký tự), không đặt vào FE. JWT issuer/audience/key phải khớp IAM; CORS cho đúng origin FE. Giữ Email__Enabled=false và Sms__Enabled=false. Không gửi secret trong log/chat.

Áp dụng migration WorkflowNotificationOutbox trên DB Workflow và InitialNotifications/ReliableNotificationDelivery trên DB Notifications trước khi bật workers. Dùng một replica AnalyticsSystem và một revision phục vụ tại một thời điểm: hiện chưa có SignalR backplane. /health hiện chỉ xác nhận tiến trình, không chứng minh RabbitMQ/IAM/database hoặc chuỗi gửi thông báo hoạt động.

### Kiểm thử bằng tài khoản thử nghiệm

1. Mở hai phiên công dân và cán bộ đúng phường; kết nối Hub với token tương ứng. Khi kết nối, server gửi UpdateUnreadCount. Sau reconnect FE gọi lại GetNotifications(1,20) và GetUnreadCount(), gộp thông báo theo id để chống hiển thị trùng.
2. Công dân nộp hồ sơ: công dân nhận thông báo nộp thành công; cán bộ có quyền queue ở đúng phường nhận thông báo có hồ sơ cần tiếp nhận. Cán bộ phường khác không nhận.
3. Cán bộ nhận hồ sơ, thêm nhận xét, yêu cầu bổ sung, rồi xác nhận DOCUMENTS_READY sau khi bổ sung: công dân nhận các cập nhật tương ứng. Notification chỉ là đường dẫn hỗ trợ; quyền mở hồ sơ vẫn do Workflow kiểm tra.
4. Gọi MarkRead(id) từ công dân: bộ đếm cập nhật trên những connection hợp lệ, gọi lại không thay readAt. Dùng tài khoản khác gọi cùng id phải báo không tìm thấy.
5. Ngắt trình duyệt, tạo sự kiện, kết nối lại: danh sách vẫn có thông báo. Delivery Sent không phải xác nhận trình duyệt đã nhận; đây không phải cơ chế push exactly-once.
6. Trên môi trường thử riêng, tạm dừng RabbitMQ rồi đổi trạng thái hồ sơ: nghiệp vụ vẫn lưu cùng outbox. Khi RabbitMQ trở lại, sự kiện được gửi. Giao lại cùng EventId chỉ có một inbox và một notification mỗi người/kênh; không tạo id mới khi replay.
7. Tạm dừng IAM: inbox/delivery lỗi được retry; khôi phục rồi kiểm tra tiếp tục xử lý. Sau 10 lần lỗi chuyển Dead, không tự chạy lại mãi. Lỗi lưu Inbox rollback phần notification/delivery trước khi lưu lần thử tiếp theo. Nếu DB cũng hỏng thì chưa thể ghi số lần retry, phải khôi phục DB trước.
8. Đổi phường/quyền hoặc khóa tài khoản: connection cũ bị ngắt khi tái xác minh (mặc định 15 giây, cấu hình 5–300 giây); trước khi gửi realtime cũng kiểm tra lại connection. Mở lại chỉ nhận dữ liệu theo quyền/phường mới.

### Kiểm tra hàng đợi bằng SQL chỉ đọc

Chạy trên DB Workflow:

```sql
SELECT count(*) AS unpublished, min(created_at) AS oldest_pending
FROM workflow_outbox WHERE published_at IS NULL;
SELECT id, attempts, last_error, next_attempt_at
FROM workflow_outbox WHERE published_at IS NULL
ORDER BY created_at LIMIT 20;
```

Chạy trên DB Notifications (các cột Inbox/Delivery giữ tên PascalCase):

```sql
SELECT "State", count(*) FROM notification_inbox GROUP BY "State";
SELECT "Channel", "State", count(*) FROM notification_deliveries GROUP BY "Channel", "State";
SELECT "Id", "Attempts", "ErrorCode", "NextAttemptAt"
FROM notification_inbox WHERE "State" = 'Dead';
SELECT "Id", "NotificationId", "Attempts", "ErrorCode"
FROM notification_deliveries WHERE "State" = 'Dead';
SELECT source_event_id, recipient_user_id, channel, count(*)
FROM notifications WHERE source_event_id IS NOT NULL
GROUP BY source_event_id, recipient_user_id, channel HAVING count(*) > 1;
```

Truy vấn cuối phải không có dòng. Đối chiếu EventId với inbox.Id và notifications.source_event_id. Log lỗi mới ghi EventId/DeliveryId, attempt và state, không ghi nội dung hồ sơ.

### Phục hồi sự kiện lỗi

Sửa nguyên nhân trước (IAM/service key/DB), sau đó người vận hành mới reset đúng một bản ghi Dead được đối soát về Pending, Attempts=0, NextAttemptAt=now(), ErrorCode=NULL trong DB tương ứng. Giữ nguyên Id/Payload; không xóa notification đã lưu và không reset toàn bảng. Delivery chỉ reset bản IN_APP cần thiết; có thể gửi realtime lặp nên FE deduplicate theo notification id. Sự kiện sai JSON/hợp đồng vào RabbitMQ queue wardmate.notifications.workflow.v1.dead: không redrive nguyên trạng khi chưa sửa nguyên nhân. Chưa có API quản trị replay trong bản này.

Các bước trên là checklist kiểm thử cần thực hiện trên môi trường chạy; build không xác nhận các ca này đã pass.


## Chặng 1 P0 — Phạm vi cán bộ/Manager và đổi mật khẩu (21/10/2026)

### GET /api/v1/officer/applications

FD/Manager có workflow.queue.read dùng để xem lịch sử hồ sơ phường mình. Bearer bắt buộc; phường lấy từ IAM hiện hành, không lấy phường do client gửi. Query: status (đơn hoặc danh sách phân cách dấu phẩy), procedureId, applicationCode (tìm chuỗi con), submittedFrom/submittedTo, page=1, pageSize=20 (tối đa100). status lọc theo giá trị đang lưu, không phân biệt hoa/thường; giá trị chưa tồn tại trả danh sách rỗng. Loại hồ sơ do chính người xem nộp; muốn xem hồ sơ riêng dùng API công dân.

200 trả { items, page, pageSize, total }, mỗi item thêm assignedOfficerId nullable. FE dùng null để hiển thị “Chưa phân công”. Danh sách tổng hợp chuẩn hóa page tối thiểu1 và pageSize trong1..100 theo logic sẵn có; token sai401, thiếu quyền/role403. Chưa có phường trả danh sách rỗng; Admin cần gán phường cho tài khoản. Không đổi tên total thành totalCount.

### GET /api/v1/officer/applications/pending

Vẫn lọc SUBMITTED/UNDER_REVIEW, query và phân trang hiện có. FD thấy hồ sơ chưa giao hoặc giao cho mình; Manager có workflow.queue.read thấy các hồ sơ pending của phường để giám sát. Response items thêm assignedOfficerId; không mở quyền assign cho Manager.

### GET /api/v1/officer/applications/{id}

FD hoặc Manager có workflow.read xem chi tiết bất kỳ hồ sơ cùng phường, trừ hồ sơ tự nộp; không còn yêu cầu người xem là assignedOfficerId. 200 trả ApplicationDto. Không có role/phường403, khác phường/tự nộp/không tồn tại404. Dùng API công dân cho hồ sơ riêng. Cán bộ khác người thụ lý được xem nhưng không được sửa. Manager không được assign, thêm comment, request-revision, confirm-readiness dù có permission tương ứng; tài khoản đồng thời Manager+FD cũng chỉ đọc nghiệp vụ cán bộ.

### POST /api/v1/users/me/change-password

Mọi người dùng đăng nhập bằng Bearer dùng để đổi mật khẩu của chính mình trong phần tài khoản. Không dùng cho quên mật khẩu hoặc đổi hộ người khác.

```json
{ "currentPassword": "MatKhauCu123!", "newPassword": "MatKhauMoi456!", "confirmNewPassword": "MatKhauMoi456!" }
```

Mật khẩu mới tối thiểu8 ký tự, gồm hoa, thường, số, ký tự đặc biệt; tối đa72 byte UTF-8. 200 trả { message: "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." }. FE xóa access token hiện tại, quay về đăng nhập; toàn bộ refresh token cũ đã bị thu hồi. Access token cũ không bị vô hiệu ngay và có thể dùng tới khi hết hạn. Không hiển thị rằng đã đăng xuất tức thì mọi thiết bị.

400 validation_failed khi dữ liệu sai (xem errors theo trường); 400 iam.current_password_invalid nếu mật khẩu hiện tại sai; 400 iam.password_unchanged nếu dùng lại mật khẩu; 401 khi Bearer/tài khoản không hợp lệ; 409 iam.concurrent_update nếu tài khoản thay đổi đồng thời, đăng nhập lại rồi thử. Không lưu mật khẩu vào log/localStorage. API này dùng Bearer, không xác thực bằng refresh cookie, không yêu cầu CSRF header của endpoint refresh. Body quá4096 byte bị từ chối413.

### Triển khai và xác nhận P0

Áp dụng migration IAM 20261021090000_ManagerWorkflowReadScope để Manager có hai permission đọc. Workflow tiếp tục xác minh quyền hiện hành qua IAM; FE refresh/login để đồng bộ menu. Đưa IAM, Workflow và Gateway bản mới vào môi trường test. Xác nhận FD B đọc hồ sơ FD A cùng phường được nhưng không sửa; khác phường/tự nộp bị chặn; Manager đọc được nhưng POST thao tác403; danh sách có assignedOfficerId thật. Kiểm tra đổi mật khẩu sai/không khớp/yếu/đúng, login mật khẩu cũ thất bại, mới thành công, refresh token cũ thất bại. Chưa triển khai P1/P2.

## Chặng 2 P1 — API bổ sung ngày 21/10/2026

### Tiếp nhận hồ sơ giấy tại quầy

`POST /api/v1/officer/applications/{id}/officially-received`

Cán bộ đang thụ lý gọi sau khi người dân đến phường và đã đối chiếu giấy tờ. Cần Bearer, role FRONT_DESK_OFFICER và permission `workflow.receipt.confirm`; Manager chỉ đọc, kể cả tài khoản có thêm role cán bộ. Hồ sơ phải cùng phường, được giao cho cán bộ này và không do chính cán bộ nộp.

```json
{
  "paperDossierVerified": true,
  "notes": "Đã đối chiếu bộ hồ sơ giấy tại quầy",
  "appointmentDate": "2026-11-23T09:00:00Z"
}
```

Ngày hẹn có thể bỏ trống/null, nếu có phải không nằm trong quá khứ; ghi chú tối đa 4.000 ký tự. Body tối đa 16 KiB. Chỉ nhận hồ sơ DOCUMENTS_READY. Thành công 200 trả `applicationId`, `status: OFFICIALLY_RECEIVED`, `receiptNumber`, `receivedAt`, `appointmentDate`. Số biên nhận dạng BN-ngày UTC-GUID hồ sơ; đây là số hệ thống sinh, không đồng bộ số biên nhận của hệ thống hành chính bên ngoài.

Sai dữ liệu trả400; thiếu đăng nhập401; sai quyền/phường/người thụ lý403; không có hồ sơ404; chưa sẵn sàng hoặc gọi lại sau tiếp nhận409. Khi409, tải lại chi tiết hồ sơ trước khi thao tác. Không dùng API này để phê duyệt kết quả hành chính. Hồ sơ sau tiếp nhận vẫn được tải tài liệu nhưng không sửa nội dung.

### Công dân hủy hồ sơ tiền kiểm

`POST /api/v1/applications/{id}/cancel`

Chủ hồ sơ có Bearer và `workflow.cancel` gọi khi không muốn tiếp tục hồ sơ DRAFT hoặc NEED_REVISION. Không cần body. Thành công200 trả ApplicationDto với status CANCELLED và lưu lịch sử; dữ liệu không bị xóa. Hồ sơ không thuộc mình/không tồn tại trả404; thiếu quyền403; trạng thái khác hoặc hủy lần hai409. Đọc lại hồ sơ khi gặp409. Không thể hủy bằng API này sau khi đã nộp hoặc đã sẵn sàng.

### Đăng nhập và refresh cho React Native

`POST /api/v1/auth/login`: giữ body đăng nhập hiện có, thêm header `X-Client-Type: Mobile`. Thành công200 có thêm `refreshToken` trong JSON, không ghi cookie. Lưu refresh token trong kho bảo mật của thiết bị; không log hoặc đưa vào URL.

`POST /api/v1/auth/refresh-token`: cũng gửi header Mobile và body:

```json
{ "refreshToken": "TOKEN_DANG_LUU_TREN_THIET_BI" }
```

Thành công200 trả cặp token mới; thay refresh token cũ ngay vì token chỉ dùng một lần. Thiếu/sai/hết hạn token trả401: yêu cầu đăng nhập lại; không gửi nhiều refresh đồng thời. Login/refresh giới hạn body4096 byte. `POST /api/v1/auth/revoke-token` trên Mobile dùng cùng body và header, kèm Bearer; thành công204, ứng dụng xóa token cục bộ.

Web không gửi header Mobile: tiếp tục dùng HttpOnly cookie và header CSRF hiện có; refresh token không xuất hiện trong JSON Web. Native không có Origin/Sec-Fetch-Site được đi qua kiểm tra CSRF; request có dấu hiệu trình duyệt vẫn phải qua kiểm tra CSRF/Origin. Header Mobile không cấp role hay quyền. Không dùng header này như cách sửa lỗi CORS trên FE web.

### Xem trực tiếp giấy tờ minh chứng

`GET /api/v1/supporting-documents/{documentId}/preview`

Gửi Bearer của chủ tài liệu hoặc cán bộ được phép theo kiểm tra download hiện có. Trả200 stream PDF/PNG/JPEG với Content-Disposition inline; hỗ trợ range khi stream cho phép. FE dùng fetch có Bearer, tạo URL từ Blob để hiển thị; mở URL trực tiếp bằng tab thường không tự gửi Bearer. Response không cache. Sai quyền403, không có tài liệu404, dịch vụ lưu trữ không khả dụng503; DOC/DOCX trả415, hãy dùng API download. Preview không chuyển Word thành PDF và không mở truy cập công khai.

### Thủ tục nổi bật

`GET /api/v1/procedures/featured`

Public, không cần Bearer/body. Trả200 là mảng tối đa5 ProcedureSummaryDto, chỉ thủ tục active, mới tạo nhất trước. Đây là danh sách mới nhất, chưa phải thống kê lượt xem. Có thể trả mảng rỗng hoặc dưới5 mục. Không có metadata phân trang; muốn tìm kiếm/phân trang dùng GET /api/v1/procedures.

### Upload Word đã điền offline

`POST /api/v1/citizen/submissions/upload-filled-docx`

Công dân có quyền DocumentCitizen dùng khi đã tải và điền mẫu Word trên máy. Đây là alias của POST /api/v1/citizen/submissions/draft, cùng xử lý và kiểm tra. Gửi Bearer và multipart/form-data gồm `TemplateId`, `ApplicantId`, `ApplicationId`, `ChecklistId`, `File` (.docx). Hai ID hồ sơ/checklist vẫn bắt buộc để gắn đúng giấy tờ; không upload chỉ với ba trường đầu trong đề xuất FE. Để trình duyệt tự đặt boundary multipart.

Thành công201 trả DTO chứa `submissionId`, trạng thái và thông tin file. ApplicantId phải là người đăng nhập; hồ sơ phải thuộc người đó và đang DRAFT/NEED_REVISION; checklist phải thuộc hồ sơ và đúng template. Sai file/dữ liệu400, sai quyền403, thiếu dữ liệu liên quan404, trùng bản nháp hoặc trạng thái không phù hợp409. Dùng submissionId đã có để cập nhật thay vì tạo trùng. API này lưu bản điền, không tự submit hồ sơ hay xác nhận đủ giấy tờ.

## Chặng 3 P2 — AI/OCR cho công dân (21/10/2026)

Các API dưới đây nằm trong AIOCR, yêu cầu Bearer JWT IAM hợp lệ, không yêu cầu role quản trị riêng. Gateway có route giữ nguyên /api/v1/ai-ocr/... và /api/v1/ai/chat/... . Quyền đọc giấy tờ vẫn do DocumentForm/Workflow kiểm tra; đăng nhập không có nghĩa được đọc giấy tờ người khác.

### POST /api/v1/ai-ocr/cccd/extract

Dùng để điền gợi ý thông tin từ ảnh CCCD; người dùng phải xem lại trước khi lưu hồ sơ. Multipart/form-data: frontImage bắt buộc, backImage tùy chọn; PNG/JPEG, mỗi ảnh tối đa8MiB, tổng request tối đa17MiB. Không tự đặt boundary khi dùng FormData. Không lưu ảnh hoặc kết quả CCCD vào DB.

200 trả citizenId, fullName, dateOfBirth, gender, nationality, placeOfOrigin, placeOfResidence, dateOfExpiry, confidenceScore. Ngày YYYY-MM-DD; dữ liệu không đọc được/hạn không xác định có thể null. CCCD nếu có phải đủ12 chữ số. Confidence0..1 do AI tự đánh giá, không phải bằng chứng xác thực danh tính. Không dùng kết quả làm eKYC.

Thiếu ảnh/sai kích thước400, request quá lớn413, sai định dạng415, token thiếu/sai401; AI chưa bật/chưa có key/model/quota hết503; output sai/cắt502, timeout504. Chụp ảnh rõ đủ góc và thử lại; không tự thay dữ liệu null bằng thông tin đoán.

### POST /api/v1/ai/chat/completions

Dùng để hỏi cách chuẩn bị giấy tờ. Body:

```json
{ "message": "Tôi cần chuẩn bị gì để đăng ký tạm trú?", "conversationId": null, "wardCode": "WARD_001" }
```

Message bắt buộc, tối đa4000 ký tự; wardCode gồm chữ/số/gạch dưới/gạch ngang,1..50 ký tự. ConversationId null tạo phiên mới; các lượt tiếp dùng ID từ response và cùng wardCode. Phường là ngữ cảnh do user khai báo, chưa xác minh danh mục phường và không cấp quyền nghiệp vụ.

200 trả {conversationId,answer,relatedProcedures:[]}. Bản P2 này gọi Gemini thuần, CHƯA truy xuất catalog/pgvector và không cung cấp trích dẫn đã xác minh. Không xem câu trả lời là phí/thời hạn/quy định địa phương chính thức. FE hiển thị như văn bản, không render HTML không tin cậy. Không nhập mật khẩu, token hay số CCCD vào chat.

Chỉ chủ phiên được tiếp tục, ID khác chủ/không tồn tại404. Đổi wardCode trong phiên409: tạo phiên mới. Gửi đồng thời cùng phiên409 chat.busy: đợi lượt trước xong. Input sai400; database/migration/AI chưa sẵn sàng503; kết quả AI sai502; timeout504. User+Assistant lưu cùng transaction; nếu AI lỗi rollback cả lượt, không tạo câu trả lời giả. Không có idempotency key: timeout mạng sau commit có thể đã lưu; hãy đọc lịch sử trước gửi lại. Context gửi AI chỉ tối đa10 tin gần nhất và tin mới, không phải toàn bộ lịch sử.

### GET /api/v1/ai/chat/sessions

Lấy các phiên của chính người đăng nhập, mới nhất trước. Query pageNumber mặc định1 (1..100000), pageSize mặc định20 (1..50). 200 {items:[{id,wardCode,createdAt}],pageNumber,pageSize,totalCount}. Không có phiên trảitems rỗng. Sai phân trang400; token401; DB chưa sẵn sàng503.

### GET /api/v1/ai/chat/sessions/{sessionId}/messages

Lấy lịch sử một phiên của mình theo thời gian tăng dần, cùng query phân trang như trên. 200 {items:[{id,role,content,createdAt}],pageNumber,pageSize,totalCount}; role User hoặc Assistant. Phiên khác chủ/không tồn tại404; không thử ID người khác. Lịch sử lưu DB AIOCR, không đọc DB IAM. Hiện chưa có API xóa phiên hoặc chính sách tự xóa lịch sử.

### POST /api/v1/ai-ocr/documents/verify

Tiền kiểm giấy tờ đã upload, không upload lần hai:

```json
{ "applicationId": "GUID_HO_SO", "documentId": "GUID_GIAY_TO" }
```

Hai ID phải khác Guid.Empty; body<=4096byte. expectedType cũ vẫn được nhận (tối đa255 ký tự) nhưng bị bỏ qua, không dùng làm căn cứ AI. AIOCR chuyển Bearer sang API danh sách giấy tờ của hồ sơ trong DocumentForm, kiểm tra documentId nằm đúng applicationId rồi gọi download. Không dùng service key để bỏ qua quyền. Người gọi phải là chủ hoặc cán bộ được phân công theo DocumentForm; Manager không tự có quyền tải.

Hỗ trợ PDF/PNG/JPEG tối đa8MiB. 200 {isValid,documentTypeDetected,qualityIssues:[],confidence}. Kiểm tra mờ/lóa và loại giấy tờ là gợi ý từ AI; expectedType được lấy từ Title của mục checklist đã sao chép vào hồ sơ Workflow, thông qua ChecklistId của giấy tờ trong DocumentForm. Không lấy từ bản thủ tục hiện tại và không dùng loại do client truyền. Response thêm checklistId và expectedType để đối soát. Không xác nhận thật giả/tính pháp lý. Nếu có vấn đề, loại rỗng hoặc confidence<0.7 thì isValid bị hạ false. Không tự cập nhật checklist/trạng thái hồ sơ; cán bộ và công dân vẫn đối soát.

Sai IDs/body400; thiếu token401; bị DocumentForm từ chối403/404; file không thuộc hồ sơ404; file quá lớn413; DOC/DOCX/định dạng không hỗ trợ415 (chuyển sang PDF/ảnh); phụ thuộc không sẵn sàng503; AI trả sai502; timeout504. Lỗi trả ProblemDetails với code,traceId; lỗi model binding mặc định có errors. JWT401 và limiter429 có thể không có body ProblemDetails.

### Cấu hình và giới hạn P2

- Assistant__Enabled=true để bật các lời gọi Gemini mới; mặc địnhfalse. Không ảnh hưởng Extraction__Enabled/UseAI của thủ tục.
- Gemini__Key tham chiếu secret; Gemini__Model dùng model của dự án đã chọn. Không đổi billing hoặc tự chọn model khác. Các request gửi dữ liệu tài liệu tới Google Gemini; kiểm thử bằng tài liệu mẫu không chứa dữ liệu thật trước.
- Jwt__Key/Jwt__Issuer/Jwt__Audience phải khớp IAM. Luồng procedure-extractions nội bộ vẫn dùng X-Service-Key riêng.
  - Cập nhật 23/10/2026 (AIOCR-JWT-01): thiếu Jwt__Key hoặc khóa dưới 32 byte thì AIOCR dừng ngay khi khởi động với lỗi "Cần khóa Jwt:Key ít nhất 32 byte, giống IAM." — toàn bộ service (kể cả procedure-extractions) không chạy cho tới khi cấu hình đúng. Trước đây service vẫn chạy nhưng mọi request Bearer bị 401 `WWW-Authenticate: Bearer error="invalid_token"`.
  - Khi không đặt Jwt__Issuer/Jwt__Audience, AIOCR mặc định `wardmate`/`wardmate-client`, giống IAM và các service khác (trước đây mặc định sai `WardMate.IAM`/`WardMate.Client`). Nếu IAM trên môi trường của bạn cấp token với giá trị khác, phải đặt hai biến này cho AIOCR bằng đúng giá trị của IAM.
  - Cách khắc phục 401 `invalid_token` trên AIOCR: so sánh Jwt__Key/Jwt__Issuer/Jwt__Audience của container AIOCR với IAM (cùng secret khóa). Sau khi token hợp lệ, nếu nhận 503 `auth.session_unavailable` thì kiểm tra Iam__BaseUrl của AIOCR trỏ tới IAM thật, không phải localhost.
- ConnectionStrings__AiOcrDatabase: PostgreSQL riêng AIOCR; không dùng database/schema của service khác. Cần migration 20261021080951_ChatSessions trước dùng chat.
- Database__AutoMigrate mặc địnhfalse; chỉ bậttrue nếu muốn service tự apply migration lúc startup và đã cấu hình đúng database. Khi thiếu connection và bật migration, startup dừng.
- Services__DocumentForm__BaseUrl: URL cố định DocumentForm; dùng HTTPS trên Azure. Không có URL tùy ý trong request.
- Docker Compose profile ai thêm AIOCR_ASSISTANT_ENABLED, AIOCR_DATABASE_CONNECTION_STRING, AIOCR_AUTO_MIGRATE; dùng JWT_KEY hiện có. Không tự tạo PostgreSQL AIOCR trong Compose: phải cung cấp database riêng.
- Mỗi user tối đa10 request/phút cho nhóm endpoint mới (kể cả đọc lịch sử), vượt trả429. Bộ đếm ở từng replica, chưa là quota phân tán. HTTP Gemini timeout90giây; không tự retry để tránh nhân chi phí. Health hiện là liveness, Healthy không chứng minh DB/Gemini hoạt động.
- Gemini adapter dùng generateContent + inlineData + JSON Schema. Tham khảo hợp đồng: https://ai.google.dev/api/generate-content .

## Hạn kiểm tra/chuẩn bị giấy tờ — 21/10/2026

Đây là hạn hoàn tất tiền kiểm trên WardMate, KHÔNG phải hạn giải quyết hành chính hoặc AppointmentDate tại phường. Chính sách chủ dự án chốt:5ngày làm việc=45giờ, thứHai–thứSáu,07:30–16:30 Asia/Ho_Chi_Minh (UTC+07), tạm dừng khi chờ công dân bổ sung. Khung liên tục chưa trừ nghỉ trưa. Holidays hiệnrỗng do chưa cung cấp danh sách ngày nghỉ; không tự suy ra ngày lễ.

### Đọc deadline trong API hồ sơ

GET /api/v1/applications/{id}, GET /api/v1/officer/applications/{id} và response thao tác hồ sơ có thêm:

- firstSubmittedAtUtc: thời điểm nộp đầu, không đổi khi resubmit; dữ liệu cũ chưa có căn cứ để điền vẫnnull.
- dueAtUtc: hạnUTC do Workflow tính, có thể thay khi resume.
- documentsReadyAtUtc: aliasUTC của documentsReadyAt hiện có, không thêm mốc hoàn thành khác.
- deadlineStatus: KNOWN, UNKNOWN hoặc NOT_APPLICABLE. UNKNOWN không đồng nghĩa trễ hạn; NOT_APPLICABLE chỉ khi chính sách được cấu hình rõ Applicable=false lúc nộp đầu.
- deadlinePolicyCode/deadlinePolicyVersion, deadlineCalendarCode/deadlineCalendarVersion: mã/phiên bản đã áp dụng cho hồ sơ.
- deadlinePausedAtUtc: thời điểm bắt đầu pause đang mở, null nếu không pause.
- deadlineHistory: mảng {action,atUtc,actorId,dueBeforeUtc,dueAfterUtc}, action PAUSE/RESUME/CANCEL. Đây là auditdeadline riêng, không thay lịch sử trạng thái.

Các API danh sách công dân/cán bộ cũng trả firstSubmittedAtUtc,dueAtUtc,documentsReadyAtUtc,deadlineStatus,deadlinePausedAtUtc. FE không gửi dueAtUtc hay tự tính hạn để lưu. Quyền owner/cùng phường/Manager read-only giữ nguyên.

POST submit thành công khởi tạo firstSubmittedAtUtc và chụp toàn bộ chính sách/lịch ngày nghỉ vào DB. POST request-revision dừng đồng hồ; POST resubmit dùng lịch đã chụp để cộng giờ làm việc bị tạm dừng vào dueAtUtc. Không reset45giờ khi nộp lại. Hồ sơ trễ trước pause vẫn trễ, không được xóa phần trễ bằng yêu cầu bổ sung. Cancel đóng pause bằng sự kiện CANCEL, không chuyển sang hoàn thành.

Ví dụ nộp thứHai07:30 thì hạn thứSáu16:30; nộp ngoài giờ bắt đầu tính vào phiên làm việc tiếp theo. Backend lưuUTC, FE hiển thị giờViệtNam. Với lịch ngày nghỉ đã chốt cần tạo CalendarVersion mới trong cấu hình; hồ sơ cũ giữ snapshot, không tự áp dụng lịch mới.

### GET /api/v1/analytics/overview

Giữ Bearer và quyền Manager/phường hiện có, query From,To,ProcedureId như trước. Thêm deadlines:

- completedOnTime: đã sẵn sàng và documentsReadyAtUtc<=dueAtUtc.
- completedLate: đã sẵn sàng và documentsReadyAtUtc>dueAtUtc.
- completedMissingDeadline: đã sẵn sàng nhưng UNKNOWN, ghi riêng.
- completedNotApplicable: đã sẵn sàng nhưng NOT_APPLICABLE, ghi riêng.
- completedSampleCount: completedOnTime+completedLate. Chỉ số này là mẫu số của onTimeRate/lateRate, tỷ lệ0..100; không có mẫu trảnull.
- openOverdue: hồ sơ SUBMITTED/UNDER_REVIEW/NEED_REVISION còn mở và quá hạn, tách khỏi completedLate.
- openMissingDeadline, openNotApplicable, openPaused: số hồ sơ mở tương ứng.
- asOfUtc: thời điểm snapshotdeadline.

Hồ sơ hoàn thành lọc theo ngày documentsReadyAtUtc trong From–To (ngàyVN). Backlog mở là snapshot HIỆN TẠI của cùng phường/thủ tục, không giới hạn ngày nộp và không cộng dồn từng ngày; không dùng nó như backlog tại ngày To trong quá khứ. Pause trước hạn không bị tính quá hạn trong lúc chờ; pause sau hạn vẫn thuộc openOverdue. openPaused có thể giao với openOverdue.

TimelinessStatus=AVAILABLE nếu có mẫu hợp lệ; NO_KNOWN_COMPLETED_DEADLINES nếu không có. Metrics/totals cũ vẫn dùng cache theo Coverage; phầndeadline đọc trực tiếp Workflow nên có asOfUtc riêng và có thể mới hơn totals. Trends/procedures/officers cũng trả deadline và tỷ lệ; xem hướng dẫn bổ sung bên dưới. Nguồn Workflow mất kết nối/chưa deploy/khóa sai/nguồn quá lớn:overview trả lỗi, không giả lập0 hoặc tính UNKNOWN thành trễ. API nội bộ /internal/analytics/v1/deadlines dùng X-Analytics-Key, không dùng trực tiếp từ FE. Analytics cần Workflow:BaseUrl và Analytics:ServiceKey kể cả JobsEnabled=false.


### Bổ sung BE ngày 22/10/2026 — Đánh giá, tiền kiểm theo checklist và thống kê deadline

**POST /api/v1/citizen/feedback/{applicationId}**

Công dân có role REGISTERED_CITIZEN đánh giá hồ sơ của chính mình, kể cả sau khi cán bộ tiếp nhận tại quầy. Gửi Bearer và body `{"rating":5,"comment":"Hướng dẫn rõ ràng"}`. Hai trạng thái nhận đánh giá là DOCUMENTS_READY và OFFICIALLY_RECEIVED. Thành công201; mỗi hồ sơ chỉ có một đánh giá. Sai trạng thái409 feedback.not_ready: đợi giấy tờ sẵn sàng; đã đánh giá409 feedback.already_exists: gọi GET cùng đường dẫn để xem lại. Rating1–5, comment tối đa2000; không đổi quyền sở hữu hoặc cho phép sửa/xóa đánh giá. Hồ sơ người khác404, thiếu quyền403, token không hợp lệ401.

**POST /api/v1/ai-ocr/documents/verify**

Dùng kiểm tra sơ bộ ảnh/PDF đã tải lên đúng mục giấy tờ. FE chỉ gửi applicationId và documentId cùng Bearer. BE lấy ChecklistId từ DocumentForm, đọc checklist của hồ sơ từ Workflow và dùng Title làm loại giấy tờ cần đối chiếu. Giữ nguyên quyền tải của DocumentForm: chủ hoặc cán bộ được phân công; không dùng token đặc quyền. expectedType từ FE được bỏ qua kể cả khi khác checklist. Response200 giữ các trường cũ, thêm checklistId và expectedType thực tế.

Thiếu liên kết409 ai.checklist_required: tải giấy tờ lên đúng mục; mục không còn trong hồ sơ hoặc không có tên409 ai.checklist_mismatch: đối soát checklist. Sai hồ sơ/quyền401/403/404; Workflow chưa cấu hình503 ai.workflow_not_configured, dữ liệu nguồn sai503 ai.checklist_source_invalid. Cấu hình trên AIOCR thêm `Services__ApplicationWorkflow__BaseUrl` là địa chỉ Workflow; giữ `Services__DocumentForm__BaseUrl`. Docker Compose đã thêm URL nội bộ. Không thay model/key AI. AI chỉ tư vấn về loại và chất lượng, không chứng minh tính pháp lý, số bản, hay tự đánh dấu checklist hoàn tất.

**GET /api/v1/analytics/trends**, **GET /api/v1/analytics/procedures**, **GET /api/v1/analytics/officers**

Manager dùng Bearer, dữ liệu giới hạn phường của Manager do IAM xác minh. Giữ query From/To dạng yyyy-MM-dd (1–366ngày, không tương lai), ProcedureId tùy chọn; procedures/officers giữ Page/PageSize. Thành công200; mỗi item thêm deadlines với completedOnTime, completedLate, completedMissingDeadline, completedNotApplicable, completedSampleCount, openOverdue, openMissingDeadline, openNotApplicable, openPaused, asOfUtc, onTimeRate, lateRate.

- Trends: phân ngày hoàn tất theo UTC+07; deadlines của mỗi ngày chỉ chứa hồ sơ đã sẵn sàng. currentBacklog ở cấp response là hồ sơ đang mở tại asOfUtc, không phải lịch sử backlog ngày To; không cộng backlog vào từng ngày.
- Procedures: nhóm theo procedureId của hồ sơ; gồm cả nhóm có deadline nhưng cache metrics chưa có. Coverage vẫn phản ánh phần cache chưa cập nhật.
- Officers: nhóm theo assignedOfficerId lưu trên hồ sơ, không suy đoán người phụ trách cho dữ liệu cũ. unassignedDeadlines ở cấp response chứa hồ sơ chưa có cán bộ, không nằm trong totalCount cán bộ. Không dùng đây như bằng chứng trách nhiệm cá nhân hoặc thống kê lịch sử điều chuyển.
- Hoàn tất đúng hạn khi ready<=due; trễ khi ready>due. UNKNOWN/NOT_APPLICABLE tách riêng và loại khỏi mẫu số. Tỷ lệ0–100, null khi mẫu số0. OpenOverdue chỉ tính hồ sơ đang mở, giữ quy tắc pause hiện có; openPaused có thể giao với openOverdue nếu đã quá hạn trước khi dừng.
- Deadline đọc trực tiếp Workflow còn totals đọc cache Analytics: xem asOfUtc và Coverage riêng. Cả ba API nay cần Workflow:BaseUrl và Analytics:ServiceKey kể cả JobsEnabled=false. Nguồn thiếu procedureId trả503 analytics.deadline_dimensions_missing: deploy Workflow mới trước Analytics. Các lỗi nguồn khác giữ ProblemDetails, không giả0. Dữ liệu cũ vẫn UNKNOWN; không tạo deadline hồi tố.

Không có migration mới trong lượt này. Cần cập nhật image Workflow, AnalyticsSystem, AIOCR; FE nghiệm thu trên giao diện sau deploy.

## TASK-26 — Email trước, SMS sau (22/10/2026)

Email được tạo từ sự kiện Workflow qua RabbitMQ → inbox → notification_deliveries; không có API public gửi email tùy ý. IN_APP vẫn hoạt động độc lập. Nội dung HTML responsive và plain-text cùng được gửi, tên hiển thị “Hệ thống Một cửa WardMate”. Các trạng thái SUBMITTED/NEED_REVISION/DOCUMENTS_READY/OFFICIALLY_RECEIVED có hướng dẫn tương ứng, mã hồ sơ, thủ tục, tên công dân từ IAM, thời gian UTC+07. Yêu cầu bổ sung hiển thị Reason của lịch sử trạng thái; tiếp nhận tại quầy hiển thị ReceiptNumber/AppointmentDate. Thiếu thông tin hiển thị rõ “Chưa có thông tin”, không suy đoán. Các loại thông báo khác dùng nội dung chung.

### API nội bộ phục vụ email — không gọi từ FE

GET `/internal/notifications/v1/applications/{applicationId}/events/{eventId}/email-context` tại Workflow: gửi header X-Analytics-Key trùng Analytics:ServiceKey (tối thiểu32byte), không body. 200 trả applicantId, applicationCode, procedureTitle, status, notes, receiptNumber, appointmentDate, updatedAtUtc. Chỉ trả khi eventId là lịch sử trạng thái thuộc đúng applicationId; không có404, khóa sai/chưa cấu hình401. Cache-Control:no-store. Chỉ Analytics dùng API này, không đưa khóa lên FE. Notes lấy lịch sử của sự kiện, không phải ghi chú mới nhất; receipt/appointment chỉ trả cho OFFICIALLY_RECEIVED. Tên thủ tục, biên nhận, ngày hẹn là dữ liệu đang lưu trong hồ sơ tại lúc gửi, chưa có snapshot email riêng.

POST `/internal/v1/notification-recipients` tại IAM giữ body/quyền/khóa X-Notification-Service-Key hiện tại; DTO bổ sung fullName nullable từ hồ sơ người dùng. Không thay hợp đồng notification public/SignalR. Tên đọc từ IAM tại thời điểm gửi; không có profile sẽ hiển thị chưa có thông tin. Không đưa tên/ý kiến/email/số điện thoại thêm vào RabbitMQ event.

### Cấu hình và chạy local

Mẫu `appsettings.Development.json` của Analytics có Gmail smtp.gmail.com:587, Email.Enabled=true, From/Username đã cấu hình và Password rỗng. SMS mặc định false. Password phải lấy từ biến môi trường Email__Password; không điền vào file mẫu. Chạy ở root bằng PowerShell:

```powershell
./scripts/start-notifications-local.ps1
```

Script đọc docker/.env vào biến môi trường tiến trình rồi chạy Analytics localhost:5006; không tự chạy Docker, migration hoặc gửi email thử. Cần IAM localhost:5001, Workflow localhost:5003, PostgreSQL notifications localhost:5436 đã migration và RabbitMQ localhost:5672. Dừng container Analytics đang chiếm5006 trước khi chạy bản local. Có thể phát sinh email thật cho sự kiện đang chờ khi worker chạy.

Cấu hình cần có: NOTIFICATIONS_DB_PASSWORD, JWT_KEY, NOTIFICATIONS_ENABLED=true, NOTIFICATIONS_SERVICE_KEY (giống IAM), ANALYTICS_SERVICE_KEY (giống Workflow), RABBITMQ_USER/PASSWORD, NOTIFICATION_EMAIL_ENABLED=true, NOTIFICATION_EMAIL_HOST/PORT/FROM/USERNAME/PASSWORD. Script dùng issuer wardmate/audience wardmate-client theo Compose; nếu hệ thống của bạn dùng giá trị khác, chỉnh cho khớp IAM trước khi chạy. Không dùng app password Gmail như mật khẩu tài khoản thường.

Email:FrontendBaseUrl (Compose: NOTIFICATION_FRONTEND_URL) mặc định https://wardmate-frontend.vercel.app/. CTA ghép notification.ActionUrl với URL này, chỉ chấp nhận cùng origin HTTP(S), escape HTML tất cả nội dung. URL sai/khác origin: bỏ CTA và hướng dẫn đăng nhập, không gửi liên kết lạ.

SMTP bật TLS, timeout thực tế bằng cancellation15giây (thêm SmtpClient.Timeout=15000). Lỗi SMTP/timeout ghi mã notification.smtp_failed / notification.smtp_timeout, retry hiện có tối đa10lần rồi Dead; worker tiếp tục. Sent chỉ nghĩa SMTP chấp nhận, không bảo đảm vào inbox. Gmail không có idempotency: nếu đã nhận thư nhưng kết nối/commit DB lỗi, retry có thể gửi trùng. X-WardMate-Delivery-Id giúp đối soát, không tự chống trùng ở Gmail. Thiếu cấu hình/contact trả Blocked; hàng Blocked không tự chạy lại khi đổi cấu hình. Chưa có API requeue hoặc webhook bounce.

Ghi nhận lịch sử 22/10/2026 (đã được thay thế bởi tích hợp eSMS 23/10/2026 ở cuối tài liệu): SMS vẫn tắt. ISmsSender trả SmsDeliveryResult(Success,Status,ProviderMessageId,ErrorCode); adapter chưa có nhà cung cấp trả false/Blocked/null/sms.provider_not_configured, không gọi mạng. Chưa lưu ProviderMessageId vào DB hoặc nhận webhook trạng thái. Khi chọn Brandname sẽ bổ sung adapter, quy tắc retry và lưu mã nhà cung cấp; không báo đã hoàn tất gửi SMS thật.

Deploy code liên quan IAM và Workflow trước Analytics để có fullName và email-context; giữ hai service key đúng. Không migration mới. Không thay Gemini/billing.

## IAM — Email chào mừng và OTP đổi/đặt lại mật khẩu (22/10/2026)

**Đăng ký**: POST `/api/v1/auth/register` giữ body và response201 hiện tại. Tạo tài khoản và một bản ghi `iam.welcome_emails` trong cùng giao dịch; worker gửi HTML/plain-text chào mừng gồm FullName, Username, Email, hướng dẫn và CTA đăng nhập. SMTP lỗi không làm mất đăng ký; retry tối đa10 lần, không tạo trùng hàng theo UserId. SMTP không có bảo đảm exactly-once: trường hợp đã nhận thư nhưng commit DB lỗi có thể gửi trùng. Email này không xác minh quyền sở hữu email và không phải bước kích hoạt tài khoản.

### Yêu cầu OTP

POST `/api/v1/auth/password-otp/request`

Dùng khi quên mật khẩu hoặc muốn xác nhận bằng email. Browser gửi `X-CSRF-Protection: 1`, Origin đúng cấu hình như login; body JSON:

```json
{ "emailOrUsername": "ten_dang_nhap", "channel": "EMAIL", "purpose": "FORGOT_PASSWORD" }
```

Channel mặc định EMAIL, Purpose mặc định FORGOT_PASSWORD (không cần đăng nhập). Nếu purpose=CHANGE_PASSWORD, gửi thêm Bearer của chính tài khoản cần đổi; thiếu đăng nhập401. Purpose và channel phân biệt hoa thường. FE không nhận/gửi actorId; BE lấy từ JWT.

202 trả `{message: ...}` chung cho tài khoản hợp lệ, tài khoản không tồn tại/khóa/không đúng chủ, hoặc chưa đủ60giây. Không hiểu202 là SMTP đã giao thư vào inbox. Kiểm tra đúng email/tên đăng nhập và thư rác; đợi60giây rồi thử lại. Lỗi SMTP được ghi mã log chung và vô hiệu mã vừa tạo, vẫn trả202 để không lộ tài khoản. Cấu hình email bị tắt/thiếu503 iam.email_unavailable. SMS luôn400 iam.sms_unavailable “Kênh SMS đang được bảo trì, vui lòng chọn Email”. Dữ liệu sai400 validation_failed; CSRF/Origin sai403 iam.csrf_rejected.

Mỗi tài khoản (kể cả dùng cả email lẫn username, cả hai purpose) chỉ sinh mã mới sau60giây; mã mới vô hiệu mọi mã chưa dùng cũ. Mã ngẫu nhiên mật mã6chữ số,5phút tính từ lúc sinh. Thời gian SMTP có thể làm thời gian còn lại ngắn hơn5phút khi thư đến. Rate limit theo IP chung cho hai API:10request/phút/replica, vượt429 iam.otp_rate_limited + Retry-After60. Giới hạn tài khoản/attempt được thực thi trong DB; giới hạn IP không phải distributed limiter.

### Xác nhận OTP và đổi mật khẩu

POST `/api/v1/auth/password-otp/verify-and-change`

```json
{
  "emailOrUsername": "ten_dang_nhap",
  "otp": "839201",
  "newPassword": "ViDu-MatKhauMoi!2026",
  "confirmNewPassword": "ViDu-MatKhauMoi!2026",
  "purpose": "FORGOT_PASSWORD"
}
```

OTP là chuỗi6chữ số để giữ số0 đầu. Purpose phải trùng yêu cầu gửi; CHANGE_PASSWORD cần Bearer của chính chủ. Mật khẩu tối thiểu8ký tự, có hoa/thường/số/ký tự đặc biệt, tối đa72byte UTF-8 theo bcrypt; xác nhận phải khớp. Password không được gửi qua query string/log. Body haiAPI tối đa4096byte.

200 `{message: "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."}`. Giao dịch khóa user bảo đảm một mã không dùng hai lần khi request đồng thời: cập nhật password_hash, đánh dấu mã IsUsed=true, vô hiệu các mã khác, thu hồi toàn bộ refresh token. Browser xóa refresh cookie; FE xóa access token trên client rồi về login. Access JWT đã cấp vẫn có thể dùng đến khi hết hạn tại các service; không tuyên bố bị vô hiệu ngay trên toàn hệ thống.

400 iam.otp_invalid dùng chung cho mã sai/hết hạn/đã dùng/tài khoản không khả dụng; sai mã tăng Attempts và hết5lần thì không nhận nữa. Yêu cầu mã mới sau cooldown. Sai định dạng/mật khẩu400 validation_failed; không tăng Attempts khi validation thất bại trước bước kiểm tra OTP. 429 đợi Retry-After. API đổi bằng mật khẩu hiện tại `/api/v1/users/me/change-password` vẫn giữ tương thích và nay cũng vô hiệu OTP cũ; không bắt buộc OTP cho API cũ trong lượt này.

### Cấu hình và migration

IAM appsettings có Email Host smtp.gmail.com, Port587, Username/From, DisplayName; Password rỗng và Enabled=false mặc định. Compose IAM ánh xạ NOTIFICATION_EMAIL_* từ docker/.env giống Analytics. Email:LoginUrl (IAM_EMAIL_LOGIN_URL) là CTA FE cấu hình được. Không đặt app password vào repo. Chạy không Docker cần đặt Email__Enabled=true và các Email__* trong môi trường process hoặc user-secrets; .NET không tự đọc docker/.env.

OTP lưu HMAC-SHA-256 (64hex) có salt Id/UserId/Purpose và khóa bí mật, không lưu OTP thô. PasswordOtp__HashKey tối thiểu32byte nếu cấu hình; nếu không có dùng Jwt:Key hiện có. Các replica phải cùng khóa; thay khóa làm mã đang chờ không dùng được. Đây là SHA-256 có khóa, tăng bảo vệ so với hash trần6số. OTP không nằm trong welcome queue, không tự retry gửiOTP vì không lưu mã thô; người dùng yêu cầu lại. Chưa có job dọn OTP hết hạn hoặc màn hình requeue welcome hết retry.

Áp dụng migration AddUserOtpsTable trên database IAM trước khi deploy code mới (kể cả Email tắt, đăng ký vẫn ghi hàng đợi). Tạo schema iam với user_otps và welcome_emails; FK tới users hiện tại ở public. SQL idempotent và hướng dẫn baseline xem PROGRESS.md. Không chạy script ở DB Workflow/DocumentForm. Chưa áp dụng SQL lên cloud trong lượt này.

### SQL Supabase cho IAM OTP

File: `docs/sql/iam-add-user-otps.sql`. Chỉ chạy trong database IAM, yêu cầu lịch sử đã có `20261021100000_CounterReceiptPermissions`. Script EF idempotent có preflight và transaction; nếu thiếu baseline sẽ dừng, không tự sửa lịch sử migration. Nếu đã có migration đích, script không tạo lại bảng.

1. Chọn đúng dự án Supabase IAM → SQL Editor.
2. Mở file SQL trên, sao chép toàn bộ và Run.
3. Xác nhận bằng truy vấn:

```sql
SELECT migration_id, product_version
FROM public."__EFMigrationsHistory"
WHERE migration_id = '20261022063951_AddUserOtpsTable';
SELECT to_regclass('iam.user_otps'), to_regclass('iam.welcome_emails');
```

4. Deploy IAM mới với Email__Enabled=true, cấu hình SMTP và Jwt__Key hợp lệ. Đăng ký mới sẽ tạo welcome queue; tài khoản cũ không được gửi chào mừng hồi tố.

Nếu preflight báo thiếu baseline: cập nhật các migration IAM trước đó rồi chạy lại, không INSERT thủ công vào __EFMigrationsHistory. Mất kết nối hoặc lỗi transaction: kiểm tra trạng thái rồi chạy lại script; không tự tạo bảng riêng ngoài migration.


### Cập nhật IAM từ baseline cũ (22/10/2026)

Nếu SQL OTP báo thiếu baseline CounterReceiptPermissions và migration IAM mới nhất là `20261016154952_WorkflowReadinessPermission`, dùng toàn bộ `docs/sql/iam-catchup-readiness-to-otp.sql` trong SQL Editor của database mà IAM đang kết nối. Script áp dụng lần lượt ManagerWorkflowReadScope, CounterReceiptPermissions, AddUserOtpsTable; bỏ qua migration đã ghi nhận. Không cần chạy lại script OTP riêng sau đó.

Script cấp MANAGER quyền đọc Workflow và bỏ bốn quyền xử lý (assign/comments/revision/readiness); cấp quyền tiếp nhận cho FRONT_DESK_OFFICER và quyền hủy cho role đã có workflow.submit; cuối cùng tạo iam.user_otps và iam.welcome_emails. Giữ nguyên lịch sử Procedure nếu database đang dùng chung. Không xóa preflight hay chèn migration history thủ công. Mỗi migration có transaction riêng; nếu bước sau lỗi, bước trước có thể đã commit. Sửa nguyên nhân rồi chạy lại script. Không chạy đồng thời với auto-migrate của ứng dụng.

Xác nhận đủ ba migration mới trong public."__EFMigrationsHistory" và hai bảng iam.user_otps, iam.welcome_emails trước khi thử API OTP. Đây là cập nhật schema/quyền, không đổi hợp đồng API.


### IAM — OTP qua eSMS (22/10/2026)

Dùng POST /api/v1/auth/password-otp/request với body {"emailOrUsername":"ten-dang-nhap","channel":"SMS","purpose":"FORGOT_PASSWORD"}. API dùng số di động đã lưu trong UserProfile, không nhận số tùy ý từ client. CHANGE_PASSWORD cần Bearer của chính tài khoản. Browser tiếp tục cần Origin được phép và X-CSRF-Protection: 1; giới hạn 10 request/IP/phút hiện hữu không đổi.

202 trả message chứa số đã che khi eSMS chấp nhận yêu cầu gửi thật. CodeResult=100 là nhà cung cấp chấp nhận, không phải xác nhận điện thoại đã nhận. Sandbox=1 trả message thử nghiệm không gửi SMS. Tài khoản không khả dụng hoặc đang cooldown vẫn trả 202 chung. Thiếu số:400 iam.phone_not_configured; sai định dạng:400 iam.phone_invalid; tắt/thiếu cấu hình:400 iam.sms_unavailable; eSMS lỗi/timeout:503 iam.sms_delivery_failed. Cập nhật số hoặc chọn EMAIL; đợi tối thiểu60s khi gửi lại. Không tự retry SMS để tránh gửi trùng/tính phí trùng.

Xác nhận bằng POST /api/v1/auth/password-otp/verify-and-change với {"emailOrUsername":"ten-dang-nhap","otp":"123456","newPassword":"<mật khẩu mới>","confirmNewPassword":"<mật khẩu mới>","purpose":"FORGOT_PASSWORD"}. Không cần channel; lấy challenge mới nhất theo purpose dù EMAIL hay SMS. Thành công200, sai/hết hạn400 iam.otp_invalid; dùng một lần,5phút,5lần thử. Thành công thu hồi refresh token; JWT access hiện hữu vẫn có thể sống đến hết hạn. FE xóa token và yêu cầu đăng nhập lại.

Cấu hình IAM: Sms__Enabled=true, Sms__Provider=eSMS, Sms__ApiKey/Sms__SecretKey từ secret môi trường, Sms__Brandname=Baotrixemay, Sms__SmsType=2. Sms__Sandbox=1 chỉ kiểm tra tích hợp; =0 gửi thật có thể tính phí. Compose đọc đúng các tên Sms__* từ docker/.env; appsettings và .env.example không chứa khóa, mặc định tắt và Sandbox1. Nội dung mẫu: {CODE} la ma xac minh dang ky Baotrixemay cua ban. Brandname khác được thay vào cùng mẫu nhưng phải đăng ký/duyệt mẫu tương ứng với eSMS. Không thay cấu hình SMS của AnalyticsSystem. Không cần migration mới vì user_otps đã hỗ trợ SMS.

Tài liệu nhà cung cấp: https://developers.esms.vn/esms-api/ham-gui-tin/tin-nhan-sms-otp-cskh và https://developers.esms.vn/en/esms-api/moi-truong-test-sandbox .


### Bảo mật tài khoản — xác minh số, hạn mức OTP, thu hồi phiên (23/10/2026)

**Xác minh số điện thoại (chính chủ, Bearer bắt buộc):**
- GET /api/v1/users/me/phone-verification trả200 {verified,maskedPhoneNumber}. Chỉ dùng cho tài khoản đang đăng nhập; số cũ chưa tự được công nhận là đã xác minh.
- POST /api/v1/users/me/phone-verification/request: {"phoneNumber":"0987654321","currentPassword":"<mật khẩu hiện tại>"}. Trả202 message khi eSMS chấp nhận; nhận số mới chỉ trong challenge, chưa thay profile. Cần profile cá nhân và SMS thật (Enabled, Sandbox0).400 iam.phone_invalid/iam.sms_unavailable/iam.otp_limit;401 mật khẩu sai/phiên không hợp lệ;503 nhà mạng lỗi. Giới hạn body4KiB và rate limiter OTP chung10request/IP/phút. Không nhập số của người khác.
- POST /api/v1/users/me/phone-verification/confirm: {"otp":"123456"}.200 message khi khớp mã trong5phút, tối đa5lần thử. Sau đó cập nhật phone profile và dấu xác minh;400 iam.otp_invalid hoặc iam.profile_required. Khi nhập sai mã đã đúng định dạng, Attempts tăng. Request mới vô hiệu OTP trước đó. Thay phone qua API profile thông thường xóa dấu xác minh; cần thực hiện luồng này lại.

**Khôi phục mật khẩu:** POST /api/v1/auth/password-otp/request giữ body hiện có nhưng202 luôn là message chung: không trả masked phone hoặc lỗi thiếu phone theo tài khoản nữa. SMS chỉ gửi nếu số profile khớp số đã xác minh. Người chưa xác minh dùng Email để khôi phục, đăng nhập rồi xác minh số. Không dựa vào202 để khẳng định tin đã đến; provider lỗi cũng trả202 chung và expire mã. Lỗi cấu hình kênh vẫn400/503, limiterIP429. Verify-and-change giữ hợp đồng cũ. Giới hạn mặc định rolling24h:10 yêu cầu/tài khoản, trong đó5SMS (bao gồm xác minh phone, lần gửi lỗi đã ghi nhận); cooldown60s xuyên kênh. Có thể chỉnh PasswordOtp__MaxRequestsPer24Hours (1..100), PasswordOtp__MaxSmsPer24Hours (1..20). Đây không phải ngân sách toàn hệ thống.

**Thu hồi phiên:** JWT IAM mới có security_stamp. Khi đổi mật khẩu bằng mật khẩu cũ hoặc OTP, BE đổi stamp và revoke mọi refresh token cùng transaction. IAM từ chối access token có stamp cũ/thiếu stamp. Các service AIOCR, ProcedureCatalog, DocumentForm, Workflow, Analytics kiểm tra IAM qua Iam__BaseUrl sau authentication, không cache;401 auth.session_revoked khi phiên bị từ chối,503 auth.session_unavailable khi IAM không xác minh được. FE nhận401 thì xóa phiên/đăng nhập lại;503 thì báo tạm gián đoạn, không xóa phiên ngay. Request đã vào xử lý trước lúc đổi mật khẩu không bị hủy hồi tố. SignalR hiện kiểm tra trước gửi/gọi hub và định kỳ (mặc định15s), không khẳng định socket đóng ngay tức thì.

**Triển khai:** áp dụng docs/sql/iam-account-security-hardening.sql vào đúng DB IAM đã có AddUserOtpsTable; sau đó deploy IAM và cả5service tiêu thụ token. Cấu hình Iam__BaseUrl tới IAM thực trên Azure, không dùng localhost. Compose đã map; chạy trực tiếp có localhost5001 cho AIOCR/Procedure/Document. Không còn chấp nhận JWT thiếu stamp: client đăng nhập lại hoặc refresh nếu refresh còn hợp lệ. Không chạy SQL đồng thời auto-migrate. Chưa áp dụng SQL cloud trong lượt triển khai code.

**Dọn dữ liệu:** worker IAM chạy mỗi giờ, mỗi loại tối đa1000 hàng/lượt; xóa OTP hết hạn, refresh token hết hạn và welcome mail gửi thành công quá30ngày. AccountSecurity__RetentionDays clamp2..365; giữ tối thiểu2ngày để không mất cửa sổ hạn mức24h. Email đang chờ/thất bại giữ lại để điều tra; chưa có màn quản trị gửi lại.

Khi sửa số qua profile hoặc xóa profile, các mã OTP SMS đang hiệu lực cũng bị hủy. FE phải yêu cầu mã mới sau khi hoàn tất xác minh số.


### TASK-25/27 — Chốt hợp đồng thông báo Workflow (23/10/2026)

Luồng BE: lưu trạng thái/nhận xét và workflow_outbox cùng SaveChanges -> publisher persistent RabbitMQ có confirms/mandatory -> queue wardmate.notifications.workflow.v1 -> notification_inbox theo EventId (ACK chỉ sau insert) -> transaction tạo notifications và notification_deliveries -> worker phát tín hiệu PostgreSQL -> listener mỗi replica Analytics -> kiểm tra IAM -> SignalR ReceiveNotification/UpdateUnreadCount.

Người nhận: công dân chủ hồ sơ nhận thay đổi trạng thái/nhận xét; hồ sơ SUBMITTED chưa phân công báo cán bộ cùngphường có workflow.queue.read; SUBMITTED/UNDER_REVIEW đã phân công chỉ cán bộ đó nếu còn đúngphường/quyền. Không gửi nội dung biểu mẫu/nhận xét hay token qua RabbitMQ; notification chỉ hướng dẫn mởhồsơ. Tài khoản khóa/đổi phường bị kiểm tra lại trước gửi; người dùng offline vẫn có thông báo trong DB.

SignalR /hubs/notifications giữ nguyên JWT và contract: GetNotifications(page=1,pageSize=20) (1..100), GetUnreadCount(), MarkRead(notificationId). FE gọi GetNotifications/GetUnreadCount sau mỗi kết nối/reconnect và khi quay lại tab; deduplicate ReceiveNotification theo id vì transport có thể giao lại. MarkRead chỉ chínhchủ/đúngphường; cập nhật bộđếm tới replica khác bằngNOTIFY. Không có HTTP REST notifications mới trong lượt này.

Triển khai: bật RabbitMq__Enabled trên Workflow/Analytics, Notifications__WorkersEnabled trên Analytics; cùng host/vhost/user/password/TLS RabbitMQ. IAM/Analytics dùng cùng Notifications__ServiceKey đủ32ký tự. Analytics Iam__BaseUrl đúng; migrations outbox/inbox/deliveries hiện hữu đã áp dụng. Giữ ít nhất1replica Workflow và Analytics khi cần worker xử lý liên tục; HTTP scale-to-zero không tự thức dậy chỉ vì có hàng trong DB. RabbitMQ cần volume bền và quyền giới hạn vhost.

Realtime nhiều replica: NotificationsDatabase phải cùngdatabase giữa các replica; LISTEN cần kết nối direct hoặc session-pooling, không dùng transaction-pooling. Nếu connection DB chính dùng transaction pool, thêm ConnectionStrings__NotificationRealtimeDatabase trỏ trực tiếp/session tới cùngDB. Listener tắt multiplexing, keepalive30s; không có Redis dependency. SignalR vẫn cần sticky session khi dùng negotiate và nhiều replica (hoặc WebSocket skipNegotiation phù hợp). NOTIFY chỉ có UUID, phát sau commit, không bền khi listener offline; client tải lại DB qua hub sau reconnect/refresh. Sent của IN_APP nghĩa tín hiệu đã commit, không phải UI đã nhận/đãđọc.

Vận hành: Outbox retry không giới hạn với backoff tối đa300s; Inbox/Delivery tối đa10lần rồiDead. Event sai contract vào Rabbit dead queue; Blocked là kênh chưacấuhình, Suppressed là người nhận khôngcònphùhợp. SQL kiểmtra dưới đây chạy ở DB tươngứng, không trộn database service:

Workflow:
```sql
SELECT count(*) AS pending, min(created_at) AS oldest_pending
FROM workflow_outbox WHERE published_at IS NULL;
SELECT id, attempts, next_attempt_at, last_error
FROM workflow_outbox WHERE published_at IS NULL ORDER BY created_at LIMIT 50;
```
Analytics:
```sql
SELECT "State", count(*) FROM notification_inbox GROUP BY "State";
SELECT "Channel", "State", count(*) FROM notification_deliveries GROUP BY "Channel", "State";
SELECT "Id", "Attempts", "ErrorCode" FROM notification_inbox WHERE "State"='Dead' LIMIT 50;
SELECT "Id", "NotificationId", "Channel", "ErrorCode" FROM notification_deliveries WHERE "State" IN ('Dead','Blocked') LIMIT 50;
```
Sau khi sửa nguyên nhân, operator có thể replay đúng một InboxDead hoặc DeliveryDead/Blocked: cập nhật State=Pending,Attempts=0,NextAttemptAt=now(),ErrorCode=NULL theo Id và điều kiệnstate. Không xóa notification/inbox, không đổi EventId, không replay Sent tựđộng (Email cóthểgửitrùng). Dead queue Rabbit phải kiểmtra/sửa producer và replay cóchọnlọc giữ EventId, không đẩy lại toàn bộ vôhạn. Hiện chưa có API quản trịreplay; thao tácDB/broker dooperator cóquyền. Email/SMS làTASK26; SMS Analytics đã tích hợp eSMS ngày 23/10/2026; xem runbook notification-sms-operations.md, khác SMS OTP IAM.

### Chẩn đoán lỗi xác minh phiên IAM — cập nhật 23/10/2026
Áp dụng cho API được bảo vệ bởi AccountSessionMiddleware, gồm DocumentForm. Response 401 auth.session_revoked hoặc 503 auth.session_unavailable bổ sung traceId để đối chiếu log; không thay đổi request hoặc quyền truy cập. FE gặp 503 giữ phiên và thông báo thử lại, gửi traceId cho vận hành.
Log "IAM session check failed" phân biệt invalid_base_url, upstream_response (kèm HTTP status), transport_* (loại lỗi mạng .NET), timeout và invalid_authorization_header. Chỉ log authority IAM và traceId; không log token, body, query hoặc exception message. Nếu target là localhost trên Azure, kiểm tra cấu hình runtime Iam__BaseUrl; nếu upstream 5xx, kiểm tra IAM cùng thời điểm. Không suy ra lỗi role hoặc applicationId từ 503.

### TASK-23 — Tra cứu QR chuyển hướng trực tiếp PDF & Tự động gộp đơn trọn bộ (23/10/2026)

- **Method & Tuyến đường:** `GET /api/v1/public/applications/{applicationId}/pdf-redirect`
  - **Service:** DocumentForm (Public API, `[AllowAnonymous]`)
  - **Mục đích & Cách sử dụng:** Khi công dân hoặc cán bộ quét mã QR in trên giấy tờ, trình duyệt hoặc thiết bị quét tự động nhận mã HTTP 302 chuyển hướng thẳng đến SAS URL của file PDF lưu trên Azure Blob Storage (thời hạn 15 phút, không cần đăng nhập).
  - **Cơ chế gộp tự động đa đơn (Multi-submission PDF Merge):**
    - Nếu hồ sơ chỉ có đúng 1 đơn: Chuyển hướng thẳng đến file PDF của đơn đó.
    - Nếu hồ sơ có nhiều đơn (2, 3, 5, 10 đơn...): Hệ thống tự động gom các đơn, lọc lấy bản PDF mới nhất của từng đơn (nhóm theo `SubmissionId`), nạp sang Gotenberg `/forms/pdfengines/merge` để nối thành 1 file PDF trọn bộ `combined_application_{guid}.pdf` và chuyển hướng đến file này.
    - Cache: File gộp được tái sử dụng nếu thời gian tạo lớn hơn thời gian của tất cả các đơn thành phần. Khi bất kỳ đơn nào được bấm xuất lại PDF (có bản PDF mới hơn), hệ thống sẽ tự động ghép lại bản mới nhất.
  - **Loại bỏ mã QR trong nội dung tờ đơn:** Quá trình xuất PDF không dán hình ảnh mã QR vào trang cuối biểu mẫu, giữ trang in văn bản của người dân sạch sẽ, chuẩn thể thức văn bản hành chính.
  - **Lưu ý mã lỗi:**
    - `404 qr.not_found`: Hồ sơ chưa có mã QR tra cứu hoặc chưa xuất giấy tờ.
    - `404 qr.pdf_not_found`: Hồ sơ chưa có file PDF nào được tạo.
    - `404 qr.pdf_blob_missing`: File PDF không tìm thấy trên hệ thống Azure Blob Storage.


### Analytics Azure migration và worker — 23/10/2026
Đã áp dụng migration AnalyticsMetricsAndCitizenFeedback vào database cấu hình của Analytics (fallback NotificationsDatabase), tạo schema analytics riêng. Không dùng Database__AutoMigrate để chạy migration các context không liên quan. Analytics__JobsEnabled=true, LookbackDays=7, Cron=15 1 * * *, timezone Asia/Ho_Chi_Minh; worker đã có heartbeat. Dữ liệu metrics chưa có trước lượt tổng hợp đầu tiên: FE hiển thị coverage/missingDates; IT_ADMIN có thể gọi POST /api/v1/analytics/jobs/rebuild với {"from":"2026-10-17","to":"2026-10-23"}, sau đó GET /api/v1/analytics/jobs/{jobId}. Không coi bảng rỗng là đầy đủ dữ liệu. Chưa kiểm tra lại endpoint với JWT người dùng thật sau sửa.

## IAM — Khởi tạo Manager và Procedure Manager (Option A, 23/10/2026)

Hai API dành cho IT_ADMIN đang hoạt động, có quyền RBAC quản trị hiện hữu và `iam.accounts.manage`. Dùng để tạo tài khoản công vụ trực tiếp, không đăng ký công dân trước rồi nâng role. Bearer token và JSON; không nhận role từ client.

### POST /api/v1/accounts/manager

Tạo lãnh đạo gắn phường. Ví dụ body (dữ liệu minh họa):

```json
{"username":"manager_demo","email":"manager@example.com","password":"Example@123","fullName":"Lãnh đạo minh họa","wardId":"11111111-1111-1111-1111-111111111111"}
```

Lấy wardId thật từ `GET /api/v1/accounts/wards`, không dùng GUID minh họa. wardId thiếu/null trả400 `iam.ward_required`; GUID đúng định dạng nhưng không tồn tại (kể cả Guid.Empty) trả404 `iam.ward_not_found`; GUID sai định dạng trả lỗi binding400.

### POST /api/v1/accounts/procedure-manager

Tạo người quản lý danh mục thủ tục. Body cùng các trường như trên; wardId có thể null hoặc bỏ qua. Nếu truyền thì phải tồn tại. Role cố định PROCEDURE_MANAGER, không tự giới hạn quyền Catalog theo wardId.

```json
{"username":"procedure_demo","email":"procedure@example.com","password":"Example@123","fullName":"Cán bộ danh mục minh họa","wardId":null}
```

### Kết quả và cách xử lý lỗi cho cả hai API

- 201 Created: Location `/api/v1/accounts/{id}`; body ManagedUserDto gồm id,username,email,isActive,wardId,wardName,profile,roles. Chỉ có role MANAGER hoặc PROCEDURE_MANAGER tương ứng; profile có fullName; không trả password/hash. ID role lấy từ DB, không hard-code số trong FE. JSON hiện bỏ thuộc tính null: wardId/wardName/các field profile trống có thể không xuất hiện.
- Username <=100 ký tự theo mẫu chữ không dấu/số/_.-; email hợp lệ <=255; fullName bắt buộc <=255. Username/email lưu trim và lowercase, fullName trim.
- Password >=8 ký tự, có hoa/thường/số/ký tự đặc biệt, <=72 byte UTF-8. Không dùng mật khẩu mẫu trong vận hành.
- 400 `validation_failed`: sửa trường theo lỗi; body null hoặc JSON/GUID sai có thể do model binding trả400 theo pipeline ASP.NET hiện tại.
- 401: thiếu/token không hợp lệ, đăng nhập lại. 403 `iam.forbidden` theo policy/handler: tài khoản không đủ vai trò/quyền, không xử lý bằng cách thử lại token của công dân.
- 404 `iam.ward_not_found`: tải lại danh sách phường. 404 `iam.role_not_found`: role hệ thống bị thiếu, nhờ IT Admin khôi phục cấu hình role trước khi thử lại.
- 409 `iam.duplicate_account`: username/email đã có, kể cả tạo đồng thời; đổi thông tin hoặc kiểm tra tài khoản vừa tạo trước khi retry.
- User/profile/role/audit lưu chung transaction dưới khóa RBAC. Audit action `account.manager_created` / `account.procedure_manager_created`, details chỉ wardId; không ghi mật khẩu.
- API này không gửi email welcome/OTP, không tạo token đăng nhập, không thêm cơ chế buộc đổi mật khẩu lần đầu. Người dùng đăng nhập bằng luồng IAM hiện có.
- Giữ nguyên API RBAC nâng cao và quyền tạo front-desk của IT_ADMIN/Manager hiện tại. “Một role” chỉ bảo đảm tại thời điểm tạo qua hai API này, không cấm quản trị RBAC gán thêm role sau đó.
- Không cần migration/cấu hình mới. Cần deploy IAM mới để Swagger/routes xuất hiện; build local không chứng minh đã deploy.
## TASK-26 — SMS thông báo hồ sơ qua eSMS (23/10/2026)

Đã thay stub SMS Analytics bằng adapter eSMS; phần SMS OTP IAM không thay đổi. Workflow phát sự kiện qua TASK-27, inbox tạo delivery SMS khi `Sms:Enabled=true`, worker lấy liên hệ hiện tại từ IAM và gửi nền. FE không gọi API gửi SMS trực tiếp; routes, body, response HTTP, DTO và token của API hồ sơ/thông báo giữ nguyên. Việc gửi thất bại không đổi kết quả tạo/nộp hồ sơ. Không dùng trạng thái hồ sơ hoặc HTTP 200 để kết luận SMS đã đến điện thoại.

Hướng dẫn cấu hình, migration, truy vấn đối soát và phục hồi: [SMS operations](notification-sms-operations.md). Mẫu cấu hình mặc định tắt, Sandbox=1, secret và template để trống. Mẫu CSKH cần được nhà mạng duyệt; không tái sử dụng mẫu OTP. Missing template/phone/config trả trạng thái delivery Blocked, không phải ProblemDetails của API hồ sơ. Timeout/kết quả không rõ là Unknown; crash có thể giữ Dispatching; operator phải đối soát trước khi gửi lại. Không áp dụng hướng dẫn replay tổng quát cho Unknown/Dispatching.

Migration `20261023150528_EsmsNotificationDelivery` thêm `external_message_id`, `provider`, `is_sandbox` vào `notification_deliveries`. SQL idempotent: [notification-esms-delivery.sql](sql/notification-esms-delivery.sql), dành cho database Analytics có baseline ReliableNotificationDelivery. Không có endpoint webhook xác nhận SMS đến điện thoại trong phạm vi hiện tại. Không gọi SMS thật hoặc sandbox trong lượt phát triển này.

## Phân công hồ sơ theo lĩnh vực — 24/10/2026

Lĩnh vực là **ưu tiên mềm**: cán bộ vẫn xem/nhận được hồ sơ lĩnh vực khác trong cùng phường. Cán bộ không có assignedCategories phụ trách mọi lĩnh vực. Manager được giao/chuyển người phụ trách qua quyền mới `workflow.assign.manage`; không được comment, yêu cầu bổ sung, xác nhận sẵn sàng hoặc tiếp nhận tại quầy thay cán bộ.

### Dữ liệu FE cần đọc

- GET `/api/v1/users/me` và `/api/v1/users/access-context`: thêm `assignedCategories: [3,7]`. Access-context còn trả `fullName`; Workflow lấy lại ngữ cảnh từ IAM mỗi request, không tin field FE tự gửi.
- Summary của danh sách công dân/cán bộ và ApplicationDto chi tiết có `categoryId`, `categoryName`, `assignedOfficerName`, `assignedAt`, `resubmitCount`, `assignmentVersion`. Các snapshot mới nullable, version bắt đầu 0. Chi tiết thêm `assignmentHistory` (action, fromOfficerId, toOfficerId, actorId, reason, createdAt).
- Snapshot lĩnh vực lấy từ Catalog khi tạo; đổi thủ tục không đổi hồ sơ cũ. Hồ sơ cũ giữ NULL và FE hiển thị “Chưa xác định lĩnh vực”. Không chạy backfill từ Catalog hiện tại vì không chứng minh lĩnh vực lịch sử. Tên cán bộ là snapshot lúc nhận/giao/chuyển, có thể NULL nếu IAM chưa có profile.

### GET `/api/v1/officer/applications` và `/pending`

Giữ quyền đọc, phạm vi phường và loại hồ sơ do chính người gọi nộp. Danh sách tổng hợp giữ các bộ lọc cũ, thêm:

| Query | Cách dùng |
|---|---|
| categoryIds=3,7 | ID dương, tối đa100 phần tử; lọc snapshot |
| scope=my-categories | Lọc lĩnh vực được giao; danh sách lĩnh vực rỗng không lọc; Manager bỏ qua scope này |
| assignee=me hoặc unassigned | Hồ sơ của mình hoặc chưa phân công |
| assignee={GUID} | Chỉ Manager được lọc theo cán bộ cụ thể |
| overdue=true | Deadline KNOWN, có dueAtUtc, chưa sẵn sàng, đang SUBMITTED/UNDER_REVIEW/NEED_REVISION, và dueAtUtc trước thời điểm bắt đầu pause (nếu đang pause) hoặc trước UTC hiện tại (nếu không pause). Hồ sơ đã quá hạn rồi mới chuyển sang chờ bổ sung vẫn là quá hạn, giống `openOverdue` của Analytics |
| sort=submittedAt hoặc -submittedAt hoặc dueAt | Sắp xếp rõ ràng; giá trị NULL xếp cuối |

Không truyền sort: cán bộ thấy lĩnh vực mình trước, rồi UpdatedAt giảm dần (pending dùng SubmittedAt tăng dần), Id làm khóa phụ. Đây là ưu tiên, không ẩn lĩnh vực khác. Manager giữ thứ tự thời gian, không ưu tiên lĩnh vực. `/pending` vẫn giữ phạm vi trạng thái và người phụ trách hiện hữu; dùng danh sách tổng hợp để xem toàn phường. Trả200 ApplicationPage; bộ lọc mới sai trả400 `workflow.validation`, sửa query rồi gọi lại. Deadline UNKNOWN/NOT_APPLICABLE hoặc đã hoàn thành không được gộp vào đang quá hạn. Hồ sơ đang pause chỉ tính quá hạn nếu đã quá hạn trước lúc pause.

### Cán bộ tự nhận / trả lại

- POST `/api/v1/officer/applications/{id}/assign` giữ request không body và policy `workflow.assign`. Nhận khác lĩnh vực vẫn được. Nay ghi snapshot tên, thời điểm, tăng assignmentVersion và lịch sử CLAIM. Người phụ trách được giữ qua lần nộp lại.
- POST `/api/v1/officer/applications/{id}/release`, Bearer cán bộ đang thụ lý, policy `workflow.assign`, body `{"reason":"Chuyển cán bộ phụ trách đất đai","expectedAssignmentVersion":1}`. Version tùy chọn, nên gửi để phát hiện dữ liệu cũ. Lý do trim 5–500 ký tự. Chỉ UNDER_REVIEW: về SUBMITTED, xóa người/tên/thời điểm phân công; giữ nhận xét, snapshot, deadline. Ghi RELEASE. Trả200 ReviewResultDto;400 `workflow.validation`,403 `workflow.not_assignee`,404 ngoài phạm vi,409 `workflow.invalid_status` hoặc `workflow.assignment_conflict`. Gặp409 tải lại; không tự retry với version mới.

### Manager giao/chuyển

PUT `/api/v1/manager/applications/{id}/assignee`, Bearer role MANAGER + `workflow.assign.manage`:

```json
{"officerId":"00000000-0000-0000-0000-000000000001","reason":"Cân bằng khối lượng công việc","expectedAssignmentVersion":0}
```

`expectedAssignmentVersion` **bắt buộc**, lấy từ summary/detail gần nhất. Lệnh khóa dòng và so sánh version để một yêu cầu giao cũ không biến thành chuyển người sau khi cán bộ vừa nhận. Nếu chưa phân công ở SUBMITTED: chuyển UNDER_REVIEW và ghi ASSIGN. Nếu đang UNDER_REVIEW/NEED_REVISION/DOCUMENTS_READY và có người phụ trách: giữ trạng thái, ghi TRANSFER. Không cho DRAFT/OFFICIALLY_RECEIVED/CANCELLED hoặc hồ sơ cũ không có snapshot nộp. Không thay deadline/pause; không cho Manager xử lý hồ sơ mình nộp.

Cán bộ đích được tra IAM: active, FRONT_DESK_OFFICER, không kiêm MANAGER, cùng phường, không phải công dân nộp hồ sơ. Không ràng buộc lĩnh vực. Trả200 ReviewResultDto;400 `workflow.validation`;403 thiếu quyền;404 ngoài phường/hồ sơ mình;422 `workflow.invalid_assignee`;409 `workflow.same_assignee`, `workflow.assignment_conflict`, `workflow.invalid_status`, `workflow.snapshot_missing`;503 `workflow.iam_unavailable` khi chưa xác minh được IAM. Sửa dữ liệu đích hoặc tải lại sau409; khi503 đợi IAM phục hồi.

IAM bổ sung GET `/api/v1/manager/officers/{userId}/assignment-context`: Manager có `workflow.assign.manage` tra cán bộ đích, trả200 `{id,fullName,wardCode}`;403 thiếu quyền,404 cán bộ không hợp lệ/ngoài phạm vi. Workflow chuyển tiếp Bearer; không đọc database IAM trực tiếp. Kiểm tra này phản ánh thời điểm IAM trả lời, không phải transaction phân tán khóa tài khoản giữa hai DB.

### Thông báo và triển khai

Outbox ghi sự kiện `APPLICATION_ASSIGNED`, Version2, thêm `previousOfficerId`, `assignmentAction` CLAIM/RELEASE/ASSIGN/TRANSFER; không mang lý do hoặc PII lên RabbitMQ. Sự kiện này thay cho sự kiện STATUS của cùng thao tác, nên không có thông báo trùng. Người nhận theo từng hành động (người nhận phải còn active; cán bộ phải cùng phường; người nộp hồ sơ không nhận thông báo dành cho cán bộ):

| Hành động | Ai nhận | Tiêu đề / type |
|---|---|---|
| CLAIM (cán bộ tự nhận) | Công dân | "Giấy tờ đang được kiểm tra" / APPLICATION_STATUS_CHANGED |
| ASSIGN (Manager giao mới) | Công dân; cán bộ được giao | Công dân như CLAIM; cán bộ "Hồ sơ được giao cho bạn" / ASSIGNED_OFFICER |
| TRANSFER (Manager chuyển) | Cán bộ mới; cán bộ cũ | "Hồ sơ được giao cho bạn"; "Hồ sơ đã chuyển cho cán bộ khác" / ASSIGNED_OFFICER |
| RELEASE (cán bộ trả lại) | Các cán bộ cùng phường, trừ người vừa trả | "Có hồ sơ cần tiếp nhận" / APPLICATION_STATUS_CHANGED |

Công dân không nhận thông báo khi chuyển người hoặc trả lại, vì trạng thái công dân thấy không đổi theo nghĩa nghiệp vụ. Khi Email:Enabled=true, sự kiện phân công vẫn tạo delivery EMAIL cho cùng danh sách người nhận. Email dùng tiêu đề/nội dung thông báo và template dự phòng, không gọi endpoint email-context của sự kiện STATUS, không đưa lý do phân công nội bộ vào thư. SMS: cán bộ được giao dùng mẫu OFFICER_ASSIGNED, cán bộ cũ dùng OFFICER_UNASSIGNED, nhóm cán bộ khi trả lại dùng OFFICER_SUBMITTED, công dân dùng mẫu trạng thái UNDER_REVIEW (thiếu mẫu thì Blocked, không gửi mẫu khác thay thế).

Lịch sử phân công (`assignmentHistory`, gồm lý do) chỉ có trong response dành cho cán bộ/Manager. Các API của công dân (tạo, xem, cập nhật checklist, nộp, nộp lại, hủy hồ sơ) trả `assignmentHistory: null`; công dân vẫn thấy `assignedOfficerId`/`assignedOfficerName` như trước.

Gateway: route mới `/api/v1/manager/applications/{**catch-all}` → ApplicationWorkflow, khai báo riêng trước route `/api/v1/manager/{**catch-all}` của IAM (cả `appsettings.json` và `appsettings.Production.json`). Gateway phải deploy kèm Workflow, nếu không PUT assignee sẽ bị chuyển sang IAM và trả 404.

Deploy Analytics hỗ trợ Version2 **trước Workflow**, rồi IAM và Workflow cùng migration tương ứng, và Gateway có route mới. Consumer mới vẫn đọc Version1. Không để worker cũ nhận Version2 vì có thể đưa vào dead-letter queue. Chưa triển khai backfill suy đoán, analytics theo lĩnh vực, audit thay đổi assignedCategories hay kiểm tra category mồ côi.

### Đính chính bảo mật phân công và Email — 24/10/2026

Phản hồi dành cho công dân tiếp tục không trả assignmentHistory; history[].reason của hai chuyển trạng thái SUBMITTED → UNDER_REVIEW (nhận/giao) và UNDER_REVIEW → SUBMITTED (trả lại) được đặt NULL, kể cả dữ liệu cũ. Timeline vẫn giữ trạng thái, người thao tác và thời gian. Ghi chú yêu cầu bổ sung ở chuyển sang NEED_REVISION được giữ nguyên. FE không dựa vào reason của thao tác phân công để hiển thị cho công dân.

Các thao tác giao/trả mới chỉ lưu lý do trong application_assignment_history, không sao chép vào application_status_history. Cán bộ/Manager đọc lý do trong assignmentHistory của chi tiết hồ sơ thuộc phạm vi. Không thay route/body/status code/token, không cần migration hay sửa dữ liệu cũ.

Email phân công vẫn được gửi khi bật Email, theo người nhận CLAIM/ASSIGN/TRANSFER/RELEASE đã mô tả. NotificationEmailSource không lấy ngữ cảnh STATUS cho các event này; sender vẫn render template dự phòng và gửi SMTP. Thư chứa thông báo và liên kết hồ sơ, không chứa lý do nội bộ. Chưa gửi Email thật trong lượt sửa này.

### ResubmitCount trong danh sách hồ sơ — 24/10/2026

GET `/api/v1/officer/applications`, GET `/api/v1/officer/applications/pending` và GET `/api/v1/applications` trả `items[].resubmitCount` là số nguyên, mặc định0. Cả ba truy vấn lấy trực tiếp cột resubmit_count, không suy luận từ người phụ trách. Hồ sơ chưa nộp lại trả0, nộp lại thành công lần1 trả1, lần2 trả2. FE dùng số này để hiển thị nhãn gửi lại; không suy luận từ SUBMITTED + assignedOfficerId.

Nộp lại vẫn về SUBMITTED, giữ AssignedOfficerId. Cán bộ phụ trách gọi POST `/api/v1/officer/applications/{id}/assign` để bắt đầu kiểm tra phiên bản mới (UNDER_REVIEW). Không thêm trạng thái RESUBMITTED, không đổi request, phân quyền, HTTP status hay ProblemDetails. Không cần migration. FE đang nhận nullable vẫn tương thích với giá trị số nguyên này.

## Kiểm tra và đối soát lĩnh vực cán bộ — 24/10/2026

PUT `/api/v1/manager/officers/{userId}/categories` giữ body `{"categories":[3,7]}`, quyền/phạm vi hiện hữu và204 khi thành công. IAM kiểm tra các ID với danh sách đầy đủ GET `/api/v1/procedures/categories` của Catalog, không đọc DB chéo service. Vẫn tối đa100 ID dương không trùng; mảng rỗng hợp lệ (phụ trách mọi lĩnh vực).

- ID không tồn tại:400 `iam.invalid_categories`; tải lại danh mục và chọn lại. Catalog thiếu cấu hình, lỗi HTTP/JSON hoặc timeout:503 `iam.catalog_unavailable`; dữ liệu cũ giữ nguyên, thử lại khi Catalog phục hồi. Ngay cả gửi mảng rỗng cũng cần xác minh Catalog trong triển khai hiện tại.
- Thay đổi thật ghi rbac_audit_logs action `OFFICER_CATEGORIES_ASSIGNED`, actor/target/time và details.before/after là các ID đã sắp xếp. Gửi lại cùng tập ID trả204, không thêm audit. Audit dùng endpoint quản trị RBAC hiện có, không mở quyền xem audit cho Manager.
- Xóa lĩnh vực ở Catalog vẫn409 category.in_use nếu còn thủ tục liên kết. Sau khi xóa hợp lệ, IAM worker đối soát danh sách đầy đủ và loại ID không còn tồn tại khỏi assignedCategories. Mỗi lượt tối đa100 tài khoản, mặc định300giây; backlog cần nhiều lượt. Không cam kết dọn ngay tại thời điểm DELETE. Audit hệ thống `OFFICER_CATEGORIES_RECONCILED`, ActorUserId=Guid.Empty, details.source=CATALOG_RECONCILIATION và before/after. Không gửi thông báo hoặc sửa snapshot lĩnh vực của hồ sơ cũ.
- Khi mất hết lĩnh vực, mảng rỗng có nghĩa mọi lĩnh vực theo quy tắc ưu tiên mềm; đây không phải thu hồi quyền. Có thể có ID cũ giữa hai lượt hoặc khi IAM/Catalog tạm lỗi. Lỗi Catalog không được chuyển thành danh sách rỗng; worker giữ dữ liệu và thử sau. Danh sách rỗng200 hợp lệ từ endpoint đầy đủ là Catalog thực sự không còn lĩnh vực.

Cấu hình mới **trên IAM**: `ProcedureCatalog__BaseUrl` trỏ đúng Catalog của cùng môi trường (local thường http://localhost:5002/, Docker đã cấu hình http://procedure-catalog:8080/, Azure dùng URL HTTPS của Catalog). `CategoryReconciliation__Enabled=true`, `CategoryReconciliation__IntervalSeconds=300` (giới hạn30..86400). BaseUrl mẫu để trống để không âm thầm trỏ sang môi trường khác; thiếuURL sẽ503 khi gán và worker chỉ log lỗi chung. HTTP timeout10s, không redirect. Không cần migration; dùng users.assigned_categories và rbac_audit_logs hiện hữu. Deploy IAM kèm cấu hình mới trước khi FE dùng tính năng.

