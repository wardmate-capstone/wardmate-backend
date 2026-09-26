# IAM-005 — API quản trị RBAC

## Chạy và mở Swagger

Sau khi tự build lại IAM/Gateway bằng Docker Compose theo `docs/docker-local.md`, mở `http://localhost:5001/swagger`. Nhóm **Rbac** có 13 operations. Container/image cũ không tự nhận code mới. Chưa build image hoặc khởi động stack ứng dụng trong task này.

Migration `20260926032128_RbacAdministration` tạo bảng `rbac_audit_logs` và đồng bộ sequence ID vai trò để tránh trùng các ID đã seed. AutoMigrate áp dụng khi IAM khởi động nếu được bật. Database Local Dev/Azure chưa được cập nhật trực tiếp trong phiên này; migration đã chạy trong PostgreSQL tạm của test suite.

## Quyền quản trị

Tất cả API `/api/v1/rbac/*` yêu cầu JWT hợp lệ, tài khoản đang hoạt động và vai trò **IT_ADMIN có quyền iam.manage** trong database hiện tại. Có `iam.manage` qua vai trò tùy chỉnh chỉ cho phép quản lý tài khoản/hồ sơ theo policy hiện có, không cho phép quản trị RBAC.

Actor lấy từ `sub` của token; body không có ActorId. API không dựa vào claim role cũ để quyết định quyền quản trị. Với lệnh ghi, quyền của actor được kiểm tra lại sau khi lấy khóa transaction trong PostgreSQL.

## Endpoints

Tất cả dùng `Authorization: Bearer <accessToken>`. Tiền tố `/api/v1/rbac`:

