# WardMate — ba trang ERD toàn hệ thống

Mở **wardmate-erd.drawio** bằng diagrams.net → File → Open From → Device. Có đúng 3 tab: **Conceptual**, **Logical**, **Physical**. Không có tab theo service.

Mỗi trang chứa toàn bộ 6 service và các quan hệ xuyên service. Nét liền là FK thật; nét đứt là REF logic, không phải FK xuyên database. Các ô FUTURE là 6 bảng dự kiến, chưa có schema triển khai. R01… đối chiếu danh mục quan hệ ngay dưới mỗi hình.

- [Conceptual SVG](conceptual.svg)
- [Logical SVG](logical.svg)
- [Physical SVG](physical.svg)

SVG có thể phóng to mà không vỡ chữ. Physical là canvas lớn để chứa toàn bộ cột của mọi bảng; không ép về một trang A4. Màu tiêu đề chỉ ra service sở hữu bảng, không tách thành sơ đồ riêng.

Tái sinh bằng node docs/diagrams/erd/generate-erd.cjs (đọc DDL đã xuất), sau đó node docs/diagrams/erd/generate-drawio.cjs. Các file physical-*.mmd và archive-* là nguồn tham khảo/bản cũ, không phải bộ 3 trang bàn giao.

DDL schema/*.sql chỉ để tham khảo, không phải migration nâng cấp. Không tạo FK xuyên service. Chưa xác nhận trạng thái DB Azure.
