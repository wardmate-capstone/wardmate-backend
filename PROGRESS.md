# WardMate — Nhật ký phát triển

**Trạng thái IAM Ingress CSRF (IAM-007): HOÀN THÀNH** — đã cấu hình ForwardedHeadersOptions xử lý X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host trong middleware pipeline IAM, hỗ trợ Swagger Azure và ingress proxy tin cậy. 27/27 HTTP checks passed, 131/131 solution tests passed. Xác minh ngày 02/10/2026 lúc 01:45 (Asia/Saigon).
**Trạng thái Procedure Catalog (TASK-09 điều chỉnh): HOÀN THÀNH** — đã triển khai xuất bản thủ tục đã đối soát kèm lịch sử phiên bản PDF, phân trang danh sách Public/Manager, tìm kiếm unaccent, hoàn thiện 11 integration tests, build Docker image và migrate database Local Dev. Xác minh ngày 01/10/2026 lúc 14:15 (Asia/Saigon).
**Trạng thái Fix AddVersion Concurrency: HOÀN THÀNH** — đã fix `DbUpdateConcurrencyException` trên endpoint `POST /api/v1/form-templates/{id}/versions`. Xác minh ngày 01/10/2026 lúc 11:30 (Asia/Saigon).
**Trạng thái Document & Form Service (TASK-10, TASK-11, TASK-12): HOÀN THÀNH** — đã triển khai thực thể Document, E-Forms dynamic schema engine, OpenXML DOCX placeholder extraction, Azure Blob upload, FormTemplates API và 22 unit tests. Xác minh ngày 29/09/2026 lúc 11:15 (Asia/Saigon).
**Trạng thái Core IAM: HOÀN THÀNH** — đã bàn giao lịch sử commit tách theo file lên `kha` và `deploy`; PostgreSQL Local Dev đã migrate và Swagger IAM đang chạy. Xác minh ngày 25/09/2026 lúc 10:13 (Asia/Saigon).
**Trạng thái Shared Kernel & Central Logging (TASK-05, TASK-06): HOÀN THÀNH** — đã triển khai đầy đủ Domain primitives, CQRS MediatR abstractions, Azure Blob Storage client wrapper, Serilog tập trung, RequestLoggingMiddleware và 39 unit tests cho SharedKernel. Xác minh ngày 26/09/2026 lúc 18:10 (Asia/Saigon).

---

## FIX-001 — Fix DbUpdateConcurrencyException trên AddVersion endpoint

- Hoàn thành: **01/10/2026, 11:30 (Asia/Saigon, UTC+07:00)**
- Nhánh: `kha-feat-fix-addversion-concurrency` → merge vào `kha` → `main` → `deploy`

### Endpoint bị ảnh hưởng
- `POST /api/v1/form-templates/{templateId}/versions` → `201 Created`
- Request body: `{ "schemaDefinition": "<JSON string>" }`
- Response: `FormTemplateVersionDto` (id, templateId, versionNumber, schemaDefinition, createdAtUtc, updatedAtUtc)

### Root cause
EF Core với `.HasDefaultValue(true/false)` tự động đặt các property boolean (`IsActive`, `IsDeleted`) là `ValueGeneratedOnAdd`. Khi EF Core thực thi batch gồm UPDATE trên `FormTemplate` và INSERT trên `FormTemplateVersion`, nó chờ đọc lại giá trị từ database nhưng nhận 0 rows affected → `DbUpdateConcurrencyException`.