| Method | Route | Request | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/roles` | Query `page=1&pageSize=20`; không body | 200 RbacPage<RoleDto> | 400, 401, 403 |
| GET | `/roles/{roleId}` | Không body | 200 RoleDto | 401, 403, 404 |
| POST | `/roles` | RoleInput | 201 RoleDto + Location | 400, 401, 403, 409 |
| PUT | `/roles/{roleId}` | RoleInput | 200 RoleDto | 400, 401, 403, 404, 409 |
| DELETE | `/roles/{roleId}` | Không body | 204 | 400, 401, 403, 404, 409 |
| GET | `/permissions` | Không body | 200 PermissionDto[] | 401, 403 |
| GET | `/roles/{roleId}/permissions` | Không body | 200 PermissionDto[] | 401, 403, 404 |
| PUT | `/roles/{roleId}/permissions/{permissionId}` | Không body | 204 | 400, 401, 403, 404, 409 |
| DELETE | `/roles/{roleId}/permissions/{permissionId}` | Không body | 204 | 400, 401, 403, 404, 409 |
| GET | `/users/{userId}/roles` | Không body | 200 RoleDto[] | 401, 403, 404 |
| PUT | `/users/{userId}/roles/{roleId}` | Không body | 204 | 400, 401, 403, 404, 409 |
| DELETE | `/users/{userId}/roles/{roleId}` | Không body | 204 | 400, 401, 403, 404, 409 |
| GET | `/audit-logs` | Query `page=1&pageSize=20`; không body | 200 RbacPage<AuditDto> | 400, 401, 403 |

PUT gán quyền/vai trò là idempotent: gán lại liên kết hiện có trả 204. DELETE liên kết đã vắng trả 204 nếu các đối tượng vẫn tồn tại. Không tạo audit dư cho các thao tác không đổi liên kết.

## DTO và validation

RoleInput (tạo hoặc thay tên/mô tả vai trò):

```json
{
  "roleName": "DOCUMENT_REVIEWER",
  "description": "Nhân viên kiểm tra hồ sơ"
}
```

Tên phải bắt đầu bằng chữ cái ASCII, chỉ dùng chữ cái/chữ số/dấu gạch dưới, tối đa 50 ký tự. Tên được chuẩn hóa thành IN HOA; trùng tên trả 409. Mô tả tùy chọn, tối đa 2000 ký tự; chuỗi trắng thành null. PUT thay cả tên và mô tả; bỏ mô tả sẽ xóa mô tả.

- RoleDto: `id`, `roleName`, `description?`, `isSystem`, `permissions`.
- PermissionDto: `id`, `permissionCode`, `permissionName`, `module`.
- RbacPage: `items`, `page`, `pageSize`, `total`. Số trang 1–1000000, pageSize 1–100.
- AuditDto: `id`, `actorUserId`, `action`, `targetUserId?`, `roleId?`, `permissionId?`, `details`, `createdAt` (UTC). `details` là chuỗi JSON mô tả thay đổi; Frontend có thể parse khi cần xem trước/sau.
- JSON tiếp tục bỏ trường null. Validation và ProblemDetails bằng tiếng Việt; lỗi nested RoleInput có key như `input.RoleName` và `input.Description`, lỗi phân trang `page`, `pageSize`.

## Quy tắc an toàn nghiệp vụ

- Năm vai trò hệ thống REGISTERED_CITIZEN, FRONT_DESK_OFFICER, MANAGER, PROCEDURE_MANAGER, IT_ADMIN không được đổi tên hoặc xóa. Có thể sửa mô tả.
- Vai trò tùy chỉnh có thể tạo/sửa/xóa. Phải thu hồi mọi user-role trước khi xóa vai trò đang được dùng; quyền gắn vào vai trò được xóa theo vai trò.
- Không thể thu hồi `iam.manage` khỏi IT_ADMIN.
- Không thể thu hồi IT_ADMIN khỏi người quản trị hoạt động cuối cùng. Tài khoản admin bị khóa không được tính vào số admin hoạt động.
- API khóa tài khoản hiện có cũng dùng cùng khóa transaction và quy tắc admin cuối cùng; vẫn giữ quy tắc không cho tự khóa tài khoản đang dùng.
- Mọi lệnh ghi RBAC và đổi trạng thái tài khoản dùng chung PostgreSQL transaction advisory lock, có tác dụng giữa nhiều instance IAM. Ghi dữ liệu và audit cùng transaction; thất bại thì rollback cả hai. Quy tắc này áp dụng cho API, không thể ngăn người vận hành sửa database trực tiếp.
- Audit ghi actor, hành động, đối tượng, thời gian và snapshot vai trò khi cần; không ghi mật khẩu hoặc token. Không cung cấp API sửa/xóa audit. Các request bị từ chối không tạo bản ghi thay đổi thành công.

Mã lỗi bổ sung: `iam.role_not_found`, `iam.permission_not_found`, `iam.user_not_found` (404); `iam.rbac_conflict`, `iam.system_role_protected`, `iam.role_in_use`, `iam.last_admin`, `iam.admin_permission_protected` (409); `iam.forbidden` (403); `validation_failed` (400). Frontend xử lý theo status/code, không so sánh chuỗi thông báo.

## Quy trình dùng thử

1. Đăng nhập bằng tài khoản IT_ADMIN, bấm Authorize trên Swagger và dán access token.
2. GET permissions để lấy ID quyền, POST roles tạo vai trò nghiệp vụ.
3. PUT role-permission để cấp quyền cho vai trò mới.
4. GET accounts (API IAM-004) để lấy userId; PUT user-role để gán vai trò cho người dùng.
5. Gọi endpoint tương ứng bằng token của người dùng: quyền được đọc từ database nên không cần đăng nhập lại để áp dụng policy.
6. DELETE role-permission hoặc user-role rồi gọi lại để xác nhận bị từ chối. GET audit-logs để xem thay đổi.

JWT đang dùng có thể chứa claim quyền/vai trò cũ, nhưng authorization IAM dùng database hiện tại. Login/refresh kế tiếp phát token có dữ liệu quyền mới. Frontend nên tải lại `/api/v1/users/me` khi cần cập nhật menu; không coi claim cũ là bằng chứng được phép thao tác.

Nếu chưa có IT_ADMIN đầu tiên, người vận hành cần gán một tài khoản đã đăng ký bằng quy trình tin cậy. Ví dụ chạy thủ công trong pgAdmin trên đúng database sau khi thay username (không có API public tự cấp admin):

```sql
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id
FROM users u CROSS JOIN roles r
WHERE u.username = 'username_quan_tri_da_dang_ky'
  AND u.is_active = true
  AND r.role_name = 'IT_ADMIN'
ON CONFLICT DO NOTHING;
```

Xác nhận đúng tài khoản trước khi chạy. Thao tác bootstrap thủ công này không đi qua audit API; không có mật khẩu hoặc tài khoản mặc định được tạo tự động.

## Mở rộng chức năng

Vai trò nghiệp vụ mới được tạo ngay qua API, không cần sửa code. Danh mục permissions là quyền backend đã biết: thêm chức năng mới thì developer bổ sung permission/seed migration và gắn policy vào endpoint, sau đó IT_ADMIN có thể gán quyền đó qua API. Không cung cấp CRUD tùy ý mã quyền vì mã tự tạo chưa có endpoint thực thi.

Gateway đã thêm route `/api/v1/rbac/{**catch-all}` tới IAM. Ngoài Swagger trực tiếp cổng 5001, Frontend có thể gọi API qua cổng 5000 sau khi bạn build/chạy lại Gateway.
