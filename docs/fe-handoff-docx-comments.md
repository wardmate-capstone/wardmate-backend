# Bàn giao FE WardMate · Tích hợp Chú thích & Bình luận trực tiếp trên Đơn Word (DOCX Comments)

**Ngày cập nhật:** 16/11/2026.  
**Dịch vụ backend liên quan:** `WardMate.Services.DocumentForm` (API đã sẵn sàng trên Azure và local).  
**Mục tiêu:** Cán bộ thụ lý có thể bôi đen đoạn văn bản và thêm bình luận (comment balloon/card) trực tiếp trên giao diện web khi thẩm định tờ khai Word (.docx), lưu lại vào hồ sơ và hiển thị nguyên vẹn khi người dân mở hoặc tải về máy mở bằng Microsoft Word.

---

## 1. Hiện trạng và Vấn đề cần khắc phục

| Hạng mục | Hiện tại trên FE | Mục tiêu sau khi hoàn thiện |
|---|---|---|
| **Xem tờ khai** | Khi bấm *"Mở tờ khai"*, FE chỉ bật `DocumentFilePreview` dạng ảnh/PDF tĩnh chỉ đọc. | Bổ sung nút / luồng mở thẳng **Trình soạn thảo & Chú thích trực tuyến** (`DocxOnlineEditor`). |
| **Bình luận trên Word** | Thanh công cụ trong `DocxOnlineEditor.tsx` chưa bật công cụ Comment. Cán bộ phải tải file Word về máy -> mở bằng MS Word ngoài desktop để comment -> bấm *"Tải lên tờ khai Word có ghi chú"*. | Bổ sung nút **"Thêm bình luận"** (`review.addComment`) ngay trên thanh công cụ của `DocxOnlineEditor`. Cán bộ chọn đoạn văn bản -> nhập nhận xét ngay trên web. |
| **Lưu & Đồng bộ** | Đã có nút lưu gọi API `annotate-docx`. | Tiếp tục dùng API `annotate-docx` để lưu file `.docx` đã chứa các comment OpenXML chuẩn. |

---

## 2. Các API Backend liên quan (Đã có sẵn 100%)

Tất cả các API này nằm ở Service **DocumentForm** (`https://.../api/v1/officer/submissions/...`), yêu cầu JWT với role `FRONT_DESK_OFFICER` và policy `DocumentUser`.

### A. Tải file Word của đơn để mở trên Editor
- **Route:** `GET /api/v1/officer/submissions/{submissionId}/download-docx`
- **Output:** Stream binary `.docx` (`application/vnd.openxmlformats-officedocument.wordprocessingml.document`).
- **Hàm FE đã có sẵn:** `documentFormApi.officerDownloadSubmission(submissionId, signal)`.

### B. Lưu file Word có chứa bình luận/chú thích của cán bộ
- **Route:** `PUT /api/v1/officer/submissions/{submissionId}/annotate-docx`
- **Content-Type:** `multipart/form-data`
- **Body Form:**
  - `File`: File `.docx` nhị phân đã được chỉnh sửa/thêm comment từ Editor.
- **Hành vi Backend:**
  - Lưu file DOCX mới đè lên Azure Blob Storage.
  - Cập nhật `FileSizeBytes`, `FileName`, `UpdatedAtUtc` của bản khai (`UserSubmission`).
  - **Không** tự ý chuyển đổi trạng thái hồ sơ (Status vẫn giữ `Submitted` hoặc `RevisionRequested` để cán bộ kiểm tra xong mới bấm Yêu cầu sửa hoặc Duyệt).
- **Hàm FE đã có sẵn:** `documentFormApi.officerAnnotateSubmission(submissionId, file)`.

---

## 3. Hướng dẫn chi tiết các bước sửa trên Frontend

### Bước 1: Kích hoạt công cụ Comment trong `DocxOnlineEditor.tsx`
Thư viện `@docx-editor.dev/react` (đang cài trong `node_modules`) đã hỗ trợ sẵn các slot:
- `review.addComment`: Mở popup/draft tạo bình luận cho vùng văn bản đang bôi đen.
- `review.comments`: Bật/tắt hiển thị dải danh sách bình luận (comment rail / balloon).

**Tại file:** `src/components/documents/DocxOnlineEditor.tsx`:

1. Import thêm icon từ `@phosphor-icons/react`:
   ```tsx
   import { ChatCircleDots, ChatCircleText } from '@phosphor-icons/react';
   ```

