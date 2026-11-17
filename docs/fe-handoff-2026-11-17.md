# Bàn giao Backend → Frontend: so sánh tệp và tờ khai giữa các lần gửi (17/11/2026)

Người viết: Backend (Claude), 17/11/2026. Trả lời đề nghị "So sánh nội dung tờ khai Word và giấy tờ giữa các lần gửi". Hợp đồng chi tiết: `docs/api-guide.md` mục "Tệp theo lần gửi và so sánh tờ khai — 17/11/2026 (DOC-FILEVER-01)". Quy tắc: `docs/system-constraints.md` DOC-FILEVER-01.

## 0. Trạng thái

**Đã deploy 18/11 (DEPLOY-1118a).** Đã chạy `docs/sql/documentform-submission-file-versions.sql`. Tag image: DocumentForm `documentform-20261117-01`, Workflow `workflow-20261118-01`, Gateway `gateway-20261118-01`. Qua Gateway không token, `GET /api/v1/applications/{id}/file-versions` trả 401 (route đã sang DocumentForm). Chỉ các lần gửi **sau** khi deploy mới có dữ liệu tệp.

## 1. Đã làm theo đề nghị

| Mục FE | BE làm | API |
|---|---|---|
| 4.4 Thời điểm tải lên | Có | `GET /api/v1/applications/{id}/contents`: thêm `createdAtUtc`, `updatedAtUtc` cho `submissions` và `supportingDocuments` |
| 4.1 Lịch sử tệp | Có (bảng riêng của DocumentForm) | Tự ghi khi công dân nộp/nộp lại; FE không gọi |
| 4.2 Liệt kê và tải | Có | `GET /api/v1/applications/{id}/file-versions?versionNumber=n`, `GET .../file-versions/{fileVersionId}/download` |
| 4.3 So sánh tờ khai | Có | `GET .../file-versions/diff?fromVersion=1&toVersion=2&checklistId=...` (dạng `FieldDiffDto`) |

Lưu ý: đường dẫn là `/api/v1/applications/{id}/file-versions...` (thuộc DocumentForm), **không** phải `/api/v1/officer/applications/...` như đề nghị, vì nhánh `/officer/applications` đã thuộc Workflow trên Gateway. Quyền: cán bộ đang thụ lý hoặc được tham vấn; người nộp không dùng được.

## 2. Trả lời câu hỏi mục 7

1. **Lưu ở đâu?** Bảng riêng của DocumentForm (`submission_file_versions`). `snapshotData` của Workflow không đổi.
2. **Mẫu DOCX có ô định danh không?** Tờ khai điền trực tuyến có dữ liệu từng ô → so theo ô (`fieldLabel` = tên ô). Tờ khai DOCX công dân sửa trực tiếp không còn ô định danh sau khi điền → so **theo đoạn văn**: nhãn "Đoạn N", "Đoạn N (thêm mới)", "Đoạn N (bản cũ, đã xóa)". Ví dụ "Họ tên: Đức Anh → Họ tên: Nguyễn Đức Anh" hiện thành một dòng "Đoạn 2".
3. **Dọn blob cũ?** Hiện giữ toàn bộ tệp của các lần gửi; chưa có cơ chế dọn. Chủ dự án quyết chính sách sau (ví dụ dọn khi hồ sơ đóng/hủy quá X tháng).

## 3. FE cần làm

- **Nhãn "Mới cập nhật":** so `updatedAtUtc` của từng tờ khai/giấy tờ với `submittedAt` của lần gửi trước.
- **Phần "Tệp thay đổi" ở tab so sánh:** gọi `file-versions?versionNumber=A` và `=B`, ghép theo mục:
  - chỉ có ở B → **mới**;
  - chỉ có ở A → **bị xóa**;
  - cùng mục, `sha256` khác → **bị thay**;
  - `sha256` giống → **không đổi**.

  Nút mở bản cũ/mới dùng `.../download`.
- **Thay đổi trong tờ khai Word:** với mục có tờ khai bị thay, gọi `diff` và hiển thị bằng giao diện `FieldDiffDto` hiện có.
- **Lần gửi không có dữ liệu tệp:** mảng rỗng (lần gửi trước khi deploy, hoặc bước ghi bị lỗi) → giữ thông báo hiện tại "chưa so sánh được tệp".

## 4. Kiểm tra sau deploy

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Hồ sơ mới: nộp lần 1 | `file-versions?versionNumber=1` có đủ tờ khai và giấy tờ, có `sha256` |
| 2 | Cán bộ yêu cầu bổ sung; công dân sửa tờ khai Word và thay một ảnh, nộp lại | Lần 2: tờ khai và ảnh có `sha256` khác lần 1; giấy tờ không sửa có `sha256` giống |
| 3 | Gọi `diff` cho mục tờ khai | Có dòng đoạn bị sửa với nội dung cũ và mới |
| 4 | Tải bản lần 1 của ảnh đã thay | Mở được ảnh cũ |
| 5 | Người nộp gọi `file-versions` | 403/404 |
| 6 | `contents` | Có `createdAtUtc`, `updatedAtUtc` |
