# Bàn giao FE — lỗi giao diện còn lại sau rà soát (26/11/2026)

Nguồn: rà soát bản deploy `https://wardmate-frontend.vercel.app` ngày 25–26/11/2026 (cả 6 khu: Công khai, Người dân, Cán bộ, Quản lý, Quản lý thủ tục, Admin), sau khi BE deploy FIX-UIREVIEW-1125. Báo cáo gốc kèm ảnh: `docs/ui-review-2026-11-18.docx`. Phần BE đã sửa và đã kiểm trên Azure: `docs/fe-handoff-2026-11-25.md` (QR không PDF, Chỉ xem hồ sơ chưa nhận, điều phối bỏ DRAFT, lỗi tệp mất tiếng Việt, kho kiến thức AI).

Mức: **Cao** = chặn hoặc sai nghiệp vụ; **Vừa** = gây hiểu sai, khó dùng; **Thấp** = giao diện, câu chữ.

## A. Mức cao

| # | Khu | Lỗi | Cách tái hiện | Mong muốn |
|---|---|---|---|---|
| FE-01 (L02) | Cán bộ | Không có nút **"Tiếp nhận tại quầy"** ở bất kỳ màn nào → hồ sơ dừng ở "Giấy tờ sẵn sàng", không đạt `OFFICIALLY_RECEIVED`; ô "Đã hoàn tất" của công dân luôn 0. | Mở hồ sơ Giấy tờ sẵn sàng (HS-20261118-00000062) bằng cán bộ thụ lý. | Thêm nút + hộp nhập (số biên nhận, ngày hẹn) dùng `useOfficiallyReceiveMutation` (`src/hooks/useWorkflow.ts:191`, API `POST /api/v1/officer/applications/{id}/officially-received` đã có, xem `docs/api-guide.md`). |
| FE-02 | Cán bộ | Khi API trả **503** (service đang khởi động do `minReplicas: 0`), danh sách "Tất cả hồ sơ" hiện "Tìm thấy 3 hồ sơ" nhưng các dòng là khung chờ mãi, không báo lỗi, không tự thử lại. | Mở `/officer` → Tất cả hồ sơ sau một lúc không ai dùng (lần gọi `?Scope=my-categories` trả 503). | Hiện thông báo lỗi + nút "Thử lại" (hoặc tự thử lại 1–2 lần có giãn cách) cho mọi danh sách dùng React Query. Admin Tổng quan đã có "Thử lại" — làm tương tự. |
| FE-03 | Cán bộ | Lịch ca ghi **"Khung giờ mặc định: Thứ Hai–Thứ Sáu: 07:30–17:00"**, lệch quy tắc tính hạn 45 giờ (07:30–16:30). Bản local ngày 19/11 ghi 16:30 → bản deploy khác hoặc đã bị đổi lại. | `/officer` → Lịch ca làm. | 07:30–16:30, không Thứ Bảy (DEADLINE rule trong `docs/system-constraints.md`). |
| FE-04 | Cán bộ | **Xem nhanh** hiện giấy tờ "Đã nộp" cả khi tệp đã mất (sau sự cố blob 20/11). | HS-20261109-00000050 → Xem nhanh. | Dùng cờ `fileExists` của `GET /api/v1/applications/{id}/contents` (đã có): hiện "Tệp không còn trên hệ thống" thay vì "Đã nộp". |

## B. Mức vừa

