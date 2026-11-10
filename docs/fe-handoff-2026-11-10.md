# Bàn giao Backend → Frontend: BE-H và sửa lỗi rà soát A1–B4 (10/11/2026)

Người viết: Backend (Claude), 10/11/2026. Gồm hai việc:

- **BE-H** — mô hình thủ tục linh hoạt, trả lời file yêu cầu `BE_REQUEST_2026-11-10_PROCEDURE_TEXT_LENGTH` (mục 1–5).
- **AUDIT-FIX-1110** — sửa 6 lỗi tiềm ẩn A1–B4 tìm thấy khi rà soát sau deploy 09/11 (**mục 6**). FE cần chỉnh màn đăng nhập, trang tra cứu QR và màn quản trị thủ tục.

Hợp đồng chi tiết: `docs/api-guide.md`, mục **"BE-H (10/11/2026)"** (trong phần ProcedureInput), API 02 (đăng nhập), API 32/32a/32b (PDF thủ tục), các mục tra cứu QR. Quy tắc: `docs/system-constraints.md` mục **PROC-05**, **WF-01a**, **DOC-03a**, **KB-01a**, **AI-02a**, **IAM-01a**, **DOC-QR-06**, **PROC-01c**, **PROC-02b**, **PROC-03c**.

## 0. Trạng thái — đọc trước

| Phần | Service | Trên Azure? |
|---|---|---|
| BE-H A — bỏ giới hạn độ dài | Catalog, Workflow, Analytics (+ AIOCR cắt tiêu đề tri thức) | **Chưa** — cần chạy 3 file SQL rồi deploy |
| BE-H B, C — trường mới | Catalog, Workflow, DocumentForm, AIOCR | **Chưa** — deploy cùng phần A |
| Sửa lỗi A1–B4 (mục 6) | IAM, DocumentForm, Catalog | **Chưa** — không cần SQL, deploy cùng đợt |

Thứ tự deploy: chạy SQL Workflow + Analytics + Catalog → deploy IAM, Workflow, Analytics, DocumentForm, AIOCR → deploy Catalog **sau cùng**. Trước khi BE báo deploy xong, FE có thể code theo tài liệu này nhưng chưa test được trên Azure.

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

## 6. Sửa lỗi rà soát A1–B4 (AUDIT-FIX-1110)

### A1 — Lộ PDF qua tra cứu QR (bảo mật)

**Vấn đề:** `GET /api/v1/public/qr-lookup` (không cần đăng nhập) tìm theo chuỗi con, nên gõ `token=https` là ra mã một hồ sơ bất kỳ; có mã hồ sơ thì tải được PDF có CCCD, địa chỉ qua `pdf-redirect`.

**Đã sửa:**

- Tra cứu chỉ khớp **chính xác**: mã hồ sơ/đơn (GUID), URL QR có chứa mã đó (đọc được cả QR cũ dạng `?applicationId=`), hoặc nguyên chuỗi `qrCodeValue`. Gõ `token=https` nay trả 404 `qr.not_found`. Token dài hơn 2.048 ký tự → 400 `qr.token_too_long`.
- Mọi API `/api/v1/public/*` (qr-lookup, pdf-redirect) giới hạn **30 lần/phút/IP**; vượt → 429 `qr.rate_limited`, header `Retry-After: 60`.

**FE cần làm:** trang tra cứu QR gửi **nguyên chuỗi quét được** (hoặc `applicationId`); bỏ ô cho người dùng gõ một phần mã. Gặp 429: hiện "Bạn tra cứu quá nhiều lần, vui lòng thử lại sau 1 phút", không tự gọi lại.

### A2 — Dò mật khẩu (bảo mật)

**Vấn đề:** `POST /api/v1/auth/login` không giới hạn số lần sai, có thể dò mật khẩu mọi tài khoản, kể cả IT_ADMIN.

**Đã sửa:**

- Sai mật khẩu **10 lần trong 15 phút** với cùng một tài khoản → bị chặn đến hết 15 phút: 429 `iam.login_locked`, `title` ghi số phút phải chờ; trong thời gian này **nhập đúng mật khẩu cũng bị từ chối**. Đăng nhập bằng username hay email đều tính chung một bộ đếm. Đăng nhập đúng trước khi chạm ngưỡng thì bộ đếm về 0.
- Đổi mật khẩu bằng OTP (`password-otp/verify-and-change`) **gỡ khóa ngay**.
- Thêm giới hạn **30 lần đăng nhập/phút/IP** → 429 `iam.login_rate_limited`, `Retry-After: 60`.
- Sửa kèm lỗi có sẵn: giới hạn OTP (10 lần/phút) trước đây có thể đếm theo IP của gateway, tức mọi người dùng chung một bộ đếm; nay đếm theo IP thật của người gọi.

**FE cần làm:** màn đăng nhập xử lý 429 riêng với 401:

- `iam.login_locked` → hiện nguyên `title` (đã có số phút) kèm link **"Quên mật khẩu"**.
- `iam.login_rate_limited` → "Quá nhiều lần đăng nhập, vui lòng đợi 1 phút".
- Không tự gửi lại khi gặp 429.

### B1 — PDF vừa gắn không xem được khi thủ tục đang ẩn

**Vấn đề:** thủ tục tạo tay mặc định ẩn; `GET /api/v1/procedures/{id}/source` (API công khai) chỉ phục vụ thủ tục đang công khai, nên cán bộ gắn PDF xong bấm "Xem PDF" nhận 404.

