# TASK-09 — Tìm kiếm và xuất bản bản nháp đã đối soát

## Phạm vi

ProcedureCatalog nhận JSON đã được người quản lý kiểm tra từ màn hình đối soát. Task này không upload file, không đọc PDF/OCR, không gọi AI và không xây màn hình FE. File gốc được upload trước; FE gửi URL và tên file cùng dữ liệu đã duyệt. Toàn bộ endpoint/module/dependency CSV đã được gỡ.

Không cần đổi route Gateway. Local service: `http://localhost:5002/swagger`; Gateway: `http://localhost:5000/api/v1/...`. Người vận hành chạy lại service để nạp code mới. JWT được cấu hình cùng IAM, như tài liệu Procedure Manager.

## Tra cứu công khai

`GET /api/v1/procedures` không cần token, luôn ép `IsActive=true` cả ở handler và repository. Query không có quyền thay điều kiện này.

- `keyword`: tùy chọn, tối đa 255 ký tự; tìm chứa theo mã/tên, không dấu/không phân biệt hoa thường. `%`, `_`, `\` là ký tự tìm kiếm thông thường.
- `categoryId`: số nguyên dương, tùy chọn.
- `levelOfImplementation`: tùy chọn, tối đa 50 ký tự; so khớp toàn bộ chuỗi không dấu/không phân biệt hoa thường. Không truyền thì ưu tiên Xã/Phường khi sort Title, không loại các cấp khác.
- `pageNumber=1`, `pageSize=10`, **pageSize tối đa 50**.
- Giữ tùy chọn sort đã có: `sortBy=Title` (Title, ProcedureCode, UpdatedAt, CreatedAt, LevelOfImplementation), `isAscending=true`. Id là khóa phụ ổn định.
- Giữ alias cũ `search` (khi thiếu keyword), `page` (nếu truyền, ghi đè pageNumber). FE nên dùng tên chuẩn.

Response `PagedResult<ProcedureSummaryDto>`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "procedureCode": "DEMO-REVIEW-01",
      "title": "Thủ tục minh họa đã đối soát",
      "categoryName": "Hộ tịch",
      "levelOfImplementation": "Cấp Xã",
      "feeSummary": "Theo từng trường hợp",
      "processingTimeSummary": "Theo hình thức nộp",
      "originalPdfUrl": "https://example.blob.core.windows.net/procedures/source.pdf",
      "updatedAt": "2026-10-01T00:00:00Z"
    }
  ],
  "currentPage": 1,
  "totalPages": 1,
  "totalCount": 1,
  "pageSize": 10,
  "hasPrevious": false,
  "hasNext": false
}
```

Không có `isActive` trong DTO public vì tất cả kết quả đều đang hoạt động. Không tải JSONB vào danh sách. Trang vượt dữ liệu/danh mục không tồn tại trả 200 items rỗng; không có kết quả thì totalPages=0. Count và items là hai SQL query nên có thể lệch tức thời khi dữ liệu thay đổi đồng thời.

## Danh sách quản lý

`GET /api/v1/procedure-manager/procedures` yêu cầu Bearer với `PROCEDURE_MANAGER` hoặc `IT_ADMIN`.

Nhận cùng tham số tìm kiếm/phân trang; **pageSize tối đa 100**. `isActive` không truyền = lấy cả hai trạng thái; true/false lọc riêng.

Response `PagedResult<ProcedureManagerSummaryDto>`: các trường item của public cộng `isActive`, **`versionCount`** và `createdAt`. VersionCount đếm số bản ghi thực tế, không phải số phiên bản suy đoán; seed cũ chưa có lịch sử trả 0. EF projection dùng COUNT, không tải snapshot/JSONB.

Hai GET trả 200; query sai trả 400 `validation.failed`. Manager còn có 401/403.

## Xuất bản

`POST /api/v1/procedure-manager/procedures/publish`, Content-Type `application/json`; yêu cầu Bearer với `PROCEDURE_MANAGER` hoặc `IT_ADMIN`.

Body phẳng theo `ReviewedProcedureInput`, gồm mọi trường của `ProcedureInput` và `originalPdfUrl?`, `pdfFileName?`:

