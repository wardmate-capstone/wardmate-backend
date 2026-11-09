# Bàn giao Backend → Frontend: các sửa đổi ngày 09/11/2026

Người viết: Backend (Claude), 09/11/2026.

Tài liệu này liệt kê các thay đổi BE ngày 09/11 để sửa các vấn đề chủ dự án/FE báo. Hợp đồng chi tiết từng API vẫn ở `docs/api-guide.md`; quy tắc ở `docs/system-constraints.md`. Danh sách việc FE từ đợt test KB-00 → KB-05 vẫn là `docs/fe-handoff-2026-11-05.md`.

## 0. Trạng thái triển khai — đọc trước

| # | Thay đổi | Service | Trên Azure? |
|---|---|---|---|
| 1 | QR tạo ngay khi hồ sơ sẵn sàng | DocumentForm | **Chưa** — cần chạy SQL rồi deploy |
| 2 | Tự sinh tóm tắt lệ phí/thời hạn | ProcedureCatalog | **Có** (image `catalog-20261109-02`) |
| 3 | API gắn PDF cho thủ tục nhập tay | ProcedureCatalog | **Có** (image `catalog-20261109-02`) |
| 4 | Tạo thủ tục mặc định ẩn (`isActive`) | ProcedureCatalog | **Có nhưng còn lỗi** — gửi `false` vẫn thành công khai; bản sửa chưa deploy |
| 5 | Chỉ bắt buộc mã, tên, lĩnh vực | ProcedureCatalog | **Chưa** — cần chạy SQL rồi deploy |
| 6 | Nhiều loại bản (bản chính + bản sao) và tên trường hợp | ProcedureCatalog, AIOCR | **Chưa** — deploy cùng mục 5 (không cần SQL riêng) |
| 7 | Danh bạ cán bộ trả `permissions[]` — sửa phân công/tham vấn luôn bị chặn (BE-G) | IAM (+ Workflow) | **Chưa** — deploy IAM trước, không cần SQL |

Khi Anti deploy xong mục 1, 4, 5, 6, 7, BE sẽ báo lại. Trước đó FE có thể code theo tài liệu này nhưng chỉ test được mục 2, 3 trên Azure.

## 1. Mã QR không tạo được sau khi cán bộ xác nhận giấy tờ sẵn sàng

**Vấn đề:** cán bộ xác nhận `DOCUMENTS_READY` xong, gọi `GET /api/v1/applications/{applicationId}/approved-qr` hoặc `/approved-qr/image` nhận **404 `document.qr_not_found`**.

**Nguyên nhân:** BE chỉ sinh QR bên trong `POST /api/v1/applications/{applicationId}/generate-pdf`. Chưa ai xuất PDF thì chưa có QR; hồ sơ không có đơn DOCX thì không bao giờ có QR.

**Đã sửa:** DocumentForm tự tạo QR ngay lần đầu có API đọc hồ sơ khi hồ sơ đã `DOCUMENTS_READY` hoặc `OFFICIALLY_RECEIVED` — gồm chính `GET approved-qr`. Không cần gọi `generate-pdf` trước.

**FE cần làm:**
- Sau khi hồ sơ sẵn sàng, gọi thẳng `GET .../approved-qr` hoặc `.../approved-qr/image`; bỏ bước bắt buộc xuất PDF trước nếu đang có.
- Trường `submissionId` trong response **có thể là `null`** (thủ tục chỉ có giấy tờ đính kèm, không có tờ khai DOCX). Cập nhật schema cho nullable.
- PDF xuất ra **không chứa ảnh QR** (thiết kế hiện tại). Hiển thị QR bằng API `approved-qr/image`.
- Quét QR khi hồ sơ chưa có PDF nào sẽ ra 404 `qr.pdf_not_found` ở trang chuyển hướng công khai — giới hạn hiện tại.

**Test lại (sau deploy):** hồ sơ vừa `DOCUMENTS_READY`, chưa xuất PDF → `GET approved-qr` trả 200; `approved-qr/image` trả ảnh PNG.

## 2. Tóm tắt lệ phí và thời hạn không còn phải nhập lại

**Vấn đề:** form "Thêm thủ tục" bắt nhập "Tóm tắt lệ phí", "Tóm tắt thời hạn" dù phần 4 "Thời hạn & lệ phí" đã có cùng thông tin.