**Đã sửa — API mới (API 32b):**

```
GET /api/v1/procedure-manager/procedures/{id}/source
Quyền: procedure.source.read (Procedure Manager, IT Admin)
200: { "url": "...", "expiresInSeconds": 600 }
```

Lỗi: 404 `draft.source_not_found` (chưa gắn PDF qua hệ thống — gắn bằng API 32a) · 401/403 · 503 `draft.storage_not_configured`.

**FE cần làm:** nút "Xem PDF" ở **màn quản trị** gọi API 32b. Cổng công dân giữ `GET /api/v1/procedures/{id}/source`. Link hết hạn thì gọi lại, không lưu link.

### B2 — Sửa thủ tục làm mất PDF

**Vấn đề:** `PUT /api/v1/procedure-manager/procedures/{id}` không gửi `originalPdfUrl` thì liên kết PDF bị xóa.

**Đã sửa:** PUT (và publish đè thủ tục đã có) gửi `originalPdfUrl` null hoặc bỏ trường → BE **giữ** PDF đang gắn cùng `pdfFileName`. Tạo mới và rollback vẫn lưu đúng giá trị gửi lên.

**FE cần làm:** có thể bỏ việc gửi lại `originalPdfUrl`/`pdfFileName` khi sửa thủ tục. Thay PDF vẫn dùng API 32a (`POST .../procedures/{id}/source`). Không còn cách xóa liên kết PDF qua PUT.

### B3 — Đổi loại bản bị bỏ qua âm thầm

**Vấn đề:** dòng thành phần hồ sơ có `copyRequirements` thì BE lấy theo `copyRequirements`, bỏ qua `documentCopyType`/`quantity` gửi kèm. Form cũ đổi ô loại bản/số lượng nhưng gửi lại `copyRequirements` cũ → thay đổi biến mất mà không báo lỗi.

**Đã sửa:** request **có gửi** `documentCopyType` (khác null) hoặc `quantity` **khác** giá trị suy ra từ `copyRequirements` (1 dòng → loại và số lượng của dòng đó; nhiều dòng → `documentCopyType: null`, `quantity` = tổng) → 400 `validation.failed`, khóa `checklistSchema[i].copyRequirements`. Chỉ gửi `copyRequirements`, hoặc gửi lại nguyên dòng đọc từ GET, vẫn chạy bình thường.

**FE cần làm:**

- Form nhiều loại bản (mục 4 BE-H, `copyRequirements`): chỉ gửi `copyRequirements`, **bỏ** `documentCopyType`/`quantity`.
- Nếu còn form cũ một ô loại bản + số lượng: khi lưu **xóa `copyRequirements`** khỏi dòng (hoặc cập nhật nó theo ô mới), nếu không sẽ gặp 400 này.
- Áp dụng cả `PUT .../procedures/{id}/checklist`.

### B4 — Thông điệp QR nói "đã phê duyệt"

**Vấn đề:** tra cứu QR trả `status: "APPROVED"` và "đã được cơ quan có thẩm quyền phê duyệt và đóng dấu xác thực", trái quy tắc: `DOCUMENTS_READY` chỉ là giấy tờ đã được kiểm tra, không phải phê duyệt hành chính.

**Đã sửa:** `status` thường là `READYFORDOWNLOAD` (trạng thái đơn), hồ sơ không có đơn trả `DOCUMENTS_READY`. `message` mới: "Mã QR hợp lệ: giấy tờ của hồ sơ này do WardMate cấp và đã được cán bộ kiểm tra đầy đủ. Đây không phải văn bản phê duyệt hay kết quả giải quyết thủ tục hành chính."

**FE cần làm:** trang kết quả quét QR hiển thị `message`; **bỏ** mọi chữ "đã phê duyệt", "đóng dấu", "APPROVED" ghi cố định; không ánh xạ `status` thành "Đã duyệt".

### Kiểm tra sau deploy (A1–B4)

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | `GET /api/v1/public/qr-lookup?token=https` | 404 `qr.not_found` (trước đây 200 kèm hồ sơ bất kỳ) |
| 2 | Quét QR hợp lệ hoặc `?applicationId={id}` của hồ sơ đã sẵn sàng | 200; `status` không phải `APPROVED`; `message` không có chữ "phê duyệt" |
| 3 | Gọi qr-lookup 31 lần trong 1 phút từ một máy | Lần 31: 429 `qr.rate_limited` |
| 4 | Đăng nhập sai mật khẩu 10 lần với tài khoản thử | Lần 1–10: 401; lần 11 (kể cả đúng mật khẩu): 429 `iam.login_locked` |
| 5 | Tài khoản ở bước 4 đổi mật khẩu bằng OTP rồi đăng nhập | 200 |
| 6 | Thủ tục ẩn đã gắn PDF → API 32b | 200 có `url`; API công khai `/procedures/{id}/source` vẫn 404 |
| 7 | PUT sửa tiêu đề thủ tục, bỏ `originalPdfUrl` | 200; `originalPdfUrl` giữ nguyên; API 32b vẫn mở được |
| 8 | PUT checklist dòng `copyRequirements: [{ORIGINAL,1}]` kèm `documentCopyType: "CERTIFIED_COPY"` | 400 `validation.failed` |

Bước 4–5 dùng tài khoản thử, không dùng tài khoản thật (tài khoản bị khóa 15 phút).

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi. Không gửi mật khẩu/token.
