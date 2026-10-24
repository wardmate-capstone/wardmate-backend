# WardMate — Sổ tay ràng buộc hệ thống

## Bổ sung TASK-31–33 — 21/10/2026 (Asia/Saigon)

Các luật sau đã triển khai trong source local. Chưa xác nhận Azure. Luật cũ vẫn giữ nguyên;
phần này chỉ áp dụng Analytics/Feedback và nguồn tổng hợp nội bộ Workflow.

| ID | Điều kiện/phạm vi đã kiểm tra | Lỗi, khắc phục và nguồn |
|---|---|---|
| ANA-01 | Bearer JWT hợp lệ; mỗi API dashboard/job/feedback đọc lại IAM access-context, so UserId với sub. Role MANAGER và ward không rỗng mới đọc dashboard; role REGISTERED_CITIZEN mới gửi/xem feedback; IT_ADMIN mới chạy/xem job toàn hệ thống. Không dùng role/ward cũ trong JWT làm quyết định cuối. | 401 tài khoản/token sai, 403 role/ward thiếu, 503 IAM lỗi; đăng nhập lại/cấu hình IAM, không fail-open. `AnalyticsSystem.API/AnalyticsAccess.cs`, `NotificationSecurity.cs`. |
| ANA-02 | Dashboard lọc ward từ IAM trước tổng hợp/phân trang. Không nhận ward scope từ client, nhóm thiếu ward không được Manager xem. Nhận xét không public, không trả CitizenId. | Người khác phường thấy dữ liệu phường của họ hoặc danh sách rỗng; không có chế độ xem toàn quốc ngầm. `AnalyticsService.cs`. |
| ANA-03 | From/To gồm hai đầu, ngày UTC+7, From >= 2000-01-01, không tương lai, tối đa 366 ngày. Page 1–100000, PageSize 1–100; ProcedureId không GUID rỗng. | 400 ValidationProblemDetails hoặc `validation.date_range`/`validation.failed`; sửa query. `AnalyticsContracts.cs`, `AnalyticsController.cs`. |
| ANA-04 | Đếm theo sự kiện lịch sử: nộp đầu DRAFT→SUBMITTED, nộp lại NEED_REVISION→SUBMITTED, xác nhận DOCUMENTS_READY. Approved/Rejected cũ tách riêng. Cán bộ là ChangedBy, không dùng assignedOfficer hiện tại. Thời gian từ lần nộp đầu đến xác nhận, gồm thời gian chờ/bổ sung. | Không dùng tổng các cột làm tổng hồ sơ duy nhất, không dùng elapsed làm giờ lao động. Nguồn thiếu lịch sử thì không có số liệu suy đoán. `WorkflowAnalyticsSource.cs`. |
| ANA-05 | Workflow chưa có deadline chuẩn hóa: tỷ lệ đúng/trễ hạn null, `UNAVAILABLE_NO_WORKFLOW_DEADLINE`. | Giới hạn còn lại của TASK-32: phải có hợp đồng deadline/lịch làm việc/tạm dừng xử lý từ Workflow trước khi tính SLA thực. Không suy từ chuỗi mô tả thủ tục. `AnalyticsService.cs`, `AnalyticsContracts.cs`. |
| ANA-06 | Nguồn nội bộ dùng key chuyên biệt >=32 byte, so hash constant-time, contract version=1; chỉ trả aggregate và ID cán bộ/thủ tục/ward. Tối đa 100000 history rows/ngày, transaction RepeatableRead. | 401 key sai/thiếu; 400 ngày sai; 503 quá giới hạn, cần chia nguồn. Không đọc chéo DB. `AnalyticsSourceController.cs`, `WorkflowAnalyticsSource.cs`, `AnalyticsWorkflowClient.cs`. |
| ANA-07 | Hai bảng metric + watermark ngày được thay thế atomically, advisory lock PostgreSQL theo ngày lấy trước đọc nguồn; snapshot sai/nguồn lỗi rollback, giữ số liệu cũ. Job retry 3 lần. | Có thể hoàn thành một số ngày trước khi ngày khác lỗi; xem coverage + job status, rebuild lại khoảng an toàn. `AnalyticsAggregationJob.cs`. |
| ANA-08 | JobsEnabled mặc định false. Khi bật phải có service key giống Workflow, LookbackDays 1–366, PostgreSQL storage schema analytics_jobs. Cron mặc định 01:15 UTC+7, nhìn lại 7 ngày; server phải luôn chạy. | 503 jobs_disabled; startup lỗi nếu cấu hình sai. Nhập/sửa lịch sử ngoài lookback cần IT_ADMIN rebuild. Không mở dashboard Hangfire. `AnalyticsRegistration.cs`, API/Program.cs, AnalyticsController.cs. |
| ANA-09 | MissingDates không đồng nghĩa không có hồ sơ; ngày có watermark nhưng nguồn cũ vẫn có thể stale. API trả timestamp nguồn/cập nhật và coverage, đọc metric + watermark trong cùng DB snapshot. | FE đánh dấu chưa đầy đủ/đã cũ thay vì công bố 0 hồ sơ; đợi/chạy lại job. `AnalyticsService.cs`. |
| FB-01 | CitizenId chỉ từ IAM/sub; POST kiểm tra Workflow bằng token của người gọi và so chủ hồ sơ, ID, procedure/ward. DOCUMENTS_READY hoặc OFFICIALLY_RECEIVED được đánh giá. Không coi APPROVED cũ là sẵn sàng tự động. | 404 không thuộc chủ/không có; 409 not_ready; 503 nguồn lỗi/thiếu thông tin. `AnalyticsService.cs`, `CitizenFeedbackController.cs`. |
| FB-02 | Rating nguyên 1–5, comment <=2000 ký tự, trim, trắng thành null, body <=16KiB; mỗi ApplicationId một đánh giá (unique DB), DB check rating. Không có sửa/xóa. | 400 sai nội dung, 413 body lớn, 409 already_exists kể cả request đồng thời. GET lại bản ghi sau duplicate. FE escape comment khi render. `AnalyticsContracts.cs`, `AnalyticsDbContext.cs`, controller/service. |
| FB-03 | GET chỉ đọc feedback có CitizenId hiện tại; không gọi Workflow lại. Manager xem theo ward snapshot tại lúc đánh giá. Điểm trung bình có trọng số; satisfaction = tỷ lệ sao 4–5; chưa có đánh giá trả average/rate null. | 404 feedback của người khác/chưa có. Luật hiện tại không di chuyển feedback khi hồ sơ chuyển ward sau này; nếu thêm chuyển ward cần hợp đồng đồng bộ. `AnalyticsService.cs`. |
| FB-04 | Kiểm tra trạng thái qua HTTP và ghi feedback ở DB riêng không phải distributed transaction. | Có cửa sổ giữa đọc Workflow và lưu feedback; vòng đời hiện tại DOCUMENTS_READY kết thúc, nếu bổ sung reopen phải thêm version/eligibility contract. Không tuyên bố bảo đảm trạng thái xuyên hai DB tại cùng một thời điểm. |

Nguồn Analytics viết tắt trong bảng nằm dưới
`src/Services/WardMate.Services.AnalyticsSystem`; nguồn Workflow dưới
`src/Services/WardMate.Services.ApplicationWorkflow`. Schema `analytics` gồm
`daily_performance_metrics`, `application_procedure_metrics`, `citizen_feedback`, `metric_refreshes`.
Khóa metric gồm date/ward/procedure và thêm officer cho hiệu suất. Không tạo FK sang database khác.
Migration history riêng `analytics.__AnalyticsMigrationsHistory`, connection AnalyticsDatabase hoặc
fallback NotificationsDatabase của cùng AnalyticsSystem. Giữ secrets trong env/secret store, không commit.

Nhật ký 21/10/2026: thêm ANA-01..09 và FB-01..04; TASK-31 worker/metrics, TASK-32 dashboard có giới hạn
SLA rõ ràng, TASK-33 feedback. Các rule không biến DOCUMENTS_READY thành phê duyệt hành chính.

Cập nhật: 15/10/2026, Asia/Saigon (UTC+07:00).

Tài liệu này tổng hợp các ràng buộc nghiệp vụ, dữ liệu, quyền truy cập và cấu hình
đã đối chiếu với mã nguồn hiện tại. Dùng để thiết kế FE, kiểm tra API và bảo trì BE.
Đây không phải xác nhận rằng bản đang chạy trên Azure đã chứa mọi thay đổi này.

Hướng dẫn gọi từng API nằm trong [api-guide.md](api-guide.md).
Các giới hạn chưa được bảo đảm được ghi riêng ở cuối tài liệu.

## Cách đọc và cập nhật

- Mỗi nhóm có mã ổn định, ví dụ `IAM-01`, `WF-04`. Khi sửa quy tắc, sửa ngay
  nhóm tương ứng; không tạo thêm tài liệu ràng buộc riêng cho mỗi task.
- “Bắt buộc” nghĩa là phải có giá trị hợp lệ, không chỉ có tên thuộc tính trong JSON.
- `null`, chuỗi rỗng và mảng rỗng không luôn tương đương. Gửi đúng kiểu DTO.
- Giới hạn ký tự khác giới hạn byte: mật khẩu và dữ liệu form có kiểm tra UTF-8.
- Ràng buộc ở một API không mặc nhiên áp dụng cho tất cả API khác.
- Khi thêm/sửa/xóa ràng buộc: cập nhật điều kiện, phạm vi, phản hồi lỗi, nguồn mã,
  ví dụ hoặc cách khắc phục và nhật ký cuối file trong cùng phiên làm việc.
- Không ghi mật khẩu, JWT, API key, connection string hoặc dữ liệu người thật vào đây.

## 1. Quy tắc chung và bảo mật trình duyệt

### SYS-01 — Danh tính, quyền và phạm vi dữ liệu

- API có bảo vệ yêu cầu Bearer access token hợp lệ. Có role không có nghĩa là được
  thao tác trên mọi bản ghi: còn kiểm tra permission, chủ sở hữu, phường và trạng thái.
- Một permission có thể dùng cho nhiều API. Không có quy tắc “mỗi API một permission”.
- ID do FE truyền không thay thế danh tính trong token. Người dùng không được tự
  khai `UserId`/`OfficerId` của người khác để vượt qua kiểm tra chủ sở hữu.
- Workflow lấy access context hiện tại từ IAM cho request đã xác thực, thay các
  role/permission/phường cũ trong JWT bằng dữ liệu hiện hành. IAM không truy cập
  được thì trả lỗi phụ thuộc, không tự cho phép thao tác.
- DocumentForm còn dùng các policy theo role và kiểm tra quyền truy cập hồ sơ qua
  Workflow. Không mô tả toàn hệ thống là đã chỉ dùng permission ở mọi endpoint.
- Một số truy vấn trả 404 cho dữ liệu ngoài phạm vi để không tiết lộ bản ghi tồn tại.

### SYS-02 — JWT và cookie

- JWT được kiểm tra chữ ký HS256, issuer, audience và thời hạn; các service phải
  có cấu hình tương thích. IAM dùng `ClockSkew = 0`.
- Giá trị mặc định trong `JwtOptions`: access token 15 phút, refresh token 7 ngày.
  Cấu hình môi trường có thể ghi đè; không coi đây là thời gian cố định trên Azure.
- Refresh token nằm trong cookie `refreshToken`, `HttpOnly`, path `/api/v1/auth`.
  Response đăng nhập cho trình duyệt không chứa bí mật refresh token.
- Cookie mặc định `Secure`; chỉ cho phép HTTP localhost khi môi trường Development
  và bật rõ `AllowInsecureLocalhost`. `SameSite` là cấu hình, mặc định trong lớp là Strict.
- FE cần gửi cookie khi refresh/logout. Không trộn `localhost` và `127.0.0.1`.
  Cookie bị trình duyệt chặn có thể khiến đăng nhập thành công nhưng refresh thất bại.

### SYS-03 — CSRF, CORS và reverse proxy

- Các action gắn `BrowserAuthProtection` yêu cầu `X-CSRF-Protection: 1`.
- Nếu có `Origin`, origin phải trùng scheme/host hiện tại hoặc nằm trong CORS được phép.
  Thiếu/sai header hoặc origin bị từ chối: 403 `iam.csrf_rejected`.
- Origin CORS phải chính xác, không wildcard, không path và không dấu `/` cuối URL.
  Ngoài Development, origin phải HTTPS; cấu hình sai có thể làm ứng dụng không khởi động.
- Cho phép credentials không có nghĩa là cho phép mọi origin. Các method cho phép:
  GET, POST, PUT, PATCH, DELETE, OPTIONS. Header cho phép gồm Content-Type,
  Authorization, X-CSRF-Protection và X-SignalR-User-Agent.
- IAM xử lý forwarded headers trước các middleware phụ thuộc scheme/host.
  Khi triển khai phải bảo đảm ingress chuyển header đúng; cấu hình tin proxy không
  phải lý do để công khai container cho mọi nguồn tự gửi header giả.

### SYS-04 — Giao dịch và lỗi

- Mỗi service quản lý database riêng. ID liên service là liên kết logic, không phải
  khóa ngoại PostgreSQL nối xuyên database.
- Transaction trong một database không tạo transaction nguyên tử giữa nhiều service.
- FE nên đọc `code`, `errors` và `traceId` khi có; không phân loại lỗi chỉ bằng câu `title`.
- 400: sửa dữ liệu; 401: kiểm tra phiên đăng nhập; 403: kiểm tra quyền/CSRF;
  404: kiểm tra ID và phạm vi; 409: tải lại trạng thái/version trước khi gửi tiếp;
  413: giảm kích thước file; 422: bổ sung yêu cầu nghiệp vụ còn thiếu;
  503: kiểm tra dịch vụ/cấu hình phụ thuộc. Mã cụ thể tùy API.
- 200/202 không đồng nghĩa AI đã bóc tách đầy đủ hoặc tác vụ nền đã hoàn thành.

Nguồn: `src/BuildingBlocks/WardMate.SharedKernel/Web/`, IAM API `Security/`,
IAM Infrastructure `Security/JwtOptions.cs`, Workflow API `WorkflowAccessMiddleware.cs`.

## 2. IAM — Tài khoản, hồ sơ cá nhân và RBAC

### IAM-01 — Đăng ký và đăng nhập