### Các thay đổi
| File | Thay đổi |
|------|---------|
| [`CreateFormTemplateVersionCommand.cs`](src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Application/Commands/CreateFormTemplateVersionCommand.cs) | Thay `template.AddVersion()` bằng 2 bước Save riêng: update FormTemplate → insert FormTemplateVersion qua DbSet trực tiếp |
| `FormTemplateConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsActive` và `IsDeleted` |
| `FormTemplateVersionConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `SupportingDocumentConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `ApplicationFormConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| `GeneratedDocumentConfiguration.cs` | Thêm `.ValueGeneratedNever()` cho `IsDeleted` |
| Migrations/ | Thêm migration `AddSoftDeleteToDocumentForm` |

### Build & Test
- `dotnet build WardMate.sln -c Release`: **0 errors, 0 warnings**
- Manual test `POST .../versions` với schemaDefinition hợp lệ: **HTTP 201** ✅

---


## DOC-001 (TASK-10, TASK-11, TASK-12) — Thực thể Document, Engine E-Forms Schema & OpenXML Docx Placeholder

- Hoàn thành triển khai và kiểm thử: **29/09/2026, 11:15 (Asia/Saigon, UTC+07:00)**.
- Người phụ trách: **Nghĩa**.
- Nhánh tính năng: `nghia-feat-document-form`.
- Tổng số commit chi tiết: **35+ commit granular theo từng file/tính năng độc lập**.

### Chức năng hoàn thiện

1. **Thực thể Document & Form Service và Azure Storage (TASK-10)**:
   - Các thực thể Domain: `FormTemplate` (Aggregate Root), `FormTemplateVersion`, `ApplicationForm`, `SupportingDocument`, `GeneratedDocument` kế thừa `BaseEntity` chuẩn Clean Architecture.
   - Quản lý phiên bản schema `FormTemplateVersion` gắn với `FormTemplate`, hỗ trợ kích hoạt/vô hiệu hóa, cập nhật thông tin và URL phôi mẫu.
   - Upload file DOCX phôi mẫu lên Azure Blob Storage qua `IBlobStorageClient` (container `form-templates`) và lưu liên kết trong `form_templates`.
   - Cấu hình EF Core Fluent API đầy đủ ánh xạ tới schema `document` (`document.form_templates`, `document.form_template_versions`, `document.application_forms`, `document.supporting_documents`, `document.generated_documents`), cấu hình cột JSONB `schema_definition` và `form_data`.

2. **Engine Schema Định nghĩa Biểu mẫu động E-Forms (TASK-11)**:
   - Domain models: `FormSchemaDefinition`, `FormSection`, `FormField`, `FormFieldOption`, `FieldValidationRule`, `FormFieldType` (hỗ trợ 12 loại trường: Text, Number, Date, DateTime, Select, Radio, Checkbox, Textarea, NationalId/CCCD, PhoneNumber, Email, Currency).
   - `FormSchemaEngine`:
     - Phân tích và thẩm định cấu trúc Schema JSON: kiểm tra tính hợp lệ cú pháp, bắt buộc tiêu đề, section, chống trùng lặp `section_id` và `field_id`, kiểm tra quy tắc đặt tên trường `^[a-zA-Z0-9_]+$`, thẩm định trường select/radio phải có options không rỗng, kiểm tra biểu thức chính quy Regex tùy biến.
     - Kiểm tra dữ liệu biểu mẫu động nhập liệu (`ValidateFormData`): thẩm định bắt buộc (required), kiểm tra định dạng email chuẩn RFC, kiểm tra số điện thoại Việt Nam (đầu 0 hoặc +84), kiểm tra số định danh cá nhân / CCCD (9 hoặc 12 chữ số), kiểm tra giới hạn min/max độ dài chuỗi và giá trị số, kiểm tra giá trị chọn thuộc options cho phép, báo cáo danh sách lỗi chi tiết theo trường (`field_id`, `error_code`, `error_message`).

3. **Engine Bóc tách Placeholder từ File Word DOCX (TASK-12)**:
   - `DocxPlaceholderEngine` (sử dụng OpenXML SDK):
     - Bóc tách toàn bộ biến giữ chỗ dạng `{{ten_bien}}` từ Word document body, header và footer, xử lý triệt để hiện tượng phân mảnh văn bản XML (fragmented Run/Text elements) do kiểm tra chính tả/định dạng trong Word.
     - Chuẩn hóa tên placeholder (chữ thường, loại bỏ khoảng trắng), đếm số lần xuất hiện và xác định vị trí trong tài liệu.
     - So khớp danh sách placeholders bóc tách được với `FormSchemaDefinition` (`MatchPlaceholdersWithSchema`): tự động phát hiện các trường khớp hoàn toàn, các trường có trong DOCX nhưng thiếu trong Schema, và các trường có trong Schema nhưng không được sử dụng trong DOCX.
     - Tự động sinh cấu trúc Schema biểu mẫu dự thảo (`GenerateDraftSchema`): suy luận kiểu trường thông minh dựa trên tên biến (ví dụ `ngay_sinh` -> `Date`, `email` -> `Email`, `dien_thoai` -> `PhoneNumber`, `cccd`/`so_dinh_danh` -> `NationalId`, `so_tien`/`le_phi` -> `Currency`, `so_luong` -> `Number`) và tạo nhãn hiển thị trực quan.

4. **API Endpoints (Controllers & CQRS)**:
   - `GET /api/v1/form-templates`: Lấy danh sách biểu mẫu có phân trang (`page`, `pageSize`, `isActive`, `searchCode`).
   - `GET /api/v1/form-templates/{templateId}`: Xem chi tiết biểu mẫu kèm toàn bộ lịch sử các phiên bản schema.
   - `POST /api/v1/form-templates`: Tạo mới biểu mẫu (tùy chọn kèm schema phiên bản đầu tiên).
   - `POST /api/v1/form-templates/{templateId}/versions`: Tạo phiên bản schema mới cho biểu mẫu.
   - `POST /api/v1/form-templates/{templateId}/upload-docx`: Upload file phôi mẫu DOCX lên Azure Blob Storage, tự động bóc tách placeholder và so khớp với schema hiện tại.
   - `POST /api/v1/form-templates/extract-placeholders`: Bóc tách danh sách placeholder từ file DOCX tải lên mà không cần lưu.

5. **Tích hợp Infrastructure, DI & Swagger**:
   - Tích hợp `AddDocumentFormApplication()` và `AddDocumentFormInfrastructure()` vào `Program.cs`.
   - Tích hợp Serilog tập trung, RequestLoggingMiddleware, ExceptionHandling, OpenAPI SwaggerGen.
   - `appsettings.json` cấu hình kết nối PostgreSQL `wardmate_db` (schema `document`) và Azure Blob Storage.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Domain Models & Entities (TASK-10, 11) | `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/Entities/` (`FormTemplate.cs`, `FormTemplateVersion.cs`, `ApplicationForm.cs`, `SupportingDocument.cs`, `GeneratedDocument.cs`), `Models/` (`FormFieldType.cs`, `FormFieldOption.cs`, `FieldValidationRule.cs`, `FormField.cs`, `FormSection.cs`, `FormSchemaDefinition.cs`), `Errors/DocumentFormErrors.cs` |
| Application Layer (TASK-10, 11, 12) | `WardMate.Services.DocumentForm.Application/Commands/` (`CreateFormTemplateCommand.cs`, `CreateFormTemplateVersionCommand.cs`, `UploadFormTemplateDocxCommand.cs`), `Queries/` (`GetFormTemplatesQuery.cs`, `GetFormTemplateByIdQuery.cs`, `ExtractDocxPlaceholdersQuery.cs`), `DTOs/` (`FormTemplateDto.cs`, `FormTemplateDetailDto.cs`, `FormTemplateVersionDto.cs`), `Models/` (`DocxPlaceholder.cs`, `PlaceholderValidationResult.cs`, `FormDataValidationError.cs`), `Interfaces/` (`IDocumentDbContext.cs`, `IFormSchemaEngine.cs`, `IDocxPlaceholderEngine.cs`), `Services/FormSchemaEngine.cs`, `DependencyInjection.cs`, `.csproj` |
| Infrastructure Layer (TASK-10, 12) | `WardMate.Services.DocumentForm.Infrastructure/Persistence/` (`DocumentDbContext.cs`, `DocumentDbContextFactory.cs`, `Configurations/` cho 5 entities), `OpenXml/DocxPlaceholderEngine.cs`, `DependencyInjection.cs`, `.csproj` |
| API Layer | `WardMate.Services.DocumentForm.API/Controllers/FormTemplatesController.cs`, `Program.cs`, `appsettings.json`, `.csproj` |
| Unit Tests Mới | `tests/WardMate.Services.DocumentForm.Tests/` — `FormTemplateEntityTests.cs`, `FormSchemaEngineTests.cs`, `DocxPlaceholderEngineTests.cs`, `WardMate.Services.DocumentForm.Tests.csproj` |
| Solution | `WardMate.sln`, `PROGRESS.md` |

### Kết quả kiểm thử

- `dotnet build -c Release`: **0 errors, 0 warnings** trên toàn bộ 16 projects trong solution.
- **22 unit tests mới cho DocumentForm Service — 100% PASS**: kiểm thử đầy đủ Entity lifecycle & soft delete, schema validation, form data validation, regex constraints, OpenXML placeholder extraction, schema matching, và draft schema generation.
- **39 unit tests SharedKernel — 100% PASS**.
- **78 unit tests IAM — 100% PASS**.
- Tổng số unit tests pass: **139 passed, 0 failed, 0 skipped**.


## SHARED-001 (TASK-05 & TASK-06) — Xây dựng Shared Kernel, Azure Blob Client & Logging tập trung

- Hoàn thành triển khai và kiểm thử: **26/09/2026, 18:10 (Asia/Saigon, UTC+07:00)**.
- Người phụ trách: **Nghĩa**.
- Nhánh tính năng: `nghia-feat-shared-kernel-logging`.
- Tổng số commit chi tiết: **35+ commit theo từng file/chức năng độc lập**.

### Chức năng hoàn thiện

1. **Domain Primitives (TASK-05)**:
   - `IDomainEvent`: Marker interface cho domain events với `EventId` và `OccurredOnUtc`.
   - `BaseEntity`: Base class cho entity với UUID `Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted`, `DeletedAtUtc`, quản lý pending domain events (`RaiseDomainEvent`, `ClearDomainEvents`) và helper methods `SetCreated`, `SetUpdated`, `SoftDelete`.
   - `AggregateRoot`: Kế thừa `BaseEntity`, hỗ trợ optimistic concurrency qua thuộc tính `uint Version`.
   - `ValueObject`: Base class cho immutable value objects với structural equality (`GetEqualityComponents`, `Equals`, `GetHashCode`, `==`, `!=`).

2. **Common Railway-Oriented Programming & Pagination (TASK-05)**:
   - `Result` và `Result<TValue>`: Discriminated-union result type giúp tránh throw exception cho predictable business failures, hỗ trợ implicit conversion từ `Error` hoặc `TValue`.
   - `Error` & `ErrorType`: Structured error record gồm `Code`, `Description`, `ErrorType` (`Failure`, `NotFound`, `Validation`, `Conflict`, `Unauthorized`) và các static factory helper methods.
   - `PagedResult<T>`: Hỗ trợ pagination metadata (`Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`, `HasPreviousPage`) và factory `Create`, `Empty`.

3. **CQRS & MediatR Markers (TASK-05)**:
   - `ICommand` (trả về `Result`) & `ICommand<TResponse>` (trả về `Result<TResponse>`).
   - `ICommandHandler<TCommand>` & `ICommandHandler<TCommand, TResponse>`.
   - `IQuery<TResponse>` & `IQueryHandler<TQuery, TResponse>`.

4. **Azure Blob Storage Wrapper (TASK-05)**:
   - `BlobStorageOptions`: Cấu hình Azure Blob (`ConnectionString`, `ContainerName`, `MaxFileSizeBytes`).
   - `IBlobStorageClient`: Interface cho blob operations (`UploadAsync`, `DownloadAsync`, `DeleteAsync`, `ExistsAsync`, `GenerateSasUri`).
   - `AzureBlobStorageClient`: Triển khai với Azure.Storage.Blobs SDK, tự động khởi tạo container nếu chưa có, hỗ trợ content-type detection và SAS token generation.
   - `BlobServiceExtensions.AddAzureBlobStorage`: Extension method đăng ký `BlobServiceClient` (Singleton) và `IBlobStorageClient` (Scoped) vào DI container.

5. **Logging tập trung & Request Tracking Middleware (TASK-06)**:
   - `SerilogOptions`: Cấu hình Serilog từ appsettings (`MinimumLevel`, `EnableConsoleSink`, `EnableFileSink`, `LogFilePath`, `FileSizeLimitMb`, `RetainedFileCountLimit`, `ApplicationName`).
   - `SerilogExtensions`:
     - `AddWardMateLogging`: Bootstrap Serilog với Console sink (Compact JSON trong Production, readable console trong Development), Rolling File sink với 50MB limit và 7 ngày retention, tự động enrich `Application`, `Environment`, `ThreadId`, `ProcessId`.
     - `UseWardMateRequestLogging`: Tích hợp HTTP request logging của Serilog.
   - `RequestLoggingMiddleware`: Custom middleware ghi log chi tiết HTTP request/response (`Method`, `Path`, `QueryString`, `StatusCode`, `ElapsedMs`, `ClientIp`, `TraceId`), tự động phân loại LogLevel theo HTTP status (2xx/3xx -> Info, 4xx -> Warning, 5xx -> Error) và bỏ qua các đường dẫn nội bộ (`/health`, `/favicon.ico`).
   - Tích hợp Bootstrap Serilog và RequestLoggingMiddleware vào IAM Service `Program.cs` và cấu hình mẫu trong `appsettings.json`.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Domain Primitives | `src/BuildingBlocks/WardMate.SharedKernel/Domain/IDomainEvent.cs`, `BaseEntity.cs`, `AggregateRoot.cs`, `ValueObject.cs` |
| Common & CQRS | `src/BuildingBlocks/WardMate.SharedKernel/Common/Result.cs`, `Error.cs`, `PagedResult.cs`, `CQRS/ICommand.cs`, `ICommandHandler.cs`, `IQuery.cs`, `IQueryHandler.cs` |
| Azure Blob Storage | `src/BuildingBlocks/WardMate.SharedKernel/Blob/BlobStorageOptions.cs`, `IBlobStorageClient.cs`, `AzureBlobStorageClient.cs`, `BlobServiceExtensions.cs` |
| Central Logging | `src/BuildingBlocks/WardMate.SharedKernel/Logging/SerilogOptions.cs`, `SerilogExtensions.cs`, `RequestLoggingMiddleware.cs` |
| Project Configuration | `src/BuildingBlocks/WardMate.SharedKernel/WardMate.SharedKernel.csproj`, `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`, `appsettings.json`, `global.json` |
| Unit Tests mới | `tests/WardMate.SharedKernel.Tests/` — `ResultTests.cs`, `ErrorTests.cs`, `PagedResultTests.cs`, `BaseEntityTests.cs`, `ValueObjectTests.cs`, `BlobStorageOptionsTests.cs`, `SerilogOptionsTests.cs`, `RequestLoggingMiddlewareTests.cs`, `AggregateRootTests.cs`, `CqrsMarkerTests.cs`, `BlobServiceExtensionsTests.cs` |

### Kết quả kiểm thử

- `dotnet build -c Release`: **0 errors, 0 warnings** trên toàn bộ 15 project trong solution.
- **39 unit tests mới cho SharedKernel — 100% PASS**: kiểm thử đầy đủ Result pattern, Error, PagedResult calculation, BaseEntity domain events & soft delete, ValueObject equality, Serilog options, Blob options, MediatR pipeline integration, DI registration, và TestServer middleware execution.
- **32 unit tests IAM hiện có — 100% PASS**.
- Tổng số unit test pass: **71 passed, 0 failed, 0 skipped**.


## IAM-001 — Core IAM Service & API xác thực

- Hoàn thành triển khai và kiểm thử: **25/09/2026, 09:57 (Asia/Saigon, UTC+07:00)**.
- Nhánh tính năng: `kha-feat-iam-auth`; trình tự bàn giao: feature → `kha` → `deploy`.
- Khởi tạo `kha` từ `origin/main` vì remote chưa có nhánh `kha`; nhánh `deploy` cũng chưa tồn tại khi bắt đầu task.

### Chức năng hoàn thiện

- 7 entity và Fluent API: User, UserProfile, Role, Permission, UserRole, RolePermission, RefreshToken; khóa ngoại chỉ trong `wardmate_iam_db`.
- Migration `InitialIam`, tự migrate khi API khởi động; seed 5 role yêu cầu, 2 permission mẫu và 6 role-permission. Không seed mật khẩu/tài khoản quản trị mặc định.
- BCrypt cost 12; JWT HS256 chứa `sub`, `email`, `role`, `permissions`; refresh token sinh từ 64 byte ngẫu nhiên, chỉ lưu SHA-256 digest trong database.
- MediatR CQRS, FluentValidation, Result Pattern cho register/login/refresh/revoke/me. Controller chỉ nhận request, lấy subject từ JWT và gọi Application.
- Chặn tài khoản inactive, trùng username/email, sai chủ sở hữu token, token hết hạn/đã thu hồi. Refresh rotation và insert token mới nằm trong một giao dịch; concurrent refresh thất bại không để lại token thay thế.
- Swagger OpenAPI, bearer security cho endpoint authorized, ProblemDetails và `traceId`; thêm route IAM versioned qua YARP.
- Lưu workflow dài hạn trong `AGENTS.md`; CI build/test bằng GitHub Actions cho feature, `kha`, `deploy`.

### File/thư mục tạo mới hoặc thay đổi

| Nhóm | Đường dẫn |
|---|---|
| Workflow/tài liệu | `AGENTS.md`, `PROGRESS.md`, `README.md`, `docs/iam-auth.md`, `.github/workflows/iam-ci.yml` |
| Solution/tooling | `WardMate.sln`, `.config/dotnet-tools.json` |
| PostgreSQL local | `docker/docker-compose.yml`, `docker/.env.example` |
| Gateway | `src/Gateways/WardMate.YarpGateway/appsettings.json` |
| Domain mới | `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/Entities/` (7 entity), `RoleNames.cs` |
| Application mới | `WardMate.Services.IAM.Application/Commands/`, `Queries/`, `DTOs/`, `Interfaces/`, `Common/`, `Validation/`, `DependencyInjection.cs`; cập nhật `.csproj` |
| Infrastructure mới | `WardMate.Services.IAM.Infrastructure/Persistence/` (context, factory, store, configurations, migration/snapshot), `Security/` (BCrypt/JWT/options), `DependencyInjection.cs`; cập nhật `.csproj` |
| API | `WardMate.Services.IAM.API/Controllers/`, `Errors/`, `OpenApi/`; cập nhật `Program.cs`, `appsettings.json`, `.csproj` |
| Unit tests mới | `tests/WardMate.Services.IAM.Tests/` — `AuthFlowTests.cs`, `SecurityTests.cs`, `TestIdentityStore.cs`, `.csproj` |
| Integration tests mới | `tests/WardMate.Services.IAM.IntegrationTests/` — `IamApiTests.cs`, `IamFixture.cs`, `.csproj` |

Các đường dẫn `WardMate.Services.IAM.*` trong bảng nằm dưới `src/Services/WardMate.Services.IAM/`. Không triển khai nghiệp vụ cho năm service khác.

### API mới

Base URL local: gateway `http://localhost:5000`, IAM trực tiếp `http://localhost:5001`.

| Method | Route | Request body | Response codes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | `{"username":"citizen","email":"citizen@example.test","password":"StrongPassword123!","fullName":"Nguyen Van A"}` | 201 CurrentUserDto; 400 validation; 409 duplicate |
| POST | `/api/v1/auth/login` | `{"usernameOrEmail":"citizen","password":"StrongPassword123!"}` | 200 AuthResponseDto; 400 validation; 401 invalid/inactive |
| POST | `/api/v1/auth/refresh-token` | `{"accessToken":"...","refreshToken":"..."}` | 200 AuthResponseDto; 400 validation; 401 invalid/expired/revoked |
| POST | `/api/v1/auth/revoke-token` | `{"refreshToken":"..."}` + Bearer token | 204; 400 validation; 401 invalid/not owned/unauthenticated |
| GET | `/api/v1/users/me` | Không có body; Bearer token | 200 CurrentUserDto; 401 unavailable/unauthenticated |

Lỗi bất ngờ trả 500 ProblemDetails đã loại bỏ thông tin nội bộ.

### Kết quả kiểm thử

