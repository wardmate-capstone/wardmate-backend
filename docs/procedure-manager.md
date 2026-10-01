# TASK-08 — API quản lý thủ tục

## Chạy và xác thực

Service trực tiếp: `http://localhost:5002/swagger` (Development). Gateway: `http://localhost:5000/api/v1/procedure-manager/procedures`.

- Cấp `ConnectionStrings__ProcedureDatabase` như hướng dẫn trong `procedure-catalog.md`.
- Cấp **`Jwt__Key` giống khóa ký của IAM**, tối thiểu 32 byte UTF-8; không lưu khóa trong source. `Jwt__Issuer` mặc định `wardmate`, `Jwt__Audience` mặc định `wardmate-client`; nếu IAM dùng giá trị khác thì cấu hình đồng bộ.
- Lấy Access Token từ IAM, dùng nút **Authorize** trong Swagger hoặc header `Authorization: Bearer <accessToken>`.
- API quản lý chấp nhận vai trò `PROCEDURE_MANAGER` hoặc `IT_ADMIN`. API đọc công khai vẫn không cần token.
- Service kiểm tra chữ ký HS256, issuer, audience và hạn token. Vai trò lấy từ token; thay đổi vai trò/disable tài khoản ở IAM chỉ phản ánh tại service này khi token cũ hết hạn. Không truy cập database IAM và không có introspection thời gian thực trong task này.
- Thiếu/khóa JWT sai cấu hình sẽ chặn startup. Database migration tự chạy khi `Database__AutoMigrate=true` (Development đã bật); ngoài Development phải áp dụng migration theo quy trình vận hành.

## Hợp đồng API

| Method | Route | Request body | Response |
|---|---|---|---|
| POST | `/api/v1/procedure-manager/procedures` | ProcedureInput bên dưới | 201 + ProcedureDetailDto + Location; 400 validation/category; 409 trùng mã |
| PUT | `/api/v1/procedure-manager/procedures/{id}` | Toàn bộ ProcedureInput + decisionNumber + effectiveDate | 200 ProcedureDetailDto; 400; 404; 409 |
| PATCH | `/api/v1/procedure-manager/procedures/{id}/status` | `{ "isActive": false, "reason": "Tạm ngưng tiếp nhận" }` | 200 `{id,isActive,reason?,updatedAt}`; 400 thiếu trạng thái; 404 |
| GET | `/api/v1/procedure-manager/procedures/{id}/versions` | Không | 200 mảng phiên bản giảm dần theo versionNumber; 404 |

Tất cả endpoint quản lý có thể trả 401 (token thiếu/sai/hết hạn) hoặc 403 (không đúng vai trò). Lỗi nghiệp vụ dùng `application/problem+json` với `status`, `title` tiếng Việt, `code`, `traceId`, `instance` và `errors` khi validation. Null được bỏ khỏi response DTO; `snapshotData` là **JSON object** đóng băng, các null bên trong snapshot được giữ nguyên để phản ánh dữ liệu lịch sử.

Các mã lỗi: `validation.failed`, `procedure.category_not_found`, `procedure.code_exists`, `procedure.not_found`, `auth.unauthorized`, `auth.forbidden`. ID phải là GUID; ID sai định dạng không khớp route.

Ví dụ tạo mới (dữ liệu minh họa):

```json
{
  "categoryId": 1,
  "procedureCode": "DEMO-NEW-001",
  "title": "Thủ tục đăng ký minh họa",
  "issuingAuthority": "Cơ quan minh họa",
  "executingAgency": "Đơn vị minh họa",
  "levelOfImplementation": "Cấp Xã",
  "targetAudience": "Công dân Việt Nam",
  "feeSummary": "Miễn phí",
  "processingTimeSummary": "1 ngày",
  "contentPayload": {
    "decisionNumber": "DEMO-QD-001",
    "receivingAddress": "Bộ phận một cửa minh họa",
    "submissionMethods": [{ "methodName": "Trực tiếp", "feeAmount": 0, "feeUnit": "VND", "estimatedDays": 1 }],
    "legalReferences": [],
    "results": ["Kết quả minh họa"],
    "cases": [{ "caseCode": "NORMAL", "caseName": "Thông thường", "steps": [{ "stepOrder": 1, "stepName": "Tiếp nhận", "executor": "Cán bộ", "actionDetails": "Kiểm tra hồ sơ" }] }]
  },
  "checklistSchema": [{ "checklistId": "ID01", "caseCode": "NORMAL", "submissionType": "XUAT_TRINH", "itemName": "Giấy tờ định danh", "documentCopyType": "ORIGINAL", "quantity": 1, "isMandatory": true }],
  "formDefinitions": [{ "caseCode": "NORMAL", "formCode": "FORM01", "formName": "Tờ khai minh họa", "formType": "ONLINE_INTERACTIVE", "quantity": 1, "isMandatory": true }]
}
```