```json
{
  "procedureCode": "DEMO-REVIEW-01",
  "categoryId": 1,
  "title": "Thủ tục minh họa đã đối soát",
  "issuingAuthority": "Cơ quan minh họa",
  "executingAgency": "Đơn vị minh họa",
  "levelOfImplementation": "Cấp Xã",
  "targetAudience": "Công dân Việt Nam",
  "feeSummary": "Theo từng trường hợp",
  "processingTimeSummary": "Theo hình thức nộp",
  "originalPdfUrl": "https://example.blob.core.windows.net/procedures/source.pdf",
  "pdfFileName": "Thủ tục đã đối soát.pdf",
  "contentPayload": {
    "decisionNumber": "DEMO-QD-01",
    "receivingAddress": "Bộ phận tiếp nhận minh họa",
    "submissionMethods": [
      { "methodName": "Trực tiếp", "feeAmount": 15000, "feeUnit": "VND", "estimatedDays": 3, "note": "Mức phí minh họa; cần đối soát trước khi dùng" }
    ],
    "legalReferences": [],
    "results": ["Kết quả minh họa"],
    "cases": [
      { "caseCode": "CASE-1", "caseName": "Trường hợp thứ nhất", "steps": [
        { "stepOrder": 1, "stepName": "Tiếp nhận", "executor": "Cán bộ", "actionDetails": "Kiểm tra hồ sơ" }
      ] }
    ]
  },
  "checklistSchema": [
    { "checklistId": "DOC-1", "caseCode": "CASE-1", "submissionType": "NOP", "itemName": "Giấy tờ minh họa", "documentCopyType": "ORIGINAL", "quantity": 1, "isMandatory": true }
  ],
  "formDefinitions": [
    { "formCode": "FORM-1", "caseCode": "CASE-1", "formName": "Tờ khai minh họa", "formType": "DOCX_TEMPLATE", "quantity": 1, "isMandatory": true }
  ]
}
```

Cases/checklist/forms là danh sách có thể chứa nhiều trường hợp (đã kiểm thử 10), không cố định theo file PDF mẫu. Thuộc tính formTemplateId nếu có là ID bên DocumentForm, không phải nội dung file hoặc FK liên database.

- Code chưa có: tạo active procedure và version 1 trong cùng transaction.
- Code đã có: khóa bản ghi, chụp **trạng thái cũ** thành version tiếp theo, rồi thay thế toàn bộ input. Giữ nguyên Id, CreatedAt, IsActive và lý do đóng/mở; thủ tục đã đóng không tự mở lại khi cập nhật. Dùng PATCH status để mở lại.
- Khóa transaction theo mã bảo vệ cả hai publish đồng thời vào mã chưa tồn tại; khóa row phối hợp với update/status cũ. Race với API create/đổi mã khác vẫn có thể trả 409, không lưu dở dang.
- Version giữ OriginalPdfUrl/PdfFileName của **bản cũ**, cả cột riêng lẫn SnapshotData. Version1 giữ bản lúc tạo. Số quyết định lịch sử lấy từ lần xuất bản; ngày hiệu lực metadata dùng ngày UTC xuất bản vì input không có EffectiveDate.
- Publish lặp lại vẫn tạo version mới; FE tránh double-submit/retry mù. Không có idempotency-key trong hợp đồng này.
- Cả tạo/cập nhật trả **200 ProcedureDetailDto** với hai trường PDF. 400 validation/category; 401/403 auth; 409 conflict mã khi có thao tác đồng thời khác.
- CategoryId phải tồn tại. Mã được trim, phân biệt hoa/thường theo unique index hiện tại. Title tối thiểu 10, tối đa 500 ký tự. FeeSummary/ProcessingTimeSummary phải được gửi, không tự mặc định thành miễn phí/1 ngày khi publish.
- OriginalPdfUrl nullable, tối đa 500 ký tự, HTTPS tuyệt đối không chứa userinfo. PdfFileName nullable tối đa 255 ký tự. Không có file thì gửi null, không chuỗi URL rỗng. Backend chỉ lưu URL, không fetch/kiểm chứng file ở URL đó. Dùng link gốc bền vững; không nhúng SAS bí mật dài hạn trong dữ liệu public. Nếu Blob private, cần cơ chế cấp link xem/tải riêng của tầng lưu trữ.
- JSON null được bỏ khỏi DTO response như trước. FE cho phép thiếu hai trường PDF. PUT cũ là thay thế toàn bộ; gửi lại hai trường PDF khi muốn giữ chúng.
- ProblemDetails giữ `code`, `traceId`, `instance`, thông báo tiếng Việt và errors khi validation. Không thay đổi luồng token IAM.

## Database và kiểm thử

Migration `ProcedureOriginalPdf`: thêm hai cột nullable với độ dài tương ứng vào procedures và procedure_versions, không thay dữ liệu nghiệp vụ cũ. Phiên bản cũ không có PDF vẫn null. Migration `ProcedureUnaccentSearch` từ phần tìm kiếm vẫn được giữ.

Chạy Release build và dotnet test solution. Integration tests dùng PostgreSQL 16 tạm, kiểm tra: JSONB 10 cases/checklists/forms, URL/tên file ở live/version/snapshot, cập nhật trạng thái đóng, metadata/query filters, publish đồng thời cùng mã, rollback khi DB lỗi, validation và role/Swagger. Kết quả thực tế ghi tại PROGRESS.md. Không tự áp dụng migration vào database Local Dev hay build Docker image.