- `dotnet build WardMate.sln -c Release --no-restore`: **0 errors, 0 warnings**.
- **32 unit test cases mới — PASS**: hash/salt/verify, giới hạn UTF-8 BCrypt, JWT claims/signature/audience, đăng ký/validation/trùng dữ liệu, đăng nhập, inactive account, refresh/rotation/reuse/ownership/concurrency outcome, logout và me.
- **8 integration tests mới — PASS** trên PostgreSQL 16 dùng Testcontainers: tự migrate/seed và chạy lại idempotent; lifecycle/ownership; validation/duplicate; giao dịch refresh cạnh tranh và rollback; expired access/refresh; đăng ký đồng thời; inactive account; Swagger endpoints/bearer metadata.
- **3 test cases SharedKernel hiện có — PASS**.
- Toàn bộ solution: **43 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations has-pending-model-changes`: không có thay đổi model chưa được migration hóa.
- Lần thử integration ban đầu chưa chạy được vì Docker tắt; đã khởi động Docker và chạy lại đầy đủ thành công. Không bỏ qua test để merge.
- Container/database kiểm thử tạm đã được Testcontainers dọn sau khi chạy; không dùng database dev của người dùng.

### Hướng dẫn Frontend

- Contract chi tiết, ví dụ DTO và cấu hình: [docs/iam-auth.md](docs/iam-auth.md).
- JSON camelCase. Register trả CurrentUserDto và chưa đăng nhập; gọi login để nhận AuthResponseDto.
- AuthResponseDto gồm `accessToken`, `accessTokenExpiresAt`, `refreshToken`, `refreshTokenExpiresAt`, `tokenType`. Hạn mặc định: access 15 phút, refresh 7 ngày; thời gian UTC.
- Gửi `Authorization: Bearer <accessToken>` cho me/revoke. Sau refresh phải thay cả hai token, chỉ cho phép một refresh đang xử lý trong một phiên. Access token đã hết hạn chỉ được sử dụng ở request refresh cùng refresh token còn hợp lệ.
- Logout thu hồi refresh token được gửi, không blacklist ngay access token; access token cũ hết hiệu lực tự nhiên theo thời hạn. Refresh 401 → xóa phiên, yêu cầu đăng nhập lại.
- ProblemDetails có `code`, `traceId`; validation có `errors` theo field. Mã: `validation_failed`, `iam.duplicate_account`, `iam.invalid_credentials`, `iam.invalid_token`, `iam.user_unavailable`, `iam.unauthorized`. Không dựa vào chuỗi tiếng Anh để điều khiển UI.
- Chỉ cấp `REGISTERED_CITIZEN` khi đăng ký. `roles`/`permissions` trong DTO và JWT là mảng; `/me` đọc quyền hiện tại từ database.
- Frontend dùng same-origin proxy hoặc cấu hình CORS theo môi trường triển khai. Task này chưa thêm CORS/cookie authentication.
- Push `deploy` là bước bàn giao Git được yêu cầu. Workflow mới xác minh backend build/tests; không đồng nghĩa đã xác minh một pipeline deploy frontend bên ngoài repository.

## GIT-001 — Tách commit IAM theo từng file và lưu quy ước mới

- Hoàn thành tách lịch sử local: **25/09/2026, 10:03 (Asia/Saigon, UTC+07:00)**.
- Đã xác nhận commit tổng `25b1e72` được push lên `origin/kha` trước khi nhận yêu cầu tách commit. Chưa merge/push sang `deploy`.
- Trên `kha-feat-iam-auth`, thay commit tổng bằng **59 commit**, mỗi commit thay đổi đúng một file. Đối chiếu Git tree sau khi tách: giống hoàn toàn Git tree của `25b1e72`.
- Thay đổi bổ sung: `AGENTS.md` lưu quy ước commit mới; `PROGRESS.md` ghi nhật ký lần tách này. Mỗi cập nhật tài liệu được commit riêng.
- Quy ước từ nay: mỗi file tạo mới một commit; mỗi cập nhật chức năng một commit riêng; không squash các commit khi merge.
- API mới: không có. DTO, mã lỗi và cách xử lý token không thay đổi so với IAM-001.
- Test mới: 0. Giữ nguyên kết quả của đúng nội dung mã nguồn đã kiểm chứng: **43 passed, 0 failed**, build **0 errors, 0 warnings**. Chỉ tách lịch sử và sửa tài liệu nên không chạy lại suite.
- Bàn giao: nhánh tính năng chứa lịch sử đã tách; việc thay lịch sử đã công bố trên `kha` và tiếp tục sang `deploy` đang chờ quyết định của người dùng. Chưa force push lên nhánh chung.

## IAM-001 / LOCAL-001 — Hoàn tất bàn giao và khởi động Local Dev

- Hoàn thành xác minh: **25/09/2026, 10:13 (Asia/Saigon, UTC+07:00)**.
- Người dùng đã chấp thuận rõ ràng việc dùng `--force-with-lease` cho lần thay lịch sử `kha` này. Đã cập nhật `origin/kha` từ `25b1e72` sang lịch sử đã tách tại `43c57ac`, với lease kiểm tra đúng hash cũ. Đã merge fast-forward `kha` vào `deploy` và push `origin/deploy` thành công. Phần chờ quyết định tại GIT-001 đã được giải quyết.
- Cập nhật file được commit riêng: `PROGRESS.md`. Không thay đổi source code, DTO hoặc endpoint. File `docker/.env` được tạo riêng cho máy local với mật khẩu database và JWT key ngẫu nhiên; file này bị gitignore và không được commit.
- `docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --wait iam-db` đã chạy thành công. Container `wardmate-iam-db-1` healthy, PostgreSQL 16 tại `127.0.0.1:5433`, database `wardmate_iam_db`, dữ liệu lưu trong volume `wardmate_iam-data`.
- Đã khởi động IAM API ở cổng **5001** và YARP Gateway ở cổng **5000** trong môi trường Development, giữ các tiến trình chạy nền để người dùng test.
- Đã kiểm tra trực tiếp database: migration **`20260925024711_InitialIam`** có trong `__EFMigrationsHistory`; đủ **7 bảng nghiệp vụ**, **5 roles**, **2 permissions**, **6 role-permission assignments**.
- **Swagger UI trực tiếp IAM Service:** <http://localhost:5001/swagger/index.html> — đã kiểm tra HTTP 200. OpenAPI JSON: <http://localhost:5001/swagger/v1/swagger.json>. Gateway hiện không có route Swagger UI riêng.
- **API qua Gateway:** `http://localhost:5000/api/v1/auth/register`, `http://localhost:5000/api/v1/auth/login` và các endpoint IAM còn lại giữ nguyên đường dẫn. Swagger tại cổng 5001 gọi trực tiếp IAM Service.
- Smoke checks local đã pass: `/api/iam/health` qua gateway trả Healthy; `/api/v1/users/me` không token trả 401; register dữ liệu sai trả 400; login tài khoản không tồn tại trả 401. Không tạo tài khoản mẫu trong database dev.
- Tests mới: 0; giữ kết quả của source code không đổi: **43 tests passed**, build **0 errors, 0 warnings**. Các kiểm tra local bổ sung đã xác nhận Docker, migration và HTTP runtime.
- Log của các host local được ghi trong thư mục `%TEMP%/wardmate-local-dev`. Secrets trong `.env` chỉ được nạp vào môi trường của các tiến trình local; không ghi vào source hoặc nhật ký.
- Frontend có thể test đăng ký/đăng nhập ngay bằng Swagger cổng 5001 hoặc gọi API qua gateway cổng 5000. Đăng ký thành công trả 201; tiếp tục login để lấy access/refresh token; nhấn Authorize trên Swagger với access token để thử `/users/me`.
- Trạng thái bàn giao được xác nhận ở cấp Git và Local Dev. Không suy diễn việc push `deploy` thành công thành xác nhận một hệ thống triển khai frontend bên ngoài đã chạy thành công.

## DEPLOY-001 — Chuẩn bị triển khai Azure Portal, cập nhật chuỗi merge và đóng gói Docker

- Hoàn thành xác minh: **25/09/2026, 15:50 (Asia/Saigon, UTC+07:00)**.
- Đã xóa nhánh remote mồ côi `develop` trên GitHub; giữ lại nhánh tính năng `kha-feat-iam-auth` theo yêu cầu người dùng để phục vụ kiểm thử thủ công.
- Cập nhật quy tắc workflow trong `AGENTS.md`: trình tự merge bắt buộc qua `main` trước khi sang nhánh `deploy` (`feature` → `kha` → `main` → `deploy`), phục vụ Azure Continuous Deployment cho Frontend kết nối API trực tiếp.
- Bổ sung Dockerfile đa tầng (.NET 8) cho `WardMate.YarpGateway` và `WardMate.Services.IAM.API`, bổ sung `.dockerignore` ở root. Đã xác minh `docker build` thành công cả 2 image với 0 lỗi, 0 cảnh báo.
- Bảo mật cấu hình pgAdmin trong `docker/docker-compose.yml` và `docker/.env.example` qua biến môi trường, không để lộ plain credentials.
- Biên soạn tài liệu chi tiết [docs/azure-deployment.md](docs/azure-deployment.md) hướng dẫn toàn bộ quy trình thiết lập Azure Resource Group, ACR, Azure PostgreSQL Flexible Server, Azure Container Apps (Internal IAM + External Gateway) và tích hợp GitHub Actions Continuous Deployment từ nhánh `deploy`.


## IAM-002 — Omit null response fields and enforce registration password strength

- Completed: **2026-09-25 17:13 (Asia/Saigon, UTC+07:00)**.
- Controllers use `JsonIgnoreCondition.WhenWritingNull`. Optional profile fields are omitted when null; populated fields and empty strings remain present.
- Registration requires at least 8 characters, one uppercase letter and one special character (Unicode punctuation or symbol; whitespace does not count). Retained the BCrypt limit of 72 UTF-8 bytes. Login validation remains compatible with existing passwords.
- Changed files:
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs`
  - `tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs`
  - `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
  - `docs/iam-auth.md`
  - `PROGRESS.md`
- No new endpoints. Affected contracts:
  - `POST /api/v1/auth/register`: body `{ "username": "citizen", "email": "citizen@example.test", "password": "Abcdefg!", "fullName": "Nguyen Van A" }`; 201 CurrentUserDto with null fields omitted; 400 validation ProblemDetails; 409 duplicate account.
  - `GET /api/v1/users/me`: no request body, Bearer token required; 200 CurrentUserDto with null fields omitted; 401 unauthorized.
- Validation: solution Release build succeeded with **0 errors, 0 warnings**. Full test suite **57/57 passed, 0 failed, 0 skipped**: 41 IAM unit tests, 3 SharedKernel tests, 13 PostgreSQL integration tests. Added 9 unit cases and 5 integration cases; existing registration helper also checks raw JSON omission. Initial integration run exposed a double-read of the response stream in the new test helper; fixed before the successful complete rerun.
- Frontend: optional DTO properties may be absent; apply the same registration password rules client-side. Invalid passwords return HTTP 400, `code: validation_failed`, and `errors.password`. Token handling and endpoint routes are unchanged.
- Handoff: implementation remains uncommitted in the existing `deploy` working tree. No checkout, merge, commit or push performed; Antigravity handles Git publishing per the user's explicit instruction. Deployment/runtime restart has not been performed by this task.

## IAM-003 — Việt hóa thông báo validation và lỗi API

