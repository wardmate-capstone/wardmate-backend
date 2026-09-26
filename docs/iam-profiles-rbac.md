# IAM-004 — Hồ sơ, RBAC và quản lý trạng thái tài khoản

## Quyền và middleware

IAM dùng `UseAuthentication()` rồi `UseAuthorization()` của ASP.NET Core. Các policy được đăng ký tập trung trong `PermissionAuthorization`; handler đọc quyền hiện tại từ `user_roles -> roles -> role_permissions -> permissions`. JWT đã ký hợp lệ vẫn bị từ chối nếu tài khoản đã bị khóa hoặc không còn tồn tại. Quyền bị thu hồi có hiệu lực ở yêu cầu tiếp theo, không phải chờ JWT hết hạn.

| Quyền | Vai trò mặc định | Chức năng |
|---|---|---|
| `iam.profile.read` | Cả 5 vai trò | Xem tài khoản/hồ sơ cá nhân |
| `iam.profile.write` | Cả 5 vai trò, bao gồm REGISTERED_CITIZEN | Tạo, sửa, xóa hồ sơ cá nhân |
| `iam.manage` | IT_ADMIN | Quản lý mọi hồ sơ và trạng thái tài khoản |

Không có quyền vượt qua policy ngầm theo tên vai trò. IT_ADMIN được quyền thông qua dữ liệu role-permission. Không cho client gửi role hoặc permission để tự cấp quyền. Không tạo tài khoản quản trị hoặc mật khẩu mặc định; quản trị viên cần được gán vai trò bằng quy trình vận hành tin cậy.

## API hồ sơ

Cùng bộ HTTP methods áp dụng cho hai route:

- Cá nhân: `/api/v1/users/me/profile` — userId lấy từ token, không lấy từ body.
- Quản trị: `/api/v1/users/{userId}/profile` — yêu cầu `iam.manage`.

| Method | Body | Thành công | Lỗi |
|---|---|---|---|
| GET | Không | 200 UserProfileDto | 401, 403, 404 |
| POST | ProfileInput | 201 UserProfileDto + Location | 400, 401, 403, 404, 409 |
| PUT | ProfileInput | 200 UserProfileDto | 400, 401, 403, 404, 409 |
| DELETE | Không | 204 | 401, 403, 404, 409 |

```json
{
  "fullName": "Nguyễn Văn An",
  "identityNumber": "012345678901",
  "phoneNumber": "0901234567",
  "dateOfBirth": "2000-01-02",
  "gender": "Nam",
  "permanentAddress": "Địa chỉ thường trú",
  "temporaryAddress": null
}
```

`fullName` bắt buộc, tối đa 255 ký tự; số định danh và điện thoại tối đa 20; giới tính tối đa 10; mỗi địa chỉ tối đa 4000. Ngày sinh không được ở tương lai theo ngày Việt Nam (UTC+7). Điện thoại cho phép chữ số, dấu cộng ở đầu, khoảng trắng, dấu ngoặc và dấu gạch ngang. Không áp đặt độ dài CCCD lên mọi loại giấy tờ định danh.

PUT thay toàn bộ hồ sơ: trường tùy chọn bị bỏ qua/null/chuỗi trắng được xóa về null. Chuỗi được trim trước khi lưu. Số định danh phải duy nhất khi có giá trị. JSON response tiếp tục bỏ trường null.

Đăng ký đã tạo hồ sơ: POST trên hồ sơ hiện có trả 409. DELETE chỉ xóa `user_profiles`, giữ tài khoản, vai trò và token. GET hồ sơ sau xóa trả 404; GET `/api/v1/users/me` vẫn trả 200 nhưng không có thuộc tính `profile`. POST cho phép tạo lại hồ sơ.

## API tài khoản — IT_ADMIN

Tất cả yêu cầu `iam.manage`:

| Method | Route | Body/query | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/v1/accounts` | `page=1&pageSize=20` | 200 AccountPage | 400, 401, 403 |
| GET | `/api/v1/accounts/{userId}` | Không | 200 AccountDto | 401, 403, 404 |
| PUT | `/api/v1/accounts/{userId}/status` | `{ "isActive": false }` để khóa; true để mở khóa | 204 | 400, 401, 403, 404, 409 |

AccountPage gồm `items`, `page`, `pageSize`, `total`; pageSize từ 1 đến 100. AccountDto chỉ gồm `id`, `username`, `email`, `isActive`, `createdAt`, không chứa password hash hoặc token. Không cho quản trị viên tự khóa tài khoản của mình (`iam.self_disable`, 409).

Ban/Disable dùng chung `IsActive=false`; chưa có thời hạn ban tự động. Khóa tài khoản và thu hồi mọi refresh token thực hiện trong cùng transaction. Cấp token khóa hàng user để tuần tự hóa với thao tác khóa tài khoản, tránh token mới lọt qua yêu cầu khóa đang chạy. Mở khóa không khôi phục refresh token cũ. JWT cũ bị từ chối trong thời gian khóa; nếu mở khóa trước khi JWT hết hạn thì JWT đó có thể dùng lại. Frontend nên yêu cầu đăng nhập lại sau lỗi 401.

## JWT và refresh_tokens

Giữ cơ chế hiện có: JWT HS256 xác minh issuer/audience/signature/expiry; refresh token ngẫu nhiên được lưu bằng SHA-256 digest, xoay vòng một lần và thu hồi khi logout. Các API `/auth/login`, `/auth/refresh-token`, `/auth/revoke-token` tiếp tục quản lý vòng đời token. Không công khai API sửa tùy ý hàng refresh_tokens hoặc đọc token/hash qua API quản trị.

## Frontend và vận hành

- Dùng `Authorization: Bearer <accessToken>` cho tất cả endpoint mới.
- 401: chưa đăng nhập/token không hợp lệ/tài khoản khóa. 403: thiếu quyền (`iam.forbidden`).
- Lỗi hồ sơ: `iam.profile_not_found`, `iam.user_not_found`, `iam.profile_exists`, `iam.profile_conflict`, `iam.profile_changed`. Validation: `validation_failed`, trường hồ sơ có key như `profile.fullName`; trạng thái dùng `isActive`.
- Thông báo lỗi tiếng Việt. Dựa vào `code` và HTTP status để xử lý, không so sánh chuỗi thông báo.
- Migration `20260926014941_ProfileWritePermission` thêm quyền ghi hồ sơ và gán cho 5 vai trò; không xóa dữ liệu. AutoMigrate áp dụng khi IAM khởi động nếu được bật.
- Gateway đã cấu hình `/api/v1/accounts/{**catch-all}`; route users hiện có bao phủ API hồ sơ. Local: Gateway cổng 5000, IAM cổng 5001, Swagger tại `http://localhost:5001/swagger` khi chạy Development.
- Migration đã được kiểm thử trong PostgreSQL tạm của test suite. Chưa áp dụng riêng vào database Local Dev đang chạy hoặc triển khai Azure trong task này.