2. Bổ sung slot vào mảng `tools`:
   ```tsx
   // Thêm vào danh sách công cụ thao tác
   { id: 'review.addComment', label: 'Thêm bình luận', icon: ChatCircleDots },
   { id: 'review.comments', label: 'Hiện/ẩn bình luận', icon: ChatCircleText },
   ```

3. Cấu hình thông tin người bình luận (Author):
   - Đảm bảo prop `author` truyền vào `DocxEditor` là họ tên cán bộ đang đăng nhập (ví dụ: `user?.profile?.fullName || 'Cán bộ thụ lý'`), để khi comment sẽ xuất hiện tên người tạo giống như trong MS Word.
   ```tsx
   <DocxEditor
     ref={editorRef}
     author={author || 'Cán bộ thụ lý'}
     // ... các props khác giữ nguyên
   />
   ```

---

### Bước 2: Tối ưu trải nghiệm cán bộ trong `OfficerDocumentsPanel.tsx`

**Tại file:** `src/pages/officer/OfficerDocumentsPanel.tsx`:

1. Hiện tại, nút *"Chỉnh sửa trực tuyến"* đang nằm ở tận cuối danh sách (dòng 204). Cần đưa hành động này lên ngay cạnh từng tờ khai `submissions.data.map`:
   ```tsx
   {canAnnotate && (
     <Button
       size="small"
       variant="outline"
       className="gap-1.5 border-red-200 text-red-700 hover:bg-red-50"
       onClick={() => setEditing({ id: item.id, name: item.fileName })}
     >
       <ChatCircleDots size={16} />
       <span>Chú thích & Nhận xét Word</span>
     </Button>
   )}
   ```

2. Khi cán bộ bấm nút này, modal full-screen `DocxOnlineEditor` sẽ mở ra:
   - Cán bộ xem toàn bộ tờ khai với font chữ và bảng biểu chuẩn.
   - Bôi đen phần người dân điền sai (ví dụ dòng *“Kính gửi …”* hay *“Tôi tên là …”*).
   - Bấm nút **"Thêm bình luận"** -> gõ *"Chưa điền tên"* -> Enter.
   - Bong bóng bình luận hiển thị ở lề bên phải văn bản.
   - Bấm **"Lưu tờ khai vào hồ sơ"** -> Hệ thống tự động đóng gói DOCX và gọi API `PUT annotate-docx`.

---

### Bước 3: Đảm bảo người dân xem được bình luận khi mở lại đơn

**Tại file:** `src/pages/citizen/components/DossierChecklistView.tsx` và `CitizenSubmissionsView.tsx`:
- Khi người dân mở tờ khai xem lại (`DocxOnlineEditor` ở chế độ `readOnly={true}`), kiểm tra đảm bảo slot `review.comments` được bật để người dân đọc được các bong bóng comment của cán bộ chỉ ra các điểm cần sửa.
- Nếu người dân tải file `.docx` về máy (`downloadSubmission`), mở bằng Microsoft Word trên máy tính cũng sẽ thấy nguyên vẹn comment ở lề phải (giống hệt hình ảnh `don-nghi.docx`).

---

## 4. Kịch bản kiểm thử nghiệm thu (Acceptance Criteria)

1. **Thêm nhận xét trên web:**
   - Cán bộ đăng nhập -> Mở một hồ sơ đang ở trạng thái `UNDER_REVIEW`.
   - Vào tab xem tờ khai Word -> Bấm **"Chú thích & Nhận xét Word"**.
   - Bôi đen dòng văn bản bị thiếu/sai -> Bấm nút **"Thêm bình luận"**.
   - Gõ nội dung bình luận -> Bấm lưu -> Thông báo *"Đã lưu tờ khai vào hồ sơ"* hiển thị thành công.

2. **Kiểm tra tính tương thích với Microsoft Word:**
   - Cán bộ bấm tải file `.docx` đó về máy tính.
   - Mở bằng Microsoft Word (Desktop/Office 365).
   - **Kỳ vọng:** MS Word hiển thị đầy đủ comment bên thanh Review/lề phải với đúng tên cán bộ và nội dung nhận xét.

3. **Công dân xem lại phản hồi:**
   - Đăng nhập tài khoản công dân đã nộp hồ sơ.
   - Mở xem tờ khai -> Nhìn thấy vị trí được đánh dấu kèm nội dung nhắc nhở của cán bộ.