PUT là thay thế toàn bộ các trường cấu hình, không phải partial update. Thêm `"decisionNumber": "DEMO-QD-002"` và `"effectiveDate": "2026-10-01"` ở cấp gốc body. Số quyết định cấp gốc trở thành `contentPayload.decisionNumber` của bản hiện hành. PUT giữ nguyên `isActive` và lý do trạng thái; đổi trạng thái qua PATCH riêng. Mã thủ tục được trim, kiểm tra duy nhất theo so sánh PostgreSQL hiện tại (phân biệt hoa/thường).

## Phiên bản và transaction

- POST lưu thủ tục hoạt động và snapshot **version 1** trong cùng transaction; ngày hiệu lực ban đầu là ngày UTC tạo thủ tục, số quyết định lấy từ contentPayload.
- PUT khóa bản ghi (`FOR UPDATE`), cấp `max(versionNumber)+1`, chụp **dữ liệu cũ trước cập nhật**, sau đó ghi dữ liệu mới và UpdatedAt UTC. Hai request đồng thời được xử lý tuần tự, không sinh số phiên bản trùng.
- Vì yêu cầu snapshot trạng thái trước thay đổi, lần PUT đầu tạo version 2 có cùng nội dung ban đầu như version 1. Bản hiện hành đọc từ API procedure; lịch sử không phải danh sách snapshot trạng thái sau mỗi PUT.
- `decisionNumber` và `effectiveDate` trên bản ghi lịch sử mô tả lần thay đổi. Không dùng effectiveDate để hẹn giờ: dữ liệu mới được áp dụng ngay khi PUT thành công.
- Seed TASK-07 chưa có lịch sử: lần cập nhật đầu tiên seed sinh version 1 của dữ liệu cũ. Thủ tục tạo qua POST luôn có version 1 ngay khi tạo.
- PATCH lưu `status_change_reason` (text nullable) và UpdatedAt, không sinh version mới. Không xóa hồ sơ công dân đã nộp; việc chặn tạo hồ sơ phải được ApplicationWorkflow kiểm tra trạng thái khi tích hợp. GET công khai trả 404 khi thủ tục inactive.
- Mã thủ tục trùng trả 409 cả khi request đồng thời. Lỗi validation hoặc conflict không để lại phiên bản thừa.
- Migration mới chỉ thêm cột nullable lý do trạng thái; không thay đổi dữ liệu IAM.

## Kiểm thử

Chạy `dotnet build WardMate.sln -c Release`, sau đó `dotnet test WardMate.sln -c Release --no-build`. Testcontainers cần Docker đang chạy để khởi tạo PostgreSQL 16 tạm; không build image hoặc khởi động stack ứng dụng. Test bao gồm JSONB/version ban đầu, snapshot trước cập nhật, trạng thái, concurrency, validation, unique code, JWT và Swagger.

## TASK-08-LIST — Danh sách cho người quản lý

`GET /api/v1/procedure-manager/procedures` yêu cầu Bearer có role PROCEDURE_MANAGER hoặc IT_ADMIN. Không có request body.

Query: page=1, pageSize=20 (tối đa 100), search (tìm mã/tên, không phân biệt hoa/thường, tối đa 255 ký tự), categoryId (số nguyên dương), isActive (tùy chọn). Bỏ isActive để lấy cả đang hoạt động và đã đóng; true/false để lọc riêng. Các bộ lọc kết hợp AND. Sắp xếp procedureCode ASC rồi id ASC.

Ví dụ: `/api/v1/procedure-manager/procedures?page=1&pageSize=20&categoryId=1&isActive=false`.

200 trả ProcedureListDto `{items,page,pageSize,totalCount,totalPages}` giống API công khai (xem procedure-catalog.md). Item chỉ chứa thông tin tóm tắt, không có các khối JSONB. 400 nếu query sai; 401 token không hợp lệ; 403 thiếu quyền. Trang vượt giới hạn dữ liệu trả items rỗng. Route Gateway hiện có hỗ trợ cả hai API danh sách, không cần thay cấu hình.

TASK-09 nâng cấp hợp đồng hai GET danh sách: dùng keyword, pageNumber (mặc định 1), pageSize (mặc định 10), levelOfImplementation, sortBy, isAscending; manager vẫn hỗ trợ isActive nullable. Response metadata dùng currentPage/hasPrevious/hasNext, DTO tóm tắt mới. Chi tiết và migration unaccent xem mục TASK-09 trong procedure-catalog.md.

**TASK-09 điều chỉnh:** Xem [hợp đồng xuất bản đã đối soát](procedure-reviewed-publishing.md). POST /api/v1/procedure-manager/procedures/publish nhận JSON đã duyệt, upsert theo mã trong transaction và giữ snapshot/PDF gốc. GET quản lý trả ProcedureManagerSummaryDto có versionCount, isActive, createdAt/updatedAt. Không còn endpoint/importer CSV. CRUD cũ cũng hỗ trợ originalPdfUrl/pdfFileName; PUT phải gửi lại hai trường này để giữ liên kết PDF.
