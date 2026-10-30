# Claude — WardMate backend

Đọc và tuân thủ quy tắc dự án trước mọi thay đổi:

@AGENTS.md

## Bắt đầu phiên

1. Đọc `docs/CLAUDE_HANDOFF.md`, đặc biệt mục “Việc còn mở”.
2. Đọc phần cuối `PROGRESS.md` để biết thay đổi sau ngày bàn giao.
3. Đọc `docs/api-guide.md` và `docs/system-constraints.md` theo chức năng được giao; đối chiếu code và migration.
4. Nếu chưa có task cụ thể, báo cáo ngắn tình trạng và chờ giao việc; không tự triển khai toàn bộ backlog.

## Quy tắc bắt buộc cho Claude

- Chỉ làm backend theo phạm vi người dùng giao; .NET 8, Clean Architecture, database-per-service.
- KHÔNG chạy bất kỳ lệnh Git nào, kể cả status/diff/log. Antigravity phụ trách Git; các ghi chép cho phép Git trong lịch sử không phải quyền hiện tại.
- Không ghi đè thay đổi của người khác. Không sửa cùng file khi Codex/Antigravity đang sửa; bàn giao tuần tự.
- Không tạo lại test project. Khi thay code, chạy `dotnet build WardMate.sln -c Release -warnaserror -m:1`, báo đúng lỗi/cảnh báo. FE phụ trách kiểm thử giao diện; build không chứng minh runtime hoạt động đúng.
- Cập nhật PROGRESS.md sau mỗi phiên; API đổi thì cập nhật docs/api-guide.md; ràng buộc đổi thì cập nhật docs/system-constraints.md và giữ ID quy tắc.
- Không tự deploy, migrate cloud, thay billing/model hoặc gửi Email/SMS thật nếu task hiện tại chưa cho phép.
- Không sao chép .env, password, API key, token hoặc dữ liệu người dùng vào tài liệu/log/chat. Chỉ nêu tên cấu hình, không in giá trị secret.
- Báo cáo file thay đổi, kiểm tra đã chạy, giới hạn còn lại và commit message đề xuất; không thực thi commit.

## Quyết định nghiệp vụ không được tự đảo ngược

- Workflow hỗ trợ chuẩn bị/kiểm tra giấy tờ; DOCUMENTS_READY không phải phê duyệt hành chính.
- OFFICIALLY_RECEIVED ghi nhận tiếp nhận tại quầy. Không khôi phục luồng approve/reject hành chính từ tài liệu cũ.
- Quyền xem khác quyền sửa: MANAGER giám sát trong phường, không tự có quyền chuyển trạng thái cán bộ.
- Checklist hồ sơ là snapshot; không tự đồng bộ thay đổi thủ tục vào hồ sơ cũ.
- Deadline tiền kiểm: 45 giờ làm việc, thứ Hai–thứ Sáu 07:30–16:30 UTC+07; pause khi chờ bổ sung. Dữ liệu cũ thiếu căn cứ giữ UNKNOWN.
- SMS đã bị gỡ khỏi hệ thống (29/10/2026, cần giấy phép kinh doanh): OTP và thông báo chỉ qua Email/IN_APP. Chỉ dùng dịch vụ miễn phí. Chat có RAG (TASK-29) nhưng mặc định tắt.

## Kết thúc / bàn giao lại

Cập nhật `docs/CLAUDE_HANDOFF.md` khi trạng thái thay đổi và thêm mục mới vào PROGRESS.md: task, thời gian UTC+07, file, đã xong/chưa xong, bước tiếp theo, build và migration/config còn chờ. Phân biệt source / kiểm tra local / deploy / nghiệm thu FE. Không coi nội dung lịch sử là kết quả vừa kiểm tra.
