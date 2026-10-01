# IAM-006 — Bàn giao HttpOnly cho Frontend

## Hợp đồng mới

Access Token tiếp tục trả trong JSON, FE chỉ lưu trong RAM. Refresh Token chỉ được gửi qua `Set-Cookie`, không còn trong JSON response, request body hoặc Swagger schema. Không cần migration database; DB vẫn lưu SHA-256 digest và rotation vẫn thu hồi token cũ nguyên tử.

Base URL local: Gateway `http://localhost:5000` (khuyến nghị cho FE), hoặc IAM trực tiếp `http://localhost:5001`. Swagger: `http://localhost:5001/swagger`. Chỉ dùng route chuẩn `/api/v1/auth/*`, không dùng alias `/api/iam/api/v1/auth/*` vì cookie có Path giới hạn.

| Method / route | Body | Yêu cầu | Thành công |
|---|---|---|---|
| POST `/api/v1/auth/register` | `{username,email,password,fullName}` | Header CSRF | 201 CurrentUserDto, chưa đăng nhập |
| POST `/api/v1/auth/login` | `{usernameOrEmail,password}` | Header CSRF | 200 JSON access token + Set-Cookie refresh |
| POST `/api/v1/auth/refresh-token` | Không | Header CSRF + cookie trình duyệt tự gửi | 200 JSON access token + Set-Cookie refresh mới |
| POST `/api/v1/auth/revoke-token` | Không | Header CSRF + Bearer access token + cookie | 204, thu hồi refresh trong DB và xóa cookie |

Header CSRF của cả bốn POST: **`X-CSRF-Protection: 1`**. Đây là header buộc browser cross-origin preflight, không phải secret cần lưu. Nguồn request cũng được đối chiếu với origin hiện tại hoặc allowlist CORS; kết hợp cookie SameSite. Swagger đã thêm trường header với giá trị mặc định 1.

JSON login/refresh:

```json
{
  "accessToken": "<JWT>",
  "accessTokenExpiresAt": "2026-10-01T09:15:00Z",
  "refreshTokenExpiresAt": "2026-10-08T09:00:00Z",
  "tokenType": "Bearer"
}
```

FE bỏ thuộc tính `refreshToken` khỏi DTO public và xóa luồng đọc/ghi token vào localStorage/sessionStorage. Không mã hóa token trong sessionStorage làm phương án dự phòng. JavaScript không cần và không thể đọc header Set-Cookie; trình duyệt tự quản lý cookie.

## Ví dụ fetch

```typescript
const baseUrl = 'http://localhost:5000';
let accessToken: string | null = null;

async function login(usernameOrEmail: string, password: string) {
  const response = await fetch(`${baseUrl}/api/v1/auth/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-Protection': '1' },
    body: JSON.stringify({ usernameOrEmail, password }),
  });
  if (!response.ok) throw await response.json();
  accessToken = (await response.json()).accessToken;
}

// Gọi lúc khởi động/F5 và khi API trả 401; triển khai single-flight trong client thực tế.
async function refreshSession() {
  const response = await fetch(`${baseUrl}/api/v1/auth/refresh-token`, {
    method: 'POST', credentials: 'include',
    headers: { 'X-CSRF-Protection': '1' },
  });
  if (!response.ok) {
    accessToken = null;
    throw await response.json();
  }
  accessToken = (await response.json()).accessToken;
}

