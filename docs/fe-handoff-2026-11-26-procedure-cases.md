# Bàn giao Backend → Frontend: trường hợp của thủ tục (26/11/2026)

Người viết: Backend (Claude), 26/11/2026. Hợp đồng chi tiết: `docs/api-guide.md` (API 34, 35, 43; mục "Giấy tờ chung và giấy tờ theo trường hợp"; `POST /api/v1/applications`). Quy tắc: `docs/system-constraints.md` WF-01, **WF-CASE-01**, AI-02b.

## 0. Trạng thái

- **Backend đã có đủ cả 3 ràng buộc.** Catalog và Workflow hỗ trợ trường hợp từ trước. Lượt 26/11 thêm test và sửa phần AI bóc tách.
- **Deploy 26/11 (DEPLOY-1126d):** AIOCR `wfcase-20261126-01`. Catalog và Workflow không đổi code, bản đang chạy đã có đủ chức năng. `kha`/`main`/`deploy` ở `90102b7`.
- **Vì sao FE chưa thấy:** đã kiểm 4 thủ tục đang công khai trên Azure ngày 26/11, không thủ tục nào có từ 2 trường hợp trở lên.

  | Mã | Thủ tục | Số trường hợp | Giấy tờ |
  |---|---|---|---|
  | 1.001776 | Trợ cấp xã hội hàng tháng | 0 | 9, đều là giấy tờ chung |
  | 1.001612 | Đăng ký thành lập hộ kinh doanh | 0 | 8, đều là giấy tờ chung |
  | 1.014748 | Hưởng trợ cấp thất nghiệp | 0 | 3, đều là giấy tờ chung |
  | 1.008309 | Quyết toán thuế TNCN | 1 (`CASE-1`) | 5, tự chọn trường hợp |

  Vì vậy người dân chưa bao giờ phải chọn trường hợp. FE cần tạo một thủ tục thử có từ 2 trường hợp (mục 4) để kiểm.

## 1. Ba ràng buộc và cách Backend đáp ứng

| # | Ràng buộc | Backend |
|---|---|---|
| 1 | Hồ sơ có nhiều trường hợp: người dân phải chọn trường hợp và chỉ nộp giấy tờ của trường hợp đó | `POST /api/v1/applications` nhận `caseCode`. Có từ 2 trường hợp mà thiếu hoặc sai mã → 400 `application.invalid_case`. Hồ sơ chỉ chụp giấy tờ chung cộng giấy tờ của trường hợp đã chọn; khi nộp chỉ kiểm các giấy tờ đó. |
| 2 | Tạo thủ tục thêm được trường hợp | `contentPayload.cases[]` trong tạo/sửa thủ tục và bản nháp. Giấy tờ, biểu mẫu và cách nộp/phí gắn `caseCode`. |
| 3 | Có giấy tờ bắt buộc chung, rồi mỗi trường hợp thêm vài giấy tờ riêng | Giấy tờ có `caseCode: null` là **giấy tờ chung**, mọi trường hợp đều nộp. Giấy tờ có `caseCode` chỉ thuộc trường hợp đó. Ví dụ 5 chung + TH-1 thêm 2 + TH-2 thêm 3: chọn TH-1 → 7 giấy tờ, chọn TH-2 → 8 giấy tờ. |

## 2. Dữ liệu

```json
{
  "contentPayload": {
    "cases": [
      { "caseCode": "TH-1", "caseName": "Đăng ký lần đầu", "steps": [] },
      { "caseCode": "TH-2", "caseName": "Đăng ký lại do mất", "steps": [] }
    ],
    "submissionMethods": [
      { "methodName": "Trực tiếp", "caseCode": null, "feeAmount": 0, "feeUnit": "VND", "estimatedDays": 3 }
    ]
  },
  "checklistSchema": [
    { "checklistId": "C01", "itemName": "Tờ khai", "caseCode": null, "isMandatory": true, "quantity": 1 },
    { "checklistId": "A01", "itemName": "Giấy chứng sinh", "caseCode": "TH-1", "isMandatory": true, "quantity": 1 },
    { "checklistId": "B01", "itemName": "Đơn trình báo mất", "caseCode": "TH-2", "isMandatory": true, "quantity": 1 }
  ],
  "formDefinitions": [
    { "formCode": "TK01", "formName": "Tờ khai", "caseCode": null, "isMandatory": true, "quantity": 1 }
  ]
}
```

Các trường khác giữ nguyên như mục 4.3 của `api-guide.md`. `steps` của một trường hợp được để rỗng.