- Username bắt buộc, tối đa 100 ký tự, chỉ chữ không dấu, số, `_`, `.` và `-`.
- Email bắt buộc, đúng định dạng, tối đa 255 ký tự. Username/email phải không trùng.
- Mật khẩu đăng ký tối thiểu 8 ký tự, ít nhất một chữ hoa và một dấu câu/ký hiệu.
  Tối đa 72 byte UTF-8; 72 byte không đồng nghĩa 72 ký tự tiếng Việt.
- Validator hiện không bắt buộc thêm chữ thường hoặc chữ số. Không báo cho FE rằng
  hai điều kiện này đã được BE kiểm tra.
- Họ tên bắt buộc, tối đa 255 ký tự. Đăng ký tạo hồ sơ và gán REGISTERED_CITIZEN.
- Mật khẩu lưu bằng BCrypt với work factor 12, không lưu plaintext; việc xác minh dùng hash.
- Đăng nhập yêu cầu username/email không rỗng, tối đa 255; mật khẩu không rỗng,
  tối đa 72 byte. Tài khoản phải tồn tại, đang hoạt động và mật khẩu khớp.
- Tạo tài khoản Front Desk dùng lại các kiểm tra thông tin đăng ký này.

### IAM-02 — Refresh và logout

- Refresh token sinh ngẫu nhiên bảo mật; database lưu digest SHA-256 để đối chiếu.
- Token phải tồn tại, chưa hết hạn/chưa thu hồi, tài khoản còn hợp lệ.
  Rotation thu hồi token cũ trước khi dùng token mới.
- Không dùng cùng refresh token cho nhiều request refresh đồng thời; có thể chỉ một
  request thành công. FE nên dùng một luồng refresh dùng chung.
- Command refresh/revoke yêu cầu token không rỗng, tối đa 500 ký tự.
- Logout thu hồi refresh token phiên hiện tại và xóa cookie. Không mặc định là
  đăng xuất mọi thiết bị hoặc vô hiệu hóa tức thì mọi access token ở mọi service.

### IAM-03 — Hồ sơ cá nhân

- FullName bắt buộc, tối đa 255; IdentityNumber tối đa 20, không trùng nếu có giá trị.
- PhoneNumber tối đa 20; khi có giá trị chỉ gồm số, khoảng trắng, ngoặc, dấu gạch ngang
  và dấu `+` ở đầu. Đây không phải cùng validator điện thoại Việt Nam của E-Form.
- Gender tối đa 10 ký tự. Validator này không giới hạn thành danh sách Nam/Nữ cụ thể.
- DateOfBirth không được sau ngày hiện tại tính UTC+07:00.
- PermanentAddress và TemporaryAddress tối đa 4.000 ký tự mỗi trường.
- Mỗi user có tối đa một profile. Đăng ký đã tạo profile; POST lại có thể xung đột.
  PUT dùng để sửa; xóa profile không xóa account, role hoặc thông tin đăng nhập.
- Người dân quản lý profile của mình; API quản trị phải qua phạm vi quản lý tài khoản.
- Danh sách profile/user/account: page từ 1 đến 1.000.000, pageSize từ 1 đến 100
  tại các validator tương ứng.

### IAM-04 — Admin, Manager và phường

- IT_ADMIN đang hoạt động và có quyền quản trị IAM được quản lý toàn hệ thống.
- Manager phải có phường; chỉ quản lý người khác cùng phường có FRONT_DESK_OFFICER,
  không có role ngoài FRONT_DESK_OFFICER/REGISTERED_CITIZEN.
- Phường của người quản lý lấy từ database, không tin phường tự khai trong request/JWT.
- Manager được tạo Front Desk trong phường của mình, cấp/thu hồi role Front Desk
  trong phạm vi cho phép; không cấp IT_ADMIN/MANAGER hay tự đổi phường.
- Admin quản lý gán phường. Mã phường bắt buộc, tối đa 50, chỉ chữ không dấu/số/`_`/`-`,
  được chuẩn hóa và không trùng; tên phường bắt buộc, tối đa 255.
- Không khóa hoặc gỡ role của IT_ADMIN hoạt động cuối cùng. Các kiểm tra bảo vệ
  quản trị không được bỏ qua chỉ vì FE đã ẩn nút thao tác.
- Khóa tài khoản thu hồi refresh token. Việc kiểm tra lại access context giúp
  Workflow chặn quyền cũ; không suy rộng rằng mọi service đều kiểm tra live giống nhau.

### IAM-05 — Danh sách và lĩnh vực phụ trách

- Tìm kiếm user hỗ trợ các trường theo API directory; Search tối đa 255,
  Role/WardCode tối đa 50; AssignedCategory nếu có phải lớn hơn 0.
- Gán danh mục nhận mảng không null, tối đa 100 ID dương và không trùng.
- Bộ lọc phường không mở rộng quyền của Manager sang phường khác.
- AssignedCategories dùng cho thông tin/lọc cán bộ; không coi nó là hàng rào đã
  kiểm tra mọi thao tác hồ sơ theo lĩnh vực. Phạm vi phường/người thụ lý vẫn độc lập.

### IAM-06 — Quản lý role và permission

- RoleName bắt buộc, tối đa 50; bắt đầu bằng chữ không dấu, sau đó chữ/số/`_`.
  Khi lưu được trim và chuyển chữ hoa. Description tối đa 2.000 ký tự.
- RoleId/PermissionId phải dương; user/actor ID không được rỗng ở command tương ứng.
- Không đổi tên hoặc xóa năm role hệ thống. Role tùy chỉnh đã gán cho user không được xóa.
- Không thu hồi `iam.manage` khỏi IT_ADMIN. Không gỡ IT_ADMIN cuối cùng đang hoạt động.
- Không tạo liên kết role/permission trùng; cấp lại hoặc thu hồi liên kết đã ở đúng
  trạng thái được xử lý idempotent. Thay đổi RBAC có audit.
- Danh sách role/audit: page 1..1.000.000, pageSize 1..100.

Nguồn: IAM Application `Validation/AuthValidators.cs`, `Profiles/ProfileValidators.cs`,
`Accounts/`, `Rbac/`; Infrastructure `Persistence/ManagementScope.cs`,
`StaffAdministration.cs`, `AccountStore.cs`, `IdentityStore.cs`.

## 3. Procedure Catalog — Thủ tục và checklist mẫu

### PROC-01 — Thông tin thủ tục

- CategoryId phải dương và tồn tại. ProcedureCode bắt buộc, tối đa 50, không trùng.
- Title sau trim ít nhất 10 ký tự, tối đa 500. IssuingAuthority tối đa 255.
- LevelOfImplementation bắt buộc, tối đa 50.
- TargetAudience, FeeSummary, ProcessingTimeSummary bắt buộc, mỗi trường tối đa 255.
- OriginalPdfUrl có thể null; nếu có phải HTTPS tuyệt đối, không chứa user/password,
  tối đa 500 ký tự. PdfFileName tối đa 255.
- ContentPayload không null; DecisionNumber trong payload tối đa 100.
- Cases, SubmissionMethods, LegalReferences, Results không null; dùng `[]` khi không có.
- Mỗi case có mã, tên và danh sách bước; mỗi bước có thứ tự > 0 và tên không rỗng.
- CaseCode duy nhất, tối đa 100 ký tự, không có khoảng trắng đầu/cuối.
- Phương thức nộp phải có tên, phí và số ngày không âm.
  Căn cứ pháp lý không chứa phần tử null; kết quả không chứa chuỗi rỗng.
- Việc qua validator không chứng minh nội dung đúng quy định pháp luật.

### PROC-02 — Checklist và biểu mẫu trong thủ tục

- Mỗi checklist có ChecklistId, ItemName không rỗng, tối đa 255 ký tự.
  ChecklistId duy nhất trong thủ tục, không dư khoảng trắng đầu/cuối; tối đa 500 mục.
- SubmissionType: `NOP` hoặc `XUAT_TRINH`.
- DocumentCopyType: `ORIGINAL`, `CERTIFIED_COPY` hoặc `REGULAR_COPY`.
- Quantity > 0. IsMandatory quy định mục bắt buộc; CaseCode chỉ phạm vi áp dụng.
- FormDefinition yêu cầu FormCode/FormName; FormType là `ONLINE_INTERACTIVE` hoặc
  `DOCX_TEMPLATE`; Quantity > 0. FormTemplateId là ID liên service DocumentForm.
- FormCode/FormName tối đa 255; FormCode không dư khoảng trắng đầu/cuối.
  FormTemplateId có thể null nhưng không được Guid.Empty.
- CaseCode của checklist/form nếu có phải thuộc Cases. Mã form không được trùng trong
  cùng case hoặc trùng giữa form dùng chung và form theo case. Cùng mã form ở hai case
  riêng biệt vẫn hợp lệ vì một hồ sơ chỉ chọn một case.
- API PUT checklist chuyên biệt nhận danh sách đầy đủ: mảng không null, tối đa 500;
  `[]` là xóa hết checklist. Thứ tự mảng là thứ tự hiển thị.
- API này yêu cầu ExpectedUpdatedAt, DecisionNumber không rỗng/tối đa 100 và EffectiveDate.
- ChecklistId tại API này tối đa 255, không dư khoảng trắng đầu/cuối, không trùng;
  CaseCode nếu có phải thuộc Cases của thủ tục.
- Nếu ExpectedUpdatedAt cũ: 409 `procedure.concurrent_update`, tải lại trước khi sửa.
- Sửa checklist lưu snapshot bản cũ và tạo version tiếp theo; không sửa checklist
  đã sao chép vào hồ sơ công dân.
- Kiểm tra ID checklist trùng/độ dài/số lượng/case được dùng chung cho create,
  update, publish và API checklist chuyên biệt. ExpectedUpdatedAt vẫn là yêu cầu riêng
  của API checklist; không suy rộng rằng mọi đường cập nhật đều dùng concurrency token đó.

### PROC-03 — Version, xuất bản, trạng thái và khôi phục

- Tạo thủ tục sinh version 1 và IsActive = true.
- Cập nhật chụp trạng thái trước thay đổi vào version tiếp theo, rồi cập nhật bản hiện tại.
  Cặp ProcedureId/VersionNumber phải duy nhất.
- Publish bản đã đối soát upsert theo ProcedureCode trong transaction: chưa có thì tạo,
  đã có thì lưu snapshot cũ và cập nhật. Không được hiểu mọi snapshot đều là “bản mới sau sửa”.
- Với bản đã tồn tại, không mặc định publish sẽ bật lại thủ tục đã tắt; quản lý trạng thái riêng.
- Rollback yêu cầu version > 0, Reason bắt buộc/tối đa 4.000, DecisionNumber bắt buộc/
  tối đa 100, EffectiveDate. Khôi phục giữ lịch sử, không xóa các version đã có.
- Tắt thủ tục ngăn tiếp nhận hồ sơ mới; không tự hủy hồ sơ đã tạo.
- Danh mục: tên bắt buộc/tối đa 255, mô tả tối đa 4.000; không xóa danh mục còn thủ tục sử dụng.
  Tên sau trim không được trùng không phân biệt hoa/thường qua luồng quản trị.
  Rollback bị chặn nếu snapshot sai, danh mục cũ không còn hoặc mã bị thủ tục khác dùng.

### PROC-04 — Tra cứu

- Public chỉ trả thủ tục đang hoạt động. Không cho client dùng IsActive=false để vượt lọc.
- Manager có thể lấy cả hai trạng thái hoặc lọc một trạng thái theo quyền.
- PageNumber > 0; PageSize public 1..50, manager 1..100; offset không vượt int.MaxValue.
- Keyword tối đa 255; LevelOfImplementation tối đa 50; CategoryId nếu có phải dương.
- SortBy chỉ Title, ProcedureCode, UpdatedAt, CreatedAt, LevelOfImplementation
  (không phân biệt hoa/thường); không nhận tùy ý biểu thức SQL.
- Danh sách thu gọn không thay thế API chi tiết chứa nội dung JSONB/checklist/forms.
- Tra danh sách form qua Procedure: page 1..1.000.000, pageSize 1..100, SearchCode tối đa 100.

Nguồn: ProcedureCatalog Application `Management/ProcedureInputValidator.cs`,
`ProcedureChecklist.cs`, `AdministrationRequests.cs`, `Queries/` và Infrastructure `Persistence/`.

## 4. Bản nháp PDF và AI/OCR

### DRAFT-01 — Upload, preview và lưu bản nháp

- Preview đọc thử không thay thế thao tác lưu draft. Upload draft cần Blob được cấu hình;
  thiếu lưu trữ trả 503 `draft.storage_not_configured`.
- Upload nhận PDF tên tối đa 255, đuôi `.pdf`, chữ ký `%PDF-`, dung lượng tối đa 20 MiB.
  Tên được bỏ phần đường dẫn trước khi lưu; không dùng đường dẫn client làm vị trí storage.
- Bật extraction: trả draft `Queued`; đây là chấp nhận vào hàng đợi, chưa có kết quả cuối.
  Tắt extraction: `NeedsReview` để nhập thủ công.
- Lấy lại draft bằng ID để biết trạng thái/kết quả; không dùng payload rỗng lúc Queued để publish.
- Save nhận JSON object tối đa 500.000 ký tự; chỉ trạng thái NeedsReview/Failed được sửa.
- Save/retry/publish yêu cầu revision hiện tại. Revision cũ hoặc trạng thái không phù hợp:
  409 `draft.conflict`; tải lại dữ liệu, tránh ghi đè thay đổi của người khác.
- Retry chỉ từ Failed và extraction đang bật. Save bản Failed chuyển về NeedsReview.
- Publish yêu cầu Confirmed=true, NeedsReview, đúng revision, đủ dữ liệu và qua validator
  thủ tục; URL/tên PDF lấy từ draft trên server. Thành công chuyển Published, gắn ProcedureId.
- PDF nguồn private được lấy qua API cấp URL có hạn; không lưu SAS như URL vĩnh viễn.
  Không có nguồn đã upload tương ứng có thể trả 404 dù thủ tục vẫn tồn tại.
- Xóa draft chỉ khi NeedsReview, Failed, Queued hoặc đang Deleting; chặn draft đang
  xử lý/đã publish hoặc PDF còn được procedure/version sử dụng. Ghi trạng thái Deleting
  trước khi xóa Blob; lỗi storage không cho tái sử dụng bản nháp như bình thường.

### AI-01 — Đọc tài liệu và giới hạn