- Hoàn thành: **2026-09-25 17:26 (Asia/Saigon, UTC+07:00)**.
- Việt hóa toàn bộ quy tắc FluentValidation hiện có: đăng ký, đăng nhập, làm mới và thu hồi token. Giữ nguyên điều kiện kiểm tra dữ liệu.
- Việt hóa tiêu đề validation, lỗi nghiệp vụ IAM, lỗi body thiếu/sai định dạng, thông báo HTTP mặc định của IAM và lỗi hệ thống trong SharedKernel. Log kỹ thuật nội bộ không thuộc thông báo giao diện người dùng.
- File thay đổi:
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Common/Result.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
  - `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Errors/ValidationExceptionHandler.cs`
  - `src/BuildingBlocks/WardMate.SharedKernel/Web/GlobalExceptionHandler.cs`
  - `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
  - `docs/iam-auth.md`
  - `PROGRESS.md`
- Không thêm endpoint. Các route và body giữ nguyên: POST `/api/v1/auth/register` (username, email, password, fullName; 201/400/409), POST `/api/v1/auth/login` (usernameOrEmail, password; 200/400/401), POST `/api/v1/auth/refresh-token` (accessToken, refreshToken; 200/400/401), POST `/api/v1/auth/revoke-token` (refreshToken; 204/400/401), GET `/api/v1/users/me` (không body; 200/401).
- Kiểm thử: Release solution build **0 errors, 0 warnings**; **61/61 tests PASS**, 0 failed, 0 skipped (41 IAM unit, 3 SharedKernel, 17 integration PostgreSQL). Thêm 4 integration cases kiểm tra body lỗi và thông báo xác thực tiếng Việt; bổ sung assertion tiếng Việt vào test validation hiện có. Không thêm unit case mới.
- Frontend: hiển thị `title` và `errors` tiếng Việt; vẫn phân nhánh theo HTTP status và `code`, không so sánh nội dung thông báo. Tên trường DTO, token handling và cơ chế bỏ trường null giữ nguyên.
- Bàn giao: các thay đổi đã được commit riêng từng file theo quy ước, kiểm thử toàn bộ giải pháp 61/61 tests PASS, merge theo chuỗi kha-feat-iam-vietnamese-messages → kha → main → deploy và push lên origin.

## IAM-004 — JWT, refresh token, CRUD hồ sơ và RBAC/quản lý tài khoản

- Hoàn thành: **2026-09-26 08:56 (Asia/Saigon, UTC+07:00)**.
- REGISTERED_CITIZEN CRUD hồ sơ của mình; IT_ADMIN CRUD mọi hồ sơ, xem danh sách/chi tiết tài khoản và khóa/mở khóa tài khoản. Xóa hồ sơ không xóa tài khoản. PUT thay toàn bộ hồ sơ; POST tạo lại sau khi xóa.
- Authorization middleware dùng policy và quyền hiện tại trong database. Từ chối JWT của tài khoản bị khóa; thu hồi refresh token trong cùng transaction khóa tài khoản. Cấp token được tuần tự hóa bằng khóa hàng user để tránh race với thao tác khóa. Không thêm API công khai sửa/đọc trực tiếp refresh_tokens.
- Migration mới `20260926014941_ProfileWritePermission` thêm quyền ghi hồ sơ và seed cho 5 vai trò, tổng cộng 3 permissions/11 role-permission assignments. Đã chạy migration thật trên PostgreSQL tạm khi integration tests khởi động; chưa áp dụng vào Local Dev database hoặc Azure trong task này.
- Endpoint mới (đều yêu cầu Bearer):
  - GET/POST/PUT/DELETE `/api/v1/users/me/profile`: quyền đọc/ghi hồ sơ cá nhân; GET 200/401/403/404; POST 201/400/401/403/404/409; PUT 200/400/401/403/404/409; DELETE 204/401/403/404/409.
  - GET/POST/PUT/DELETE `/api/v1/users/{userId}/profile`: `iam.manage`; body/status tương tự route cá nhân.
  - POST/PUT hồ sơ nhận `{ fullName, identityNumber?, phoneNumber?, dateOfBirth?, gender?, permanentAddress?, temporaryAddress? }`; GET/DELETE không body.
  - GET `/api/v1/accounts?page=1&pageSize=20`: `iam.manage`, không body; 200/400/401/403; phân trang tối đa 100 items.
  - GET `/api/v1/accounts/{userId}`: `iam.manage`, không body; 200/401/403/404.
  - PUT `/api/v1/accounts/{userId}/status`: `iam.manage`, body `{ "isActive": false }` khóa, true mở; 204/400/401/403/404/409. Không cho tự khóa tài khoản quản trị đang dùng.
- Kiểm thử cuối: Release solution build **0 errors, 0 warnings**; **81/81 tests PASS**, 0 failed/skipped: 54 IAM unit, 3 SharedKernel, 24 integration PostgreSQL. Thêm 13 unit cases và 7 integration cases (CRUD, IDOR, quản trị, quyền thay đổi, validation/trùng số định danh, race khóa/cấp token, Swagger); cập nhật test migration hiện có. Build trung gian phát hiện nullable assertion/overload test và đã sửa trước lượt kiểm tra cuối.
- Frontend: thông báo tiếng Việt; giữ error codes cũ, bổ sung `iam.forbidden`, `iam.profile_not_found`, `iam.user_not_found`, `iam.profile_exists`, `iam.profile_conflict`, `iam.profile_changed`, `iam.self_disable`. Sau xóa hồ sơ, `/users/me` không có thuộc tính profile; 401 yêu cầu đăng nhập lại, 403 thiếu quyền. Refresh token bị thu hồi không phục hồi khi mở khóa; JWT còn hạn có thể hoạt động trở lại khi tài khoản được mở khóa. Xem `docs/iam-profiles-rbac.md` cho DTO, validation, migration và ví dụ đầy đủ.
- Gateway thêm route accounts; đường dẫn users hiện có bao phủ hồ sơ. Chưa kiểm thử runtime Gateway hoặc xác nhận triển khai Azure; test API dùng WebApplicationFactory và PostgreSQL thật.
- Bàn giao: đã commit tách riêng 29 commit cho từng file theo quy ước, kiểm thử toàn bộ giải pháp 81/81 tests PASS, merge theo chuỗi kha-feat-iam-profiles-rbac → kha → main → deploy và push lên origin.
- Các file tạo mới/thay đổi:
- `docs/iam-auth.md`
- `docs/iam-profiles-rbac.md`
- `PROGRESS.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/PermissionAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AccountsController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AdminProfilesController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/ProfilesController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/UsersController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Accounts/AccountRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/DTOs/IdentityDtos.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Interfaces/IProfileStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Profiles/ProfileValidators.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/PermissionCodes.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/AccountStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Configurations/IdentityConfigurations.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/IdentityStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926014941_ProfileWritePermission.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926014941_ProfileWritePermission.Designer.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/IamDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/ProfileStore.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs`
- `tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs`
- `tests/WardMate.Services.IAM.Tests/ProfileValidationTests.cs`

## DOCKER-002 — Cấu hình Local Dev để người dùng tự build

- Hoàn thành cấu hình: **2026-09-26 09:59 (Asia/Saigon, UTC+07:00)**.
- Sửa `docker/docker-compose.yml`: thêm build/service IAM và Gateway; kết nối nội bộ iam-db:5432 và iam-api:8080; bật Development/Swagger và AutoMigrate cho IAM; mở cổng localhost 5001/5000; pgAdmin dùng mật khẩu bắt buộc từ môi trường và bind localhost 5050.
- Sửa `docker/.env.example`: thêm JWT_KEY. Cập nhật `docker/.env` local (gitignored), giữ mật khẩu database/JWT đã có, tạo ngẫu nhiên mật khẩu pgAdmin còn thiếu; không ghi giá trị secrets vào tài liệu.
- Tạo `docs/docker-local.md`; cập nhật `PROGRESS.md`.
- Không thêm/thay đổi API endpoint hoặc DTO; token/ProblemDetails giữ nguyên. Frontend gọi qua localhost:5000; Swagger trực tiếp localhost:5001/swagger.
- Kiểm tra `docker compose --env-file docker/.env -f docker/docker-compose.yml config --quiet`: PASS. Không thêm unit tests. Không chạy build, test suite, migration, tải image hoặc khởi động container theo yêu cầu người dùng; runtime chưa được xác minh.
- Không commit/push; người dùng tự chạy lệnh Compose trong tài liệu, Antigravity xử lý Git.

## IAM-005 — Hoàn thiện API quản trị RBAC

- Hoàn thành: **2026-09-26 10:28 (Asia/Saigon, UTC+07:00)**.
- Thêm nhóm Swagger Rbac gồm 13 operations: CRUD vai trò tùy chỉnh, danh sách quyền, xem/gán/thu hồi quyền vai trò, xem/gán/thu hồi vai trò người dùng, đọc audit phân trang.
- Giữ tên và bảo vệ xóa 5 vai trò hệ thống; vai trò đang được gán phải thu hồi trước khi xóa. Không cho thu hồi iam.manage khỏi IT_ADMIN. Chỉ IT_ADMIN đang hoạt động có iam.manage được quản trị RBAC, dựa vào database hiện tại chứ không dùng role claim cũ.
- Bảo vệ admin cuối cùng trong cả thu hồi vai trò và khóa tài khoản. Các mutation dùng chung PostgreSQL transaction advisory lock và kiểm tra lại actor sau khi lấy khóa, bảo vệ giữa nhiều instance và các request đồng thời. Audit được ghi cùng transaction; lỗi rollback toàn bộ. PUT/DELETE liên kết idempotent, không ghi audit lặp cho no-op.
- Migration `20260926032128_RbacAdministration`: bảng rbac_audit_logs (schema hiện có 8 entity), index thời gian/ID và đồng bộ role ID sequence. Đã áp dụng vào PostgreSQL tạm qua test startup; chưa áp dụng trực tiếp vào Local Dev/Azure. Không build image hoặc khởi động stack ứng dụng.
- Endpoints mới, prefix `/api/v1/rbac`, tất cả yêu cầu Bearer + policy IT_ADMIN:
  - GET `/roles?page=1&pageSize=20`: không body; 200 RbacPage<RoleDto>, 400/401/403.
  - GET `/roles/{roleId}`: không body; 200 RoleDto, 401/403/404.
  - POST `/roles`: `{ "roleName": "DOCUMENT_REVIEWER", "description": "Nhân viên kiểm tra hồ sơ" }`; 201 RoleDto + Location, 400/401/403/409.
  - PUT `/roles/{roleId}`: cùng body POST; 200 RoleDto, 400/401/403/404/409.
  - DELETE `/roles/{roleId}`: không body; 204, 400/401/403/404/409.
  - GET `/permissions`: không body; 200 PermissionDto[], 401/403.
  - GET `/roles/{roleId}/permissions`: không body; 200 PermissionDto[], 401/403/404.
  - PUT/DELETE `/roles/{roleId}/permissions/{permissionId}`: không body; 204, 400/401/403/404/409.
  - GET `/users/{userId}/roles`: không body; 200 RoleDto[], 401/403/404.
  - PUT/DELETE `/users/{userId}/roles/{roleId}`: không body; 204, 400/401/403/404/409.
  - GET `/audit-logs?page=1&pageSize=20`: không body; 200 RbacPage<AuditDto>, 400/401/403.
- Endpoint cũ PUT `/api/v1/accounts/{userId}/status` bổ sung lỗi 409 iam.last_admin; actor hết quyền trong lúc chờ transaction trả 403. Gateway có route mới `/api/v1/rbac/{**catch-all}`.
- Kiểm thử: **115/115 PASS**, 0 failed/skipped: 78 IAM unit, 3 SharedKernel, 34 integration PostgreSQL. Thêm 24 unit cases và 10 integration cases; cập nhật kiểm tra schema/migration và account store. Release toàn solution **0 errors, 0 warnings**. Kiểm tra schema Swagger có đủ 13 operations và Bearer; test concurrent revoke/disable giữ ít nhất một admin; test negative không tạo audit hoặc ghi dữ liệu dở dang.
- Frontend: tên role chuẩn hóa IN HOA; RoleDto có isSystem và permissions. PageSize 1–100. Danh mục quyền được mở rộng cùng chức năng backend; không có CRUD tùy ý permission code. Thông báo tiếng Việt, mã lỗi/DTO chi tiết trong `docs/iam-rbac-admin.md`. Audit details là chuỗi JSON. Claim JWT có thể cũ nhưng policy đọc quyền hiện tại; login/refresh phát token với quyền mới. Swagger mới xuất hiện sau khi người dùng tự build/chạy lại image IAM.
- Bàn giao: đã commit tách riêng 33 commit cho từng file theo quy ước, kiểm thử toàn bộ giải pháp 115/115 tests PASS, merge theo chuỗi kha-feat-iam-rbac-admin → kha → main → deploy và push lên origin.
- File tạo mới/thay đổi của IAM-005:
- `docs/iam-auth.md`
- `docs/iam-profiles-rbac.md`
- `docs/iam-rbac-admin.md`
- `PROGRESS.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/PermissionAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Authorization/RbacAdministratorAuthorization.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/RbacController.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Errors/ResultExtensions.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Accounts/AccountRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Common/Result.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/IRbacStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacCommandHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacModels.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacQueryHandlers.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacRequests.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Rbac/RbacValidators.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/Entities/RbacAuditLog.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/AccountStore.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Configurations/RbacAuditConfiguration.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/IamDbContext.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926032128_RbacAdministration.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20260926032128_RbacAdministration.Designer.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/IamDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/RbacStore.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs`
- `tests/WardMate.Services.IAM.IntegrationTests/RbacAdministrationTests.cs`
- `tests/WardMate.Services.IAM.Tests/RbacValidationTests.cs`

## CLEANUP-001 — Gỡ mã nguồn và dữ liệu kiểm thử theo yêu cầu chủ dự án

- Hoàn thành: **2026-09-30 15:05 (Asia/Saigon, UTC+07:00)**.
- Xóa toàn bộ `tests/` gồm 3 project (IAM unit, IAM integration, SharedKernel unit) và 13 file được Git theo dõi; xóa cả build outputs bên trong tests và thư mục kết quả `TestResults/` local.
- Gỡ 3 project và solution folder tests khỏi `WardMate.sln`; solution còn 27 project ứng dụng/building blocks/gateway.
- Sửa `.github/workflows/iam-ci.yml`: chỉ restore và build Release với warnings-as-errors, bỏ bước chạy test; job hiện tên `build`.
- Cập nhật `AGENTS.md`, `README.md`, `docs/iam-auth.md` để phản ánh việc không còn test suite. Giữ tài liệu vận hành, migration, cấu hình build/triển khai và các kết quả lịch sử trong PROGRESS.md. Giữ ignore rules cho TestResults để tránh commit nhầm artifacts nếu phát sinh sau này.
- Code trong src, database, endpoints, DTO, ProblemDetails và token handling không thay đổi. Không có endpoint mới.
- Kiểm tra: `dotnet build WardMate.sln -c Release --no-restore --verbosity quiet` PASS, **0 errors, 0 warnings**; `git diff --check` PASS; solution và hướng dẫn chạy hiện hành không còn tham chiếu project/lệnh test đã xóa. Không chạy unit/integration tests vì suite đã được gỡ; không coi build pass là kiểm thử hành vi API.
- Không build Docker, không chạy container hoặc migration; không commit/push. Antigravity sẽ gom toàn bộ hành động dọn dẹp này vào **một commit duy nhất** theo chỉ định riêng của người dùng (ngoại lệ với quy tắc mỗi file một commit).
- Danh sách file thay đổi/xóa được Git theo dõi:
- M	.github/workflows/iam-ci.yml
- M	AGENTS.md
- M	PROGRESS.md
- M	README.md
- M	WardMate.sln
- M	docs/iam-auth.md
- D	tests/WardMate.Services.IAM.IntegrationTests/IamApiTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/IamFixture.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/ProfileRbacTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/RbacAdministrationTests.cs
- D	tests/WardMate.Services.IAM.IntegrationTests/WardMate.Services.IAM.IntegrationTests.csproj
- D	tests/WardMate.Services.IAM.Tests/AuthFlowTests.cs
- D	tests/WardMate.Services.IAM.Tests/ProfileValidationTests.cs
- D	tests/WardMate.Services.IAM.Tests/RbacValidationTests.cs
- D	tests/WardMate.Services.IAM.Tests/SecurityTests.cs
- D	tests/WardMate.Services.IAM.Tests/TestIdentityStore.cs
- D	tests/WardMate.Services.IAM.Tests/WardMate.Services.IAM.Tests.csproj
- D	tests/WardMate.SharedKernel.Tests/GlobalExceptionHandlerTests.cs
- D	tests/WardMate.SharedKernel.Tests/WardMate.SharedKernel.Tests.csproj

## SYNC-001 — Đồng bộ nhánh main (DocumentForm Service & Serilog SharedKernel) và dọn dẹp kiểm thử

- Hoàn thành: **2026-09-30 15:20 (Asia/Saigon, UTC+07:00)**.
- Kiểm tra nhánh `main` và tích hợp toàn bộ code mới từ `origin/main` (commit `12f5e84`):
  - Service mới: `WardMate.Services.DocumentForm` (API, Application, Domain, Infrastructure).
  - BuildingBlocks mới: `WardMate.SharedKernel` (Domain primitives, CQRS MediatR markers, Azure Blob Storage client, Serilog centralized logging).
  - Khởi tạo database: `docker/init-db/01-init-schemas-and-tables.sql` và cấu hình `docker-compose.yml`.
- Xử lý xung đột và đồng bộ dọn dẹp kiểm thử (CLEANUP-001):
  - Gỡ bỏ toàn bộ test projects và files vừa được kéo về từ `main` (`WardMate.Services.DocumentForm.Tests`, `WardMate.SharedKernel.Tests`), đảm bảo tuân thủ nghiêm ngặt quy định của chủ dự án (xóa bỏ test suites, solution có 0 test projects).
  - Cập nhật `WardMate.sln`: chứa đầy đủ 27 project ứng dụng và 0 project test.
  - Cập nhật `WardMate.Services.IAM.API/Program.cs`: giữ nguyên cấu hình Serilog tập trung, đồng thời khôi phục `PermissionAuthorization.AddPermissionAuthorization()`, `JsonIgnoreCondition.WhenWritingNull`, và định dạng ProblemDetails tiếng Việt.
- Kiểm tra giải pháp:
  - `dotnet restore WardMate.sln --disable-parallel`: thành công (exit code 0).
  - `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **0 Warning(s), 0 Error(s)** trên toàn bộ 27 projects.
