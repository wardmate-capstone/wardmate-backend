# Bàn giao Backend → Frontend: BE-H — Mô hình thủ tục linh hoạt (10/11/2026)

Người viết: Backend (Claude), 10/11/2026. Trả lời file yêu cầu `BE_REQUEST_2026-11-10_PROCEDURE_TEXT_LENGTH`.

Hợp đồng chi tiết: `docs/api-guide.md`, mục **"BE-H (10/11/2026)"** (trong phần ProcedureInput). Quy tắc: `docs/system-constraints.md` mục **PROC-05**, **WF-01a**, **DOC-03a**, **KB-01a**, **AI-02a**.

## 0. Trạng thái — đọc trước

| Phần | Service | Trên Azure? |
|---|---|---|
| A — bỏ giới hạn độ dài | Catalog, Workflow, Analytics (+ AIOCR cắt tiêu đề tri thức) | **Chưa** — cần chạy 3 file SQL rồi deploy |
| B, C — trường mới | Catalog, Workflow, DocumentForm, AIOCR | **Chưa** — deploy cùng phần A |

Thứ tự deploy: chạy SQL Workflow + Analytics + Catalog → deploy Workflow, Analytics, DocumentForm, AIOCR → deploy Catalog **sau cùng**. Trước khi BE báo deploy xong, FE có thể code theo tài liệu này nhưng chưa test được trên Azure.

## 1. Con số trần đã chốt

| Trường | Tối đa |
|---|---|
| `title` | **1.000** |
| `procedureCode` | **100** |
| `levelOfImplementation`, `decisionNumber` | **255** |
| Trường một dòng (tên giấy tờ, tên biểu mẫu, đối tượng, cơ quan, tóm tắt, `quantityText`, `feeText`, `processingTime.text`, `caseName`, tên cách nộp, `procedureType`, mỗi phần tử danh sách) | **10.000** |
| Văn bản nhiều dòng (`requirements`, `description`, `stepsText`, nội dung bước, nội dung mục mở rộng, `note`, `conditionNote`) | không giới hạn từng trường |
| Tổng `contentPayload` + `checklistSchema` + `formDefinitions` | **500.000** ký tự |
| `additionalSections` | 50 mục, tiêu đề ≤ 255 |
| `fees` / cách nộp · `formCodes` / giấy tờ · phần tử mỗi danh sách | 100 · 20 · 100 |

Vượt trần → 400 `validation.failed`, `errors` nêu trường.

## 2. Tên trường chính thức

Đúng như file yêu cầu, với 4 điểm khác (mục 3):

- `submissionMethods[]`: `feeAmount` (nullable), `estimatedDays` (nullable), `caseCode`, `fees[]` (`kind`, `amount`, `percent`, `unit`, `description`, `caseCode`), `feeText`, `processingTime` (`value`, `unit`, `text`).
- `contentPayload`: `procedureType`, `levels[]`, `competentAuthorities[]`, `executingAgencies[]`, `authorizedAgencies[]`, `coordinatingAgencies[]`, `requirements`, `receivingAddresses[]`, `resultDetails[]` (`name`, `code`, `validity`, `note`), `keywords[]`, `description`, `stepsText`, `commonSteps[]`, `additionalSections[]` (`title`, `content`).
- `checklistSchema[]`: `quantityText`, `note`, `formCodes[]`; `documentCopyType` thêm `"OTHER"`.
- Hồ sơ Workflow, `checklists[]`: thêm `quantityText`, `itemNote`, `forms[]` (`formCode`, `formName`, `formTemplateId`, `isMandatory`).

**Trường mới có giá trị null không xuất hiện trong JSON trả về** — FE coi "thiếu trường" là null.

## 3. Điểm BE làm khác hoặc chốt thêm so với đề xuất

1. **Gắn nhiều mẫu cho một giấy tờ:** người dân điền **một trong các** mẫu là giấy tờ đó đủ (trên Cổng, các mẫu cùng dòng thường là phương án thay thế). Bắt buộc và số lượng vẫn theo giấy tờ, không theo biểu mẫu. Nếu chủ dự án muốn "phải điền tất cả các mẫu", BE sẽ đổi sau.
2. **Ghi chú giấy tờ:** trong hồ sơ Workflow trả tên `itemNote` (vì `note` của dòng checklist hồ sơ đã là ghi chú trạng thái). Trong thủ tục (Catalog) vẫn là `note`.
3. **Bóc tách PDF bằng AI** chỉ điền thêm các trường văn bản: `requirements`, `stepsText`, `feeText`, thời hạn bằng chữ, `quantityText`, `formCodes`, các loại cơ quan, `levels`, `keywords`, `description`, `procedureType`. **Không** tự điền `fees[]`, `resultDetails[]`, `commonSteps[]`, `additionalSections[]` (để schema AI không vượt giới hạn lồng nhau) — cán bộ bổ sung khi đối soát bản nháp. Prompt đã yêu cầu bỏ đoạn lặp của bản xuất Cổng.
4. **Chatbot AIOCR:** tên thủ tục dài hơn 500 ký tự được cắt ở tiêu đề tri thức (nội dung đầy đủ vẫn có); nội dung tri thức một thủ tục tối đa 200.000 ký tự (phần thừa bị cắt).

Xác nhận câu hỏi trong file yêu cầu:

