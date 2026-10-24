# WardMate — Logical ERD

Cập nhật 24/10/2026 (UTC+07:00). Bản tương tác và draw.io tiếng Anh: [wardmate-erd-interactive.html](diagrams/erd/wardmate-erd-interactive.html), [wardmate-erd-en.drawio](diagrams/erd/wardmate-erd-en.drawio).

Mỗi trang chứa toàn bộ 6 service và các quan hệ xuyên service. Nét liền là FK thật; nét đứt là REF logic, không phải FK xuyên database. Các ô FUTURE là 6 bảng dự kiến, chưa có schema triển khai. R01… đối chiếu danh mục quan hệ ngay dưới mỗi hình.

41 bảng hiện có, 6 bảng dự kiến. Hiển thị thuộc tính và khóa; không đưa kiểu lưu trữ SQL vào từng thuộc tính.

[ Mở hình SVG để phóng to ](diagrams/erd/logical.svg) · [File draw.io — chọn trang Logical](diagrams/erd/wardmate-erd.drawio)

![Logical ERD](diagrams/erd/logical.svg)

Nguồn: [EF model inventory](diagrams/erd/model-inventory.json). [DDL đầy đủ](diagrams/erd/schema/) giữ default, index, CHECK và ON DELETE; đây là script CREATE để tham khảo, không chạy lên DB hiện hữu. Sơ đồ đối chiếu source, không xác nhận migration trên Azure. Không tạo bảng hay sửa code backend trong lượt này.
