# TASK-07 — Procedure Catalog entities và EF Core JSONB

## Phạm vi

Database riêng `wardmate_procedure_db`, user riêng `wardmate_procedure`, PostgreSQL 16 trên cổng localhost 5434. Compose service `procedure-db` có volume `procedure-data`, không mount script `docker/init-db` của database dùng chung. Không thay đổi schema IAM hoặc schema catalog cũ trong `wardmate_db`.

- `procedure_categories`: integer identity, tên tối đa 255 ký tự, mô tả text.
- `procedures`: UUID mặc định `gen_random_uuid()`, FK category bắt buộc; mã duy nhất tối đa 50 ký tự; tiêu đề tối đa 500; các trường và default tiếng Việt theo đặc tả.
- `procedure_versions`: UUID mặc định, FK procedure bắt buộc, ngày hiệu lực kiểu `date`, snapshot JSONB, unique `(procedure_id, version_number)`.
- FK dùng RESTRICT để tránh vô tình xóa danh mục có thủ tục hoặc thủ tục có lịch sử. Không có FK sang Document Service; `formTemplateId` chỉ là external ID.
- Ngày giờ dùng UTC `timestamp with time zone`. CreatedAt/UpdatedAt có default `now()` khi insert; lệnh cập nhật nghiệp vụ trong tương lai phải cập nhật UpdatedAt.

Migration: `20260930091232_Initial_Procedure_Catalog_Schema`. EF quản lý thêm bảng lịch sử migration ngoài ba bảng nghiệp vụ.

## JSONB

`content_payload`, `checklist_schema`, `form_definitions` dùng POCO strongly typed và ValueConverter System.Text.Json sang cột PostgreSQL `jsonb`. ValueComparer so sánh nội dung và clone sâu snapshot để phát hiện sửa các phần tử trong Cases/Steps, checklist, form mà không thay object gốc.

- `content_payload`: NOT NULL; hai danh sách checklist/form nullable. SQL NULL khác JSON array `[]`.
- `snapshot_data`: string JSONB NOT NULL. `ProcedureVersion.Capture` serialize tất cả scalar fields và ba JSON models của Procedure ngay lúc gọi; không serialize navigation/Versions để tránh vòng lặp. Snapshot không đổi khi sửa Procedure gốc.
- JSON dùng camelCase; DateOnly là `YYYY-MM-DD`; FeeAmount và EstimatedDays dùng decimal để giữ số tiền và phần ngày chính xác.
- GIN index trên content_payload và checklist_schema; B-tree unique trên procedure_code. Converter không tự dịch LINQ truy cập sâu POCO thành toán tử JSONB. Khi thêm tìm kiếm sâu, dùng SQL có tham số với toán tử JSONB, hoặc chuyển sang mapping ToJson phù hợp; không lọc toàn bộ dữ liệu phía client.
- Các trường chuỗi trong JSON giữ giá trị hợp đồng như NOP/XUAT_TRINH, ORIGINAL/CERTIFIED_COPY/REGULAR_COPY, ONLINE_INTERACTIVE/DOCX_TEMPLATE. Giới hạn độ dài ItemName và các quy tắc dữ liệu lồng nhau cần được kiểm tra trong command ghi ở task CRUD tiếp theo; jsonb không tạo cột varchar riêng cho các thuộc tính lồng nhau.

## Seed

Ba danh mục: Hộ tịch, Đất đai, Quản lý công sản. Có một Procedure đầy đủ các JSON models:

- ID: `07000000-0000-0000-0000-000000000001`
- Code: `DEMO-KET-HON`
- Title: Đăng ký kết hôn (dữ liệu minh họa)

Quyết định, căn cứ và quy trình trong seed là dữ liệu giả phục vụ ánh xạ; không dùng làm thông tin thủ tục pháp lý chính thức. FormTemplateId để null vì chưa có bản ghi thật bên Document Service. Migration seed chạy một lần, không ghi đè nội dung mỗi lần service khởi động.