- AIOCR nội bộ yêu cầu service key đã cấu hình, tối thiểu 32 ký tự; không đưa key cho FE.
- PDF tối đa 20 MiB, từ 1 đến 100 trang, văn bản đọc được tối đa 100.000 ký tự.
  Vượt giới hạn yêu cầu tách tài liệu, không âm thầm bỏ phần cuối.
- Ưu tiên đọc lớp chữ PDF. Trang ít chữ, có ký tự hỏng hoặc ảnh được cảnh báo;
  đọc lớp chữ không chứng minh đã đọc mọi chữ trong ảnh/bảng.
- OCR fallback chỉ chạy nếu bật và cấu hình nhà cung cấp đầy đủ.
- UseAI=false vẫn có thể đọc extractedText nhưng payload cần nhập thủ công.
- AI hỗ trợ cấu hình Gemini/AzureOpenAI; key, model/deployment phải đúng môi trường.
  Không tự thay billing/model để xử lý hết hạn mức.

### AI-02 — Hợp đồng đầu ra và lỗi nhà cung cấp

- Gửi JSON Schema cho AI; kiểm tra/chuẩn hóa payload và warnings sau khi nhận.
- Chỉ nhận kết quả hoàn tất (`finish_reason=stop`), payload JSON object;
  nội dung JSON AI tối đa 500.000 ký tự. Không nối đoán phần JSON bị cắt.
- HTTP 502/503/504 thử lại có giới hạn, tối đa ba lượt trong vòng gọi hiện tại;
  lỗi cấu trúc được thử lại khi còn lượt theo điều kiện trong code.
- Khi map AI thất bại trong luồng đọc chữ, giữ extractedText và cảnh báo để đối soát
  thủ công. Lỗi file, giới hạn hoặc hủy request không được cam kết luôn trả payload thành công.
- Thiếu trường không được xem là “không cần giấy tờ”. Cán bộ kiểm tra warnings,
  cases, checklist, form và chọn CategoryId thật trước publish.
- AI không có quyền tự quyết định xuất bản hay tự xác nhận tính pháp lý của dữ liệu.

Nguồn: ProcedureCatalog Application `Drafts/ProcedureDraftService.cs`, `DraftPayloadRequirements.cs`;
AIOCR API `Controllers/ProcedureExtractionsController.cs`, Infrastructure `Extraction/`.

## 5. Application Workflow — Hồ sơ công dân

### WF-01 — Tạo hồ sơ và chụp checklist

- Lấy người nộp từ danh tính đã xác thực; ProcedureId phải hợp lệ và thủ tục đang hoạt động.
- WardCode bắt buộc, tối đa 50, phải có trong IAM; không tự nhận phường bất kỳ.
- CaseCode tối đa 100. Nếu nhiều case phải chọn case hợp lệ; một case có thể tự chọn.
- FormData phải JSON object, tối đa 65.536 byte UTF-8.
- Chọn checklist dùng chung cộng checklist đúng case; sao chép metadata vào hồ sơ:
  mã, tên, thứ tự, bắt buộc, case, số lượng, nộp/xuất trình, loại bản sao, điều kiện, template.
- Dữ liệu checklist/form nguồn thiếu hoặc không hợp lệ bị từ chối; không tạo hồ sơ
  với các yêu cầu giấy tờ trùng mã/không thể xác định.
- Mã hồ sơ duy nhất; trạng thái đầu DRAFT. Thủ tục đổi về sau không tự sửa yêu cầu hồ sơ cũ.

### WF-02 — Cập nhật và kiểm tra giấy tờ

- API sửa checklist trực tiếp áp dụng hồ sơ DRAFT của chính người dùng, đúng checklist ID.
- Trạng thái checklist cho phép PENDING, COMPLETED, REJECTED ở validator cập nhật.
- FileUrl tối đa 500, nếu có phải HTTPS tuyệt đối, không chứa thông tin đăng nhập;
  Note tối đa 4.000. FileUrl chỉ metadata, không chứng minh file tồn tại.
- Khi đánh dấu hoàn tất/nộp/nộp lại, API kiểm tra nội dung từ DocumentForm.
- Item gắn FormTemplateId cần form đúng hồ sơ/checklist/template, ở trạng thái
  Submitted/ReadyForDownload và file thực sự tồn tại. Một form điện tử có thể in nhiều bản;
  không yêu cầu tạo nhiều submission chỉ để đủ số bản giấy.
- Item giấy tờ cần supporting documents đúng checklist, file tồn tại, số lượng dương,
  đúng loại bản sao/hình thức nếu checklist quy định; cộng số lượng phải đủ yêu cầu.
- Không dùng form khác template hoặc chỉ client đánh dấu COMPLETED để thay tài liệu.
- Thiếu checklist bắt buộc: 422 `application.checklist_incomplete`.
  Thiếu bằng chứng file: 422 `application.documents_incomplete`.
  DocumentForm không xác minh được: lỗi phụ thuộc, không cho qua mặc định.

### WF-03 — Vòng đời

| Chuyển trạng thái | Điều kiện chính |
| --- | --- |
| DRAFT → SUBMITTED | Chính chủ; đủ checklist và tài liệu; tạo snapshot nộp |
| SUBMITTED → UNDER_REVIEW | Front Desk đúng phường, không phải chủ hồ sơ; chưa bị cán bộ khác nhận |
| UNDER_REVIEW → NEED_REVISION | Đúng cán bộ thụ lý; có nhận xét OPEN ở version hiện tại |
| NEED_REVISION → SUBMITTED | Chính chủ nộp lại; đúng expected version; đủ dữ liệu/tài liệu |
| UNDER_REVIEW → DOCUMENTS_READY | Đúng cán bộ/phường; có snapshot; không còn OPEN; checklist bắt buộc COMPLETED; kiểm tra tài liệu qua DocumentForm |
| APPROVED/REJECTED cũ | Chỉ giữ dữ liệu lịch sử; không có endpoint chuyển mới, không tự suy ra giấy tờ sẵn sàng |

- DOCUMENTS_READY ghi DocumentsReadyAt UTC, lịch sử và Outbox cùng transaction; không cho sửa/bắt lỗi tiếp. Endpoint confirm-readiness không có body, chỉ FD được phân công đúng phường, không tự xác nhận hồ sơ. API xác nhận lần hai trả 409.
- Reason từ chối/yêu cầu sửa chịu validator tương ứng, tối đa 4.000 ký tự.
- DRAFT/NEED_REVISION → CANCELLED: chủ hồ sơ gọi POST /api/v1/applications/{id}/cancel với workflow.cancel; các trạng thái khác trả409. Hủy không xóa hồ sơ.
- Không gửi trực tiếp chuỗi Status để tùy ý nhảy qua vòng đời.

### WF-04 — Phường, người thụ lý, comments và version

- FD hoặc Manager có workflow.read được xem chi tiết cùng phường, trừ hồ sơ tự nộp; không yêu cầu AssignedOfficerId cho quyền đọc. Thiếu phường bị chặn. Quyền đọc không cấp quyền thao tác.
- Manager chỉ đọc (kể cả tài khoản có đồng thời role FD); assign/comment/request-revision/confirm-readiness yêu cầu FD không có role Manager và permission tương ứng. Sau assign, thao tác còn kiểm tra đúng người thụ lý. Không tự thẩm định hồ sơ mình nộp.
- Danh sách tổng hợp loại hồ sơ tự nộp, cùng phường; pending Manager thấy tất cả SUBMITTED/UNDER_REVIEW trong phường, FD giữ bộ lọc chưa phân công/được giao cho mình. Cả hai trả assignedOfficerId nullable.
- Role Admin/Manager không tự động bỏ qua điều kiện cán bộ thụ lý.
- Comment chỉ khi UNDER_REVIEW, đúng version hiện tại; target FORM_FIELD hoặc CHECKLIST_ITEM
  phải thực sự có trong snapshot. Version cũ trả 409.
- Comment target ID tối đa 100, label 255, nội dung 4.000; version > 0.
- Nộp lại phải gửi đủ chính xác bộ checklist ID của hồ sơ, không tự thêm/bỏ item;
  trạng thái trong payload nộp lại là PENDING/COMPLETED, ID không trùng.
- Payload nộp lại được validate trước khi duyệt mảng hoặc gọi DocumentForm:
  phần tử null/ID trùng/kiểu dữ liệu không hợp lệ trả lỗi validation thay vì lỗi 500.
- Nộp lại tạo version mới và xử lý nhận xét cũ; diff đọc các version của đúng hồ sơ,
  không trộn version của hồ sơ khác.
- Snapshot dữ liệu JSON không đồng nghĩa đã đóng băng tất cả blob/file nhị phân lịch sử.
- Danh sách hồ sơ: page 1..1.000.000, pageSize 1..100; bộ lọc queue kiểm tra trạng thái/ngày.

Nguồn: Workflow Application `WorkflowValidation.cs`, `WorkflowRequests.cs`,
`ReviewValidation.cs`, `ReviewHandlers.cs`, `DocumentReadinessRequests.cs`;
API `DocumentContentsClient.cs`; Infrastructure `ApplicationReviewStore.cs`.

## 6. DocumentForm — Mẫu, dữ liệu đã điền, tài liệu và PDF

### DOC-01 — DOCX upload

- Chỉ `.docx`, không rỗng, tối đa 20 MiB; kiểm tra cả độ dài thực đọc.
- Gói DOCX tối đa 5.000 entry, tổng dung lượng giải nén tối đa 100 MiB.
- Phải mở được bằng OpenXML, đúng loại Document, có document body, không VBA project.
  Đổi đuôi file khác thành `.docx` không đủ để được chấp nhận.
- File mẫu và file người dân đã điền là hai loại dữ liệu riêng; sửa file người dân
  không được ghi đè mẫu dùng chung.

### DOC-02 — Schema E-Form

- Schema phải JSON hợp lệ, có title, ít nhất một section và ít nhất một field.
- Section có ID/title, ID không trùng; Field có ID/label, ID chỉ chữ/số/`_`,
  không trùng toàn form (kiểm tra trùng không phân biệt hoa/thường).
- Field type phải được hỗ trợ; options không null/không chứa phần tử null.
- Select/Radio có ít nhất một option; label/value không rỗng, value không trùng.
- Min/max độ dài không âm và đúng thứ tự; min/max giá trị đúng thứ tự;
  RegexPattern phải biên dịch được. Kiểm tra regex dữ liệu có timeout 100 ms.
- Dữ liệu điền phải JSON object, không có field lạ/trùng; field value là scalar,
  không object/array. Tên field dữ liệu phải khớp schema.
- Một giá trị tối đa 10.000 ký tự; không ký tự điều khiển ngoài xuống dòng CR/LF.
- Checkbox dùng JSON boolean. Date dùng `yyyy-MM-dd`; DateTime phải parse được;
  Number/Currency phải là số hợp lệ và thỏa min/max nếu cấu hình.
- Email theo định dạng email; PhoneNumber theo mẫu Việt Nam 0/+84 và chín số tiếp theo;
  NationalId chấp nhận 9 hoặc 12 chữ số. Đây là kiểm tra định dạng, không xác minh danh tính.
- Select/Radio phải nằm trong options; required/min/max/regex theo schema.
  Lưu nháp có thể dùng kiểm tra chưa yêu cầu đủ field, submit phải kiểm tra đầy đủ.

### DOC-03 — Liên kết và quyền sửa

- Submission/supporting document phải gắn đúng ApplicationId và ChecklistId.
  Không liên kết checklist của hồ sơ khác; template phải khớp item nếu item yêu cầu template.
- Citizen chỉ quản lý tài liệu của mình, sửa khi Workflow DRAFT hoặc NEED_REVISION.
- API đọc giấy tờ/PDF dùng chung kiểm tra chính chủ trước, sau đó mới kiểm tra cán bộ
  được phân công nếu không tìm thấy hồ sơ chính chủ. Tài khoản đồng thời là công dân
  và Front Desk vẫn xem được tài liệu của mình. API thẩm định vẫn cấm tự thẩm định.
- Officer chỉ truy cập hồ sơ được giao, đúng phường qua Workflow; sửa/annotate chỉ UNDER_REVIEW.
- Hồ sơ DOCUMENTS_READY và hồ sơ APPROVED/REJECTED lịch sử không tiếp tục sửa qua các API bảo vệ này.
- Submission cũ thiếu liên kết hồ sơ/checklist không mặc nhiên được cán bộ duyệt;
  phải xử lý liên kết đúng, không dùng đường cũ để bỏ kiểm tra phạm vi.
- Supporting upload/delete dành cho citizen; cán bộ được đọc khi đúng phạm vi.
- Một cặp application/checklist chỉ có một user submission hoạt động theo unique index lọc.
- Cập nhật submission có sẵn không được đổi liên kết sang application/checklist khác.
  Đã có đơn cho checklist thì sửa đơn đó, không POST tạo đơn trùng.
- Lưu nháp cần template tồn tại, đang hoạt động và đã có DOCX mẫu;
  template bị tắt không được dùng để lưu nháp theo luồng này.
- Blob mất hoặc không đọc được phải báo lỗi; bản ghi database có đường dẫn không đủ
  để chứng minh tài liệu đáp ứng yêu cầu hồ sơ.
- Phản hồi DocumentForm có phần tử null, ID rỗng hoặc ID lặp trong cùng danh sách
  không được dùng cộng số lượng: Workflow trả 503 `application.documents_unavailable`.

### DOC-04 — Giấy tờ minh chứng

- File không rỗng, tối đa 20 MiB; loại cho phép theo extension gồm PDF, JPG, JPEG,
  PNG, DOC, DOCX. Không coi extension là chứng nhận nội dung đã an toàn/xác thực.
- Quantity > 0. SubmissionType nếu có là NOP/XUAT_TRINH;
  DocumentCopyType nếu có là ORIGINAL/CERTIFIED_COPY/REGULAR_COPY.
- Khai “bản sao chứng thực” chỉ là metadata; hệ thống không tự chứng thực pháp lý file.

### DOC-05 — Xác nhận giấy tờ sẵn sàng và kết xuất PDF