| # | Khu | Lỗi | Mong muốn |
|---|---|---|---|
| FE-05 | Công khai | Khách chưa đăng nhập bấm **"Xem"** mẫu đơn ở trang chi tiết thủ tục → hộp "Cần đăng nhập", trong khi câu hướng dẫn ngay trên bảng nói khách xem được. | Dùng API công khai `GET /api/v1/public/form-templates/{id}/download-docx` (BE-J) để xem/tải khi chưa đăng nhập, hoặc sửa câu hướng dẫn. |
| FE-06 | Công khai | Nút gợi ý tìm kiếm (Khai sinh, Kết hôn, Chứng thực, Khai tử, Cư trú) và câu hỏi gợi ý của chatbot đều trỏ tới thủ tục **không còn công khai** → 0 kết quả. Hiện chỉ có 3 thủ tục công khai (hộ kinh doanh, trợ cấp thất nghiệp, quyết toán thuế TNCN). | Lấy gợi ý từ thủ tục đang công khai (hoặc nổi bật), không cố định trong code. |
| FE-07 | Công khai | Câu chữ sai nghiệp vụ: bước 4 trang chủ **"Nhận QR sau khi duyệt"** (hệ thống không phê duyệt hành chính); bước 06 "Tải PDF, QR…" và FAQ "Khi nào hệ thống tạo mã QR?" nói phải tạo PDF rồi tải QR. | "Nhận QR khi giấy tờ sẵn sàng"; ghi rõ thủ tục chỉ có giấy tờ (không có tờ khai Word) thì không có PDF, QR vẫn dùng được (quét ra trang "Mã QR hợp lệ"). |
| FE-08 | Công khai | Phần **"Trình tự thực hiện"** chỉ hiện câu đầu mỗi bước rồi "…", không có cách mở đầy đủ. | Hiện đủ hoặc thêm "Xem thêm". |
| FE-09 | Người dân | Hồ sơ của thủ tục đã **ngừng công khai** (Trích lục, Khai tử) vẫn hiện bình thường, không có dòng báo. | Thêm nhãn/ghi chú "Thủ tục đã ngừng áp dụng trên cổng". |
| FE-10 | Người dân | "Tờ khai của tôi" hiện tên tệp `dccb6f4a…_don-nhap-lan-1.docx.docx` (tiền tố GUID + đuôi lặp). | Hiện tên gốc người dùng (`fileName`); nếu `fileName` từ BE đã có GUID thì báo BE. |
| FE-11 | Người dân | Thông báo lỗi tệp mất giờ là tiếng Việt (`document.file_missing`), nhưng không có lối đi tiếp. | Với hồ sơ còn sửa được (Bản nháp/Cần bổ sung): hiện nút "Tải lại tệp" ngay tại mục đó. |
| FE-12 | Admin | Sao lưu dữ liệu: ô chọn dịch vụ chỉ có "Tất cả dịch vụ" (danh sách dịch vụ trống). | Lấy danh sách dịch vụ giống trang Giám sát dịch vụ (6 dịch vụ). |
| FE-13 | Quản lý thủ tục | Kho biểu mẫu vẫn ghi "Đã có tệp Word" cho mẫu cũ đã mất tệp; bấm Xem → "Không tìm thấy biểu mẫu…" (API `download-docx` 404). | Khi 404 hiện "Tệp mẫu không còn, vui lòng tải lên lại" + nút "Thay đổi"; (BE chưa có cờ tệp tồn tại cho mẫu — nếu cần hiển thị ở danh sách, gửi yêu cầu BE). |

## C. Mức thấp (giao diện, câu chữ, trợ năng)

| # | Khu | Lỗi | Mong muốn |
|---|---|---|---|
| FE-14 | Quản lý | Lịch ca toàn phường bắt đầu tuần từ **Chủ nhật**, lịch cá nhân cán bộ bắt đầu Thứ Hai (FE ghi "chờ chọn cách hiển thị"). | Thống nhất bắt đầu Thứ Hai. |
| FE-15 | Quản lý | Danh sách Cán bộ Một cửa: avatar **"BA"** cho "Test Cán bộ A1" (góc trên hiện "TA"); nút xóa có nhãn trợ năng "Xóa hồ sơ cá nhân". | Dùng cùng hàm tính chữ viết tắt với header; nhãn "Xóa cán bộ {tên}". |
| FE-16 | Công khai | Tiêu đề viết hoa từng chữ ("Trình Tự Thực Hiện", "Cách Thức Thực Hiện"). | Viết hoa đầu câu. |
| FE-17 | Công khai | Bảng thông tin thủ tục 4 cột rất chật ở màn ≤ 500 px. | Chuyển 2 cột/thẻ trên điện thoại. |
| FE-18 | Cán bộ | (Kiểm local 19/11) Tổng quan trên điện thoại tràn ngang 431/375 px, nút "Xử lý", "Xem tất cả" bị cắt; bảng hồ sơ ở 1366 px rộng 1270 px trong khung 997 px nên cột Cán bộ/Trạng thái phải cuộn ngang. | Kiểm lại trên bản deploy hiện tại; sửa nếu còn. |
| FE-19 | Chung | Ô ngày dùng `<input type=date>` hiển thị theo ngôn ngữ trình duyệt (`mm/dd/yyyy` trên máy tiếng Anh). | Dùng bộ chọn ngày định dạng `dd/mm/yyyy`. |

## D. Chạy FE ở máy (dev)

- `vite.config.ts` vẫn thiếu `proxy['/api/v1/public'] = documentForm;` → khi chạy local, mọi API công khai (tra QR, tải phôi mẫu) đi nhầm sang IAM và 404.
- `package-lock.json` lệch `package.json`: `npm ci` lỗi, phải `npm install` (sinh thay đổi lock).

## E. Không thuộc FE (để FE biết, không cần sửa)

- Dữ liệu: lĩnh vực "Hộ Tịch Test", thủ tục/mẫu test (`Test_demo_2`, `Kb04-ks-test`, "đơn 001", "kb04"), tên tệp mẫu méo (`Mus10tiPhlcbanhnhkmtheo _VL.docx`), "Cấp thực hiện: Tỉnh" của thủ tục trợ cấp thất nghiệp, ngày lễ chỉ có "le 20/11" — Quản lý thủ tục/Admin nhập lại.
- Nhật ký nghiệp vụ trống (relay audit tắt theo thiết kế), 503 lần gọi đầu (`minReplicas: 0`) — vận hành.
- Chatbot hỏi gộp nhiều ý (giấy tờ + lệ phí + thời gian) trả thiếu danh mục giấy tờ — đã chuyển BE xử lý.