- Chuỗi merge và triển khai Git:
  - Hoàn tất merge vào `kha` và push `origin kha`.
  - Merge `kha` vào `main` và push `origin main`.
  - Merge `main` vào `deploy` và push `origin deploy` để kích hoạt Azure CI/CD.

## TASK-07 — Procedure Catalog entities và ánh xạ EF Core JSONB

- Hoàn thành triển khai/xác minh: **2026-09-30 16:18 (Asia/Saigon, UTC+07:00)**.
- Nhánh feature: `kha-feat-procedure-catalog-entities`, tạo sau fetch origin và pull --ff-only nhánh kha sạch.
- Thêm PostgreSQL 16 độc lập: service procedure-db, database wardmate_procedure_db, user wardmate_procedure, localhost:5434, volume procedure-data, password riêng qua PROCEDURE_DB_PASSWORD. Không dùng/chỉnh sửa script database dùng chung docker/init-db. Mật khẩu local được thêm vào docker/.env ignored, không commit.
- Domain: ProcedureCategory, Procedure, ProcedureVersion và đầy đủ strongly typed JSON models cho content, cases/steps, submission methods, legal references, checklist và form definitions. FormTemplateId là external ID, không có FK xuyên service.
- Infrastructure: ProcedureDbContext, Fluent API đầy đủ kiểu/độ dài/default/nullability, B-tree unique code, composite unique procedure/version, GIN content/checklist. ValueConverter JSONB + ValueComparer clone sâu theo dõi sửa POCO lồng nhau. UTC timestamps, DateOnly date, UUID default gen_random_uuid().
- Migration `20260930091232_Initial_Procedure_Catalog_Schema`: 3 bảng nghiệp vụ, 4 cột JSONB; seed 3 danh mục và 1 thủ tục minh họa Đăng ký kết hôn (DEMO-KET-HON), đầy đủ content/checklist/forms. Snapshot version serialize độc lập, không bị sửa theo Procedure đang hoạt động.
- Application: IProcedureRepository, implementation EF, GetProcedureByIdQuery (MediatR), ProcedureDetailDto. Bổ sung Swagger API công khai GET `/api/v1/procedures/{id}`, không request body; 200 DTO đầy đủ scalar/category/JSON; 404 ProblemDetails tiếng Việt với code procedure.not_found khi không tồn tại/inactive. Gateway chuyển nguyên route này. Swagger trực tiếp cổng 5002.
- Frontend: JSON camelCase, null properties bị bỏ, mảng rỗng giữ []; DateOnly YYYY-MM-DD, UTC datetime, decimal cho phí/số ngày; không cần Bearer cho query này. Seed là dữ liệu giả để kiểm thử, không phải nội dung pháp lý chính thức. Giới hạn/giá trị nghiệp vụ bên trong JSON cần validation ở command ghi khi triển khai CRUD; không có API ghi trong task này.
- Kiểm thử theo yêu cầu mới (không khôi phục test IAM/SharedKernel đã xóa): **11/11 integration cases PASS**, 0 failed/skipped; kiểm tra migration/schema/index/seed, complex JSON roundtrip repository/MediatR/HTTP, nested mutation, null vs empty, constraints code/version, snapshot frozen, defaults/identity và inactive/missing. Testcontainers dùng PostgreSQL tạm, không chạm Local Dev database.
- Release toàn solution: **0 errors, 0 warnings**. `dotnet ef migrations has-pending-model-changes --configuration Release --no-build`: không có pending model changes. Compose config --quiet PASS; git diff --check PASS. Restore NuGet cần chạy ngoài sandbox vì hạn chế network, sau đó thành công.
- Cập nhật CI chạy test suite Procedure Catalog; README và docs/procedure-catalog.md có hướng dẫn chạy, DTO, migration và giới hạn JSON query converter.
- Chưa build Docker image/khởi động stack ứng dụng hoặc migrate database Local Dev/Azure. Migration được kiểm thử thực tế trên PostgreSQL tạm.
- Git cho riêng TASK-07 theo yêu cầu mới: commit từng file, không squash; merge/push feature → kha → deploy, không thay đổi main. Kết quả push được xác nhận trong báo cáo bàn giao cuối, không suy diễn push thành triển khai Azure thành công.
- File/thư mục đã tạo hoặc thay đổi:
- `.github/workflows/iam-ci.yml`
- `.gitignore`
- `docker/.env.example`
- `docker/docker-compose.yml`
- `docs/procedure-catalog.md`
- `PROGRESS.md`
- `README.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/appsettings.Development.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureDetailDto.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProcedureByIdQuery.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/WardMate.Services.ProcedureCatalog.Application.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureCategory.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ChecklistItemSchema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/FormDefinitionSchema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/LegalReferenceDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureCaseDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureContentPayload.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/ProcedureStepDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/JsonModels/SubmissionMethodDetail.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureCategoryConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureVersionConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/JsonbMapping.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20260930091232_Initial_Procedure_Catalog_Schema.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20260930091232_Initial_Procedure_Catalog_Schema.Designer.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContext.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContextFactory.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureSeed.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/WardMate.Services.ProcedureCatalog.IntegrationTests.csproj`
- `WardMate.sln`

## TASK-08 — API quản lý thủ tục, versioning tự động và trạng thái

- Hoàn thành: **2026-10-01 08:22 (Asia/Saigon, UTC+07:00)**.
- Trạng thái: **Hoàn thành code và xác minh local**. Không chạy bất kỳ lệnh Git nào; Antigravity chịu trách nhiệm commit/merge/push. AGENTS.md đã cập nhật để thu hồi workflow Git tự động cũ.
- Application: CreateProcedureCommand tạo active procedure + snapshot version 1 atomically; UpdateProcedureCommand chụp trạng thái trước thay đổi và cấp version tiếp theo; ToggleProcedureStatusCommand lưu trạng thái/lý do; GetProcedureVersionsQuery trả lịch sử giảm dần. MediatR, FluentValidation và Result Pattern; validation tiếng Việt.
- Infrastructure: transaction PostgreSQL, khóa hàng FOR UPDATE để tuần tự hóa update/status và số version; unique code conflict trả 409 kể cả request đồng thời. Migration `20261001012015_ProcedureStatusReason` thêm nullable text `status_change_reason`; snapshot bao gồm trường này.
- API: JWT HS256 issuer/audience/lifetime, role PROCEDURE_MANAGER hoặc IT_ADMIN; Swagger Bearer; gateway chuyển tiếp nguyên đường dẫn manager. ProblemDetails tiếng Việt, JSON DTO bỏ null.

### API mới / hướng dẫn Frontend

| Method | Route | Request body | Response codes |
|---|---|---|---|
| POST | `/api/v1/procedure-manager/procedures` | categoryId, procedureCode, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, contentPayload, checklistSchema?, formDefinitions? | 201 ProcedureDetailDto + Location; 400; 401; 403; 409 |
| PUT | `/api/v1/procedure-manager/procedures/{id}` | Toàn bộ trường POST + decisionNumber, effectiveDate (YYYY-MM-DD) | 200 ProcedureDetailDto; 400; 401; 403; 404; 409 |
| PATCH | `/api/v1/procedure-manager/procedures/{id}/status` | `{ "isActive": false, "reason": "Tạm ngưng tiếp nhận" }` (reason tùy chọn) | 200 {id,isActive,reason?,updatedAt}; 400; 401; 403; 404 |
| GET | `/api/v1/procedure-manager/procedures/{id}/versions` | Không | 200 array {id,versionNumber,decisionNumber?,effectiveDate,snapshotData,createdAt}; 401; 403; 404 |

- Cấu hình `Jwt__Key` giống IAM (không ghi secret vào source); issuer/audience mặc định wardmate/wardmate-client. Gửi `Authorization: Bearer <accessToken>`. Token phải có role PROCEDURE_MANAGER hoặc IT_ADMIN. Role được xác minh từ JWT, không tra IAM realtime; token cũ giữ quyền đến hết hạn khi IAM đổi role/disable account.
- Swagger Development trực tiếp: http://localhost:5002/swagger. Qua Gateway: http://localhost:5000/api/v1/procedure-manager/procedures. Không tự khởi động lại ứng dụng local trong phiên này.
- PUT thay thế toàn bộ input, giữ nguyên trạng thái active; effectiveDate là metadata, không hẹn giờ áp dụng. Snapshot lưu dữ liệu CŨ: version 1 lúc tạo, version 2 ở lần cập nhật đầu cùng nội dung ban đầu. Seed TASK-07 chưa có version nên lần update seed đầu tiên sinh version 1.
- `snapshotData` trả object JSON, không phải JSON string; null bên trong snapshot được giữ để phản ánh lịch sử. PATCH không tạo version; lý do trạng thái được lưu DB. Chặn tiếp nhận hồ sơ mới cần ApplicationWorkflow kiểm tra IsActive khi tích hợp; không tác động hồ sơ đã nộp.
- ProblemDetails: code, title, status, traceId, instance; errors khi validation. Codes: validation.failed, procedure.category_not_found, procedure.code_exists, procedure.not_found, auth.unauthorized, auth.forbidden.
- Request mẫu và hướng dẫn cấu hình: `docs/procedure-manager.md`.

### Kết quả xác minh

- `dotnet build WardMate.sln -c Release --no-restore --verbosity quiet`: **0 errors, 0 warnings**.
- `dotnet test WardMate.sln -c Release --no-build --verbosity quiet --logger trx --results-directory TestResults`: **32 passed / 0 failed / 0 skipped**. Gồm 11 test integration TASK-07 và **21 test integration mới** TASK-08; không thêm unit test riêng (0).
- PostgreSQL 16 thật qua Testcontainers: complex JSONB + version 1, snapshot trước cập nhật, trạng thái/lý do, cập nhật khi đóng, concurrent update giữ chuỗi snapshot, concurrent duplicate create, validation rollback, 401/403/token hết hạn/chữ ký sai, IT_ADMIN và Swagger.
- EF `has-pending-model-changes`: không có thay đổi model thiếu migration.
- Migration được áp dụng trong database kiểm thử tạm. Chưa áp dụng vào database Local Dev đang dùng; Development sẽ migrate khi chủ sở hữu chạy lại service với đúng connection string/JWT. Không build Docker image và không khởi động stack ứng dụng.
- TRX local (generated, không bàn giao commit): `TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_08_21_09.trx`.

### File tạo mới

