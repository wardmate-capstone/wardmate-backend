# WardMate — Nhật ký phát triển

**Trạng thái Core IAM: HOÀN THÀNH** — đã bàn giao lịch sử commit tách theo file lên `kha` và `deploy`; PostgreSQL Local Dev đã migrate và Swagger IAM đang chạy. Xác minh ngày 25/09/2026 lúc 10:13 (Asia/Saigon).
**Trạng thái Shared Kernel & Central Logging (TASK-05, TASK-06): HOÀN THÀNH** — đã triển khai đầy đủ Domain primitives, CQRS MediatR abstractions, Azure Blob Storage client wrapper, Serilog tập trung, RequestLoggingMiddleware và 39 unit tests cho SharedKernel. Xác minh ngày 26/09/2026 lúc 18:10 (Asia/Saigon).

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
