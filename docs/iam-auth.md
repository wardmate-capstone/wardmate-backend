# IAM-001 — Core IAM authentication

## Run locally

Requires .NET SDK 8 and Docker Desktop (Linux containers). The only database introduced by this task is IAM's PostgreSQL 16 database.

1. Copy `docker/.env.example` to `docker/.env`, replace the password, then run:

```powershell
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d iam-db
```

2. In the IAM terminal, set secrets without committing them. The database password must match the value chosen above:

```powershell
$env:ConnectionStrings__Database = 'Host=localhost;Port=5433;Database=wardmate_iam_db;Username=wardmate;Password=<your-local-password>'
$env:Jwt__Key = '<random-signing-secret-at-least-32-UTF8-bytes>'
dotnet run --project src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API --launch-profile http
```

3. In a separate terminal:

```powershell
dotnet run --project src/Gateways/WardMate.YarpGateway --launch-profile http
```

API direct URL: `http://localhost:5001`. Gateway URL: `http://localhost:5000`. Both expose the same versioned IAM routes below. The previous `/api/iam` prefix remains available for the service's root/health routes. Swagger UI is at `http://localhost:5001/swagger` in Development. Click Authorize and paste the access token without a manually added Bearer prefix.

`Database:AutoMigrate` defaults to true; IAM applies its own migrations before accepting traffic. Migrations seed five roles, three permissions (`iam.profile.read`, `iam.profile.write`, `iam.manage`) and eleven role-permission assignments. Every role receives profile read/write; only IT_ADMIN receives `iam.manage`. IAM-004 adds permission policies and admin profile/account endpoints; see iam-profiles-rbac.md. No sample user/password is seeded. Set `Database__AutoMigrate=false` when applying migrations separately as a deployment step.

## Endpoints

| Method | Route | Authentication | Request body | Responses |
|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Public | `{ "username": "citizen", "email": "citizen@example.test", "password": "StrongPassword123!", "fullName": "Nguyen Van A" }` | 201 CurrentUserDto; 400 validation; 409 duplicate |
| POST | `/api/v1/auth/login` | Public | `{ "usernameOrEmail": "citizen", "password": "StrongPassword123!" }` | 200 AuthResponseDto; 400 validation; 401 invalid credentials/inactive |
| POST | `/api/v1/auth/refresh-token` | Public | `{ "accessToken": "...", "refreshToken": "..." }` | 200 AuthResponseDto; 400 validation; 401 invalid/expired/revoked/mismatched token |
| POST | `/api/v1/auth/revoke-token` | Bearer | `{ "refreshToken": "..." }` | 204; 400 validation; 401 unauthenticated/invalid token/wrong owner |
| GET | `/api/v1/users/me` | Bearer | None | 200 CurrentUserDto; 401 unauthenticated/inactive/missing account |

Unexpected server exceptions use the existing sanitized HTTP 500 ProblemDetails handler. Requests with malformed JSON or missing required values return 400.

## DTOs and frontend integration

JSON properties use camelCase. AuthResponseDto:

```json
{
  "accessToken": "<JWT>",
  "accessTokenExpiresAt": "2026-09-25T10:15:00Z",
  "refreshToken": "<opaque-random-token>",
  "refreshTokenExpiresAt": "2026-10-02T10:00:00Z",
  "tokenType": "Bearer"
}
```

CurrentUserDto:

```json
{
  "id": "<uuid>",
  "username": "citizen",
  "email": "citizen@example.test",
  "profile": {
    "fullName": "Nguyen Van A"
  },
  "roles": ["REGISTERED_CITIZEN"],
  "permissions": ["iam.profile.read"]
}
```

