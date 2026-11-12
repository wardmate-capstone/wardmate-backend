# Bàn giao Backend → Frontend: tách "Xuất bản" khỏi "Công khai", hủy xuất bản (12/11/2026)

Người viết: Backend (Claude), 12/11/2026. Hợp đồng: `docs/api-guide.md` API 36b (và API 36). Quy tắc: `docs/system-constraints.md` PROC-UNPUBLISH-01.

## 0. Trạng thái

ProcedureCatalog — **chưa deploy**; cần chạy SQL `docs/sql/catalog-procedure-unpublish.sql` trước. BE báo tag image sau khi deploy.

## 1. Thay đổi

- **Xuất bản bản nháp không còn công khai ngay.** Thủ tục mới tạo từ bản nháp có `isActive: false`. Muốn người dân thấy, bấm **"Công khai"** (gọi `PATCH /api/v1/procedure-manager/procedures/{id}/status` với `{ "isActive": true }`). Xuất bản đè lên thủ tục đã có (trùng mã) không đổi trạng thái hiện tại.
- **Chi tiết thủ tục có thêm `firstPublishedAt`** (thời điểm công khai lần đầu; `null` = người dân chưa từng thấy). Thủ tục có trước 12/11 đều đã có giá trị.
- **API mới — Hủy xuất bản:** `POST /api/v1/procedure-manager/procedures/{id}/unpublish` (quyền `procedure.publish`, không body).
  - Chỉ dùng khi `firstPublishedAt == null` và `isActive == false`.
  - Kết quả 200 `true`: thủ tục và lịch sử phiên bản bị xóa hẳn; bản nháp PDF tạo ra nó quay về `NeedsReview` (sửa, xuất bản lại hoặc xóa được).
  - Lỗi: 404 `procedure.not_found`; 409 `procedure.already_public` (đang/đã từng công khai — chỉ ẩn được bằng API status).

## 2. FE cần làm

- Màn xuất bản bản nháp: sau khi xuất bản, báo "Đã xuất bản, chưa công khai" và gợi ý bấm **"Công khai"**.
- Màn chi tiết thủ tục (quản trị):
  - Nút **"Công khai" / "Ẩn"** như hiện tại (API status).
  - Nút **"Hủy xuất bản"** chỉ hiện khi `firstPublishedAt == null && !isActive`; hỏi xác nhận (xóa không hoàn tác); thành công thì quay về danh sách bản nháp.

## 3. Kiểm tra sau deploy

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Xuất bản một bản nháp mới | Thủ tục `isActive: false`, `firstPublishedAt: null`; cổng công dân không thấy |
| 2 | Hủy xuất bản thủ tục ở bước 1 | 200; thủ tục biến mất; bản nháp về `NeedsReview` |
| 3 | Xuất bản lại bản nháp, rồi bấm "Công khai" | `isActive: true`, `firstPublishedAt` có giá trị |
| 4 | Ẩn thủ tục ở bước 3, gọi hủy xuất bản | 409 `procedure.already_public` |
| 5 | Hủy xuất bản thủ tục có trước 12/11 | 409 `procedure.already_public` |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi.

## 4. AI bóc tách thủ tục (AI-02b, chưa deploy AIOCR)

Không đổi API hay DTO. Kết quả bản nháp từ AI thay đổi như sau:

- Thủ tục chia thành phần hồ sơ theo "Trường hợp N:" (ví dụ 1.001939): `contentPayload.cases[]` có một phần tử cho mỗi trường hợp, `caseCode` dạng `TH-2`, `TH-3`… (giữ số của nguồn, có thể nhảy số), `caseName` là nguyên văn tiêu đề, `steps` có thể rỗng. Giấy tờ và mẫu trong từng bảng mang `caseCode` tương ứng. FE nên hiển thị `caseName` thay vì `caseCode` ở thẻ "Áp dụng: Trường hợp …".
- Một dòng giấy tờ có nhiều mẫu (ví dụ 1.014748, Mẫu số 10 và Mẫu số 11): chỉ một giấy tờ trong `checklistSchema`, `formCodes` có đủ hai mẫu; `formDefinitions` vẫn có hai phần tử, mẫu thay thế `isMandatory: false`.
- **Cần FE làm:** ở thẻ biểu mẫu đã chọn (bước 5 "Biểu mẫu", `ProcedureFormPicker`), tên biểu mẫu đang chỉ hiển thị, không sửa được. BE nhận và lưu nguyên `formDefinitions[i].formName` qua PUT bản nháp (bắt buộc khác rỗng, không ghi đè theo tên tệp), nên FE chỉ cần thêm ô nhập `formName` (và nếu muốn, `formCode`) trong phần "Thiết lập sử dụng mẫu".

## 5. Đánh dấu tất cả thông báo đã đọc (NOTIFY-READALL-01, đã deploy 12/11, Analytics `analytics-20261112-02`; chờ FE kiểm #1–#6)

- Hub `/hubs/notifications` có phương thức mới `MarkAllRead(before?)`, trả về số thông báo vừa đánh dấu (0 nếu không có gì, không phải lỗi).
- Nút "Đánh dấu tất cả là đã đọc" (chuông và trang thông báo): bỏ vòng lặp `MarkRead`, gọi một lần `await connection.invoke("MarkAllRead", before)`. `before` nên là `createdAt` của thông báo mới nhất đang hiển thị (hoặc `new Date().toISOString()`), để thông báo đến sau lúc bấm vẫn chưa đọc; `null` để đánh dấu tất cả.
- Phạm vi giống `GetUnreadCount`: cả thông báo chưa tải về cũng được đánh dấu; thông báo của phường khác (đã chuyển phường) giữ nguyên.
- Sau khi gọi: đặt `isRead = true` cho mục đang hiển thị có `createdAt <= before` (hoặc tải lại `GetNotifications`). Chuông nhận `UpdateUnreadCount` ở mọi tab; có thể nhận cùng số hai lần, chỉ cần gán số.
- BE báo tag image AnalyticsSystem sau khi deploy. Kiểm tra theo bảng #1–#6 FE đã gửi.
