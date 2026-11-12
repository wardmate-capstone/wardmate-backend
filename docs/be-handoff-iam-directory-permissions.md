# Bàn giao BE-G — Quyền cán bộ trong danh bạ IAM

Ngày 09/11/2026. Người thực hiện: backend phụ trách IAM. Trạng thái: kế hoạch, chưa triển khai hoặc nghiệm thu.

## Vấn đề cần sửa

Manager cần chọn cán bộ Front Desk để phân công hồ sơ hoặc mời tham vấn. Workflow gọi IAM bằng token Manager để lấy danh bạ, sau đó xét quyền của từng ứng viên. Trong mã nguồn đã kiểm tra, DirectoryUser và truy vấn List chưa trả permissions. Workflow đọc trường bị thiếu thành null và chặn ứng viên.

Bằng chứng Azure: access-context của một Front Desk có workflow.assign và workflow.consultation.respond. Cùng tài khoản đó, assignment-candidates trả HTTP 200 với eligible=false và blockingReasons gồm ASSIGN_PERMISSION_MISSING, CATEGORY_MISMATCH và NO_ACTIVE_SHIFT. Lỗi quyền đã quan sát được ở luồng phân công. Lỗi tham vấn do FE báo và phù hợp với điều kiện trong mã nguồn; cần chạy lại nghiệp vụ để nghiệm thu.

## Phạm vi IAM

1. Trong src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Application/Accounts/UserDirectory.cs, bổ sung mảng Permissions vào DirectoryUser.
2. Trong src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/UserDirectory.cs, cập nhật projection của List. Tính mã quyền từ UserRoles → Role → RolePermissions → Permission.PermissionCode của từng người trong kết quả, loại trùng như Access(). Tài khoản không có quyền trả mảng rỗng. Kiểm tra truy vấn thực trên PostgreSQL, tránh truy vấn riêng cho từng cán bộ.
3. Hai endpoint dùng DTO chung là GET /api/v1/manager/officers và GET /api/v1/admin/users. Bổ sung items[].permissions, giữ nguyên các trường cũ, phân trang, bộ lọc, quyền đọc và phạm vi phường. Không dùng quyền của Manager đang gọi thay cho quyền của người được liệt kê.
4. Không cần migration hoặc SQL cấp thêm quyền để sửa lỗi hợp đồng. Giữ kiểm tra quyền trong Workflow. Cập nhật docs/api-guide.md và PROGRESS.md khi thực hiện.

## Thứ tự nghiệm thu

Dùng Manager và hai Front Desk A, B trong cùng phường. Đối chiếu bằng userId vì có cán bộ trùng tên. Dùng tài khoản và hồ sơ thử nghiệm cho các thao tác thay đổi trạng thái.

| Bước | Thao tác | Kết quả đạt |
|---|---|---|
| 1 | Với token A, gọi GET /api/v1/users/access-context. Với token Manager, gọi GET /api/v1/manager/officers, tìm A theo tên rồi đối chiếu id. | Hai tập quyền của A khớp nhau, bỏ qua thứ tự. Nếu A được cấp hai quyền trên, danh bạ phải trả đủ cả hai. |
| 2 | Với IT Admin gọi GET /api/v1/admin/users. Kiểm tra người có nhiều role, người không có quyền và trang rỗng. Kiểm tra lại phạm vi Manager. | Không trùng mã quyền, không trả null cho permissions, trang rỗng và phạm vi phường hoạt động như trước. |
| 3 | Với Manager gọi GET /api/v1/manager/applications/{applicationId}/assignment-candidates trên hồ sơ dùng để tái hiện lỗi. | ASSIGN_PERMISSION_MISSING biến mất với A có workflow.assign. Các lý do thật như CATEGORY_MISMATCH hoặc NO_ACTIVE_SHIFT vẫn hiển thị nếu điều kiện chưa đạt. |
| 4 | Dùng hồ sơ SUBMITTED đúng lĩnh vực của A. A đang trong ca chính thức, không nghỉ đã duyệt, không phải chủ hồ sơ. Gọi POST /api/v1/manager/applications/{applicationId}/assignments theo hợp đồng Workflow. | A đủ điều kiện, phân công thành công, hồ sơ chuyển UNDER_REVIEW và có lịch sử phân công. |
| 5 | Manager gọi POST /api/v1/applications/{applicationId}/consultations với B là cán bộ khác A, đủ chuyên môn, đúng phường, đang trong ca, có workflow.consultation.respond và dùng phiên bản phân công hiện tại. | Tham vấn được tạo cho B; B đọc và trả lời được bằng tài khoản của mình. |
| 6 | Trên local dùng tài khoản thử thiếu workflow.assign hoặc workflow.consultation.respond; kiểm tra thêm cán bộ ngoài ca. | Hệ thống vẫn chặn đúng nghiệp vụ. Không thu hồi quyền của vai trò đang dùng trên production để thực hiện ca test. |

## Phát hành và bàn giao kết quả

Chạy build Release toàn solution, yêu cầu 0 lỗi, 0 cảnh báo. Test Docker cả danh bạ và luồng sử dụng trước khi phát hành. Antigravity quản lý Git, xin xác nhận trước khi push theo quy trình nhóm và triển khai riêng wardmate-iam theo docs/antigravity-azure-runbook.md. Ghi lại image trước triển khai, kiểm tra /health/ready, chạy lại bước 1 và 3 trên Azure. Phân biệt kết quả xác minh hợp đồng đọc với kết quả kiểm thử phân công/tham vấn bằng dữ liệu thử.

Bàn giao cho FE: phiên bản IAM đã kiểm tra, kết quả từng bước, response đã ẩn dữ liệu cá nhân và traceId nếu còn lỗi. Tên trường lý do trong Workflow là blockingReasons.
