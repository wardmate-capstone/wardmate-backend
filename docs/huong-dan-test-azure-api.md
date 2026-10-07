# HƯỚNG DẪN TỰ TEST TẤT CẢ API DOCUMENT & FORM TRÊN AZURE

Tài liệu này hướng dẫn chi tiết từng bước để bạn tự kiểm thử toàn bộ các API của **Document & Form Service** trực tiếp trên môi trường **Azure Container Apps** thông qua **Swagger UI**, **Postman** hoặc **cURL**.

---

## 🌐 1. Thông tin kết nối Azure

- **Swagger UI trực tiếp**: [https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html](https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html)
- **Base URL Gateway**: `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`
- **Health Check**: `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/health`

---

## 📋 2. Kịch bản Test theo luồng nghiệp vụ thực tế

Hệ thống được thiết kế theo 3 nhóm API độc lập:
1. 📑 **Quản lý Phôi mẫu (Form Templates)**: Dành cho Admin/Hệ thống định nghĩa các mẫu đơn phôi.
2. 👤 **Hồ sơ Người dân (Citizen Submissions)**: Dành cho người dân lưu nháp nhiều lần, điền tiếp và nộp hồ sơ.
3. 🏢 **Xét duyệt Hồ sơ (Officer Submissions)**: Dành cho cán bộ một cửa xem danh sách chờ duyệt, yêu cầu sửa đổi (trả về), hoặc duyệt hoàn tất.

---

### BƯỚC 1: Quản lý Phôi mẫu biểu mẫu (Form Templates)

#### 1.1 Lấy danh sách phôi mẫu hiện có
- **Method & Route**: `GET /api/v1/form-templates`
- **Query Params**:
  - `page`: `1`
  - `pageSize`: `10`
  - `isActive`: `true`
- **Swagger**: Tìm nhóm `FormTemplates` ➔ chọn `GET /api/v1/form-templates` ➔ bấm **Try it out** ➔ **Execute**.
- **Kết quả mong đợi (200 OK)**: Trả về danh sách phôi mẫu phân trang kèm `id`, `code`, `title`.

#### 1.2 Tạo một phôi mẫu mới
- **Method & Route**: `POST /api/v1/form-templates`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
```json
{
  "code": "DON-XAC-NHAN-CU-TRU",
  "title": "Đơn xác nhận thông tin về cư trú",
  "createdBy": "admin-test"
}
```
- **Kết quả mong đợi (201 Created)**: Trả về object phôi mẫu vừa tạo kèm `id` (GUID) — **hãy copy `id` này để dùng cho bước lưu nháp của người dân**.

#### 1.3 Xem chi tiết một phôi mẫu
- **Method & Route**: `GET /api/v1/form-templates/{id}`
- **Path Param**: `id` = GUID lấy từ Bước 1.2
- **Kết quả mong đợi (200 OK)**: Trả về chi tiết phôi mẫu.

---

### BƯỚC 2: Luồng Người dân (Citizen Submissions Workflow)

#### 2.1 Người dân tạo bản nháp đầu tiên (Lưu nháp - Draft)
> Người dân mở mẫu phôi, điền một vài thông tin vào file `.docx` và bấm "Lưu nháp" để ngày mai điền tiếp.

- **Method & Route**: `POST /api/v1/citizen/submissions/draft`
- **Content-Type**: `multipart/form-data`
- **Form Fields**:
  - `templateId` *(string/Guid)*: `<ID_PHÔI_MẪU_TỪ_BƯỚC_1.2>`
  - `applicantId` *(string/Guid)*: `11111111-1111-1111-1111-111111111111` (hoặc ID của người dân)
  - `file` *(file binary)*: Chọn bất kỳ 1 file `.docx` trên máy tính của bạn.
- **Swagger**: Chọn `POST /api/v1/citizen/submissions/draft` ➔ **Try it out** ➔ điền `templateId`, `applicantId` ➔ chọn file `.docx` ➔ **Execute**.
- **Kết quả mong đợi (201 Created)**:
```json
{
  "submissionId": "a1b2c3d4-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Draft",
  "fileName": "don_cu_tru.docx",
  "fileSizeBytes": 15420,
  "updatedAt": "2026-10-06T...",
  "message": "Draft created successfully."
}
```
👉 **Lưu lại `submissionId` này để thực hiện các bước tiếp theo.**

---

#### 2.2 Người dân cập nhật bản nháp (Điền tiếp - Update Draft)
> Ngày hôm sau, người dân mở lại hồ sơ, điền thêm các thông tin còn thiếu và lưu tiếp.

- **Method & Route**: `PUT /api/v1/citizen/submissions/{submissionId}/draft`
- **Path Param**: `submissionId` = GUID từ Bước 2.1
- **Content-Type**: `multipart/form-data`
- **Form Fields**:
  - `applicantId`: `11111111-1111-1111-1111-111111111111` *(phải trùng với người tạo ở bước 2.1)*
  - `file`: Chọn file `.docx` phiên bản mới hơn.
- **Kết quả mong đợi (200 OK)**:
```json
{
  "submissionId": "a1b2c3d4-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Draft",
  "message": "Draft updated successfully."
}
```

