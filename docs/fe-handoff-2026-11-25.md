# Bàn giao FE — sửa BE sau rà soát UI (25/11/2026)

Nguồn lỗi: báo cáo `docs/ui-review-2026-11-18.docx` và lần rà lại trên bản deploy ngày 25/11/2026. **Đã deploy 26/11/2026 (DEPLOY-1126a):** Workflow, DocumentForm, AIOCR, Analytics tag `uireview-20261126-01`, health 200. FE kiểm theo các mục dưới. Script SQL IAM (L29) đã chạy 26/11: `test_mgr_a` đăng nhập được bằng tên.

## 1. Mã QR của hồ sơ không có PDF (L01, DOC-QR-07)
- `GET /api/v1/public/applications/{id}/pdf-redirect` giờ trả **trang HTML** (200 "Mã QR hợp lệ" hoặc 404 "Không tìm thấy hồ sơ") khi không có PDF, thay cho JSON 404.
- FE không cần đổi gì cho người quét bằng điện thoại. Nếu FE tự gọi API này bằng `fetch` (không nên), hãy mở bằng liên kết/thẻ `<a>` thay vì đọc JSON.
- Câu FAQ "Nếu chưa có mã, mở hồ sơ để tạo PDF…" và bước 06 "Tải PDF, QR" nên sửa: thủ tục chỉ có giấy tờ thì không có PDF, QR vẫn dùng được.

## 2. Chế độ "Chỉ xem" hồ sơ chưa nhận (L13, DOC-READ-WARD-01)
- Cán bộ cùng phường đọc được tài liệu của hồ sơ đã gửi mà chưa thụ lý: `GET /api/v1/officer/submissions/by-application/{id}`, `GET /api/v1/applications/{id}/contents`, xem/tải giấy tờ, PDF đã sẵn sàng. Banner đỏ "Hồ sơ không thuộc người đăng nhập" sẽ hết.
- Thao tác ghi vẫn trả 403/409 như cũ nếu chưa nhận xử lý. Bản nháp và hồ sơ của chính cán bộ vẫn không đọc được.

## 3. Tệp đã mất khỏi kho (sau sự cố Azure 20/11)
- Xem/tải giấy tờ hoặc PDF có bản ghi nhưng mất tệp → 404 `code = document.file_missing`, `detail` tiếng Việt "Tệp không còn trên hệ thống lưu trữ. Vui lòng tải lên lại (giấy tờ) hoặc tạo lại (bản PDF)."
- Đề nghị FE: hiện câu đó (không hiện mã kỹ thuật); với công dân ở hồ sơ còn sửa được, gợi ý tải lên lại.
- `document.supporting_document_not_found` / `generated_document_not_found` giữ mã, câu đổi sang tiếng Việt.

## 4. Điều phối không còn bản nháp (L06, DISPATCH-DRAFT-01)
- `GET /api/v1/manager/applications` không trả `DRAFT`; `status=DRAFT` → 400 `dispatch.invalid_query`. FE không gửi `DRAFT` nên không phải sửa.

## 5. Nội dung thông báo (L17, NOTIFY-TEXT-01)
- `content` của thông báo mới nói rõ việc đã xảy ra; nộp lại có tiêu đề riêng ("Công dân đã gửi lại hồ sơ" / "Hồ sơ đã được gửi lại"). Loại (`type`) và `actionUrl` không đổi. Thông báo cũ giữ nguyên chữ.

## 6. Kho kiến thức AI (L07, KB-STALE-01)
- AIOCR tự tắt nguồn "Thủ tục (đồng bộ)" của thủ tục đã ẩn mỗi 60 phút. Trang "Dữ liệu kiến thức AI" sẽ thấy các nguồn đó chuyển "Ngừng dùng". Nguồn PDF/văn bản/FAQ Quản lý thủ tục vẫn tự quản lý. Để chatbot biết 3 thủ tục đang công khai, Quản lý thủ tục bấm "Đồng bộ thủ tục → Tất cả".

## 7. Việc FE (BE không làm)
- **L02:** thêm nút "Tiếp nhận tại quầy" (hook `useOfficiallyReceiveMutation` đã có, chưa dùng).
- Danh sách cán bộ: khi API trả 503 (dịch vụ đang khởi động do `minReplicas: 0`) hiện lỗi + nút "Thử lại" thay cho khung chờ vô hạn.
- Lịch ca bản deploy ghi 07:30–17:00; quy tắc tính hạn là 07:30–16:30.
- Khách chưa đăng nhập bấm "Xem" mẫu đơn bị yêu cầu đăng nhập dù có API tải mẫu công khai.