- `docs/procedure-manager.md`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Security/ProcedureAuthentication.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Security/ManagerSecurityOperationFilter.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureManagementStore.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureResult.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInput.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/UpdateProcedureInputValidator.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureCommands.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureCommandHandlers.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/GetProcedureVersionsQuery.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001012015_ProcedureStatusReason.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001012015_ProcedureStatusReason.Designer.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureManagerTests.cs`

### File cập nhật

- `AGENTS.md`
- `PROGRESS.md`
- `docs/procedure-catalog.md`
- `src/Gateways/WardMate.YarpGateway/appsettings.json`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/WardMate.Services.ProcedureCatalog.API.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/WardMate.Services.ProcedureCatalog.Application.csproj`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs`
- `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs`
- `tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs`

Gợi ý commit cho Antigravity: `feat(procedure-catalog): add manager APIs with automatic versioning`.

## IAM-006 — Refresh Token qua HttpOnly Cookie cho Frontend

- Hoàn thành: **2026-10-01 10:01 (Asia/Saigon, UTC+07:00)**.
- Theo đề xuất MD của FE: Access Token giữ trong RAM; Refresh Token chỉ qua cookie HttpOnly, không trả JSON và không nhận fallback từ body. Chưa có domain deploy; local hỗ trợ http://localhost:5173 và http://localhost:3000 theo xác nhận của chủ sở hữu.
- Cookie host-only refreshToken, Path=/api/v1/auth, SameSite=Strict, Expires theo hạn refresh token. Ngoài Development luôn Secure; Development localhost cho phép HTTP, có cấu hình bật Secure. Không cần migration DB.
- Refresh xác định user từ refresh-token digest trong database, không cần access token cũ để khôi phục phiên sau F5. Giữ rotation nguyên tử, kiểm tra expiry/revoked/account active, hash SHA-256. Logout vẫn Authorized, kiểm tra quyền sở hữu token, revoke trong DB rồi xóa cookie cùng path.
- Header X-CSRF-Protection: 1 bắt buộc trên 4 POST auth; kiểm tra Origin; CORS credentialed exact-origin ở IAM/Gateway; Cache-Control no-store; Swagger thêm header và loại secret khỏi response schema.
- Không chạy Git. Không build Docker image. Kiểm thử dùng container database và process ứng dụng tạm riêng, đã dọn sau khi chạy; không thay đổi database Local Dev.

### API / hợp đồng FE thay đổi

| Method | Route | Request | Response |
|---|---|---|---|
| POST | /api/v1/auth/register | JSON {username,email,password,fullName}; header CSRF | 201 CurrentUserDto; 400; 403 CSRF; 409 |
| POST | /api/v1/auth/login | JSON {usernameOrEmail,password}; header CSRF | 200 BrowserAuthResponse + Set-Cookie; 400; 401; 403 |
| POST | /api/v1/auth/refresh-token | Không body, cookie tự gửi; header CSRF | 200 BrowserAuthResponse + cookie mới; 400 malformed cookie; 401; 403 |
| POST | /api/v1/auth/revoke-token | Không body, cookie tự gửi; Bearer + header CSRF | 204 + xóa cookie; 400 malformed cookie; 401; 403 |

- BrowserAuthResponse gồm accessToken, accessTokenExpiresAt, refreshTokenExpiresAt, tokenType; không có refreshToken.
- FE dùng credentials: include / withCredentials: true, lưu access token trong RAM, bỏ localStorage/sessionStorage token fallback. Khi F5 gọi refresh rồi /api/v1/users/me. Single-flight refresh kể cả nhiều tab dùng chung cookie; không lặp refresh khi endpoint refresh trả 401.
- Lỗi mới 403 iam.csrf_rejected; các lỗi tiếng Việt/ProblemDetails cũ giữ nguyên. Refresh thất bại không gửi xóa cookie để tránh response đến chậm xóa cookie mới từ request đồng thời. Logout cần Bearer còn hạn (refresh trước nếu cần); JWT đã phát vẫn có hiệu lực tới hạn, chưa có blacklist.
- Dùng route chuẩn /api/v1/auth qua Gateway localhost:5000 hoặc IAM localhost:5001 nhất quán; không dùng alias /api/iam vì cookie path không khớp. Swagger localhost:5001/swagger sau khi chủ sở hữu chạy lại IAM.
- Tài liệu FE đầy đủ: docs/iam-httponly.md, cập nhật docs/iam-auth.md. Domain deploy/CORS production chưa cấu hình vì chưa có thông tin. Nếu khác site, cần xem lại SameSite và hạn chế third-party cookie; HttpOnly không bảo vệ tuyệt đối trước XSS.

### File tạo mới

- src/BuildingBlocks/WardMate.SharedKernel/Web/BrowserCorsExtensions.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Security/RefreshTokenCookie.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Security/BrowserAuthProtectionAttribute.cs
- scripts/verify-iam-cookie.ps1
- docs/iam-httponly.md

### File chỉnh sửa

- src/Gateways/WardMate.YarpGateway/Program.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Controllers/AuthController.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/appsettings.json
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/OpenApi/BearerSecurityOperationFilter.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Commands/AuthCommands.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Commands/RefreshTokenHandler.cs
- src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Validation/AuthValidators.cs
- docs/iam-auth.md
- PROGRESS.md

### Kết quả xác minh

- dotnet build WardMate.sln -c Release --no-restore --verbosity quiet: **0 errors, 0 warnings**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet: **32 passed, 0 failed, 0 skipped** (ProcedureCatalog suite hiện có; không phải test IAM).
- scripts/verify-iam-cookie.ps1: **22/22 HTTP checks passed** với PostgreSQL 16 thật, IAM/Gateway processes riêng: register/login, JSON không chứa refresh secret, cookie HttpOnly/path/SameSite/host-only, local HTTP, no-store, credentialed CORS, refresh cookie-only/F5, rotation, replay 401, không nhận body fallback, CSRF/Origin, logout/revoked, preflight, Swagger, Secure Production.
- Unit tests mới: **0**; không tái tạo test project IAM đã gỡ. Có script kiểm thử HTTP có thể chạy lại. Chưa chạy FE/browser E2E; FE cần kiểm tra trên trình duyệt thật.

Gợi ý commit cho Antigravity: feat(iam)!: move refresh tokens to HttpOnly cookies with CSRF protection
Breaking change: refresh/logout không nhận token trong JSON; mọi POST auth yêu cầu X-CSRF-Protection: 1 và credentials; response login/refresh bỏ refreshToken.

## TASK-08-LIST — API danh sách thủ tục công khai và quản lý

- Hoàn thành: **2026-10-01 11:18 (Asia/Saigon, UTC+07:00)**.
- Bổ sung CQRS GetProceduresQuery, FluentValidation tiếng Việt, projection DTO tóm tắt bằng EF Core, phân trang tại database. Không tải JSONB lớn cho danh sách. Không cần migration hoặc thay route Gateway. Không chạy Git.

### API mới

| Method | Route | Request body / query | Response |
|---|---|---|---|
| GET | /api/v1/procedures | Không body. page=1, pageSize=20, search?, categoryId? | 200 ProcedureListDto; 400 ProblemDetails |
| GET | /api/v1/procedure-manager/procedures | Không body. page=1, pageSize=20, search?, categoryId?, isActive? | 200 ProcedureListDto; 400; 401; 403 |

- Public luôn chỉ lấy IsActive=true, không cho client ghi đè. Manager yêu cầu PROCEDURE_MANAGER hoặc IT_ADMIN; không truyền isActive thì lấy cả hai trạng thái.
- page>=1, pageSize 1..100; kiểm tra overflow offset. Search tối đa 255 ký tự, trim, tìm chứa trong code/title không phân biệt hoa/thường (vẫn phân biệt dấu). Escape ký tự %, _ và backslash, pattern được parameterize. Lọc danh mục dương và trạng thái kết hợp AND. Sort cố định ProcedureCode rồi Id.
- FE nhận `{items,page,pageSize,totalCount,totalPages}`. Item gồm id, categoryId, categoryName, procedureCode, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, isActive, createdAt, updatedAt. Không bao gồm contentPayload/checklistSchema/formDefinitions/versions; gọi API chi tiết khi cần.
- Trang vượt dữ liệu / danh mục không tồn tại / không có kết quả: 200 items=[]; totalPages=0 khi totalCount=0. Query không hợp lệ: 400 application/problem+json, code validation.failed, thông báo tiếng Việt, errors, traceId. Token handling không thay đổi.
- Count và items là hai query riêng; khi có concurrent writes, metadata có thể lệch tức thời. Không cung cấp snapshot pagination giữa các request.

### File mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureListDto.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProceduresQuery.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs

### File sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs
- docs/procedure-catalog.md
- docs/procedure-manager.md
- PROGRESS.md

### Kiểm thử

- Release solution build --no-restore: **0 warnings, 0 errors**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet: **116 passed / 0 failed / 0 skipped**: ProcedureCatalog 44, DocumentForm 72 trong solution hiện tại.
- Viết thêm **12 integration cases**, unit tests mới 0: public không lộ inactive dù truyền isActive=false; phân trang/sort/summary; manager status/category/search; tìm title tiếng Việt case-insensitive; literal wildcard; query sai/overflow; 401/403/admin/public. PostgreSQL 16 thật qua Testcontainers tạm. Không thêm test project mới.
- Chưa khởi động/restart ứng dụng Local Dev, không build Docker image. Swagger hiển thị endpoint mới sau khi chủ sở hữu chạy lại service với code mới.

Gợi ý commit cho Antigravity: feat(procedure-catalog): add paginated public and manager procedure lists

## TASK-08-DOCKER — Đóng gói Docker và tích hợp Local Dev cho Procedure Catalog

- Hoàn thành: **2026-10-01 11:25 (Asia/Saigon, UTC+07:00)**.
- Thêm Dockerfile đa tầng (.NET 8 SDK build & ASP.NET runtime) cho `WardMate.Services.ProcedureCatalog.API`.
- Tích hợp service `procedure-catalog` vào `docker/docker-compose.yml`, kết nối mạng nội bộ với `procedure-db`, bật `Database__AutoMigrate=true`, cấu hình JWT đồng bộ với IAM (`docker/.env`).
- Mở cổng `5002:8080`, cho phép khởi chạy và dừng trực tiếp qua Docker Desktop GUI mà không cần chạy lệnh `dotnet run`.

### File mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Dockerfile

### File sửa

- docker/docker-compose.yml
- PROGRESS.md

### Kiểm thử & Xác minh

- Release solution build `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **0 warnings, 0 errors**.
- Docker compose build: Image `wardmate-procedure-catalog:local` được build thành công.
- Khởi chạy container `wardmate_procedure_catalog`:
  - EF Core migration `20261001012015_ProcedureStatusReason` tự động áp dụng thành công.
  - Liveness check `GET http://localhost:5002/health`: **HTTP 200 Healthy**.
  - Kiểm tra API danh sách mới `GET http://localhost:5002/api/v1/procedures`: Trả về 200 kèm `items`, `page`, `pageSize`, `totalCount`, `totalPages`.
  - Kiểm tra API quản lý `GET http://localhost:5002/api/v1/procedure-manager/procedures`: Xác thực JWT thành công với vai trò `PROCEDURE_MANAGER`.
  - Swagger UI sẵn sàng tại `http://localhost:5002/swagger`.

Gợi ý commit cho Antigravity:
1. `build(procedure-catalog): add Dockerfile for Procedure Catalog API`
2. `feat(docker): add procedure-catalog service to docker-compose`

## TASK-09 — Tìm kiếm nâng cao & import (ĐANG THỰC HIỆN)

- Cập nhật phiên: **2026-10-01 13:34 (Asia/Saigon, UTC+07:00)**. Chưa hoàn thành toàn task.
- Bước 0 đã xác minh: GET /api/v1/procedures luôn ép IsActive=true; GET /api/v1/procedure-manager/procedures có JWT role policy và mặc định lấy cả hai trạng thái. Giữ nguyên phân tách này khi nâng cấp.
- Đã triển khai GetProceduresPagedQuery, keyword không dấu qua PostgreSQL unaccent/ILIKE, lọc cấp/danh mục/trạng thái, whitelist sort và phân trang pageNumber/pageSize (default10/max100). PagedResult<ProcedureSummaryDto> dùng currentPage,totalPages,totalCount,pageSize,hasPrevious,hasNext; DTO tóm tắt không tải JSONB. Alias search/page vẫn được nhận; response page cũ đã thay bằng currentPage.
- Migration ProcedureUnaccentSearch cài unaccent. Build Release: 0 errors, 0 warnings. EF has-pending-model-changes: không có thay đổi thiếu migration.
- dotnet test toàn solution: 120 passed, 0 failed, 0 skipped (ProcedureCatalog 48; DocumentForm 72). Thêm 4 integration cases cho keyword không dấu, cấp/sort/metadata và validation mới; cập nhật 12 test danh sách theo DTO mới. Các con số này CHƯA xác minh importer CSV.
- API GET không body, query mới và response như docs/procedure-catalog.md; status 200/400; Manager thêm401/403. Không thay token handling.
- Phần import CSV đã có bản nháp: CsvHelper 33.0.1, batch200, upsert và snapshot, endpoint multipart /api/v1/procedure-manager/procedures/import-csv. CHƯA có kiểm thử import/performance, chưa xác nhận ready. Không import dữ liệu thật.
- Trong phiên, chủ sở hữu cho biết muốn import từng DOCX/PDF và hỏi khả năng tự đọc/lưu database. Đã đề xuất DOCX/PDF có chữ → trích xuất → người quản lý xem/sửa → xác nhận lưu; PDF scan cần OCR (AIOCR hiện chỉ có khung service). Đang chờ chọn thay CSV trong TASK-09 hay làm tài liệu ở task sau và có bước duyệt hay tự lưu. Không suy diễn trả lời thay chủ sở hữu; chưa triển khai luồng DOCX/PDF.
- Không chạy Git; chưa bàn giao task hoàn chỉnh cho Antigravity.