---

#### 2.3 Người dân nộp hồ sơ chính thức (Submit Application)
> Khi đã điền đầy đủ và kiểm tra kỹ, người dân bấm "Nộp hồ sơ". Trạng thái chuyển từ `Draft` sang `Submitted`.

- **Method & Route**: `POST /api/v1/citizen/submissions/{submissionId}/submit`
- **Path Param**: `submissionId` = GUID từ Bước 2.1
- **Content-Type**: `application/json`
- **Request Body**:
```json
{
  "applicantId": "11111111-1111-1111-1111-111111111111"
}
```
- **Kết quả mong đợi (200 OK)**:
```json
{
  "submissionId": "a1b2c3d4-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Submitted",
  "submittedAt": "2026-10-06T...",
  "message": "Application submitted successfully."
}
```

---

### BƯỚC 3: Luồng Cán bộ xét duyệt (Officer Workflow)

#### 3.1 Cán bộ lấy danh sách hồ sơ đang chờ duyệt
- **Method & Route**: `GET /api/v1/officer/submissions?status=Submitted&page=1&pageSize=10`
- **Swagger**: Chọn nhóm `Officer` ➔ `GET /api/v1/officer/submissions` ➔ điền `status` = `Submitted` ➔ **Execute**.
- **Kết quả mong đợi (200 OK)**: Danh sách chứa hồ sơ vừa nộp ở Bước 2.3 với trạng thái `"status": "Submitted"`.

#### 3.2 Cán bộ xem chi tiết hồ sơ
- **Method & Route**: `GET /api/v1/officer/submissions/{submissionId}`
- **Path Param**: `submissionId` = GUID từ Bước 2.1
- **Kết quả mong đợi (200 OK)**: Thông tin chi tiết hồ sơ kèm lịch sử nộp.

#### 3.3 (Tùy chọn A) Cán bộ yêu cầu sửa đổi, bổ sung (Request Revision)
> Nếu cán bộ thấy file đính kèm hoặc thông tin bị sai/mờ, cán bộ gửi trả về kèm lý do.

- **Method & Route**: `POST /api/v1/officer/submissions/{submissionId}/request-revision`
- **Path Param**: `submissionId`
- **Content-Type**: `application/json`
- **Request Body**:
```json
{
  "officerId": "22222222-2222-2222-2222-222222222222",
  "comment": "Ảnh chụp CCCD đính kèm trong file bị mờ, vui lòng chụp lại rõ nét hơn."
}
```
- **Kết quả mong đợi (200 OK)**: Trạng thái hồ sơ chuyển thành **`RevisionRequested`** kèm `officerComment`.
- *Luồng tiếp theo:* Người dân gọi lại `PUT /api/v1/citizen/submissions/{id}/draft` (upload file đã sửa) ➔ gọi `POST /api/v1/citizen/submissions/{id}/submit` để nộp lại.

#### 3.4 (Tùy chọn B) Cán bộ duyệt hồ sơ hoàn tất (Approve Application)
> Khi hồ sơ đã đầy đủ và hợp lệ, cán bộ bấm duyệt.

- **Method & Route**: `POST /api/v1/officer/submissions/{submissionId}/approve`
- **Path Param**: `submissionId`
- **Content-Type**: `application/json`
- **Request Body**:
```json
{
  "officerId": "22222222-2222-2222-2222-222222222222"
}
```
- **Kết quả mong đợi (200 OK)**:
```json
{
  "submissionId": "a1b2c3d4-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Approved",
  "message": "Application approved successfully."
}
```

---

### BƯỚC 4: Tải file DOCX hồ sơ về máy (Download DOCX)

- **Method & Route**:
  - Dành cho Người dân: `GET /api/v1/citizen/submissions/{submissionId}/download-docx`
  - Dành cho Cán bộ: `GET /api/v1/officer/submissions/{submissionId}/download-docx`
- **Swagger / Trình duyệt**: Paste trực tiếp đường link `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/{submissionId}/download-docx` vào thanh địa chỉ trình duyệt.
- **Kết quả mong đợi (200 OK)**: Trình duyệt tự động download file `.docx` về máy.

---

## 🧪 3. Mã lệnh cURL mẫu để test nhanh qua Terminal

### 1. Tạo bản nháp (Upload file .docx)
```bash
curl -X POST "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/draft" \
  -F "templateId=31eacf66-b1d6-4d67-8d5e-af823e6e967f" \
  -F "applicantId=11111111-1111-1111-1111-111111111111" \
  -F "file=@/path/to/your/file.docx"
```

### 2. Nộp hồ sơ chính thức
```bash
curl -X POST "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/<SUBMISSION_ID>/submit" \
  -H "Content-Type: application/json" \
  -d "{\"applicantId\": \"11111111-1111-1111-1111-111111111111\"}"
```

### 3. Cán bộ phê duyệt hồ sơ
```bash
curl -X POST "https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/officer/submissions/<SUBMISSION_ID>/approve" \
  -H "Content-Type: application/json" \
  -d "{\"officerId\": \"22222222-2222-2222-2222-222222222222\"}"
```
