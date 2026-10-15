# WardMate — Sổ tay ràng buộc hệ thống

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
  Submitted/Approved và file thực sự tồn tại. Một form điện tử có thể in nhiều bản;
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
| UNDER_REVIEW → APPROVED | Đúng cán bộ thụ lý; có snapshot; không còn nhận xét OPEN hiện tại |
| UNDER_REVIEW → REJECTED | Đúng cán bộ thụ lý; lý do từ chối bắt buộc |

- APPROVED ghi ApprovedAt UTC và lịch sử; không cho sửa/bắt lỗi tiếp qua luồng hiện tại.
- Reason từ chối/yêu cầu sửa chịu validator tương ứng, tối đa 4.000 ký tự.
- CANCELLED là giá trị trạng thái có trong database; không vì vậy kết luận có API
  hủy hoặc mọi chuyển đổi đến CANCELLED đã được triển khai.
- Không gửi trực tiếp chuỗi Status để tùy ý nhảy qua vòng đời.

### WF-04 — Phường, người thụ lý, comments và version

- Hàng đợi cán bộ chỉ trong phường công tác; chi tiết/thao tác thụ lý còn kiểm tra
  AssignedOfficerId. Cán bộ không tự thẩm định hồ sơ mình nộp.
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
`ReviewValidation.cs`, `ReviewHandlers.cs`, `FinalDecisionRequests.cs`;
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
- Hồ sơ đã APPROVED/REJECTED không tiếp tục sửa qua các API bảo vệ này.
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

### DOC-05 — Duyệt chung và kết xuất PDF

- Trạng thái hồ sơ chính thức nằm ở Workflow. API approve/request-revision cũ của
  DocumentForm chuyển yêu cầu sang Workflow, không duyệt riêng để vượt trạng thái hồ sơ.
- Khi xác minh hồ sơ APPROVED, DocumentForm đồng bộ các submission Submitted liên quan
  sang Approved. Đây là đồng bộ khi đọc/kiểm tra truy cập, không phải transaction xuyên service.
- Annotate comment trong Word không tự tạo comment OPEN của Workflow. Muốn yêu cầu
  bổ sung phải có nhận xét hợp lệ trong Workflow.
- Generate PDF phải dùng submission đã lưu, đúng application, chưa xóa, đã Approved;
  không nhận file tùy ý trong request để thay tài liệu đã duyệt.
- API còn xác minh Workflow APPROVED và quyền chủ hồ sơ/cán bộ được phân công.
- Gotenberg phải cấu hình URL HTTP/HTTPS hợp lệ và hoạt động. Kết quả phải bắt đầu `%PDF-`.
  Lỗi chuyển đổi báo lỗi; không trả PDF mẫu giả để coi như thành công.
- Xóa comment nội bộ trên bản sao để kết xuất; giữ DOCX nguồn phục vụ đối soát.
- Mỗi lần sinh PDF tạo bản ghi/file mới; không ghi đè PDF của form khác chỉ vì cùng hồ sơ.

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
- Scale nhiều replica cần cơ chế SignalR dùng chung phù hợp; group trong một process
  không tự trở thành group dùng chung toàn cluster.
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
- Workflow status chỉ DRAFT, SUBMITTED, UNDER_REVIEW, NEED_REVISION, APPROVED, CANCELLED,
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
  `workflow.revision.request`, `workflow.approve`, `workflow.reject`.
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
- [Schema và dữ liệu biểu mẫu](../src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Services/FormSchemaEngine.cs).
- [Thông báo của người nhận](../src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationService.cs).

## Nhật ký tài liệu

- 15/10/2026 — FLOW-REVIEW-001: thống nhất kiểm tra checklist/case/form trước xuất bản;
  validate resubmit trước gọi phụ thuộc; chặn phản hồi bằng chứng null/trùng;
  sửa quyền đọc tài liệu cho tài khoản có đồng thời role công dân và Front Desk.

- 14/10/2026 — DOC-CONSTRAINTS-001: tạo sổ tay theo source hiện hành; bao gồm IAM,
  Procedure/Drafts/AIOCR, Workflow/checklist, DocumentForm/PDF, Notifications,
  ràng buộc database, CSRF/CORS/token và các giới hạn liên service.