- Register does not log in automatically; call login afterward. New users receive only REGISTERED_CITIZEN. Clients cannot request a privileged role through registration.
- Controller JSON responses omit properties whose values are null, including optional profile fields. Frontend DTOs must allow missing optional properties; populated values and empty strings remain present.
- Usernames are ASCII letters, digits, `_`, `.` or `-`, maximum 100 characters; stored usernames/emails are lowercase. Registration passwords need at least 8 characters, one uppercase letter and one special character (Unicode punctuation or symbol; whitespace does not count), and at most 72 UTF-8 bytes (BCrypt limit). No digit is required. Login continues to accept existing credentials without applying the new registration strength rules. Full names cannot be blank and have a 255-character limit.
- Send `Authorization: Bearer <accessToken>` to authorized endpoints. Access token defaults to 15 minutes, refresh token to 7 days; timestamps are UTC, dates are `YYYY-MM-DD`.
- JWT contains `sub`, `email`, `role` and `permissions`; role and permissions are arrays in the JWT payload. UI permission checks do not replace backend authorization. `/users/me` reloads the current profile/permissions from IAM storage.
- Refresh accepts a correctly signed expired access token only when its subject matches a still-valid stored refresh token. API authorization itself rejects expired access tokens. Invalid signature, issuer, audience or future activation time fails refresh.
- Replace **both** tokens after successful refresh. Serialize refresh requests in the frontend (one in-flight request per session): the previous token becomes unusable after the first successful rotation, including concurrent calls.
- Logout revokes only the supplied refresh token and is idempotent for that user's already-revoked token. Existing access tokens remain valid until their short expiry; this implementation does not provide immediate JWT blacklisting or revoke-all-sessions.
- Refresh tokens are returned as JSON; this task does not issue authentication cookies. Keep tokens out of URLs/logs. Gateway CORS is not configured by this task: use a same-origin frontend proxy or configure allowed frontend origins in a separate deployment change.

ProblemDetails carries a stable `code` and `traceId`. Validation adds an `errors` dictionary keyed by field name, without passwords/tokens. Codes: `validation_failed`, `iam.duplicate_account`, `iam.invalid_credentials`, `iam.invalid_token`, `iam.user_unavailable`, `iam.unauthorized`. Branch on the code/status, not English display text. On refresh 401, clear the session and require login.

## Maintenance and tests

```powershell
dotnet tool restore
dotnet ef migrations add YourIamChange --project src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure --output-dir Persistence/Migrations
dotnet build WardMate.sln -c Release
dotnet test WardMate.sln -c Release --no-build
```

The complete suite requires Docker: integration tests use Testcontainers with a random-port, disposable PostgreSQL instance, never the developer database. Unit-only IAM tests: `dotnet test tests/WardMate.Services.IAM.Tests -c Release`. Schema uses snake_case, local foreign keys and UTC `timestamp with time zone`; nullable identity numbers can coexist. Refresh tokens are stored only as SHA-256 digests of 64 random bytes. BCrypt uses a random salt and work factor 12. The persisted `is_revoked` field is a concurrency token; an EF SaveChanges transaction atomically revokes the old token and inserts the replacement, rolling back a losing concurrent update.

Git workflow is recorded in root `AGENTS.md`: feature → `kha` → `deploy`. GitHub Actions validates build and tests on these branches. This repository's workflow is CI validation; successful push does not by itself verify an external frontend deployment.

## Ngôn ngữ thông báo API

Thông báo validation và lỗi nghiệp vụ IAM trả về bằng tiếng Việt, không phụ thuộc `Accept-Language`. Tên trường JSON, HTTP status và mã lỗi (`validation_failed`, `iam.invalid_credentials`, ...) giữ nguyên để Frontend xử lý ổn định. Lỗi thiếu trường hoặc sai định dạng JSON dùng thông báo an toàn bằng tiếng Việt; không trả chi tiết exception của bộ đọc JSON. Lỗi hệ thống qua Global Exception Handler dùng thông báo tiếng Việt chung, chi tiết kỹ thuật chỉ ghi log nội bộ.

## Hồ sơ và RBAC (IAM-004)

Xem [API hồ sơ, RBAC và khóa/mở khóa tài khoản](iam-profiles-rbac.md) cho các endpoint mới. IAM hiện kiểm tra quyền từ database trên mỗi yêu cầu có policy; migration mới bổ sung `iam.profile.write`. Sau khi xóa hồ sơ, `/users/me` vẫn trả tài khoản nhưng bỏ thuộc tính `profile`.

## API quản trị RBAC (IAM-005)

Nhóm **Rbac** trong Swagger có CRUD vai trò nghiệp vụ, xem/gán/thu hồi quyền, gán/thu hồi vai trò người dùng và nhật ký thay đổi. Xem [hướng dẫn API RBAC](iam-rbac-admin.md). Chỉ IT_ADMIN hoạt động có quyền iam.manage được quản trị RBAC; account/profile management vẫn dùng policy iam.manage hiện có.