async function logout() {
  // Logout vẫn Authorized. Nếu access token đã hết hạn, refresh trước rồi retry một lần.
  const response = await fetch(`${baseUrl}/api/v1/auth/revoke-token`, {
    method: 'POST', credentials: 'include',
    headers: { 'X-CSRF-Protection': '1', Authorization: `Bearer ${accessToken}` },
  });
  accessToken = null;
  if (!response.ok) throw await response.json(); // Không báo đã thu hồi server nếu request thất bại.
}
```

Axios tương đương: `withCredentials: true`; thêm header CSRF cho auth. Các API nghiệp vụ khác vẫn gửi Bearer; không chuyển sang cookie authentication cho Access Token.

- F5: gọi refresh không body, nhận access token mới, sau đó GET `/api/v1/users/me` để khôi phục user/roles/permissions.
- Chỉ cho phép một refresh đang chạy; phối hợp nhiều tab nếu dùng chung phiên. Request refresh dùng token cũ trả 401 sau rotation. Không tự xóa cookie khi refresh thất bại để tránh response đến chậm xóa cookie mới; FE dừng vòng lặp refresh và yêu cầu login khi phiên không còn hợp lệ.
- Logout thu hồi phiên refresh hiện tại. Access Token đã phát vẫn tồn tại đến hết hạn; chưa có JWT blacklist. Nếu thiếu cookie nhưng Bearer hợp lệ, logout trả 204 và gửi xóa cookie idempotent.
- Các lỗi vẫn ProblemDetails tiếng Việt. 403 `iam.csrf_rejected`: header thiếu/sai hoặc Origin không được phép; 401 `iam.invalid_token`: cookie thiếu/hết hạn/đã thu hồi hoặc tài khoản inactive. CORS từ origin lạ có thể hiện lỗi mạng phía trình duyệt vì response không được cấp quyền đọc.

## Cookie và môi trường

- Tên `refreshToken`; `HttpOnly=true`; `Path=/api/v1/auth`; host-only (không đặt Domain); Expires bằng hạn refresh token; `SameSite=Strict` mặc định. Login/refresh/logout response có `Cache-Control: no-store`.
- Development trên localhost/127.0.0.1 cho phép cookie không Secure để dùng HTTP. Có thể bật Secure local bằng `AuthCookie__AllowInsecureLocalhost=false` và dùng HTTPS. Ngoài Development Secure luôn bật; cấu hình cho phép insecure ở production bị chặn khi startup.
- Local FE mặc định cho phép **http://localhost:5173** và **http://localhost:3000**, áp dụng cho IAM và Gateway. Không trộn localhost với 127.0.0.1; cookie phân biệt host, không phân biệt port. Dùng nhất quán một base URL.
- Chưa có domain deploy nên không hardcode domain production. Khi có, cấu hình **cùng danh sách** `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ... ở IAM/Gateway, giá trị origin HTTPS chính xác, không dấu `/` cuối, không wildcard. CORS dùng AllowCredentials, không AllowAnyOrigin.
- Nếu FE/API khác site thực sự, Strict sẽ chặn gửi cookie. Khi đó cân nhắc proxy cùng site; hoặc cấu hình `AuthCookie__SameSite=None` với Secure và allowlist đúng. Third-party cookie có thể bị browser chặn dù SameSite=None. Không bật None với local insecure (startup sẽ từ chối).
- YARP chuyển tiếp Cookie và Set-Cookie; public HTTPS ở reverse proxy vẫn nhận Secure cookie vì ngoài Development không phụ thuộc Request.IsHttps nội bộ.

## Giới hạn bảo mật và kiểm tra

HttpOnly ngăn script đọc cookie nhưng không ngăn XSS thực hiện thao tác bằng phiên của nạn nhân. Access Token trong RAM vẫn có thể bị mã độc cùng trang sử dụng. DevTools trên máy người dùng vẫn nhìn thấy network/cookie; không có cơ chế giấu API khỏi chủ máy. CORS không thay thế JWT/RBAC và không chặn các client ngoài trình duyệt. Phạm vi task này không triển khai thêm WAF/rate limit.

Nguồn tham khảo: [MDN Set-Cookie](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie), [Microsoft ASP.NET Core credentialed CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-8.0).

Kiểm tra lại sau Release build bằng PowerShell 7:

```powershell
pwsh -NoProfile -File scripts/verify-iam-cookie.ps1
dotnet test WardMate.sln -c Release --no-build
```

Script HTTP tự tạo PostgreSQL container riêng và hai process IAM/Gateway tạm, tự dọn khi kết thúc. Không build Docker image, không dùng DB Local Dev, không thêm test project IAM đã bị gỡ. Kiểm tra HTTP/header không thay thế E2E trên browser của FE; FE vẫn cần xác minh credentials/cookie policy trong môi trường chạy thật.