### Các đường dẫn đã tác động trong phiên (bao gồm bản nháp chưa hoàn tất)

- Application/DTOs/PagedResult.cs (mới), Application/Queries/GetProceduresPagedQuery.cs (mới).
- Application/Import/ImportProceduresFromCsvCommand.cs (mới, bản nháp import).
- Application/Interfaces/IProcedureRepository.cs (sửa).
- Application/DTOs/ProcedureListDto.cs và Application/Queries/GetProceduresQuery.cs (xóa, thay bằng hợp đồng mới).
- Infrastructure/Persistence/ProcedureRepository.cs, ProcedureDbContext.cs, Migrations/ProcedureDbContextModelSnapshot.cs (sửa).
- Infrastructure/Persistence/Migrations/*ProcedureUnaccentSearch.cs và *.Designer.cs (mới).
- Infrastructure/Import/ProcedureCsvRow.cs, ProcedureCsvBatchWriter.cs, ProcedureCsvImporter.cs (mới, bản nháp import).
- Infrastructure/DependencyInjection.cs, WardMate.Services.ProcedureCatalog.Infrastructure.csproj (sửa cho CsvHelper/import).
- API/Controllers/ProceduresController.cs, ProcedureManagerController.cs (sửa).
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs, ProcedurePersistenceTests.cs (sửa).
- docs/procedure-catalog.md, docs/procedure-manager.md, PROGRESS.md (sửa).

Các đường dẫn Application/Infrastructure/API trên thuộc src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.<tầng>/. Chưa đề xuất commit toàn task vì phạm vi import đang được điều chỉnh.

## TASK-09 (điều chỉnh) — HOÀN THÀNH: tìm kiếm và xuất bản bản nháp đã đối soát

- Hoàn thành: **2026-10-01 14:10 (Asia/Saigon, UTC+07:00)**.
- Thay thế phạm vi import cũ: đã xóa toàn bộ 4 file importer CSV, endpoint import-csv, DI và dependency CsvHelper; xóa các thư mục Import rỗng. Không triển khai CSV. Các mục TASK-09 trước đây ở trạng thái nháp/chờ phạm vi chỉ là lịch sử.
- Đã xác minh lại: Public luôn active-only (cả query và repository); Manager lấy cả active/inactive nếu không truyền filter, có JWT role PROCEDURE_MANAGER/IT_ADMIN. Public cap50, Manager cap100, mặc định10.
- Tách GetPublicProceduresPagedQuery/GetManagerProceduresPagedQuery; tìm không dấu unaccent/ILIKE, lọc danh mục/cấp, phân trang. Giữ tùy chọn sort và alias page/search cũ. DTO Public có OriginalPdfUrl, không có IsActive; Manager thêm IsActive, VersionCount, CreatedAt/UpdatedAt.
- Bổ sung OriginalPdfUrl (nullable varchar500), PdfFileName (nullable varchar255) cho Procedure và ProcedureVersion. API chi tiết/lịch sử và snapshot JSON cũng có hai trường. Migration mới: 20261001070638_ProcedureOriginalPdf. Migration unaccent trước đó được giữ.
- PublishReviewedProcedureCommand: validation tiếng Việt; category tồn tại; mã mới tạo active + version1; mã có sẵn snapshot trạng thái cũ + version tiếp theo rồi thay dữ liệu. Transaction toàn vẹn; advisory lock theo mã bảo vệ publish concurrent khi chưa có row, row lock bảo vệ cập nhật. Giữ nguyên trạng thái đóng/mở, Id, CreatedAt khi cập nhật; không tự mở lại thủ tục đã đóng.
- Task nhận JSON đã đối soát, không thực hiện upload/OCR/AI/split-view FE. Không đọc/lưu PDF thật trong phiên này. Backend chỉ lưu URL, không xác minh nội dung file trên Azure Blob. Không thay schema/service IAM.
- Đã build Docker image `wardmate-procedure-catalog:local` và khởi động lại container trên Docker Desktop (cổng 5002); migrations đã tự động áp dụng vào PostgreSQL Local Dev khi container khởi chạy.

### Endpoints / hợp đồng FE

| Method | Route | Request | Response |
|---|---|---|---|
| GET | /api/v1/procedures | Không body. keyword?, categoryId?, levelOfImplementation?, pageNumber=1, pageSize=10 (max50); sortBy/isAscending và alias page/search còn hỗ trợ | 200 PagedResult<ProcedureSummaryDto>; 400 |
| GET | /api/v1/procedure-manager/procedures | Không body. Cùng filter; isActive nullable; pageSize max100 | 200 PagedResult<ProcedureManagerSummaryDto>; 400; 401; 403 |
| POST | /api/v1/procedure-manager/procedures/publish | JSON ReviewedProcedureInput: procedureCode, categoryId, title, issuingAuthority?, executingAgency?, levelOfImplementation, targetAudience, feeSummary, processingTimeSummary, originalPdfUrl?, pdfFileName?, contentPayload, checklistSchema?, formDefinitions? | 200 ProcedureDetailDto cho cả tạo/cập nhật; 400; 401; 403; 409 |

- Public item: id, procedureCode, title, categoryName, levelOfImplementation, feeSummary, processingTimeSummary, originalPdfUrl?, updatedAt. Manager item thêm isActive, versionCount, createdAt. Không tải contentPayload/checklist/form/version snapshots trong danh sách.
- Pagination: items/currentPage/totalPages/totalCount/pageSize/hasPrevious/hasNext. Danh sách rỗng trả200, totalPages=0; count và items có thể lệch tức thời nếu có concurrent writes (hai SQL riêng).
- JWT Bearer có role PROCEDURE_MANAGER hoặc IT_ADMIN cho manager/publish; token handling không thay đổi.
- OriginalPdfUrl nullable, HTTPS tuyệt đối, không userinfo, tối đa500 ký tự; PdfFileName tối đa255. JSON null bị bỏ khỏi response. Dùng URL ổn định; không nhúng secret/SAS dài hạn vào URL công khai. Blob private cần cơ chế cấp link của storage riêng.
- Publish thay toàn bộ input; snapshot trước sửa giữ URL/tên PDF cũ. Metadata ngày hiệu lực mặc định ngày UTC xuất bản do request không có EffectiveDate. Publish lặp vẫn tạo version; FE chặn double-submit. FeeSummary/ProcessingTimeSummary phải gửi rõ, không tự điền miễn phí/1 ngày.
- 400 validation.failed hoặc procedure.category_not_found, 409 procedure.code_exists; ProblemDetails tiếng Việt có traceId, instance, errors khi validation. Giao diện/AI gửi đầy đủ dữ liệu đã đối soát, không gửi file/multipart tới publish.
- Hướng dẫn và request mẫu: docs/procedure-reviewed-publishing.md. Swagger http://localhost:5002/swagger sau khi chủ sở hữu chạy lại service. Gateway chuyển nguyên route hiện có.

### Kiểm thử và migration

- dotnet build WardMate.sln -c Release --no-restore --verbosity quiet: **0 errors, 0 warnings**.
- dotnet test WardMate.sln -c Release --no-build --verbosity quiet --logger trx --results-directory TestResults: **131 passed / 0 failed / 0 skipped** (ProcedureCatalog59 + DocumentForm72).
- Thêm **11 integration cases**, unit tests mới0: JSONB10 cases + checklist/forms roundtrip; cột PDF/live/version/snapshot; update và versionCount; giữ trạng thái đóng; public/manager filters; concurrent publish cùng mã mới; invalid payload/category/URL/length rollback; rollback lỗi DB thật; giới hạn pageSize; 401/403/IT_ADMIN; Swagger không còn CSV.
- Cập nhật test danh sách theo DTO public/manager mới và test migration count. Không tạo thêm test project.
- EF has-pending-model-changes: không có thay đổi thiếu migration. Kiểm tra migration thêm đúng 4 cột nullable, không xóa dữ liệu nghiệp vụ. Database Local Dev đã được migrate đầy đủ 2 migration (`20261001063051_ProcedureUnaccentSearch` và `20261001070638_ProcedureOriginalPdf`) tự động qua Docker container startup.
- TRX local: TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_14_07_33.trx (DocumentForm), TestResults/LAPTOP_KHA_KHANGUYEN_2026-10-01_14_07_40.trx (ProcedureCatalog); generated artifacts không bàn giao commit.

### File mới trong phiên điều chỉnh

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/ProcedureSearchOptions.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetPublicProceduresPagedQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetManagerProceduresPagedQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/PublishReviewedProcedureCommand.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001070638_ProcedureOriginalPdf.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261001070638_ProcedureOriginalPdf.Designer.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ReviewedProcedureTests.cs
- docs/procedure-reviewed-publishing.md

### File chỉnh sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/Procedure.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureVersion.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/PagedResult.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/DTOs/ProcedureDetailDto.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Interfaces/IProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInput.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/ProcedureInputValidator.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Management/GetProcedureVersionsQuery.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureRepository.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureVersionConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProceduresController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureManagerController.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureListTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs
- docs/procedure-catalog.md
- docs/procedure-manager.md
- PROGRESS.md

### File đã gỡ

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Queries/GetProceduresPagedQuery.cs (thay bằng query riêng public/manager và options chung)
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Import/ImportProceduresFromCsvCommand.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvRow.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvBatchWriter.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Import/ProcedureCsvImporter.cs

Gợi ý commit cho Antigravity: feat(procedure-catalog): publish reviewed procedures with PDF version history

## IAM-007 — Sửa CSRF trên Swagger sau Azure ingress bằng Forwarded Headers

- Hoàn thành: 2026-10-02 01:44:58 Asia/Saigon (UTC+07:00).
- Phạm vi: cấu hình IAM API nhận X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host; xóa KnownNetworks/KnownProxies theo yêu cầu. Middleware chạy ngay sau UseGlobalExceptionHandling, trước logging, CORS, Swagger và authentication/authorization. Giữ ForwardLimit mặc định 1.
- Đã kiểm tra BrowserAuthProtectionAttribute: sameOrigin sử dụng Request.Scheme/Host sau chuẩn hóa; không cần sửa attribute. Vẫn bắt buộc X-CSRF-Protection: 1 và từ chối origin không cùng domain, không thuộc CORS allowlist.

### File thay đổi

| File | Nội dung |
| --- | --- |
| src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs | Cấu hình và thứ tự middleware Forwarded Headers |
| scripts/verify-iam-cookie.ps1 | Thêm 5 kiểm tra HTTP cho forwarded origin và bảo toàn CSRF/CORS |
| docs/iam-httponly.md | Hướng dẫn ingress, trust boundary và giới hạn proxy nhiều hop |
| PROGRESS.md | Nhật ký IAM-007 và kết quả kiểm chứng |

### API / Frontend

- Không thêm endpoint hay thay đổi DTO/request body/token contract.
- POST /api/v1/auth/login vẫn nhận UsernameOrEmail và Password, trả 200 với access token và cookie refresh HttpOnly khi hợp lệ; CSRF không hợp lệ trả 403 ProblemDetails với code iam.csrf_rejected.
- Swagger cùng public domain không cần thêm domain đó vào CORS allowlist nếu ingress chuyển đúng Host/Proto. FE khác domain vẫn phải nằm trong AllowedOrigins và gửi credentials cùng X-CSRF-Protection: 1.
- Container phải chỉ nhận traffic từ ingress/proxy được kiểm soát; proxy phải làm sạch/ghi đè forwarded headers. Với nhiều hop, proxy gần IAM nhất phải chuyển đúng public host/proto.

### Kiểm chứng

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release: PASS 131/131, 0 failed, 0 skipped (DocumentForm 72, ProcedureCatalog 59). Không tạo mới unit test project.
- pwsh -NoProfile -File scripts/verify-iam-cookie.ps1: PASS 27/27 HTTP checks trên PostgreSQL tạm và IAM/Gateway local, gồm 5 checks mới. Origin HTTPS của Swagger Azure được chấp nhận với forwarded host/proto dù không nằm trong CORS allowlist; thiếu forwarding, thiếu CSRF header hoặc origin lạ bị từ chối; CORS cho FE được phép vẫn hoạt động.
- Không chạy Git. Chưa deploy hoặc kiểm tra trực tiếp trên Azure; cần Antigravity triển khai và kiểm tra lại ingress thực tế.

Gợi ý commit: fix(iam): honor forwarded headers before CSRF validation

## IAM-008 — Chuẩn bị tạo 5 tài khoản kiểm thử trên deploy (chưa hoàn tất)

- Thời gian ghi nhận: 2026-10-02, Asia/Saigon (UTC+07:00).
- Đã kiểm tra Swagger Azure IAM: GET /swagger/v1/swagger.json trả HTTP 200; có API đăng ký, đăng nhập và quản lý role.
- Chưa tạo tài khoản hoặc thay đổi dữ liệu deploy: đang chờ phiên/thông tin đăng nhập IT_ADMIN để gán role qua API được bảo vệ. Đăng ký công khai chỉ gán REGISTERED_CITIZEN.
- Dự kiến sử dụng POST /api/v1/auth/register (Username, Email, Password, FullName; 201), POST /api/v1/auth/login (UsernameOrEmail, Password; 200), GET /api/v1/rbac/roles (200), PUT/DELETE /api/v1/rbac/users/{userId}/roles/{roleId} (204), và xác minh đăng nhập sau khi gán role.
- File thay đổi trong phiên này: PROGRESS.md. Không sửa mã nguồn, không chạy lại build/unit tests; chưa có kiểm thử tạo tài khoản thành công. Không chạy Git.
- Token/Frontend: auth POST cần X-CSRF-Protection: 1; quản lý role cần token IT_ADMIN. Không lưu thông tin đăng nhập/token vào tài liệu hoặc mã nguồn.

### IAM-008 — Cập nhật điều kiện bootstrap (2026-10-02, Asia/Saigon UTC+07:00)

- Chủ hệ thống xác nhận chưa có IT_ADMIN và yêu cầu tạo mỗi role một tài khoản trên deploy.
- Đã xác định quy trình bootstrap trong docs/iam-rbac-admin.md: gán IT_ADMIN đầu tiên trực tiếp qua kết nối database quản trị tin cậy, sau đó quản lý role bằng API.
- Chưa có kết nối quản trị Azure/PostgreSQL trong môi trường thao tác (không có Azure CLI hoặc biến môi trường kết nối phù hợp). Đã yêu cầu đường dẫn file kết nối deploy hoặc phiên Azure Portal được đăng nhập.
- Chưa tạo tài khoản, chưa thay đổi database deploy; chưa chạy kiểm thử cấp tài khoản. Không thêm endpoint tự cấp quyền, không chạy Git.

## IAM-009 — Khởi tạo 5 tài khoản chính thức cho từng vai trò và dọn dẹp tài khoản kiểm thử cũ

- Hoàn thành: 2026-10-02 13:16:00 Asia/Saigon (UTC+07:00).
- Phạm vi:
  - Loại bỏ các tài khoản kiểm thử tạm thời (`citizentest`, `officertest`, `managertest`, `procmanagertest`, `admintest`, `testcitizen1`).
  - Thiết lập 5 tài khoản chính thức chuẩn hóa theo từng vai trò của hệ thống WardMate, không chứa hậu tố `test`:
    1. **IT Admin**: `wardmateadmin` (Tên hiển thị: `WardMate Admin`, Email: `admin@wardmate.vn`, Vai trò: `IT_ADMIN` - Id 5)
    2. **Procedure Manager**: `wardmateprocmanager` (Tên hiển thị: `WardMate Procedure Manager`, Email: `procmanager@wardmate.vn`, Vai trò: `PROCEDURE_MANAGER` - Id 4)
    3. **Manager**: `wardmatemanager` (Tên hiển thị: `WardMate Manager`, Email: `manager@wardmate.vn`, Vai trò: `MANAGER` - Id 3)
    4. **Front Desk Officer**: `wardmateofficer` (Tên hiển thị: `WardMate Officer`, Email: `officer@wardmate.vn`, Vai trò: `FRONT_DESK_OFFICER` - Id 2)
    5. **Citizen**: `wardmatecitizen` (Tên hiển thị: `WardMate Citizen`, Email: `citizen@wardmate.vn`, Vai trò: `REGISTERED_CITIZEN` - Id 1)
  - Mật khẩu mặc định: `Password123!` (đáp ứng đầy đủ chính sách độ phức tạp mật khẩu: >=8 ký tự, có chữ hoa, ký tự đặc biệt, UTF-8 <= 72 byte).
  - Phương án triển khai: Khởi tạo trực tiếp qua Database Bootstrap Script vào cơ sở dữ liệu PostgreSQL (đã áp dụng và xác minh trên container `wardmate-iam-db-1` local, sẵn sàng thực thi trên Supabase Deploy qua SQL Editor).
  - Giữ mã nguồn hệ thống hoàn toàn sạch (clean architecture), không hardcode thông tin tài khoản mẫu trong code backend để đảm bảo an toàn bảo mật và tránh nguy cơ vô tình ghi đè mật khẩu khi khởi động lại.

### File thay đổi

| File | Nội dung |
| --- | --- |
| `PROGRESS.md` | Ghi nhận hoàn thành task IAM-009 |

### API / Frontend

- Đăng nhập: POST `/api/v1/auth/login` với `UsernameOrEmail` là username (`WardMateAdmin`, `wardmateadmin`, ...) hoặc email (`admin@wardmate.vn`, ...), `Password` là `Password123!`.
- Header bắt buộc cho browser/cookie authentication: `X-CSRF-Protection: 1`.
- Không tạo endpoint công khai mới; bảo toàn kiến trúc bảo mật RBAC.

### Kết quả kiểm chứng

- `dotnet build WardMate.sln -c Release --no-restore -warnaserror`: **PASS, 0 errors, 0 warnings**.
- `dotnet test WardMate.sln -c Release --no-build`: **PASS 131/131, 0 failed, 0 skipped** (DocumentForm 72, ProcedureCatalog 59).
- Đã xác minh dữ liệu thực tế tại PostgreSQL container local: 5 tài khoản hiển thị đầy đủ kèm đúng vai trò và thông tin profile.
- Tuân thủ quy định `AGENTS.md`: không tự ý thực thi các lệnh Git.

Gợi ý commit cho Antigravity:
- `docs: update PROGRESS.md with official role accounts seeding`


## PDF-001 — Backend upload PDF, bóc tách AIOCR và đối soát bản nháp thủ tục

- Hoàn thành mã nguồn và kiểm thử local: 2026-10-02 14:57:59 Asia/Saigon (UTC+07:00).
- Trạng thái: hoàn thành backend và adapter có thể cấu hình. Chưa kích hoạt/kiểm chứng Azure Blob, OCR và AI thật; chủ dự án xác nhận chưa có OCR. Chưa xây màn hình FE Split-view trong repository backend, chưa deploy, chưa áp dụng migration lên database Local Dev/deploy.
- Không chạy Git; không build Docker image hoặc khởi động cụm ứng dụng. Các container PostgreSQL được tạo bởi integration tests là tạm thời và độc lập database người dùng.

### Chức năng

- Procedure Catalog quản lý PDF nguồn private trên Azure Blob, bảng procedure_drafts với JSONB payload/warnings, trạng thái Queued/Processing/NeedsReview/Failed/Published và người tạo/đối soát.
- Nghiệp vụ ở tầng Application qua IDraftPersistence/IDraftFileStorage; Infrastructure triển khai EF Core, Blob, HTTP AIOCR và background worker. AIOCR không tham chiếu assembly/database Procedure Catalog.
- Khi OCR tắt, upload tạo bản nháp thủ công NeedsReview. Thiếu cấu hình Blob trả 503, không giả vờ upload thành công. File PDF tối đa 20 MiB, kiểm tra phần mở rộng và chữ ký đầu file.
- Worker sử dụng hàng đợi PostgreSQL, row locks SKIP LOCKED và lease phục hồi khi restart; có thời hạn xử lý, số lần nhận giới hạn và retry thủ công. Kết quả AI không tự xuất bản.
- Revision GUID chống ghi đè; xuất bản yêu cầu confirmed=true, giữ nguồn PDF của bản nháp, dùng handler publish hiện có. Trạng thái bản nháp, thủ tục và version commit/rollback cùng transaction; xác nhận trùng trả 409.
- Adapter AIOCR dùng Azure Document Intelligence prebuilt-layout + Azure OpenAI JSON mode, giữ trường chưa biết ở dạng null và warnings. Publish không tự biến trường thiếu thành các giá trị POCO mặc định. Adapter mặc định tắt, không gọi cloud trong tests.
- Link PDF read SAS 10 phút chỉ cấp cho manager hoặc thủ tục đã xuất bản còn active; SAS không lưu vào DB. Dockerfile AIOCR, Compose profile ai và biến môi trường mẫu đã được thêm; Compose đã validate, image chưa build.

### API và hợp đồng FE

| Method | Route | Request body | Response |
| --- | --- | --- | --- |
| POST | /api/v1/procedure-manager/drafts | Multipart file PDF | 202 DraftDto; 400/413; 503 thiếu Blob |
| GET | /api/v1/procedure-manager/drafts?page=1&pageSize=10 | Không | 200 DraftSummaryDto[], pageSize tối đa 50; 400 |
| GET | /api/v1/procedure-manager/drafts/{id} | Không | 200 DraftDto; 404 |
| GET | /api/v1/procedure-manager/drafts/{id}/source | Không | 200 {url,expiresInSeconds:600}; 404/503 |
| PUT | /api/v1/procedure-manager/drafts/{id} | {revision,payload} | 200 DraftDto; 400/404/409 |
| POST | /api/v1/procedure-manager/drafts/{id}/retry | {revision} | 200 DraftDto; 404/409/503 |
| POST | /api/v1/procedure-manager/drafts/{id}/publish | {revision,confirmed:true} | 200 ProcedureDetailDto; 400/404/409 |
| GET | /api/v1/procedures/{id}/source | Không, public | 200 {url,expiresInSeconds:600}; 404/503 |
| POST | AIOCR /internal/v1/procedure-extractions | Binary PDF, X-Service-Key | 200 {payload,extractedText,warnings}; 400/401/413/502/503/504 |

Manager endpoints yêu cầu Bearer role PROCEDURE_MANAGER hoặc IT_ADMIN (401/403 khi không đủ quyền). DTO bản nháp có revision/status/payload/warnings/extractedText/pdfFileName/failureCode/publishedProcedureId và timestamps. FE poll GET khi Queued/Processing, lấy link source hiển thị PDF, sửa payload và gửi revision mới nhất. Chỉ bấm publish sau đối soát. Lỗi ProblemDetails tiếng Việt có code/traceId/errors khi phù hợp. Không thay đổi token contract IAM. Hướng dẫn FE, giới hạn và cấu hình: docs/procedure-pdf-drafts.md.

### Kiểm thử thực tế

- dotnet build WardMate.sln -c Release --no-restore -warnaserror: PASS, 0 errors, 0 warnings.
- dotnet test WardMate.sln -c Release --no-restore: PASS 144/144, 0 failed, 0 skipped: DocumentForm 72; ProcedureCatalog và adapter AIOCR 72.
- Thêm 13 test cases trong project hiện có (10 kiểm thử luồng draft/PostgreSQL/HTTP, 3 kiểm thử adapter HTTP mô phỏng); không tạo test project mới.
- Kiểm tra: upload/manual edit, 10 cases, PDF source/private URL, active filtering, role authorization, revision conflict, publish trùng, rollback khi ghi draft thất bại, khôi phục lease, worker không tự publish, field unknown không biến thành 0, provider response bị cắt, thiếu cấu hình provider không gọi mạng.
- dotnet ef migrations has-pending-model-changes: không có model change chưa được đưa vào migration. Migration mới chạy thành công trên PostgreSQL tạm của tests.
- docker compose -f docker/docker-compose.yml --profile ai config --quiet: PASS. Không build image.
- Chưa thử OCR trên PDF thật, chưa đo độ chính xác/chi phí AI, chưa xác minh cloud hoặc UI frontend. Blob storage trong integration tests là fake; provider HTTP được mô phỏng và không phát sinh phí.

### File tạo mới

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/ProcedureDraft.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftContracts.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftPayloadRequirements.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/DraftPersistence.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Application/Drafts/ProcedureDraftService.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Drafts/DraftPersistence.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Drafts/DraftExtractionWorker.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Configurations/ProcedureDraftConfiguration.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261002072134_ProcedureDrafts.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261002072134_ProcedureDrafts.Designer.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureDraftsController.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Controllers/ProcedureSourcesController.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Application/Extraction/ProcedureExtraction.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Extraction/AzureProcedureDocumentExtractor.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Controllers/ProcedureExtractionsController.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Dockerfile
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureDraftTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/DraftTestBlobStorage.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/AzureExtractionAdapterTests.cs
- docs/procedure-pdf-drafts.md

### File chỉnh sửa

- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureDbContext.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/ProcedureManagementStore.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/ProcedureDbContextModelSnapshot.cs
- src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/appsettings.json
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/WardMate.Services.AIOCR.Infrastructure.csproj
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Program.cs
- src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/appsettings.json
- docker/docker-compose.yml
- docker/.env.example
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedureFixture.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/ProcedurePersistenceTests.cs
- tests/WardMate.Services.ProcedureCatalog.IntegrationTests/WardMate.Services.ProcedureCatalog.IntegrationTests.csproj
- PROGRESS.md

Gợi ý Conventional Commit theo chức năng để Antigravity tách từng file theo quy định: feat(procedure-catalog): add PDF drafts and reviewed publication; feat(aiocr): add configurable Azure procedure extraction; test(procedure-catalog): verify PDF draft lifecycle and extraction; docs: document PDF review workflow and configuration.