**Quy tắc Catalog** (sai → 400 ValidationProblemDetails):

| Lỗi | Khóa lỗi | Thông báo |
|---|---|---|
| Trường hợp thiếu mã hoặc tên | `ContentPayload.Cases` | "Trường hợp phải có mã và tên…" |
| Mã trùng, quá 100 ký tự hoặc có khoảng trắng đầu/cuối | `ContentPayload.Cases` | "Mã trường hợp phải duy nhất, tối đa 100 ký tự và không có khoảng trắng đầu/cuối." |
| `caseCode` của giấy tờ không có trong `cases` | `ChecklistSchema` | "Mã trường hợp của checklist phải thuộc thủ tục." |
| `caseCode` của biểu mẫu không có trong `cases` | `FormDefinitions` | "Mã trường hợp của biểu mẫu phải thuộc thủ tục." |
| `caseCode` của cách nộp không có trong `cases` | `ContentPayload.SubmissionMethods` | "Mã trường hợp của cách nộp phải thuộc thủ tục." |
| Giấy tờ gắn biểu mẫu của trường hợp khác (`formCodes`) | `ChecklistSchema` | — |
| Trùng mã biểu mẫu trong cùng trường hợp, hoặc trùng với biểu mẫu chung | `FormDefinitions` | — |

## 3. FE cần làm

### 3.1 Quản lý thủ tục (tạo, sửa, bản nháp AI)

API dùng:
- tạo: `POST /api/v1/procedure-manager/procedures` (API 34);
- sửa: `PUT /api/v1/procedure-manager/procedures/{id}` (API 35);
- bản nháp: `PUT /api/v1/procedure-manager/drafts/{id}` (API 43, payload cùng cấu trúc).

Màn hình:
- **Khối "Trường hợp":**
  - thêm, sửa, xóa dòng; mỗi dòng có Mã (`caseCode`, gợi ý tự sinh `TH-1`, `TH-2`…) và Tên (`caseName`);
  - bước riêng (`steps`) để tùy chọn.
- **Mỗi dòng giấy tờ, mỗi biểu mẫu và mỗi cách nộp có ô chọn "Áp dụng cho":**
  - "Tất cả trường hợp (giấy tờ chung)" → gửi `caseCode: null`;
  - hoặc một trường hợp cụ thể → gửi mã của trường hợp đó.
- **Xóa một trường hợp** khi vẫn còn giấy tờ, biểu mẫu hay cách nộp trỏ tới nó: FE chặn, hoặc chuyển các dòng đó về "giấy tờ chung" trước khi gửi. Nếu không, Backend trả 400.
- **`PUT /api/v1/procedure-manager/procedures/{id}/checklist`** chỉ thay danh mục giấy tờ, không thêm được trường hợp. Muốn thêm trường hợp thì dùng API 35.
- **Bản nháp từ PDF:**
  - AI tạo `cases` với mã `TH-N` khi PDF có tiêu đề "Trường hợp N:";
  - giấy tờ chung để `caseCode: null`;
  - dòng tự ghi điều kiện, như "(i) Trường hợp ủy quyền…", được đặt không bắt buộc, kèm `conditionNote`;
  - hiển thị đủ `warnings`, đặc biệt các cảnh báo bắt đầu bằng `checklistSchema:` hoặc `contentPayload.cases:`.

### 3.2 Người dân tạo hồ sơ

- **Lấy dữ liệu:** `GET /api/v1/procedures/{id}`, đọc `contentPayload.cases` và `checklistSchema[].caseCode`.
- **Có từ 2 trường hợp trở lên:**
  - hiện bước **"Chọn trường hợp của bạn"** (danh sách `caseName`) trước khi tạo hồ sơ;
  - chưa chọn thì không cho bấm tạo;
  - khi chọn, xem trước danh mục bằng cách lọc `checklistSchema` theo `caseCode == null || caseCode == chọn`, chia làm "Giấy tờ chung" và "Giấy tờ của trường hợp này".
- **Có 0 hoặc 1 trường hợp:** không cần hỏi; gửi `caseCode: null`, Backend tự chọn khi chỉ có 1.
- **Tạo hồ sơ:** `POST /api/v1/applications` với body `{ "procedureId": "…", "caseCode": "TH-1", "wardCode": "…", "formData": {} }`.
  - 201: `caseCode` và `checklists[]` chỉ gồm giấy tờ chung cộng giấy tờ của TH-1. Hiển thị theo `checklists[]` của hồ sơ, **không** theo `checklistSchema` của thủ tục, vì danh mục được chụp lại lúc tạo.
  - 400 `application.invalid_case` "Vui lòng chọn đúng trường hợp của thủ tục.": quay lại bước chọn trường hợp. Mã phân biệt chữ hoa và chữ thường; gửi đúng `caseCode` đọc được từ thủ tục.