## Query/API

`IProcedureRepository` thuộc Application, triển khai EF ở Infrastructure. `GetProcedureByIdQuery` qua MediatR trả `ProcedureDetailDto` gồm mọi scalar field, category name và ba JSON models đã deserialize. Query công khai chỉ trả thủ tục đang hoạt động.

| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/v1/procedures/{id}` | Không | 200 ProcedureDetailDto; 404 ProblemDetails nếu thiếu hoặc inactive |

Không cần token cho API đọc danh mục này. Lỗi 404 có `code: procedure.not_found`, title tiếng Việt và traceId. Thuộc tính null được bỏ khỏi response, mảng rỗng vẫn là `[]`. ID không đúng định dạng GUID không khớp route. Không có API ghi hoặc API quản lý phiên bản trong TASK-07.

Swagger trực tiếp: `http://localhost:5002/swagger` trong Development. Gateway đã có route mới chuyển nguyên `/api/v1/procedures/*` vào Procedure Catalog; cổng Gateway 5000. Prefix cũ `/api/procedure-catalog` vẫn dùng được.

## Chạy Local Dev bằng tay

1. Đặt mật khẩu độc lập tại `PROCEDURE_DB_PASSWORD` trong `docker/.env`. File này được Git/Docker bỏ qua. Trên máy mới sao chép từ `.env.example` và thay placeholder.
2. Khởi động riêng database, không build image ứng dụng:

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d procedure-db
```

3. Connection string không chứa password được commit trong `appsettings.Development.json`. Cấp password qua biến môi trường khi chạy service:

```powershell
$procedurePassword = Read-Host 'Mật khẩu PostgreSQL của Procedure Catalog' -MaskInput
$env:ConnectionStrings__ProcedureDatabase = "Host=localhost;Port=5434;Database=wardmate_procedure_db;Username=wardmate_procedure;Password=$procedurePassword"
$env:Jwt__Key = Read-Host 'Khóa JWT giống IAM (tối thiểu 32 byte UTF-8)' -MaskInput
dotnet run --project src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API --launch-profile http
```

`Read-Host -MaskInput` dành cho PowerShell 7. Password phải khớp docker/.env. Trong container dùng host `procedure-db`, port `5432`; không dùng localhost. AutoMigrate bật trong Development, mặc định tắt ngoài Development. Production cần cấp `ConnectionStrings__ProcedureDatabase` từ secret/config và chủ động áp dụng migration.

Thử GET `http://localhost:5002/api/v1/procedures/07000000-0000-0000-0000-000000000001` hoặc mở Swagger. Local Dev database và ứng dụng không tự được khởi động bởi phiên triển khai task này.

## Xác minh

```powershell
dotnet restore WardMate.sln
dotnet build WardMate.sln -c Release --no-restore
dotnet test WardMate.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure --configuration Release --no-build
```

11 integration cases chạy PostgreSQL 16 tạm: ba bảng/bốn cột JSONB/index/seed, complex JSON roundtrip qua repository/MediatR/HTTP, sửa JSON lồng nhau, NULL so với array rỗng, unique code và version pair, snapshot độc lập, SQL defaults/category identity, required JSON và 404 cho inactive/missing. Không tái tạo test IAM/SharedKernel đã gỡ.

TASK-08 bổ sung API ghi, kiểm tra dữ liệu lồng nhau, JWT/RBAC và migration lý do trạng thái. Xem [hướng dẫn Procedure Manager](procedure-manager.md) để biết request, versioning và cấu hình token bắt buộc mới. Số lượng test TASK-08 được ghi tại PROGRESS.md.

## TASK-08-LIST — Danh sách thủ tục (bổ sung)

`GET /api/v1/procedures` là API công khai, luôn lọc `isActive=true`, kể cả client cố gửi `isActive=false`.

