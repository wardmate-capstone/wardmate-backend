# Rà soát kế hoạch calendar closures — 2026-11-01

Trạng thái: đề xuất, chưa triển khai. Đối chiếu đặc tả bên ngoài với StaffingService, StaffingController, ReviewDeadline, WorkflowDbContext và migration AddStaffingTablesOnly hiện tại.

## Phần phù hợp
- Đặt lịch đóng cửa trong Workflow; phân biệt nghỉ của cán bộ với đóng cửa cả phường.
- Giữ snapshot chính sách hồ sơ, bổ sung lịch hiệu lực bên ngoài snapshot.
- Manager chỉ quản lý phường từ access-context; IT_ADMIN quản lý lịch toàn quốc bằng quyền riêng.
- Hủy mềm, chống trùng ngày còn hiệu lực, hợp lịch toàn quốc/phường để không tính hai lần.
- Controller riêng tránh policy staffing.read cấp class chặn IT_ADMIN.

## Cần sửa trước khi lập trình
1. Ngày đóng cửa khai báo giữa ngày cần lưu thời điểm hiệu lực UTC, không chỉ DateOnly. Tập ngày nghỉ không biểu diễn được phần ngày còn lại và có thể loại sai giờ đã trôi qua khi tính lại hồ sơ tạm dừng. Dùng khoảng đóng cửa giao với cửa sổ làm việc.
2. Resume cần bảo toàn ngân sách giờ làm còn lại tại lúc pause rồi tính hạn từ lúc resume theo lịch hiệu lực. Không chỉ cộng WorkingTicks(paused, now) vào due cũ: nếu due cũ nằm trong ngày vừa đóng cửa, phép cộng có thể kéo dài sai. Phải kiểm thử cả ngày đóng cửa nằm trước, trong và sau khoảng pause, có nhiều lần pause/resume.
3. Nếu khai báo rồi hủy mà thời gian đã trôi qua, không hứa deadline luôn quay về giá trị ban đầu; cần định nghĩa rõ cách xử lý trường hợp đã quá hạn hoặc đang pause. Không thay đổi deadline hồ sơ đã hoàn thành/quá hạn theo quy tắc đã chốt.
4. Chốt thứ tự khóa thống nhất cho Start, Resume và thay đổi closure. Khóa riêng thao tác closure không ngăn submit đọc lịch cũ. Không nạp lịch trước khóa rồi dùng như dữ liệu chắc chắn còn mới. Cần thiết kế khóa chung/shared-exclusive hoặc kiểm tra phiên bản lịch trong transaction.
5. Không mặc định thứ Bảy/Chủ nhật không làm: dùng WorkingDays trong snapshot từng hồ sơ. Giờ 07:30–16:30 hiện là 9 giờ liên tục; muốn trừ nghỉ trưa phải đổi mô hình cửa sổ làm việc và chốt nghiệp vụ riêng.
6. Chốt ảnh hưởng đến staffing: đóng cửa có chặn tạo ca, nhận hồ sơ và phân công không? Đặc tả hiện chỉ điều chỉnh deadline, chưa làm các kiểm tra đó.
7. Hiện chưa tìm thấy docs/sql/workflow-staffing-tables.sql trong checkout. Không dựa vào script mẫu được nhắc là đã tồn tại. Đối chiếu schema và lịch sử migration thực tế trước khi tạo SQL; không đánh dấu migration đã chạy chỉ vì có tên bảng.
8. DeadlineHistory hiện là JSON trên ApplicationRecord, không phải bảng deadline_history riêng. Ghi sự kiện phải giữ cấu trúc hiện có, nên bổ sung closureId để truy vết.

## API đề xuất giữ như đặc tả
- GET /api/v1/staffing/closures?from=&to=: xem lịch trong phạm vi.
- POST /api/v1/staffing/closures: Manager tạo ngày đóng cửa phường.
- POST /api/v1/staffing/closures/national: IT_ADMIN tạo ngày lễ toàn quốc.
- POST /api/v1/staffing/closures/{id}/cancel: hủy ngày tương lai trong phạm vi.
- Body tạo: {"date":"2026-11-20","reason":"Lý do đóng cửa"}; hủy: {"reason":"Lý do hủy"}.
- Đây là đề xuất, chưa có endpoint mới được triển khai.

## Chẩn đoán /api/v1/staffing/leaves trả 500
- GET và POST dùng chung path, cần method + query/body + môi trường + exception log để kết luận.
- GET đọc officer_leaves; POST khóa phường, kiểm tra trùng và ghi officer_leaves. Dữ liệu khoảng ngày không hợp lệ được trả 400 trong service; lỗi dependency HTTP/JSON được controller chuyển 503.
- DbContext và migration 20261029161554_AddStaffingTablesOnly đều có officer_leaves. Chưa xác minh database runtime đã áp dụng migration hoặc có đúng kiểu cột.
- Program chỉ migrate khi Database:AutoMigrate=true. Health hiện không đăng ký kiểm tra database, nên health thành công không chứng minh schema đúng.
- Không tìm thấy trace 66d89a142bbfe97e2db1f2619729d907 trong các file log/txt local đã tìm. Chưa tái hiện và chưa sửa lỗi 500; không chuyển lỗi database thành thành công rỗng.
- Kiểm tra read-only trên đúng DB Workflow: information_schema.columns cho officer_leaves và lịch sử EF; log PostgreSQL 42P01 là thiếu relation, 42703 là thiếu cột. Đây là các hướng kiểm tra, không phải nguyên nhân đã xác nhận.

## Cập nhật triển khai local 2026-11-01
Đã thêm 4 API CalendarClosuresController, entity/migration, quyền IT_ADMIN và thuật toán khoảng nghỉ (hỗ trợ giữa ngày). Giữ 18 API cũ. Snapshot policy không đổi; closure overlay + shared/exclusive lock trước row lock. Chi tiết hợp đồng tại docs/api-guide.md, rule DEADLINE-CLOSURE-01 trong docs/system-constraints.md. Không tự chặn staffing/assignment. Build solution Release 0 lỗi/0 cảnh báo; 15 kiểm tra console thuật toán/DI đạt. Docker image calendar-local đã build; trạng thái kiểm thử HTTP xem PROGRESS.md (không suy ra đạt từ build image). Chưa deploy Azure.

Kết quả Docker bổ sung 2026-11-01: image calendar-local build thành công; 46/46 kiểm tra HTTP/schema trên Docker Workflow + PostgreSQL disposable đạt, dùng IAM/Catalog giả lập. Ba API danh sách manager/applications, staffing/leaves, staffing/consultations đều 200. Chưa xác minh Azure/IAM thật; không coi đây là test đủ tất cả nghiệp vụ 18 API.