- Workflow sở hữu trạng thái. DocumentForm confirm-readiness/request-revision chuyển yêu cầu sang Workflow, không xác nhận form độc lập để vượt điều kiện của Application.
- Workflow DOCUMENTS_READY hoặc OFFICIALLY_RECEIVED cho phép xuất/list/download PDF và đọc QR. Thiếu điều kiện trả 409 document.application_not_ready; không nới quyền cho cán bộ.
- DocumentForm đồng bộ submission Submitted sang ReadyForDownload khi xác minh truy cập. Đây là đồng bộ khi đọc, có thể thử lại, không phải transaction xuyên service. Form Draft/RevisionRequested không tự được xác nhận.
- PDF dùng submission đã lưu, đúng application, chưa xóa, ReadyForDownload; không nhận file thay thế. Form chưa sẵn sàng trả document.submission_not_ready.
- QR chỉ là tham chiếu giấy tờ chuẩn bị. Giữ tên bảng document.approved_qrs và routes approved-qr để tương thích. QR được tạo khi kết xuất PDF nếu chưa có; unique application_id giữ nguyên. Không phải ký số/con dấu/quyết định hành chính.
- Gotenberg hoạt động và kết quả phải có header %PDF-. Chỉ bỏ comment trên bản sao để kết xuất, không sửa DOCX gốc. Mỗi lần xuất tạo file/bản ghi mới.
- Nhận xét trong Word không tự sinh comment OPEN Workflow. Yêu cầu bổ sung phải có nhận xét hợp lệ trong Workflow.
- Kiểm tra file tồn tại/metadata qua HTTP trước xác nhận; trạng thái, snapshot, comment, checklist được kiểm tra lại dưới khóa hàng khi chuyển trạng thái. Chưa có khóa giao dịch phân tán chống request sửa file đang chạy hoặc xóa Blob ngoài hệ thống đồng thời.

### DOC-06 — Mẫu dùng chung và giới hạn lưu trữ

- Template code/title không rỗng; code trim và chuyển chữ hoa, không trùng.
  Database giới hạn code 50, title 255 ký tự.
- Các version mẫu có cặp TemplateId/VersionNumber duy nhất; submission tham chiếu
  version mẫu có khóa ngoại hạn chế xóa, tránh xóa mất mẫu đang được tham chiếu.
- UserSubmission dùng UpdatedAtUtc làm concurrency token: không xem cập nhật đồng thời
  là được phép ghi đè bản mới của người khác.
- Database UserSubmission: BlobUrl tối đa 2.048, FileName 512, Status 50,
  OfficerComment 2.000 ký tự. Không nhầm giới hạn comment này với comment Workflow 4.000.
- SupportingDocument: FileName tối đa 255, ContentType 100;
  SubmissionType/DocumentCopyType tối đa 30 ký tự ở database.
- Các giới hạn database không tự bảo đảm mọi vi phạm đều đã được controller chuyển
  thành lỗi 400 thân thiện; FE vẫn cần tuân theo dữ liệu API/schema hợp lệ.

Nguồn: DocumentForm Application `Services/`, `Commands/`, Domain `Entities/`;
API `Security/ApplicationAccess.cs`, `Security/DocumentAuthentication.cs`, `Controllers/`;
Infrastructure `Pdf/GotenbergPdfRenderEngine.cs`.

## 7. Notifications — Thông báo và SignalR

### NOTI-01 — Dữ liệu và người nhận