**Đã sửa:** khi `feeSummary` / `processingTimeSummary` gửi rỗng hoặc `null` mà `contentPayload.submissionMethods` có ít nhất một cách nộp, BE tự sinh:

| Dữ liệu cách nộp | `feeSummary` | `processingTimeSummary` |
|---|---|---|
| Mọi cách nộp lệ phí 0 / 0 ngày | `Miễn phí` | `Trong ngày` |
| Cùng một mức | `10.000 VND` | `7 ngày` |
| Khác mức | `10.000 – 20.000 VND` | `3 – 7 ngày` |

Người nhập tự gõ tóm tắt thì BE giữ nguyên. Hai trường này là thứ duy nhất API **danh sách** thủ tục trả về về lệ phí/thời hạn.

**FE cần làm:** bỏ dấu `*` ở hai ô tóm tắt (hoặc ẩn hai ô, hiển thị gợi ý "để trống để hệ thống tự tóm tắt"). Ô "Kết quả giải quyết" (`contentPayload.results`) **BE chưa bao giờ bắt buộc** — bỏ dấu `*`.

## 3. Thủ tục nhập tay không mở được PDF gốc

**Vấn đề:** thủ tục tạo bằng tay gọi `GET /api/v1/procedures/{id}/source` luôn nhận **404 `draft.source_not_found`** ("Thủ tục không có PDF được tải lên qua hệ thống bản nháp").

**Đã sửa — API mới (API 32a):**

```
POST /api/v1/procedure-manager/procedures/{id}/source
Content-Type: multipart/form-data   (trường: file)
Quyền: procedure.update (Procedure Manager)
```

- **200**: trả `ProcedureDetailDto` mới với `originalPdfUrl`, `pdfFileName`. Sau đó `GET /api/v1/procedures/{id}/source` trả link tạm 600 giây như thủ tục tạo từ bản nháp → cán bộ xem PDF gốc khi xử lý hồ sơ.
- Gắn lại lần nữa = thay PDF. Mỗi lần gắn tạo thêm một dòng trong "Lịch sử phiên bản"; PDF cũ vẫn mở qua nguồn của phiên bản cũ.
- Lỗi: **400 `draft.invalid_pdf`** (không phải PDF / tên không đuôi .pdf / quá 20 MB) · **404 `procedure.not_found`** · **503 `draft.storage_not_configured`**.
- PDF gắn kiểu này hiện trong danh sách "PDF và bản nháp" với trạng thái `Published`.

**FE cần làm:**
- Ở trang chi tiết/sửa thủ tục: khi `GET .../source` trả 404, hiện nút **"Tải PDF gốc"** gọi API trên; thay khung lỗi "Thủ tục không có PDF..." bằng nút này.
- Khi `PUT /api/v1/procedure-manager/procedures/{id}` (sửa thủ tục), **gửi lại `originalPdfUrl` và `pdfFileName` đang có**. Gửi `null` sẽ làm mất liên kết PDF.

## 4. Tạo thủ tục bằng tay bị công khai ngay

**Vấn đề:** `POST /api/v1/procedure-manager/procedures` luôn tạo thủ tục `isActive = true`, người dân thấy ngay.

**Đã sửa:** body tạo có thêm trường **`isActive`** (boolean, không bắt buộc, **mặc định `false`**):
- Không gửi / `false` → thủ tục tạo ở trạng thái **ẩn**.
- `true` → công khai ngay.
- Bật/tắt sau bằng API có sẵn `PATCH /api/v1/procedure-manager/procedures/{id}/status`.
- Response 201 trả `isActive` đúng giá trị đã lưu.

**Lưu ý:** bản đang chạy trên Azure có lỗi — gửi `isActive: false` vẫn bị lưu thành `true` (do cấu hình giá trị mặc định của cột). Đã sửa trong source, chờ deploy cùng mục 5. FE cứ gửi đúng `isActive`; sau deploy hành vi sẽ đúng.