Query string: `page` (mặc định 1, >=1), `pageSize` (mặc định 20, từ 1 đến 100), `search` (tối đa 255 ký tự, tìm chứa trong mã hoặc tên, không phân biệt hoa/thường, có phân biệt dấu), `categoryId` (số nguyên dương, tùy chọn). Search được trim; chuỗi trắng không lọc; %, _ và backslash được hiểu là ký tự thường. Thứ tự cố định `procedureCode ASC, id ASC`.

Ví dụ: `GET /api/v1/procedures?page=1&pageSize=20&search=đăng&categoryId=1`.

Response 200: `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 45, "totalPages": 3 }`. Item gồm id, categoryId, categoryName, procedureCode, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, isActive, createdAt, updatedAt. Không tải contentPayload/checklistSchema/formDefinitions/versions trong danh sách; dùng GET chi tiết để lấy nội dung JSONB.

Trang vượt số lượng hoặc bộ lọc không có kết quả trả 200 với items rỗng; danh mục không tồn tại cũng trả danh sách rỗng. totalPages=0 nếu không có kết quả. Query sai trả 400 ProblemDetails tiếng Việt (`validation.failed`, errors, traceId). Count và items được đọc bằng hai câu SQL; dữ liệu có thể thay đổi giữa hai lần đọc khi có request ghi đồng thời. Không có migration database mới.

## TASK-09 — Hợp đồng tìm kiếm nâng cao (thay thế TASK-08-LIST)

Hai API GET giữ nguyên URL và phạm vi truy cập. Public ép isActive=true; Manager mặc định lấy tất cả trạng thái và yêu cầu PROCEDURE_MANAGER hoặc IT_ADMIN.

- Query chuẩn: keyword?, categoryId?, levelOfImplementation?, pageNumber=1, pageSize=10 (1..100), sortBy=Title, isAscending=true. Manager thêm isActive?.
- Keyword tìm chứa theo mã/tên, không dấu và không phân biệt hoa/thường bằng PostgreSQL unaccent + ILIKE, không phải tìm kiếm ngữ nghĩa. %, _ và backslash là ký tự thường. LevelOfImplementation so khớp toàn bộ chuỗi sau bỏ dấu/không phân biệt hoa thường (ví dụ cap xa).
- sortBy cho phép Title, ProcedureCode, UpdatedAt, CreatedAt, LevelOfImplementation. Id làm khóa phụ để ổn định phân trang. Khi sort theo Title và không lọc cấp, ưu tiên Cấp Xã/Cấp Phường trước các cấp khác; không loại bỏ cấp khác.
- Alias cũ: search được dùng khi không có keyword; page ghi đè pageNumber nếu cả hai cùng được truyền. FE nên chuyển sang tên chuẩn.
- Response đổi thành PagedResult<ProcedureSummaryDto>: items, currentPage, totalPages, totalCount, pageSize, hasPrevious, hasNext. Không còn trường page. Item chỉ có id, procedureCode, title, categoryName, levelOfImplementation, feeSummary, processingTimeSummary, isActive, updatedAt.
- Migration ProcedureUnaccentSearch cài extension unaccent. Tài khoản chạy migration phải có quyền cài extension; môi trường managed PostgreSQL cần cho phép extension trước. Không thay dữ liệu IAM.
- 400 validation.failed cho tham số sai; trang không có dữ liệu trả 200 items rỗng. Metadata count/items vẫn là hai query riêng.

Tham khảo: https://www.postgresql.org/docs/16/unaccent.html

**TASK-09 điều chỉnh:** Hợp đồng chính thức hiện tại nằm tại [Tìm kiếm & xuất bản bản nháp đã đối soát](procedure-reviewed-publishing.md). Query Public/Manager đã tách riêng; public pageSize tối đa 50 và không trả isActive, manager tối đa 100 và có versionCount/createdAt. Hai DTO có originalPdfUrl. Thay phần import CSV bằng POST publish nhận JSON đã duyệt. API chi tiết/lịch sử bổ sung originalPdfUrl/pdfFileName. Các mô tả TASK-09 trước phần này là lịch sử và được thay thế theo tài liệu mới.