- RecipientUserId không rỗng; ApplicationId có thể null nhưng không Guid.Empty.
- Title không rỗng, tối đa 255; Content không rỗng.
- Type: APPLICATION_STATUS_CHANGED, INLINE_COMMENT_ADDED, ASSIGNED_OFFICER, SYSTEM_ALERT.
- Channel database: IN_APP, SMS, EMAIL. Có enum/channel không chứng minh SMS/Email thật đã hoạt động.
- ActionUrl nếu có tối đa 500, bắt đầu `/`, không `//`, dấu `\` hoặc ký tự điều khiển;
  không dùng thông báo để chuyển hướng tới website tùy ý.
- Chưa đọc: IsRead=false và ReadAt=null; đã đọc phải có ReadAt.
- Chỉ lấy/đánh dấu thông báo của người nhận hiện tại, kèm phạm vi phường nếu thông báo gắn phường.
- Danh sách page 1..1.000.000, pageSize 1..100. Đánh dấu đã đọc lại không tạo lần đọc mới.

### NOTI-02 — Kết nối và sự kiện

- Hub yêu cầu JWT; user group do server xác định, không cho client tự chọn user khác.
  Nhóm phường theo access context cán bộ, không theo ward tùy ý do FE truyền.
- Có kiểm tra lại quyền/phiên và đóng kết nối hết hạn; thay đổi quyền không được hiểu
  là đã đồng bộ tức thì trên mọi connection ở mọi thời điểm.
- Chu kỳ revalidate mặc định 15 giây, cấu hình được clamp trong 5..300 giây.
  Thay đổi fingerprint user/phường/role/permission hoặc không xác minh được IAM
  khiến connection bị ngắt. Nhóm phường yêu cầu role và permission tương ứng.
- Lưu notification và delivery trước khi worker gửi. Workflow dùng outbox;
  consumer dùng inbox/chống trùng để xử lý sự kiện được giao lại.
- Inbox/delivery retry khi lỗi; đủ 10 lần lỗi chuyển Dead. Kênh chưa cấu hình có thể
  chuyển Blocked với `notification.channel_not_configured`, không được coi là đã gửi.
  Sự kiện sai hợp đồng được chuyển dead-letter; không retry vô hạn như dữ liệu hợp lệ.
- Unique event/người nhận/channel và notification/channel hạn chế tạo trùng dữ liệu.
  Không cam kết trình duyệt chỉ nhận đúng một lần; FE nên deduplicate theo notification ID.
- Mất realtime không làm mất dữ liệu đã lưu: FE tải lại danh sách/số chưa đọc sau reconnect.
- Lỗi cập nhật bộ đếm realtime không hoàn tác trạng thái đã đọc đã lưu trong database.
- Cấu hình hỗ trợ realtime hiện tại: AnalyticsSystem một replica, một revision nhận traffic. Chưa có backplane SignalR; không chạy nhiều replica/revision cùng xử lý delivery nếu cần đảm bảo push tới connection đang mở. Notification đã lưu vẫn đọc lại được sau reconnect.
- Trước khi phát notification/bộ đếm, kiểm tra lại từng connection với IAM và chỉ gửi tới connection hợp lệ trên replica hiện tại; notification gắn phường còn đối chiếu quyền queue/phường. Không cam kết quyền bất biến trong khoảng giữa lần xác minh IAM và gửi.
- Inbox dùng savepoint: lỗi xử lý/lưu notification rollback toàn bộ notification/delivery của lần đó trước khi ghi retry. Đủ 10 lần lỗi chuyển Dead. Nếu chính database không thể ghi retry, transaction rollback và dữ liệu Pending vẫn còn; cần phục hồi DB.
- Delivery Sent chỉ nghĩa worker hoàn tất lệnh gửi SignalR, không chứng minh người dùng online hoặc đã nhận/đọc. FE luôn đồng bộ danh sách và bộ đếm sau reconnect.
- Email/SMS chưa có cấu hình nhà cung cấp được người dùng xác nhận: không coi gửi thật
  đã hoàn tất hoặc tự bật dịch vụ trả phí.

Nguồn: AnalyticsSystem Application `Notifications.cs`, Infrastructure
`NotificationService.cs`, `NotificationHub.cs`, `NotificationConnectionGuard.cs`,
`NotificationDbContext.cs`; EventBus và các worker outbox/inbox/delivery.

## 8. Ràng buộc database không được bỏ qua

### DB-01 — Duy nhất và khóa liên kết

- IAM: Username, Email, IdentityNumber khi có, RoleName, PermissionCode, Token digest,
  Ward.Code là các trường unique. UserRole và RolePermission dùng khóa ghép.
  Profile quan hệ 1–1 bằng UserId.
- Procedure: ProcedureCode unique; ProcedureId/VersionNumber unique; CategoryId phải
  liên kết category. ContentPayload/SnapshotData lưu JSONB; mảng checklist/form cũng JSONB.
- Workflow: ApplicationCode unique, ApplicationId/VersionNumber unique,
  ApplicationId/Checklist.Code unique. VersionNumber > 0.
- Workflow status gồm DRAFT, SUBMITTED, UNDER_REVIEW, NEED_REVISION, DOCUMENTS_READY; DB còn giữ APPROVED, CANCELLED,
  REJECTED. Checklist status chỉ PENDING, COMPLETED, REJECTED.
- Comment status OPEN/RESOLVED; target type FORM_FIELD/CHECKLIST_ITEM; liên kết version
  phải đúng hồ sơ theo cấu hình khóa của database.
- DocumentForm: template code unique; template/version unique; application/checklist
  submission unique theo điều kiện index (không áp dụng như unique vô điều kiện cho mọi dòng đã xóa).
- Notifications: các unique/check đã nêu ở NOTI; index người nhận/trạng thái phục vụ truy vấn.
- JSONB hợp lệ cú pháp không đồng nghĩa hợp lệ nghiệp vụ: các điều kiện case, số lượng,
  template và quyền vẫn phải qua Application/API.
- Kiểm tra “chưa tồn tại” ở Application không thay thế unique index khi hai request chạy đồng thời.

Nguồn: các `Infrastructure/Persistence/Configurations/`, `WorkflowDbContext.cs`,
`NotificationDbContext.cs` và migrations tương ứng. Giới hạn chuỗi API nêu ở từng nhóm
có thể hẹp hơn kiểu `text` của database; không bỏ validator vì cột vẫn lưu được.

## 9. Các giới hạn phải hiểu đúng

- Tài liệu mô tả quy tắc đã thấy trong source, không xác nhận migration/config/image
  của Azure đã đồng bộ. Local chạy được không chứng minh deploy có cùng cấu hình.
- Không bảo đảm AI luôn trả đủ trường hoặc luôn đúng; có luồng đối soát thủ công.
  Chưa coi cơ chế tách tài liệu theo nghiệp vụ/ghép liên kết tự động là đã hoàn thiện.
- Không có transaction phân tán khóa toàn bộ Workflow + DocumentForm + Blob.
  Kiểm tra trạng thái trước ghi vẫn có khả năng race với request đang chạy ở service khác.
- Kiểm tra định dạng CCCD/điện thoại hoặc metadata bản sao không xác minh dữ liệu với cơ quan nhà nước.
- JSON Schema của AI không thay thế validator xuất bản; JSONB của DB không thay thế cả hai.
- Một số thông báo lỗi DocumentForm vẫn bằng tiếng Anh. Không khẳng định toàn bộ lỗi
  trong source đã Việt hóa chỉ vì tài liệu này viết tiếng Việt.
- Chủ dự án đã bỏ test projects. Build sạch chỉ chứng minh biên dịch; không phải bằng
  chứng mọi ràng buộc đã có unit/integration test hay đã qua kiểm thử deploy.
- Khi thêm tính năng mới, phải đối chiếu lại nguồn bên dưới từng nhóm và cập nhật chính file này.

## 10. Danh mục permission nghiệp vụ dùng chung

Các mã dưới đây lấy từ `WardMate.SharedKernel/Web/FeaturePermissions.cs`.
Chúng không thay thế kiểm tra phạm vi bản ghi và không phải danh sách mọi policy
riêng của từng service. Quyền thực tế còn phụ thuộc dữ liệu gán role trong IAM.

**Procedure:**

- Đọc/tạo/sửa/xuất bản: `procedure.read`, `procedure.create`, `procedure.update`, `procedure.publish`.
- Trạng thái/lịch sử/nguồn: `procedure.status`, `procedure.versions.read`,
  `procedure.rollback`, `procedure.source.read`.
- Danh mục: `procedure.categories.manage`.
- Bản nháp: `procedure.drafts.read`, `procedure.drafts.upload`, `procedure.drafts.update`,
  `procedure.drafts.extract`, `procedure.drafts.publish`, `procedure.drafts.delete`.

**Workflow:**

- Người nộp: `workflow.create`, `workflow.read`, `workflow.checklist.write`,
  `workflow.submit`, `workflow.resubmit`.
- Cán bộ: `workflow.queue.read`, `workflow.assign`, `workflow.comments.write`,
  `workflow.revision.request`, `workflow.readiness.confirm`.
- Đối soát: `workflow.comments.read`, `workflow.versions.read`, `workflow.diff.read`.

**IAM trong danh mục dùng chung:**

- `iam.accounts.read`, `iam.accounts.manage`, `iam.wards.read`, `iam.wards.manage`,
  `iam.rbac.manage`, `iam.audit.read`.
- IAM còn có các quyền riêng như `iam.manage`, `iam.profile.read`, `iam.profile.write`.
  Không dùng tổng số ở danh mục dùng chung để kết luận số permission trong database deploy.

## 11. Điểm bắt đầu đối chiếu mã nguồn

Đường dẫn tương đối dưới đây dùng được khi mở repository trong IDE hoặc GitHub:

- [Validator đăng ký/đăng nhập](../src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs).
- [Phạm vi quản lý tài khoản](../src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/ManagementScope.cs).
- [Validator thủ tục](../src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs).
- [Luồng bản nháp](../src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/ProcedureDraftService.cs).
- [Đọc PDF và fallback thủ công](../src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/TextFirstProcedureExtractor.cs).
- [Thẩm định/nộp lại hồ sơ](../src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewHandlers.cs).
- [Kiểm tra bằng chứng file trước nộp](../src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/DocumentContentsClient.cs).
- [Phạm vi tài liệu theo hồ sơ](../src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Security/ApplicationAccess.cs).
- [Tra cứu QR công khai](../src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Queries/QrLookupQuery.cs).
- [Danh sách hồ sơ cán bộ](../src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Application/ReviewContracts.cs).
- [Schema và dữ liệu biểu mẫu](../src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Services/FormSchemaEngine.cs).
- [Thông báo của người nhận](../src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationService.cs).

## Nhật ký tài liệu

- 20/10/2026 — QR-BACKWARD-COMPAT-001: Bổ sung cơ chế tương thích ngược cho mã QR đã cấp trước đây: (1) Endpoint `GET /api/v1/applications/{id}` cho phép truy cập nặc danh (`[AllowAnonymous]`), nếu không có Bearer token sẽ tự động HTTP 302 Redirect về `GET /api/v1/public/qr-lookup?applicationId={id}`, đảm bảo người dân quét lại mã QR cũ trên PDF in trước đây vẫn xem được thông tin công khai hợp lệ mà không bị lỗi 401/403. (2) `GeneratePdfDocumentCommand` tự động cập nhật bản ghi `ApprovedQr` cũ sang URL tra cứu công khai mới và sinh lại hình ảnh QR khi xuất PDF mới.

- 19/10/2026 — OFFICER-QR-001: bổ sung endpoint danh sách tổng hợp hồ sơ phường `GET /api/v1/officer/applications` (ràng buộc: chỉ xem hồ sơ cùng `ward_code`, không xem hồ sơ do chính cán bộ nộp, hỗ trợ lọc nhiều trạng thái phân tách bằng dấu phẩy, phân trang tối đa 100). Thêm endpoint tra cứu QR công khai không cần auth `GET /api/v1/public/qr-lookup` hỗ trợ phân giải URL/GUID/token trả thông tin hợp lệ của tờ khai đã duyệt. Cấu hình Gotenberg URL trên Azure Container Apps yêu cầu HTTPS.

- 15/10/2026 — QR-PDF-001 (TASK-22, TASK-23): thêm ràng buộc mã QR tra cứu hồ sơ đã phê duyệt
  (bảng `document.approved_qrs`, unique index `application_id`), đóng dấu QR OpenXML 3.5cm x 3.5cm
  vào cuối văn bản DOCX trước khi render Gotenberg PDF; bảo vệ quyền truy cập API tải QR theo trạng thái APPROVED.

- 15/10/2026 — FLOW-REVIEW-001: thống nhất kiểm tra checklist/case/form trước xuất bản;
  validate resubmit trước gọi phụ thuộc; chặn phản hồi bằng chứng null/trùng;
  sửa quyền đọc tài liệu cho tài khoản có đồng thời role công dân và Front Desk.

- 14/10/2026 — DOC-CONSTRAINTS-001: tạo sổ tay theo source hiện hành; bao gồm IAM,
  Procedure/Drafts/AIOCR, Workflow/checklist, DocumentForm/PDF, Notifications,
  ràng buộc database, CSRF/CORS/token và các giới hạn liên service.


### 17/10/2026 — WF-READINESS-001

- Cập nhật luật vòng đời và DOC-05, bỏ API approve/reject; giữ dữ liệu cũ không tự chuyển. Application DTO dùng documentsReadyAt. Checklist REJECTED là trạng thái mục giấy tờ, không phải từ chối giải quyết Application.
- Bắt buộc apply migration Workflow DocumentReadiness và IAM WorkflowReadinessPermission trước khi chạy code mới. Giữ Database:AutoMigrate=false ở production nếu dùng job migration riêng. Không chạy Down tự động: IAM không tự cấp lại quyền cũ; Workflow chặn Down khi đã có DOCUMENTS_READY.
- Grant mới kế thừa các role đang có workflow.approve; gỡ hai permission cũ. Workflow lấy quyền mới qua access-context IAM mỗi request; FE nên refresh/login để đồng bộ menu và claims.
- Nguồn: ApplicationWorkflow.Application/DocumentReadinessRequests.cs, API/Controllers/ApplicationReviewController.cs, Infrastructure/WorkflowDbContext.cs; DocumentForm.API/Security/ApplicationAccess.cs; SharedKernel/Web/FeaturePermissions.cs; migration IAM WorkflowReadinessPermission.

- Triển khai đồng bộ IAM/Workflow/DocumentForm/AnalyticsSystem, tạm dừng thao tác review trong lúc chuyển phiên bản: migration IAM gỡ quyền cũ nên API cũ sẽ không còn đủ quyền. Kiểm tra luồng Docker trước Azure; chưa xác nhận runtime toàn luồng trong phiên này.

### 17/10/2026 — TASK-25/27: kiểm tra độ tin cậy thông báo

- Cập nhật NOTI-02: rollback Inbox trước khi ghi retry, log định danh sự kiện/delivery và số lần lỗi; kiểm tra từng connection trước khi gửi notification/bộ đếm. Không log token hoặc payload.
- Phân biệt Sent với xác nhận client; giới hạn một replica/revision do chưa có backplane. SMS/Email giữ hoãn. Nguồn: NotificationInboxWorker.cs, NotificationDeliveryWorker.cs, NotificationConnectionGuard.cs, NotificationService.cs.

### IAM-07 — Đổi mật khẩu cá nhân (P0, 21/10/2026)

- POST /api/v1/users/me/change-password lấy UserId từ Bearer; không nhận UserId tùy ý, không yêu cầu quyền quản trị. Tài khoản phải active. Body tối đa 4096 byte.
- currentPassword phải đúng hash, newPassword khác mật khẩu cũ, confirmNewPassword khớp. Mật khẩu mới >=8 ký tự, có hoa/thường/số/ký tự đặc biệt; mật khẩu hiện tại/mới tối đa72 byte UTF-8 do bcrypt. Không trim mật khẩu. Sai mật khẩu/validation trả400; tài khoản không khả dụng401; cập nhật cạnh tranh409.
- Khóa hàng user, so hash kỳ vọng trước cập nhật và revoke toàn bộ refresh token trong cùng transaction. Token issuance kiểm tra hash chưa thay đổi sau khi chờ khóa. JWT access token đã cấp KHÔNG bị thu hồi tức thì: tồn tại tới hết hạn. FE xóa access token, quay lại đăng nhập; refresh cookie cũ không còn refresh được.
- Nguồn: ChangePasswordCommand.cs, IdentityStore.cs, UsersController.cs.

### 21/10/2026 — Chặng 1 P0

- Cập nhật WF-04, thêm IAM-07; ReviewActor tách CanReadWard/CanMutate. Manager nhận workflow.queue.read và workflow.read qua migration ManagerWorkflowReadScope, gỡ các grant thao tác của Manager. Không tự cấp role FD cho Manager.
- Comments/version/diff vẫn yêu cầu permission riêng; migration này chỉ cấp hai quyền đọc theo yêu cầu. Danh sách dùng metadata total (không phải totalCount trong ví dụ audit); status READY_TO_SUBMIT chưa có, lọc sẽ không có kết quả. DocumentForm không được mở quyền ghi theo thay đổi này.


### 21/10/2026 — Chặng 2 P1

- WF-03 bổ sung DOCUMENTS_READY → OFFICIALLY_RECEIVED qua CounterReceiptRequests.cs: chỉ CanMutate, đúng phường/assignment và không tự xử lý; policy workflow.receipt.confirm. PaperDossierVerified phải true, notes<=4000 ký tự, appointmentDate nullable nhưng nếu có không được quá khứ. Chuyển trạng thái, ReceivedAt UTC, biên nhận và lịch sử/Outbox trong transaction khóa hồ sơ. Lần hai409. Không thêm approve/reject.
- WF-03 bổ sung DRAFT/NEED_REVISION → CANCELLED qua API chính chủ với workflow.cancel. Trạng thái khác409, không sở hữu404, không xóa dữ liệu. Bản nháp chưa từng nộp có currentVersionNumber=0 sau hủy.
- DB-P1: receipt_number nullable varchar64 unique, received_at/appointment_date nullable timestamptz; migration Workflow 20261021073245_CounterReceipt. Down chặn khi đã có dữ liệu biên nhận hoặc OFFICIALLY_RECEIVED để tránh mất dữ liệu. IAM CounterReceiptPermissions cấp receipt cho FD, cancel cho role đang có workflow.submit; Manager không được cấp quyền tiếp nhận. Cần áp dụng cả hai migration trước dùng endpoint.
- DOC-05: OFFICIALLY_RECEIVED tiếp tục cho phép đọc/xuất PDF như DOCUMENTS_READY; không mở quyền sửa. Preview kế thừa kiểm tra download; chỉ PDF/PNG/JPEG inline, Word415; nosniff/no-store. Alias upload-filled-docx giữ đầy đủ ApplicationId/ChecklistId, ownership và giới hạn DOCX của draft hiện có.
- IAM-P1: X-Client-Type Mobile chỉ chọn cách truyền token, không phải xác thực thiết bị. Mobile login/refresh trả refreshToken JSON, bỏ đọc/ghi cookie; refresh/revoke đọc token body. Web giữ cookie. Request có Origin hoặc Sec-Fetch-Site vẫn qua CSRF; native không có hai header này được miễn CSRF. Rotation/revocation vẫn dùng logic hiện có, access JWT còn hiệu lực tới expiry. Nguồn AuthController.cs, BrowserAuthProtectionAttribute.cs, RefreshTokenCookie.cs.
- PROC-P1: featured chỉ5 bản active theo CreatedAt giảm dần, Id để ổn định thứ tự; projection summary không lấy JSON lớn. Không khẳng định là thủ tục được xem nhiều nhất.
- NOTI-02: consumer chấp nhận OFFICIALLY_RECEIVED, thông báo có tiêu đề cho tiếp nhận/hủy. Draft hủy chưa có mã hồ sơ dùng GUID làm mã trong event để hợp đồng sự kiện hợp lệ; không thay mã hồ sơ trong DB. SMS/Email vẫn hoãn.

### AI-P2-01 — CCCD, tiền kiểm và chat (21/10/2026)

- Nguồn: AIOCR.Application/AssistantContracts.cs; AIOCR.Infrastructure/GeminiAssistant.cs, AuthorizedDocumentSource.cs, ChatService.cs, ChatDbContext.cs; AIOCR.API/Controllers/AssistantController.cs, AssistantRegistration.cs.
- Bearer bắt buộc, kiểm tra chữ ký HS256/issuer/audience/expiry/sub GUID; chưa truy vấn access-context để thu hồi JWT tức thì. Không tự thêm role/permission mới; tất cả user có JWT hợp lệ được dùng AI, DocumentForm bảo vệ đọc giấy tờ.
- CCCD frontImage bắt buộc, backImage optional, PNG/JPEG kiểm tra magic bytes,<=8MiB/file,request17MiB. Không lưu CCCD vào DB/log; nội dung gửi Gemini. Chưa giải mã ảnh cục bộ để xác minh toàn vẹn; file hỏng có thể bị provider từ chối. JSON bắt buộc đủ fields, không trùng key; giá trị thiếu null; CCCD12số, ngàyISO, confidence hữu hạn0..1. Output sai502, không bịa kết quả.
- Verify yêu cầu IDs không rỗng, expectedType tùy chọn<=255 và bị bỏ qua; caller token được gửi tới DocumentForm, xác minh tài liệu nằm trong danh sách hồ sơ trước download. Giữ phân quyền owner/officer của service đó, không privileged bypass. Chỉ URL cấu hình, không redirect, response file giới hạn8MiB. PDF/PNG/JPEG; không xử lý DOCX. expectedType lấy từ Title của checklist hồ sơ Workflow qua ChecklistId của giấy tờ DocumentForm, không lấy từ client. Thiếu liên kết/mục không hợp lệ trả409, chưa cấu hình Workflow trả503; không gọi AI khi không có căn cứ. isValid chỉ gợi ý, không ghi checklist, không chứng nhận pháp lý; hạ false khi confidence<0.7/qualityIssues/loại rỗng.
- Chat message1..4000ký tự, wardCode1..50 chữ/số/_/-, không đổi ward giữa phiên. Ward chỉ là ngữ cảnh, chưa xác minh phường. Queries luôn lọc UserId, foreign session404. Lưu cặp User/Assistant trong transaction; lỗi AI rollback, cùng phiên khóa NOWAIT trả409 khi cạnh tranh. Không có idempotency, timeout sau commit cần đọc lại lịch sử. Giữ khóa DB trong tối đa thời gian gọi model; cần cân nhắc pool khi tăng tải.
- chat_sessions/chat_messages thuộc DB AIOCR; FK cascade session->messages, logical UserId không FK chéo IAM; role check User/Assistant, content<=20000; index UserId+CreatedAt, SessionId+CreatedAt. Chưa có delete/retention tự động; migration Down xóa bảng và dữ liệu, không chạy trên DB có lịch sử cần giữ.
- Chat thuần LLM, relatedProcedures rỗng, chưa RAG/vector/citations; chỉ10 tin gần nhất và tin mới gửi model. Không bảo đảm thông tin pháp luật/phí/thời hạn đúng hiện tại. Confidence do model tự đánh giá, chưa hiệu chuẩn.
- Assistant:Enabled mặc địnhfalse, cần Gemini Key/Model; không đổi model/billing hiện có. Chat cần ConnectionStrings:AiOcrDatabase riêng và migration ChatSessions. Database:AutoMigrate opt-in; không migrate mặc định. Cần đồng bộ JWT/CORS với IAM/FE.
- Giới hạn10request/user/phút/replica, chưa globalquota/backplane; Gemini90s, DocumentForm60s; không auto retry. HTTP response Gemini<=1MiB, text<=20000. 400/401/403/404/409/413/415/429/502/503/504 theo API guide. Không lộ upstream body/API key trong ProblemDetails.

### Nhật ký 21/10/2026 — Chặng 3 P2

Thêm AI-P2-01 và tài liệu cấu hình. Chỉ thay đổi local; không áp dụng DB, không gọi Gemini với dữ liệu cá nhân, không triển khai Azure. OCR CCCD không thay đổi danh tính IAM; prevalidation không thay workflow/checklist. API nội bộ đọc thủ tục giữ hợp đồng cũ.

### WF-05 — Hạn tiền kiểm giấy tờ (21/10/2026)

- Workflow sở hữu cách tính; Analytics chỉ phân loại/tổng hợp theo mốc Workflow. Không dùng AppointmentDate, ApprovedAt hay processingTimeSummary làm hạn kiểm tra. Mặc định mới theo chủ dự án:45giờ làm việc, thứHai–thứSáu07:30–16:30 UTC+07, khung liên tục; pause khi yêu cầu bổ sung. Holidays=[] hiện chưa loại ngày lễ, cần cấu hình lịch thực tế và tăng CalendarVersion trước áp dụng.
- FirstSubmittedAtUtc chỉ gán trong submit lần đầu; SubmittedAt cũ vẫn là lần nộp gần nhất. Không backfill hồ sơ cũ bằng ngày hiện tại, lịch hiện tại hoặc ngày hẹn. UNKNOWN/due=null cho dữ liệu cũ. Draft cũ nếu CHƯA nộp và nộp lần đầu sau nâng cấp được chụp chính sách mới.
- Khi Enabled=true, snapshot chính sách và calendar JSONB được lưu cùng transaction với submit; code/version,khung giờ,ngày làm,holidays,PauseOnRevision,WorkingMinutes đi cùng hồ sơ. Không đổi hạn cũ khi cấu hình mới thay đổi. Applicable=false tạo NOT_APPLICABLE; Enabled=false giữ UNKNOWN. Config invalid fail startup, không im lặng gán hạn giả.
- Tất cả timestamps UTC; lịch VN cố định+07. AddWorkingTicks bảo toàn phần giây/ticks và nhảy cuối tuần/ngày nghỉ. Không reset45giờ khi resubmit. Pause ghi PAUSE; Resume cộng số workingticks trong khoảng pause vào due cũ rồi ghi RESUME với dueBefore/dueAfter và actor. Nếu đã quá hạn trước pause, giữ phần trễ. Cancel ghi CANCEL đóng pause, không giả lập resume/hoàn thành.
- DB check: KNOWN cần due/firstsubmit/snapshot/mãphiênbản; UNKNOWN/NOT_APPLICABLE không códue hoặc pause. Index WardCode+DeadlineStatus+DueAtUtc. deadlineHistory JSONB [] mặc định. documentsReadyAtUtc là DTO alias của DocumentsReadyAt cũ; không cần cột timestamp thứ hai. Down migration chặn nếu có chứng cứ deadline mới để tránh xóa audit.
- Quyền/status/locking của submit,revision,resubmit,cancel giữ nguyên; client không được đặt deadline. Detail+list có trường mới, không bỏ fields cũ.
- Nguồn: ApplicationWorkflow.Domain/ReviewDeadline.cs, ApplicationRecord.cs; Application/WorkflowRequests.cs,ReviewHandlers.cs,CounterReceiptRequests.cs,WorkflowContracts.cs; Infrastructure/WorkflowDbContext.cs,ApplicationStore.cs,ApplicationReviewStore.cs; API/Program.cs,appsettings.json.

### ANA-DEADLINE-01 — Đúng/trễ hạn và backlog riêng

- Analytics overview chỉ lấy mẫu completed KNOWN (documentsReadyAtUtc<=due đúng; >due trễ), rate0..100, mẫu0 trảnull. CompletedUNKNOWN và NOT_APPLICABLE ghi riêng và loại mẫu số. Không gộp openOverdue với completedLate.
- Completed cohort theo ngày sẵn sàng trong From–To (UTC+07). Backlog hiện tại cùng phường/thủ tục, không lọc ngày nộp và không phải backlog lịch sử ngàyTo. Open statuses chỉSUBMITTED/UNDER_REVIEW/NEED_REVISION; loạiDRAFT/CANCELLED/legacyAPPROVED/REJECTED/OFFICIALLY_RECEIVED khi không có readiness. Open có pause so due với thời điểm pause để tránh tính thời gian chờ; đãtrễ trướcpause vẫntrễ.
- Snapshot facts qua HTTP internal /internal/analytics/v1/deadlines, servicekey>=32byte cốđịnh và so sánh constant-time. Không truy cập DB chéo service. Manager chỉ lấy ward từ access-context, không nhận ward tùy ý của FE. Query1..366ngày, ward<=50; nguồn>100000rows trả503 không cắt mẫu gây sai tỷ lệ. DTO kiểm tra version,UTC,status,uniqueness và nhấtquán known/due.
- Overview đọc deadline trực tiếp Workflow; totals cũ đọc cache Analytics với Coverage riêng. asOfUtc riêng cho deadline, không khẳng định cùng snapshot với cache. Endpointoverview nay cần Workflow và Analytics:ServiceKey kể cảjobsdisabled; lỗi nguồn không giả0. Deadline đã thêm vào trends/officer/procedure statistics (22/10/2026), không tạo metric cache mới, chưa lưu backlog snapshot lịch sử.
- Nguồn: WorkflowAnalyticsSource.cs,AnalyticsSourceController.cs,Application/DeadlineFacts.cs; AnalyticsSystem.Application/DeadlineStatistics.cs,AnalyticsContracts.cs; Infrastructure/AnalyticsService.cs,AnalyticsWorkflowClient.cs.

### Nhật ký 21/10/2026 — Workflow review deadline

Thêm WF-05/ANA-DEADLINE-01 theo quyết định chủ dự án45giờ,Monday–Friday07:30–16:30, pause khi bổ sung. Migration Workflow mới; không backfill deadline cũ, không đổi hạn hành chính, không chạyGit/DBdeploy. Các chú thích cũ UNAVAILABLE_NO_WORKFLOW_DEADLINE là lịch sử trước lần bổ sung này.


### Bổ sung FB-01 / AI-P2-01 / ANA-DEADLINE-01 — 22/10/2026

- FB-01: giữ unique ApplicationId, quyền chủ và rating1–5; tiếp nhận tại quầy không mất quyền đánh giá. Nguồn: AnalyticsSystem.Infrastructure/AnalyticsService.cs.
- AI-P2-01: DocumentForm vẫn quyết định quyền đọc/tải, AIOCR chuyển tiếp cùng Bearer sang Workflow; fallback API cán bộ chỉ sau API chủ trả403/404. Không mở rộng quyền Manager hoặc cán bộ chưa phân công. ChecklistId lấy từ dữ liệu BE, không nhận từ FE; Title là tên yêu cầu trong snapshot hồ sơ. Response thêm checklistId/expectedType. 409 thiếu liên kết: tải lại đúng mục; 503 thiếu cấu hình: bổ sung Services:ApplicationWorkflow:BaseUrl trên AIOCR. Nguồn: AuthorizedDocumentSource.cs, AssistantController.cs, AssistantContracts.cs, docker/docker-compose.yml.
- ANA-DEADLINE-01: nguồn bổ sung ProcedureId/AssignedOfficerId (hợp đồng cộng thêm trường); dữ liệu thiếu ProcedureId không được gán nhóm mặc định mà báo503. Trends dùng ngày hoàn tất UTC+07, backlog hiện tại riêng; thủ tục/cán bộ hợp nhất nhóm cache và nguồn live để không mất nhóm chưa có cache. Cán bộ null nằm trong unassignedDeadlines; tỷ lệ null khi không có mẫu Known. CompletedMissingDeadline/NotApplicable không nằm trong mẫu số; quá hạn đang mở không gộp completedLate. Nguồn: WorkflowAnalyticsSource.cs, DeadlineFacts.cs, AnalyticsService.cs, DeadlineStatistics.cs, AnalyticsContracts.cs.
- Giới hạn: chưa có lịch sử backlog theo ngày, chưa quy trách nhiệm theo lịch sử điều chuyển, AI không kiểm định tính pháp lý hoặc số bản. Không đổi schema DB, chính sách45giờ/ngày nghỉ hay quyền các API.

Nhật ký22/10/2026: cập nhật ba nhóm quy tắc trên; chỉ BE local và build, không chạy Git/triển khai; nghiệm thu giao diện do FE thực hiện.

### NOTIFY-EMAIL-01 — Email nghiệp vụ và SMS chuẩn bị (22/10/2026)

- Gửi SMTP chỉ khi Email.Enabled và host/from/username/password/contact đủ; thiếu trả Blocked. TLS bắt buộc, timeout gửi15giây. SMTP exception không dừng worker, mã lỗi tách biệt, retry tối đa10 như delivery hiện hành. Sent là SMTP chấp nhận; có thể trùng nếu SMTP nhận nhưng commit DB thất bại. Không có đảm bảo exactly-once, bounce tracking hoặc requeue Blocked tự động.
- Email HTML/plain-text UTF-8, mọi dữ liệu động HTML-encode; CTA chỉ cùng origin Email.FrontendBaseUrl và HTTP(S). Dữ liệu notes từ status history event được kiểm tra thuộc hồ sơ, tên công dân IAM hiện hành. Thiếu dữ liệu không giả lập tên/ngày hẹn. Nội dung broker giữ tối thiểu như cũ; email-context qua HTTP nội bộ X-Analytics-Key>=32byte/no-store; IAM contact API thêm fullName và vẫn khóa hiện hành. Không truy vấn DB chéo service.
- Development sample không chứa mật khẩu. Script local đọc docker/.env vào process và khôi phục biến sau chạy; không in secret, không tự migrate/start Docker. Môi trường phải có các DB/service và khóa khớp. Chạy worker có thể gửi thư thật từ backlog; Codex không tự gửi thư trong lượt này.
- Sms.Enabled=false mặc định; ISmsSender có SmsDeliveryResult. UnconfiguredSmsSender không gọi provider, trả Blocked. Success+Sent mới được đánh Sent, lỗi khác retry; chưa lưu provider ID/webhook. SMS thật chưa hoàn thành.
- Nguồn: AnalyticsSystem.Infrastructure/ExternalNotificationSender.cs, NotificationEmailTemplate.cs, NotificationEmailSource.cs, NotificationDeliveryWorker.cs; Workflow AnalyticsSourceController/WorkflowAnalyticsSource; IAM NotificationRecipients; scripts/start-notifications-local.ps1.

Nhật ký22/10/2026: thêm NOTIFY-EMAIL-01 cho TASK-26 Email trước/SMS sau; giữ hạn chế SMTP trùng và khóa nội bộ. Không đổi schema, không đưa secret vào repo, không thay billing.

Đính chính trạng thái22/10/2026: các đoạn cũ ghi “SMS/Email hoãn” là lịch sử trước TASK-26. Hiện Email đã triển khai template và adapterSMTP/cấu hình local; chưa xác nhận gửi thật. SMS vẫn hoãn, giữ tắt.

### IAM-OTP-01 — OTP mật khẩu và email chào mừng (22/10/2026)

- RegisterCitizenHandler + IdentityStore: tài khoản và hàng welcome cùng transaction; unique userId ngăn tạo trùng hàng, không bảo đảm SMTP exactly-once. Worker retry10, không log recipient/nội dung/credential; timeout15s, TLS, HTML-encode giá trị động, plain-text alternate. Chưa xác thực email khi đăng ký.
- RequestPasswordOtpCommand: EMAIL/SMS, CHANGE_PASSWORD/FORGOT_PASSWORD; SMS400 bảo trì. CHANGE cần JWT chính chủ, FORGOT anonymous; không nhận actorId từ client. Body4KiB, nhận dạng<=255. Cùng tài khoản khóa FOR UPDATE: cooldown60s áp dụng mọi purpose/alias, gửi lại expire mã trước. 202 chung không khẳng định gửi tới inbox; SMTP lỗi expire mã và không lộ account qua nội dung response (không cam kết chống phân tích timing).
- VerifyPasswordOtpCommand: OTP6số,5phút,Attempts<5,IsUsed=false; so constant-time HMAC-SHA256 dùng salt và server key. Sai tăngAttempts commit; dùng thành công ghi mật khẩu/consume/revoke refresh/invalidate mã khác atomically cùng khóa user. Luồng đổi mật khẩu cũ cũng vô hiệu OTP. Không lưu/log mã thô. Password8+ có hoa/thường/số/special,max72byteUTF8, confirmkhớp; lỗi không chứa giá trịpassword.
- Rate limiter10/IP/phút/replica,429+RetryAfter60, không rate-limit phân tán; cooldown và attempts bảo vệ bằng DB. Browser giữ X-CSRF-Protection/Origin; không bypassCSRF cho anonymousOTP. SMS stub, không gửi SMS.
- Access JWT cũ chưa bị thu hồi ngay; refresh đã revoked, FE xóa token và login lại. Email chưa bật/thiếu cấu hình503. KhóaPasswordOtp:HashKey (fallbackJwt:Key)>=32byte, thống nhấtreplicas; thaykey vô hiệu OTPpending. Không cleanup OTP hết hạn tự động.
- Schema iam.user_otps (FKpublic.users,checkattempt0..5,channel/purpose), iam.welcome_emails (uniqueuser). Migration bắt buộc trướccode; chưa chạyDBcloud. Source: UserOtp.cs, UserOtpConfiguration.cs, PasswordOtpCommands.cs, PasswordOtpService.cs, IamEmailSender.cs, WelcomeEmailWorker.cs, RegisterCitizenHandler.cs, IdentityStore.cs, AuthController.cs, Program.cs.

Nhật ký22/10/2026: thêm IAM-OTP-01; không đổi SMTP credential thật, không Git/Cloud; API cũ đổi mật khẩu tiếp tục hỗ trợ, không chuyển toàn hệ thống sang bắtbuộcOTP.


Nhật ký 22/10/2026 — IAM-OTP-01: bổ sung script catch-up docs/sql/iam-catchup-readiness-to-otp.sql cho baseline WorkflowReadinessPermission. Preflight kiểm tra baseline và bảng IAM trong từng transaction, cố định search_path public, giữ lịch sử service khác; không chạy đồng thời auto-migrate. Script thay đổi quyền MANAGER thành read-only theo migration có sẵn, thêm quyền receipt/cancel và bảng OTP/welcome. Không xác nhận dữ liệu cloud chỉ từ việc sinh SQL.


### IAM-SMS-01 — OTP eSMS (22/10/2026)
- Mở kênh SMS của IAM-OTP-01; stub SMS Analytics vẫn không đổi. Số nhận chỉ từ profile tài khoản hợp lệ; normalize 0/84/+84 về dạng0, chấp nhận mobile10số đầu03/05/07/08/09. Không xác minh số còn hoạt động hay chủ thuê bao.
- IIamSmsSender/IamSmsSender: HTTPS endpoint eSMS cố định, không redirect, timeout10s, không tự retry; chỉ CodeResult100 thành công. Log HTTP status hoặc mã lỗi numeric giới hạn4ký tự/loại exception, không raw response/content/OTP/khóa/số điện thoại. Keys chỉ cấu hình môi trường. Provider=eSMS, SmsType2, Sandbox0/1; brand ASCII chữ/số/khoảng trắng1..32. Đổi brand cần duyệt template nhà mạng.
- PasswordOtpService: channel ghi SMS, giữ HMAC-SHA256 có khóa và salt hiện có (không hạ xuống SHA256 thuần). Khóa user, cooldown60s xuyên kênh/purpose, expire challenge cũ,5phút,5lần thử, dùng một lần. Verify lấy challenge mới nhất theo purpose không lọcEMAIL. Lỗi gửi được invalidate và giữ cooldown. Không có bảo đảm exactly-once khi nhà mạng đã nhận nhưng timeout/transaction lỗi.
- Phản hồi SMS có số masked và400 thiếu/sai phone theo yêu cầu nên có thể phân biệt tài khoản tồn tại; không tuyên bố chống enumeration hoàn toàn. Sandbox không gửi thực; CodeResult100 không chứng minh SMS đã đến máy. Cấu hình mẫu disabled/Sandbox1; bật gửi thật phải cung cấp secret và Sandbox0.
- Nguồn: Application/Interfaces/IIamSmsSender.cs; Infrastructure/Security/IamSmsSender.cs, PasswordOtpService.cs; Infrastructure/DependencyInjection.cs; API/appsettings.json; docker/docker-compose.yml, docker/.env.example. Không đổi schema/API routes. Lịch sử22/10/2026: thay hạn chế SMS stub của IAM-OTP-01 bằng tích hợp eSMS, giữ các giới hạn access JWT và rate limit hiện có.


### IAM-SEC-02 — Hoàn thiện bảo mật tài khoản (23/10/2026)
- Thay giới hạn cũ IAM-OTP-01/IAM-SMS-01: SMS khôi phục chỉ dùng số đã xác minh và còn khớp profile. Dữ liệu cũ verified_phone_number=NULL. Xác minh số yêu cầu Bearer + mật khẩu hiện tại + OTP gửi thật tới số mới; không hỗ trợ Sandbox để công nhận sở hữu. HMAC gắn cả target phone/purpose/challenge/user, mã không lưu thô. Profile edit bình thường xóa dấu xác minh khi sửa phone. Không bảo đảm số thuê bao không được tái cấp, không ép mỗi số chỉ gắn một tài khoản.
- PhoneVerificationService và PasswordOtpService dùng user FOR UPDATE, cooldown60s chung, quota10OTP/5SMS rolling24h cấu hình clamp; request provider lỗi đã commit vẫn tính quota. Không quota chi phí toàn cục/theo số across accounts; IP limiter chỉ mỗi replica. Hủy request/transaction lỗi có thể xảy ra sau khi nhà mạng nhận tin; không bảo đảm exactly-once hoặc hạn mức trước lỗi transaction.
- Password recovery202 nội dung đồng nhất cho account không có/thiếu số/chưa verified/cooldown/quota/provider lỗi, không masked phone; không cam kết chống timing analysis. Endpoint chính chủ status mới được trả masked phone.
- users.security_stamp + JWT claim được so tại IAM mỗi request. Hai đường đổi mật khẩu đổi stamp/revoke refresh/invalidateOTP atomically. Token issuance hiện serialize user/password để tránh cấp token hợp lệ bằng hash cũ. Client thiếu stamp bị từ chối access; refresh hợp lệ chưa revoked vẫn có thể lấy token mới.
- SharedKernel/Web/AccountSessionMiddleware được gắn5API sauauthentication: IAM access-context qua HTTP timeout5s, không redirect/cache, fail closed401 hoặc503. Cần Iam:BaseUrl đúng và mạng IAM khả dụng. IAM không tự gọi lại chính mình. Các request đang thực thi không bị hủy; SignalR theo guard hiện hữu trước gửi/hub và polling, không đóng socket tức thì. Tăng phụ thuộc và tải IAM; chưa có revocation cache phân tán.
- Cleanup giữ ít nhất2ngày,mặc định30; mỗi giờ/batch1000 mỗi bảng. Chỉ purge refresh sau hết hạn quáretention, không purge refresh còn hạn dù revoked. Welcome chỉ purge sent, không purge failed/pending. Worker bắt lỗi có logtype, không chứa secrets; nhiều replica có thể cùng cleanup, ExecuteDelete idempotent.
- Migration AccountSecurityHardening thay checkpurpose thêm VERIFY_PHONE, thêm target_phone_number/security_stamp/verified_phone_number. Cần áp dụng trướccode; rollback về check cũ không được khi còn dữ liệu VERIFY_PHONE, cần kế hoạch dữ liệu riêng.
Nguồn: IAM Domain/User.cs,UserOtp.cs; Infrastructure/Security/PhoneVerificationService.cs,OtpLimits.cs,AccountSecurityCleanupWorker.cs,PasswordOtpService.cs,JwtTokenService.cs; Persistence/IdentityStore.cs; API/Controllers/UsersController.cs; SharedKernel/Web/AccountSessionMiddleware.cs; Program.cs5service và cấu hình. Lịch sử23/10/2026: bổ sung IAM-SEC-02, thay phản hồi masked anonymous và hạn chế accessJWT cũ của tài liệu trước; chưa deploy/chưa gửiSMS thật.

Đính chính IAM-SEC-02: IdentityStore khi sửa phone hoặc xóa profile còn vô hiệu mọi OTP SMS chưa dùng/chưa hết hạn cùng transaction; không giữ mã khôi phục gửi tới số cũ.


### NOTIFY-PIPELINE-02 — TASK-25/27 rà soát 23/10/2026
- Outbox cùngtransaction history/comment, publisher confirm/persistent/mandatory; ACK sauInbox insert, PKEventId và unique(SourceEventId,RecipientUserId,Channel) chốngtrùngnotification; deliveries unique(NotificationId,Channel). Không exactonce send: crash sau sideeffect trướccommit cóthểgiao lại.
- Inbox/Delivery rowlock SKIPLOCKED, retry10/Dead; Inbox savepoint rollback partialwrites. Outbox retry mãi với backoff cap300s. Thiếu ward/application tracked khôngtạoevent theo capture hiện hữu; cần dữ liệu ward chuẩn. Người nhận được resolve ở thờiđiểmconsume, không snapshot danh sách toànphường tại thờiđiểmnộp.
- Đã sửa lỗ hổng realtime đa replica: DeliveryWorker transactionpg_notify UUID -> NotificationRealtimeListener trênmọireplica -> guardIAM -> sendlocalconnections. MarkRead cũngpháttínhiệu. LISTEN dùng dedicatedsession, khôngmultiplexing; direct/sessionpool cùngDB, cấu hình NotificationRealtimeDatabase tùychọn. Không hỗ trợ transactionpool choLISTEN, không tựpháthiện poolmode; deployments cần cấu hình đúng.
- NOTIFY tạmthời khôngbền, listenerreconnect5s, pendingbounded10000; tínhiệu mất/overflow hoặc lỗiIAM/send đượclog và clientreload danh sách đãlưu. SentIN_APP khôngchứngminh đãnhận/đãđọc. Không autoACK từclient, không lưu vôhạn eventtrênRAM. Unreadcount tính scope từngconnection; list/read quyền khôngđổi. SignalR nhiều replica cần sticky/appropriateWebSocket transport. Worker cần replica hoạtđộng, không tựwake từDB.
- Chưa có adminreplayAPI/alert tựđộng; SQLrunbook trongapi-guide, không reset/delete history tựđộng. Các hàngDead/Blocked cầnoperator xửlý, EmailSMTP khôngexactonce, SMS Analytics đã bổ sung theo NOTIFY-SMS-01 dưới đây. Ràsoátsource/build không thay thế nghiệmthu trênAzure.
Nguồn: WorkflowOutbox.cs, WorkflowDbContext.cs, RabbitTransport.cs, WorkflowNotificationConsumer.cs, NotificationInboxWorker.cs, NotificationDbContext.cs, NotificationDeliveryWorker.cs, NotificationRealtimeListener.cs, NotificationService.cs, NotificationConnectionGuard.cs, IAM/NotificationRecipients.cs. Lịch sử23/10/2026: thêmNOTIFY-PIPELINE-02; khôngmigration/khôngđổiDTO.

### IAM-SEC-02 — bổ sung chẩn đoán phiên (23/10/2026)
- Iam:BaseUrl phải là HTTP/HTTPS URI tuyệt đối, không chứa userinfo, query hoặc fragment. Sai cấu hình trả 503 auth.session_unavailable; vận hành sửa URL và nạp lại cấu hình.
- Giữ kiểm tra IAM mỗi request, timeout 5 giây, không redirect/cache và không bỏ qua xác minh khi IAM lỗi. 401/403 từ IAM ánh xạ 401; lỗi upstream khác/transport/timeout trả 503.
- Response lỗi bổ sung traceId. Log phân loại nguyên nhân, host/port và upstream status; không ghi Bearer token/body/exception message. Logging chỉ hỗ trợ chẩn đoán, không đảm bảo sửa được sự cố kết nối Azure.
Nguồn: src/BuildingBlocks/WardMate.SharedKernel/Web/AccountSessionMiddleware.cs.
Lịch sử: 23/10/2026 bổ sung chẩn đoán và kiểm tra định dạng BaseUrl cho IAM-SEC-02; chưa xác nhận nguyên nhân runtime trên Azure.

### IAM-SEC-02 — Analytics Azure runtime configuration (23/10/2026)
Analytics trên môi trường Azure hiện tại phải cấu hình Iam__BaseUrl bằng HTTPS FQDN của IAM. Kiểm tra runtime xác nhận hostname ngắn wardmate-iam không phân giải được; đây là giới hạn môi trường đã kiểm chứng, không phải quy tắc chung cho mọi Azure environment. Cập nhật cấu hình trên control plane chưa nạp vào replica cũ trong lần này; stop/start riêng Analytics đã tạo replica mới với URL đúng. Lỗi DNS làm xác minh phiên trả503; không bỏ qua IAM hoặc phân quyền để khắc phục. Nguồn: AccountSessionMiddleware.cs, AnalyticsSystem.Infrastructure/DependencyInjection.cs; xác minh runtime23/10/2026.

### DOC-QR-04 — Tự động gộp đơn PDF và loại bỏ dấu QR trong nội dung văn bản (23/10/2026)
- **Phạm vi & Điều kiện:** Áp dụng cho module DocumentForm khi kết xuất PDF (`GeneratePdfDocumentCommand`) và tra cứu chuyển hướng QR (`GetQrPdfRedirectQuery`).
- **Nội dung ràng buộc:**
  1. Loại bỏ hoàn toàn việc dán ảnh QR vào DOCX/PDF: văn bản xuất ra không bị dán hình ảnh mã QR vào trang cuối.
  2. Tra cứu qua mã QR (`GET /api/v1/public/applications/{applicationId}/pdf-redirect`) chuyển hướng HTTP 302 trực tiếp đến SAS URL trên Azure Blob Storage (thời hạn 15 phút, không cần token).
  3. Với hồ sơ có từ 2 đơn trở lên, hệ thống tự động gộp tất cả các đơn của hồ sơ đó thành 1 file PDF trọn bộ bằng Gotenberg PDF merge (`/forms/pdfengines/merge`).
  4. Lọc bản mới nhất: Quá trình gộp nhóm theo `SubmissionId ?? Id` và chỉ lấy bản PDF mới nhất của từng đơn, không giới hạn số lượng đơn trong hồ sơ và không ghép lặp lại các bản PDF cũ khi người dùng regenerate nhiều lần.
  5. Cập nhật cache: Bản gộp `COMBINED_APPLICATION` được tái sử dụng nếu thời gian tạo lớn hơn hoặc bằng thời gian của tất cả các đơn thành phần; nếu có bất kỳ đơn nào được tạo lại mới hơn, hệ thống sẽ tự động gộp lại bản mới.
- **Nguồn:** `GetQrPdfRedirectQuery.cs`, `GeneratePdfDocumentCommand.cs`, `GotenbergPdfRenderEngine.cs`.


### Analytics deployment prerequisites — cập nhật 23/10/2026
Migration 20261021031538_AnalyticsMetricsAndCitizenFeedback phải được áp dụng vào AnalyticsDatabase hoặc NotificationsDatabase fallback trước khi phục vụ API metrics/feedback. Thiếu schema analytics gây lỗi truy vấn500 dù health200. Đã áp dụng migration trên Azure, giữ AutoMigrate=false; không chạy migration Notification cùng lúc. JobsEnabled=true cần key Workflow đúng, quyền tạo schema analytics_jobs, worker chạy và kết nối DB; đã xác minh heartbeat và recurring analytics-daily 01:15 Asia/Ho_Chi_Minh. Bật job không có nghĩa historical metrics đã được tổng hợp; dùng rebuild hoặc chờ lịch, FE dựa coverage.
Nguồn: AnalyticsRegistration.cs, AnalyticsDbContext.cs, AnalyticsAggregationJob.cs và migration AnalyticsMetricsAndCitizenFeedback. Lịch sử23/10/2026: xác minh triển khai schema/worker; chưa nghiệm thu authenticatedAPI.

### AIOCR-JWT-01 — Cấu hình JWT bắt buộc của AIOCR (23/10/2026)
- Điều kiện: khi khởi động, AIOCR đọc Jwt:Key; nếu thiếu hoặc dưới 32 byte UTF-8 thì ném InvalidOperationException "Cần khóa Jwt:Key ít nhất 32 byte, giống IAM." và service không chạy. Phạm vi: toàn bộ AIOCR, gồm cả API AI công khai và procedure-extractions nội bộ.
- Issuer/Audience mặc định `wardmate`/`wardmate-client` khi không cấu hình, khớp mặc định IAM (JwtOptions.cs) và các service khác. Biến Jwt__Issuer/Jwt__Audience vẫn ghi đè; phải trùng IAM đang chạy. Kiểm tra token giữ nguyên: HS256, issuer, audience, expiry, chữ ký, sub GUID.
- Lỗi/khắc phục: service không khởi động → đặt Jwt__Key giống IAM (dùng secret, không ghi vào repo). 401 `invalid_token` → khóa/issuer/audience lệch IAM. 503 `auth.session_unavailable` sau khi token hợp lệ → Iam__BaseUrl sai/không kết nối được IAM.
- Giới hạn: chỉ kiểm tra độ dài khóa, không kiểm tra khóa có trùng IAM; Iam:BaseUrl trong appsettings vẫn là localhost cho chạy trực tiếp, môi trường cloud phải đặt Iam__BaseUrl. docs/azure-deployment.md còn ví dụ issuer/audience `WardMate.IAM`/`WardMate.Client` cho IAM; giá trị thực tế trên Azure chưa được xác minh trong lượt này.
- Nguồn: src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/AssistantRegistration.cs. Thay cho mô tả "thiếu/sai khóa thì không xác thực được" trong AI-P2-01.
- Lịch sử 23/10/2026: thêm AIOCR-JWT-01; không đổi route/body/status của API AI.

### IAM-STAFF-PROVISION-01 — Tạo tài khoản công vụ bởi IT Admin (23/10/2026)
- Phạm vi: POST /api/v1/accounts/manager và /procedure-manager. Policy AccountManagement + RbacAdministrator + iam.accounts.manage; transaction kiểm tra lại IsAdministrator và HasPermission từ DB để không chỉ tin claim cũ.
- MANAGER cần wardId (null400 iam.ward_required); mọi wardId khác null phải tồn tại (404 iam.ward_not_found). PROCEDURE_MANAGER có thể không gắn phường. Không tự bổ sung ward-scope cho Catalog.
- Validator mới tái dùng chuẩn đăng ký và bổ sung lowercase/digit cho hai API; không đổi validator đăng ký/front-desk. FullName bắt buộc <=255; username/email chuẩn hóa và unique; password >=8/<=72byte UTF-8, hoa/thường/số/đặc biệt.
- Tạo user/profile/đúng một role/audit trong rbac.Exclusive; unique index bảo vệ race với đăng ký công dân ngoài advisory lock. Conflict từ lưu mới được ánh xạ iam.duplicate_account409; không có user/audit một phần sau rollback. Không có migration mới.
- Không nhận role tự do; missing role404 iam.role_not_found. Audit chỉ wardId, targetUserId, roleId, action, không password. Không trả token/hash, không gửi email welcome hay buộc đổi mật khẩu lần đầu.
- Giới hạn: RBAC nâng cao vẫn có thể gán thêm role; quyền tạo front-desk giữ nguyên. Không có bảo đảm cấm đa role toàn hệ thống. Response bỏ null theo serializer hiện hữu.
- Nguồn: IAM Application/Accounts/ProvisionStaffRequests.cs, IManagementScope.cs; Infrastructure/Persistence/StaffAdministration.cs, RbacStore.cs; API/Controllers/StaffAdministrationController.cs.
- Lịch sử: thêm rule IAM-STAFF-PROVISION-01 ngày23/10/2026, không thay các rule cũ.
### NOTIFY-SMS-01 — eSMS thông báo hồ sơ (23/10/2026)

- Thay giới hạn stub Analytics của TASK-26; IAM OTP không đổi. Inbox chỉ tạo SMS khi Sms:Enabled; bật sau không backfill sự kiện đã xử lý. Kênh chỉ dùng recipient đã được pipeline xác định và liên hệ IAM hiện tại, kiểm tra scope phường hiện hữu.
- Cấu hình riêng: Provider=eSMS, SmsType=2, Sandbox=0/1; khóa bắt buộc, brand ASCII chữ/số/khoảng trắng 1..32. Gửi thật còn cần TemplatesApproved=true. Mặc định disabled/Sandbox1; không tự chép secret IAM.
- Mẫu theo status/kind và recipient: chỉ thay ApplicationCode từ event inbox đúng SourceEventId/ApplicationId, code ASCII chữ/số/_/- tối đa80; nội dung ASCII in được 1..459, không giữ placeholder chưa thay. Không gửi tên công dân, ghi chú tự do hay CCCD từ sự kiện. Thiếu/sai mẫu trả Blocked với sms.template_missing/invalid; operator sửa mẫu đã được duyệt. Đây là giới hạn ứng dụng, không cam kết số segment hay giá SMS.
- Số VN 0/84/+84 chuẩn hóa về 10 số đầu03/05/07/08/09; không xác nhận sở hữu hoặc hoạt động của số. Endpoint HTTPS cố định, không redirect, timeout10s, response buffer64KiB. Không log raw response, nội dung, OTP, phone hay secret.
- CodeResult100 là accepted: SMSID hợp lệ lưu external_message_id; provider và is_sandbox được lưu. Sandbox không có SentAt. Accepted thiếu ID không tự retry, lưu sms.accepted_without_id. Không có DLR webhook; Sent không chứng minh máy nhận.
- Trước gửi, commit Dispatching để tránh crash gửi lại tự động. Rejection rõ ràng/validation => Blocked; timeout/HTTP lỗi/kết quả không rõ => Unknown; hủy/crash có thể giữ Dispatching. Các trạng thái này không tự retry. RequestId=deliveryId dạng N chỉ để đối soát, không cam kết exactly-once. Operator cần đối soát trước replay; Email giữ retry cũ. Chưa có quota chi phí toàn cục hoặc API replay.
- Nguồn: Analytics Infrastructure/EsmsNotificationSender.cs, ExternalNotificationSender.cs, NotificationDeliveryWorker.cs, NotificationDbContext.cs, NotificationPipelineModels.cs, DependencyInjection.cs; API/appsettings.json; docker/docker-compose.yml, docker/.env.example. Lịch sử: bổ sung quy tắc này và migration EsmsNotificationDelivery; chưa deploy/chưa gửi SMS.

### WORKFLOW-ASSIGN-01 — Phân công theo lĩnh vực (24/10/2026)

- Thay giới hạn “assignedCategories chỉ hiển thị/lọc” của IAM-05: nay thêm ưu tiên mềm mặc định; không phải ranh giới phân quyền. Scope my-categories mới lọc; không gán lĩnh vực là toàn bộ, Manager bỏ qua scope. Ward và loại hồ sơ tự nộp vẫn bắt buộc. GUID assignee chỉ Manager; query sai400 workflow.validation.
- CategoryId/Name snapshot lúc tạo; dữ liệu cũ giữNULL, không lấy trạng thái Catalog hiện tại làm lịch sử. Tên cán bộ snapshot, thời điểm nullable; assignmentVersion tăng mỗi CLAIM/RELEASE/ASSIGN/TRANSFER. Không reset qua nộp lại.
- Manager cần role + workflow.assign.manage, không mở các policy ghi review cũ. Cán bộ đích phải active/cùng phường/FRONT_DESK_OFFICER, không kiêm Manager, không là người nộp; lấy qua IAM HTTP, không FK liên service. Có cửa sổ thay đổi IAM sau kiểm tra; không cam kết khóa phân tán.
- Release chỉ cán bộ đang phụ trách UNDER_REVIEW, lý do5..500; xóa người/tên/assignedAt, vềSUBMITTED, giữ comments và deadline. Manager giao mới chỉSUBMITTED chưa có người; chuyển chỉUNDER_REVIEW/NEED_REVISION/DOCUMENTS_READY đã có người. Không thay trạng thái khiTRANSFER, không reset/tiếp tục đồng hồ đangpause.
- Mutate FOR UPDATE ghi lịch sử, snapshot phân công và outbox trong cùng transaction. Manager bắt buộc expectedAssignmentVersion>=0; lệch409 workflow.assignment_conflict, người đích hiện tại409 workflow.same_assignee. Lý do sai400 workflow.validation; đích sai422 workflow.invalid_assignee; IAM unavailable503; ngoài phạm vi404. Release hỗ trợ version tùy chọn; yêu cầu cũ không gửi version chỉ được bảo vệ bởi trạng thái/người phụ trách hiện tại.
- Deadline overdue lọc KNOWN + có dueAtUtc + chưa ready + trạng thái tiền kiểm đang mở (SUBMITTED/UNDER_REVIEW/NEED_REVISION) + dueAtUtc < (DeadlinePausedAtUtc nếu đang pause, ngược lại now). Hồ sơ quá hạn trước khi pause vẫn là quá hạn, cùng quy tắc openOverdue của Analytics (ANA-DEADLINE-01). Không coi DeadlineStatus là enum OVERDUE và không đưa UNKNOWN vào mẫu số.
- Event phân côngVersion2 riêng có người cũ/mới/action, không cóPII/reason. Consumer tương thíchVersion1; deployconsumer trướcproducer. Sms templates OFFICER_ASSIGNED/OFFICER_UNASSIGNED riêng, không dùng mẫu trạng thái công dân.
- Event phân công thay event STATUS của cùng thao tác, nên người nhận được chọn theo action (NotificationInboxWorker.AssignmentRecipient): CLAIM → công dân; ASSIGN → công dân + cán bộ mới; TRANSFER → cán bộ mới + cán bộ cũ; RELEASE → cán bộ cùng phường trừ người vừa trả. Cán bộ phải active, cùng phường, không là người nộp. Khi Email:Enabled=true vẫn gửi email cho event phân công tới cùng người nhận; dùng template dự phòng, không lấy STATUS email-context hoặc lý do nội bộ. RELEASE dùng mẫu SMS OFFICER_SUBMITTED cho nhóm cán bộ.
- Lịch sử phân công và lý do là dữ liệu nội bộ: chỉ trả trong response cho cán bộ/Manager. Response API công dân (ApplicationDto.ForApplicant) đặt assignmentHistory = null.
- Gateway phải có route /api/v1/manager/applications/{**catch-all} → ApplicationWorkflow trước route /api/v1/manager/{**catch-all} của IAM; thiếu route này PUT assignee bị chuyển sang IAM và trả 404.
- Đính chính 24/10/2026 (Claude tiếp quản): sửa điều kiện overdue khi pause, chọn người nhận thông báo theo action, ẩn lịch sử phân công với công dân, thêm route gateway. Nguồn bổ sung: Infrastructure/ApplicationReviewStore.cs, Application/WorkflowContracts.cs (AssignmentHistoryDto, ForApplicant), WorkflowRequests.cs, CounterReceiptRequests.cs, ReviewHandlers.cs; Analytics NotificationInboxWorker.cs, EsmsNotificationSender.cs; Gateway appsettings.json, appsettings.Production.json.
- Nguồn: Workflow Application/AssignmentRequests.cs, AssignmentFilters.cs, ReviewHandlers.cs, WorkflowContracts.cs; Infrastructure/ApplicationReviewStore.cs, WorkflowDbContext.cs, WorkflowOutbox.cs, WorkflowDirectory.cs; API/Controllers/ManagerAssignmentController.cs, ApplicationReviewController.cs, WorkflowAccessMiddleware.cs; IAM UserDirectory/IdentityDtos và migrationManagerAssignmentPermission; EventBus/WorkflowEvent.cs; Analytics NotificationInboxWorker/WorkflowNotificationConsumer/EsmsNotificationSender.
- Lịch sử24/10/2026: mở ngoại lệ phân công choManager, giữ cấmreview; thêm bảngapplication_assignment_history và snapshot lĩnh vực. Không tự chạy cloud migration, không gửiSMS, không Git.

### WORKFLOW-ASSIGN-02 — Bảo vệ lý do nội bộ (24/10/2026)

- Bổ sung WORKFLOW-ASSIGN-01: ForApplicant vừa bỏ AssignmentHistory vừa đặt Reason=NULL cho history SUBMITTED→UNDER_REVIEW và UNDER_REVIEW→SUBMITTED. Bảo vệ cả bản ghi cũ mà không phụ thuộc tải đủ assignment history; không xóa lịch sử DB.
- Handler ASSIGN/RELEASE mới chỉ ghi lý do vào application_assignment_history; status history giữ chuyển trạng thái/thời gian/actor nhưng không sao chép lý do. TRANSFER vẫn ghi lịch sử phân công. Cán bộ/Manager có phạm vi hợp lệ vẫn đọc được lý do qua AssignmentHistory.
- Không lọc reason của yêu cầu bổ sung NEED_REVISION hoặc các chuyển trạng thái công khai khác. Không đổi validation, quyền, HTTP status hay schema.
- Đính chính Email: Email.Enabled tạo delivery cho cả sự kiện phân công, dùng template dự phòng khi không có STATUS context; không chứa lý do nội bộ. Giữ pipeline/nhóm người nhận hiện tại.
- Nguồn: Application/WorkflowContracts.cs, AssignmentRequests.cs; Analytics Infrastructure/NotificationInboxWorker.cs, NotificationEmailSource.cs, ExternalNotificationSender.cs. Lịch sử24/10/2026: khắc phục lọt reason qua status history; không migration/deploy/gửi tin trong phiên này.

### WORKFLOW-RESUBMIT-01 — Số lần nộp lại trên summary (24/10/2026)

ApplicationSummaryDto.ResubmitCount là int mặc định0; danh sách công dân, cán bộ và pending đều map giá trị lưu trong DB. Không suy ra từ AssignedOfficerId. Giữ quy tắc ResubmitApplicationCommand tăng bộ đếm khi nộp lại thành công, vềSUBMITTED và giữ cán bộ; assign chuyển sangUNDER_REVIEW. Không đổi schema/validation/quyền. Nguồn: Application/WorkflowContracts.cs, ReviewHandlers.cs; Infrastructure/ApplicationReviewStore.cs, ApplicationStore.cs. Bản đính chính này thay mô tả nullable trước đó riêng cho resubmitCount của summary.

### IAM-CATEGORY-01 — Tính hợp lệ và audit lĩnh vực (24/10/2026)

- Bổ sung IAM-05/WORKFLOW-ASSIGN-01: sau kiểm tra quyền/phạm vi, PUT categories xác minh ID qua full Catalog API, không FK/DB liên service. ID sai400 iam.invalid_categories; lỗi/thiếuconfig Catalog503 iam.catalog_unavailable, rollback. Mảng0..100 ID dương không trùng giữ validator hiện hữu. Không hạn chế quyền nhận hồ sơ khác lĩnh vực.
- Cập nhật và audit trước/sau cùng transaction và advisory lock RBAC1464680777. Audit chỉ khi tậpID thay đổi; actor/target/time cùng actionOFFICER_CATEGORIES_ASSIGNED, không có dữ liệu cá nhân. Không thay quyền đọc audit.
- Worker dùng cùng IAM lock, lấy Catalog mới trong lock rồi xử lý tối đa100 tài khoản có IDmồcôi/lượt. ActionOFFICER_CATEGORIES_RECONCILED actorGuid.Empty đánh dấu system. Định kỳ300s clamp30..86400; Enabled=false dừng worker. Lỗi giữ dữ liệu; không coi lỗi là danh sách rỗng. Không reset hồ sơ/snapshot, không gọi IAM từ transaction Catalog.
- Đồng bộ eventual: Catalog có thể xóa sau kiểm tra và trước commitIAM; worker khắc phục lượt sau. Không đảm bảo liên DB nguyên tử. Catalog giữ chặn xóa khi còn procedure. Danh sách sau dọnrỗng nghĩa mọi lĩnh vực theo D4. ID không được tái sử dụng thủ công ở Catalog; URL phải đúng môi trường và endpoint danh mục đầy đủ, không được thay bằng danh sách phân trang/lọc.
- HTTPtimeout10s, response tối đa1MiB, khôngredirect; JSONnull/IDkhôngdương/trùngID bị từ chối. Không migration. Nguồn: IAM Application/Accounts/ICategoryDirectory.cs; Infrastructure/Persistence/CategoryDirectory.cs, CategoryReconciliationWorker.cs, UserDirectory.cs; DependencyInjection.cs; API/appsettings.json; docker/docker-compose.yml. Lịch sử24/10/2026 thay hạn chế chưa validate/audit/dọn ID mồ côi; chưa deploy/chạy worker thực tế trong phiên này.