**FE cần làm:**
- Ô "Tôi đã kiểm tra thông tin và xác nhận công khai thủ tục này": tick → `isActive: true`, không tick → `isActive: false` (cho phép lưu khi chưa tick).
- Đổi câu "Thủ tục sẽ được công khai khi lưu thành công. Muốn lưu nháp, dùng luồng PDF và bản nháp." thành nội dung đúng, ví dụ "Bỏ chọn để lưu ở trạng thái ẩn; bật công khai sau trong danh sách thủ tục."
- Danh sách quản lý: thủ tục ẩn cần có nút bật công khai (API status).

## 5. Chỉ bắt buộc mã, tên, lĩnh vực

**Vấn đề:** thủ tục có nhiều dạng, nhiều trường không áp dụng nhưng BE bắt nhập.

**Đã sửa:** khi tạo, sửa, xuất bản bản nháp, khôi phục phiên bản — chỉ còn bắt buộc:
- `categoryId` (lĩnh vực, phải tồn tại)
- `procedureCode` (không rỗng, tối đa 50, không trùng)
- `title` (không rỗng, tối đa 500 — **bỏ luật tối thiểu 10 ký tự**)

Mọi trường khác được gửi `null` hoặc bỏ trống: `levelOfImplementation`, `targetAudience`, `feeSummary`, `processingTimeSummary`, `issuingAuthority`, `executingAgency`, `contentPayload` và các mảng bên trong (`cases`, `submissionMethods`, `legalReferences`, `results`), `checklistSchema`, `formDefinitions`. Chuỗi chỉ có khoảng trắng được lưu thành `null`. Phần tử nào đã gửi vẫn bị kiểm (ví dụ cách nộp phải có tên, số tiền/ngày không âm; kết quả không được là chuỗi rỗng).

**Response thay đổi:** các trường chuỗi trên **có thể trả `null`** ở danh sách thủ tục (`GET /api/v1/procedures`, `/featured`, danh sách quản lý) và chi tiết. `contentPayload` luôn là object, các mảng có thể rỗng.

**FE cần làm:**
- Bỏ dấu `*` và validate bắt buộc ở mọi ô trừ mã, tên, lĩnh vực.
- Cập nhật type/schema: `levelOfImplementation`, `targetAudience`, `feeSummary`, `processingTimeSummary` thành nullable.
- Hiển thị: gặp `null` thì hiện "Chưa cập nhật" hoặc ẩn dòng (thẻ danh sách, trang chi tiết, màn quản lý). Không hiển thị chữ `null`.
- Bộ lọc "Cấp thực hiện" sẽ không trả các thủ tục có cấp `null` — đây là đúng.

## 6. Thành phần hồ sơ có nhiều loại bản; trường hợp thiếu tên khi tạo tay

**Vấn đề 1:** giấy tờ thường yêu cầu "01 bản chính và 01 bản sao", nhưng form tạo tay chỉ cho chọn một loại bản (`documentCopyType`) và một số lượng.

**Đã sửa (PROC-02a):** mỗi dòng `checklistSchema` có thêm `copyRequirements`:

```json
{
  "checklistId": "CCCD", "itemName": "Căn cước công dân", "submissionType": "NOP", "isMandatory": true,
  "copyRequirements": [
    { "documentCopyType": "ORIGINAL", "quantity": 1 },
    { "documentCopyType": "REGULAR_COPY", "quantity": 1 }
  ]
}
```

- Mỗi loại bản (`ORIGINAL` bản chính, `CERTIFIED_COPY` bản sao chứng thực, `REGULAR_COPY` bản sao) chỉ khai **một dòng**, tối đa 3 dòng, số lượng 1–100. Sai → 400 `validation.failed`.
- BE tự tính: 1 dòng → `documentCopyType` = loại đó, `quantity` = số lượng; từ 2 dòng → `documentCopyType = null`, `quantity` = tổng (ví dụ 2).
- Gửi kiểu cũ (chỉ `documentCopyType` + `quantity`) vẫn chạy; response sẽ có `copyRequirements` 1 dòng tương ứng. FE nên đọc/hiển thị từ `copyRequirements`.
- Áp dụng cho tạo, sửa thủ tục, xuất bản bản nháp và API cập nhật riêng checklist.
- **Giới hạn:** khi người dân nộp hồ sơ, Workflow chỉ kiểm **tổng số bản** đã khai cho giấy tờ có nhiều loại bản (không kiểm riêng từng loại). Ở màn hồ sơ, FE lấy chi tiết "1 bản chính + 1 bản sao" từ thủ tục (Catalog) theo `checklistId` để hiển thị cho người dân và cán bộ.