- **Chọn nhầm trường hợp:** chưa có API đổi. Hướng dẫn người dân hủy hồ sơ nháp (`POST /api/v1/applications/{id}/cancel`) rồi tạo lại.

### 3.3 Cán bộ xem hồ sơ

- Hồ sơ có trường `caseCode`, không có tên trường hợp. FE lấy `caseName` từ chi tiết thủ tục (`procedureId`) để hiện "Trường hợp: Đăng ký lại do mất".
- Mỗi dòng trong `checklists[]` có `caseCode`; `null` là giấy tờ chung. Nên hiện nhãn "Chung" hoặc nhãn tên trường hợp.

## 4. Kịch bản kiểm thử sau deploy

Tạo một thủ tục thử bằng API 34 (`isActive: true`):
- 2 trường hợp: `TH-1` "Trường hợp A", `TH-2` "Trường hợp B";
- 5 giấy tờ chung, bắt buộc: `C01`–`C05`, `caseCode: null`;
- `TH-1` thêm `A01` (bắt buộc) và `A02` (không bắt buộc);
- `TH-2` thêm `B01`–`B03` (bắt buộc).

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Lưu thủ tục như trên | 201; chi tiết thủ tục có 2 `cases` và 10 giấy tờ |
| 2 | Lưu thủ tục có giấy tờ gắn `caseCode: "TH-9"` (không có trong `cases`) | 400, khóa `ChecklistSchema` |
| 3 | Người dân mở thủ tục | Có bước chọn trường hợp; xem trước TH-1 thấy 5 chung + 2 riêng |
| 4 | Tạo hồ sơ không gửi `caseCode` | 400 `application.invalid_case` |
| 5 | Tạo hồ sơ `caseCode: "TH-1"` | 201; `checklists` có 7 dòng `C01–C05, A01, A02`; không có `B01–B03` |
| 6 | Tạo hồ sơ `caseCode: "TH-2"` | 201; 8 dòng `C01–C05, B01–B03` |
| 7 | Hồ sơ TH-1: tải đủ 6 giấy tờ bắt buộc (bỏ `A02`) rồi nộp | Nộp được, không bị đòi giấy tờ của TH-2 |
| 8 | Hồ sơ TH-1: thiếu `C03` rồi nộp | Bị chặn vì thiếu giấy tờ bắt buộc (giấy tờ chung vẫn bắt buộc) |
| 9 | Tạo hồ sơ `caseCode: "th-1"` (chữ thường) | 400 `application.invalid_case` |
| 10 | Thủ tục 1 trường hợp (1.008309), tạo hồ sơ không gửi `caseCode` | 201, `caseCode: "CASE-1"` |
| 11 | Sửa thủ tục, bỏ `A02` khỏi TH-1 | Hồ sơ cũ ở bước 5 vẫn có `A02` (danh mục đã chụp lại); hồ sơ mới không có |
| 12 | Bóc tách PDF có "Trường hợp 1:", "Trường hợp 2:" và giấy tờ chung ở đầu (không bắt buộc kiểm vì tốn hạn mức AI) | `cases` TH-1, TH-2; giấy tờ chung `caseCode: null`; có cảnh báo liệt kê giấy tờ chung để kiểm |

## 5. Giới hạn hiện tại

- Chưa đổi được trường hợp của hồ sơ nháp; phải hủy và tạo lại. Chủ dự án chưa quyết có làm hay không.
- Catalog không cảnh báo khi thủ tục có nhiều trường hợp mà không giấy tờ nào gắn mã. Khi đó chọn trường hợp không đổi danh mục.
- Mỗi hồ sơ chỉ chọn được một trường hợp. Điều kiện cộng dồn (ví dụ "ủy quyền" và "chưa có định danh điện tử" cùng lúc) nên ghi bằng `conditionNote` trên giấy tờ không bắt buộc, không làm thành trường hợp.
- Dữ liệu cần Quản lý thủ tục sửa: 1.001612 (hộ kinh doanh) có 5 dòng điều kiện hoặc ghi chú đang bắt buộc cho mọi người. 1.001776 (thực hiện / điều chỉnh / thôi hưởng) có thể tách thành trường hợp nếu nguồn chia như vậy.