- Workflow **không** dùng `estimatedDays`/`feeAmount` để tính hạn. Cho null an toàn ở Workflow, Analytics; AIOCR đã sửa để đọc null.
- Workflow **không** đọc bước thực hiện; AIOCR có đọc và nay dùng cả `stepsText` + `commonSteps`.
- Thủ tục không có trường hợp: tạo hồ sơ không bắt chọn trường hợp (đã đúng từ trước).
- `legalReferences`: `issueDate`, `authority` tuỳ chọn, không bắt buộc trường con nào.

## 4. FE cần làm (sau khi BE deploy)

- Bỏ/nới giới hạn độ dài theo bảng mục 1; bộ đếm ký tự dùng các số đó.
- Form "Thời hạn & lệ phí": mỗi cách nộp có danh sách mức phí (`fees[]`) + ô "Phí ghi bằng chữ" (`feeText`); thời hạn = số + đơn vị + ô "ghi bằng chữ" (`processingTime`); chọn trường hợp áp dụng (`caseCode`).
- Thông tin chung: thêm các ô danh sách cơ quan, cấp, từ khóa; ô "Yêu cầu, điều kiện", "Mô tả", "Loại thủ tục"; kết quả kèm mã (`resultDetails`).
- Trình tự: ô dán nguyên văn (`stepsText`) + bước chung (`commonSteps`) + bước theo trường hợp.
- Thành phần hồ sơ: ô "Số lượng ghi bằng chữ" (`quantityText`), "Ghi chú" (`note`), lựa chọn loại bản "Khác" (`OTHER`), ô **"Mẫu đơn kèm theo"** chọn nhiều mẫu từ tab Biểu mẫu (`formCodes`).
- Mục "Thông tin khác" (`additionalSections`), hiển thị theo thứ tự sau các mục chuẩn.
- Trang công dân: ưu tiên văn bản dự phòng (`feeText`, `processingTime.text`, `quantityText`) khi có; bỏ chữ ghi cố định (ví dụ loại thủ tục) nếu có `procedureType`.
- Màn hồ sơ (dân và cán bộ): hiện `forms[]` ngay dưới dòng giấy tờ; nút "Tải mẫu / Điền tờ khai" theo từng mẫu.

## 5. Kiểm tra sau khi BE deploy (theo tiêu chí nghiệm thu của FE)

| # | Dữ liệu thử | Kỳ vọng |
|---|---|---|
| 1 | Tên giấy tờ 2.000 ký tự; đối tượng 5.000 ký tự | Lưu, đọc đủ; người dân tạo hồ sơ được, checklist hồ sơ đủ tên |
| 2 | Một cách nộp 9 mức phí, có mức 0 và một mức theo % | Lưu được; `feeAmount = 0`; `feeSummary` dạng "0 – X VND; 0,5%" |
| 3 | Chỉ `feeText` "Theo quy định của Bộ Tài chính" | `feeSummary` = nguyên văn |
| 4 | `processingTime` `{ value: 15, unit: "MINUTE" }` và một cách nộp không có thời hạn | Lưu được; `estimatedDays` null; tạo hồ sơ, phản hồi, chatbot không lỗi |
| 5 | Mỗi cách nộp phí/thời hạn khác nhau, một cách gắn `caseCode` | Lưu và trả đúng |
| 6 | Giấy tờ `OTHER` + `quantityText` "01 bản chính và 01 bản sao" | Lưu được; tạo hồ sơ được; tải giấy tờ loại bản nào cũng được tính |
| 7 | Không có trường hợp, chỉ `commonSteps` | Lưu được; tạo hồ sơ không bắt chọn trường hợp |
| 8 | 2 cấp, 3 cơ quan phối hợp, điều kiện dài, kết quả có mã | Trả đủ ở chi tiết công khai; `levelOfImplementation` = "Cấp Tỉnh, Cấp Xã" nếu để trống |
| 9 | 3 mục `additionalSections` | Trả đúng thứ tự |
| 10 | Tên thủ tục 1.000 ký tự | Lưu được; tạo hồ sơ và gửi phản hồi không lỗi |
| 11 | Thủ tục cũ | Đọc, sửa, xuất bản, tạo hồ sơ bình thường |
| 12 | Tổng nội dung > 500.000 ký tự, hoặc tên giấy tờ > 10.000 | 400 `validation.failed` nêu trường |
| 13 | Chỉ `stepsText` ~5.000 ký tự nhiều dòng | Trả đúng nguyên văn, giữ xuống dòng |
| 14 | 3 giấy tờ, 2 mẫu `M25`, `01.LPTB` gắn qua `formCodes` | Hồ sơ có đúng 3 dòng; dòng 1, 3 có `forms[]` tương ứng |
| 14b | Một giấy tờ gắn 2 mẫu | Lưu được; dòng có 2 phần tử `forms[]`; điền 1 mẫu là đủ |
| 14c | `formCodes` có mã không tồn tại | 400 nêu mã giấy tờ và mã mẫu |
| 15 | Căn cứ pháp lý chỉ có tên + mã | Lưu được |
| 16 | `keywords` 5 từ khoá | `GET /api/v1/procedures?keyword=<một từ khóa>` trả đúng thủ tục |
| 17 | Nhập đủ 25 mục thủ tục 3.000781 | Lưu không cắt; chi tiết trả đủ |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi. Không gửi mật khẩu/token.
