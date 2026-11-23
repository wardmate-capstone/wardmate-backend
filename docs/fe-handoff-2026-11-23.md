# Bàn giao Backend → Frontend: căn cứ pháp lý 4 cột (23/11/2026)

Người viết: Backend (Claude), 23/11/2026. Hợp đồng: `docs/api-guide.md` (mục căn cứ pháp lý). Quy tắc: `docs/system-constraints.md` PROC-LEGAL-01.

## 0. Trạng thái

**Chưa deploy** Catalog và AIOCR. Không có SQL. API, đường dẫn và tên trường không đổi.

## 1. Dữ liệu

`contentPayload.legalReferences[]` (tạo/sửa thủ tục, bản nháp, chi tiết thủ tục) có 4 trường, **đều được `null`**:

| Cột Cổng DVC (4 cột) | Cột 2 cột | Trường | Ghi chú |
|---|---|---|---|
| Số ký hiệu | Mã văn bản | `documentNumber` | ≤ 255 ký tự |
| Trích yếu | Tên văn bản pháp lý | `documentName` | |
| Ngày ban hành | — | `issueDate` | `yyyy-MM-dd` |
| Cơ quan ban hành | — | `authority` | |

- Mỗi dòng cần **số ký hiệu hoặc trích yếu**; thiếu cả hai → 400, lỗi ở khóa `ContentPayload.LegalReferences`.
- Ô để trống: gửi `null` hoặc chuỗi rỗng; BE lưu thành `null`.
- AI bóc tách PDF: ngày dạng `29-11-2019` được đổi thành `2019-11-29`; không có ngày/cơ quan thì `null`, không cảnh báo.

## 2. FE cần làm

- **Màn nhập (bản nháp, sửa thủ tục):** bảng 4 cột Số ký hiệu, Trích yếu, Ngày ban hành (ô chọn ngày, có thể để trống), Cơ quan ban hành. Không bắt buộc ô nào, chỉ báo lỗi khi cả Số ký hiệu và Trích yếu đều trống.
- **Màn xem (cổng công dân và quản trị):** nếu có ít nhất một dòng có `issueDate` hoặc `authority` thì hiện bảng 4 cột (ô `null` để trống, ngày hiển thị dd-MM-yyyy); ngược lại giữ bảng 2 cột "Tên văn bản pháp lý / Mã văn bản" như hiện nay.

## 3. Kiểm tra sau deploy

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Lưu thủ tục có dòng đủ 4 cột | 200; xem lại đủ 4 cột |
| 2 | Lưu dòng chỉ có Số ký hiệu (3 cột còn lại trống) | 200; 3 trường `null` |
| 3 | Lưu dòng chỉ có Cơ quan ban hành | 400 `ContentPayload.LegalReferences` |
| 4 | Thủ tục cũ chỉ có 2 cột | Hiển thị như cũ |
| 5 | Bóc tách PDF có bảng 4 cột (ví dụ thủ tục đăng ký hộ kinh doanh) | Có ngày ban hành dạng ISO, cơ quan ban hành |