**FE cần làm:** ở bước "Thành phần hồ sơ", thay ô chọn một loại bản + số lượng bằng danh sách dòng "Loại bản – Số lượng" có nút thêm dòng (tối đa 3, không trùng loại). Hiển thị dạng "01 bản chính + 01 bản sao".

**Vấn đề 2:** tạo thủ tục bằng tay không khai được tên các trường hợp.

**Nguyên nhân:** BE đã có sẵn `contentPayload.cases[]` với `caseCode` và `caseName`, nhưng form tạo tay chưa có chỗ nhập trường hợp; ngoài ra BE bắt mỗi trường hợp phải có danh sách bước.

**Đã sửa:** trường hợp chỉ cần `caseCode` (duy nhất, tối đa 100 ký tự, không khoảng trắng đầu/cuối) và `caseName` (tối đa 500); `steps` được bỏ trống/`null`. Chatbot hiển thị tên trường hợp thay cho mã.

**FE cần làm:**
- Thêm khu "Trường hợp" (ví dụ ở bước 1 hoặc bước 3 "Quy trình"): mỗi dòng gồm mã + tên, bước thực hiện là tùy chọn.
- Ở "Thành phần hồ sơ" và "Biểu mẫu", ô `caseCode` chọn từ danh sách trường hợp đã khai (hiện **tên**, gửi **mã**). Mã không thuộc danh sách → 400.
- Thủ tục chỉ có một trường hợp chung thì để trống `caseCode` ở các dòng (áp dụng mọi trường hợp).

## 6b. Phân công và tham vấn luôn báo cán bộ không đủ quyền (BE-G)

**Vấn đề:** `assignment-candidates` trả mọi cán bộ `eligible=false` với `ASSIGN_PERMISSION_MISSING`; phân công tay và mời tham vấn (422 `specialist_ineligible`) đều thất bại dù cán bộ có quyền.

**Nguyên nhân:** danh bạ IAM không trả quyền của cán bộ nên Workflow coi như thiếu quyền.

**Đã sửa:** `GET /api/v1/manager/officers` và `GET /api/v1/admin/users` trả thêm `items[].permissions` (mảng mã quyền của chính cán bộ đó, không bao giờ `null`). FE **không cần đổi** gì để luồng phân công/tham vấn chạy; có thể dùng `permissions` để hiển thị. Sau deploy, các lý do thật (`CATEGORY_MISMATCH`, `NO_ACTIVE_SHIFT`, ...) vẫn hiện khi điều kiện chưa đạt — đó là đúng.

## 7. Kiểm tra sau khi BE deploy đủ

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Tạo thủ tục chỉ điền mã, tên (ví dụ "Test"), lĩnh vực, không tick công khai | 201, `isActive: false`, các trường khác `null`; không thấy ở cổng công dân |
| 2 | Bật công khai bằng API status | Thấy ở danh sách công dân, các ô trống hiện "Chưa cập nhật" |
| 3 | Tạo thủ tục có 2 cách nộp 10.000 và 20.000 VND, 3 và 7 ngày, bỏ trống tóm tắt | `feeSummary = "10.000 – 20.000 VND"`, `processingTimeSummary = "3 – 7 ngày"` |
| 4 | Ở thủ tục nhập tay, bấm "Tải PDF gốc", chọn file PDF | 200; "Xem PDF" mở được; lịch sử phiên bản thêm 1 dòng |
| 5 | Tải file .docx vào nút "Tải PDF gốc" | 400 `draft.invalid_pdf` |
| 6 | Hồ sơ vừa `DOCUMENTS_READY`, chưa xuất PDF → mở QR | 200 + ảnh QR |
| 7 | Tạo thủ tục có trường hợp `TH1` tên "Kết hôn lần đầu" (không bước), giấy tờ CCCD 1 bản chính + 1 bản sao gắn `TH1` | 201; checklist trả `copyRequirements` 2 dòng, `quantity: 2`, `documentCopyType: null` || 8 | Khai 2 dòng cùng `ORIGINAL` | 400 `validation.failed` |
| 8 | Khai 2 dòng cùng `ORIGINAL` cho một giấy tờ | 400 `validation.failed` |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi. Không gửi mật khẩu/token.
