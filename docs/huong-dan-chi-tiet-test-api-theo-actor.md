# HƯỚNG DẪN TEST TỪNG API THEO LUỒNG CỦA TỪNG ACTOR (AZURE LIVE)

Tài liệu này cung cấp hướng dẫn kiểm thử chi tiết, chính xác 100% cho **tất cả 18 API** của `WardMate.Services.DocumentForm`, được chia theo **4 cụm Actor** với luồng nghiệp vụ thực tế.

- **Swagger URL**: [https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html](https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/swagger/index.html)
- **Base Gateway URL**: `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`

---

# CỤM 1: 📑 FormTemplates — Quản trị viên / Hệ thống

> **Mục đích**: Khởi tạo, quản lý biểu mẫu phôi, upload file DOCX gốc, trích xuất placeholders và cung cấp link tải phôi mẫu cho người dân.

```
[Admin] POST /form-templates (Tạo phôi)
   │
   ▼
[Admin] POST /form-templates/{id}/upload-docx (Upload file mẫu DOCX)
   │
   ├─► GET /form-templates (Xem danh sách phôi)
   ├─► GET /form-templates/{id} (Xem chi tiết phôi)
   ├─► GET /form-templates/{id}/docx-url (Lấy link SAS tải nhanh)
   └─► GET /form-templates/{id}/download-docx (Tải trực tiếp binary DOCX)
```

---

### 1.1 `POST /api/v1/form-templates` — Tạo phôi biểu mẫu mới
- **Actor**: Quản trị viên (Admin).
- **Mục đích**: Khởi tạo thông tin biểu mẫu (mã biểu mẫu, tên biểu mẫu).
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/form-templates`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
```json
{
  "code": "DON-XAC-NHAN-CU-TRU-2026",
  "title": "Đơn xác nhận thông tin cư trú (Bản chuẩn)",
  "createdBy": "admin-system"
}
```
- **Response Thành công (201 Created)**:
```json
{
  "id": "31eacf66-b1d6-4d67-8d5e-af823e6e967f",
  "code": "DON-XAC-NHAN-CU-TRU-2026",
  "title": "Đơn xác nhận thông tin cư trú (Bản chuẩn)",
  "fileDocxUrl": null,
  "isActive": true,
  "createdAtUtc": "2026-10-06T15:44:38Z"
}
```
👉 **Copy `id` (`templateId`) để dùng cho các bước tiếp theo.**

---

### 1.2 `POST /api/v1/form-templates/{templateId}/upload-docx` — Upload file DOCX phôi mẫu
- **Actor**: Quản trị viên (Admin).
- **Mục đích**: Tải file Word `.docx` chứa placeholder (ví dụ: `{{HoTen}}`, `{{NgaySinh}}`) lên Azure Blob Storage và gắn vào biểu mẫu.
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/form-templates/{templateId}/upload-docx`
- **Path Param**: `templateId` = GUID từ bước 1.1
- **Headers**: `Content-Type: multipart/form-data`
- **Form Fields**:
  - `file`: Chọn file `.docx` mẫu từ máy tính của bạn.
- **Response Thành công (200 OK)**:
```json
{
  "templateId": "31eacf66-b1d6-4d67-8d5e-af823e6e967f",
  "blobUrl": "https://...blob.core.windows.net/form-templates/form-templates/DON-XAC-NHAN-CU-TRU-2026/...",
  "placeholders": [
    { "name": "HoTen", "key": "ho_ten", "dataType": "text" },
    { "name": "NgaySinh", "key": "ngay_sinh", "dataType": "date" }
  ]
}
```

---

### 1.3 `GET /api/v1/form-templates` — Lấy danh sách phôi mẫu
- **Actor**: Mọi người dùng / Frontend.
- **Mục đích**: Hiển thị danh mục các biểu mẫu mà người dân có thể chọn để làm thủ tục.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/form-templates?page=1&pageSize=10&isActive=true`
- **Query Params**:
  - `page`: Trang cần lấy (mặc định `1`).
  - `pageSize`: Số lượng mục / trang (mặc định `20`).
  - `isActive`: `true` (lọc biểu mẫu đang hoạt động).
  - `searchCode`: (tùy chọn) tìm theo mã code.
- **Response Thành công (200 OK)**:
```json
{
  "items": [
    {
      "id": "31eacf66-b1d6-4d67-8d5e-af823e6e967f",
      "code": "DON-XAC-NHAN-CU-TRU-2026",
      "title": "Đơn xác nhận thông tin cư trú (Bản chuẩn)",
      "fileDocxUrl": "https://...",
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

---

### 1.4 `GET /api/v1/form-templates/{templateId}` — Xem chi tiết 1 phôi mẫu
- **Actor**: Mọi người dùng / Frontend.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/form-templates/{templateId}`
- **Path Param**: `templateId` = GUID biểu mẫu.
- **Response Thành công (200 OK)**: Trả về chi tiết phôi mẫu và đường dẫn `fileDocxUrl`.

---

### 1.5 `GET /api/v1/form-templates/{templateId}/docx-url` — Lấy link SAS tải DOCX có thời hạn
- **Actor**: Người dân / Frontend.
- **Mục đích**: Lấy link tải bảo mật có chữ ký SAS (Shared Access Signature) để Frontend render hoặc nhúng vào trình xem.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/form-templates/{templateId}/docx-url?validForMinutes=60`
- **Query Params**: `validForMinutes` (tùy chọn, mặc định 60 phút).
- **Response Thành công (200 OK)**:
```json
{
  "url": "https://...blob.core.windows.net/form-templates/...?sv=2021-08-06&se=...&sp=r"
}
```

---

### 1.6 `GET /api/v1/form-templates/{templateId}/download-docx` — Tải trực tiếp file phôi DOCX
- **Actor**: Người dân / Frontend.
- **Mục đích**: Người dân bấm tải file mẫu `.docx` gốc về máy.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/form-templates/{templateId}/download-docx`
- **Response Thành công (200 OK)**: Trả về trực tiếp Binary Stream của file `.docx` với Header `Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document`.

---

# CỤM 2: 👤 Citizen — Hồ sơ người dân (Luồng Người dân)

> **Mục đích**: Người dân tạo hồ sơ từ phôi mẫu, lưu nháp nhiều lần, tải file về xem lại, điền tiếp và nộp hồ sơ chính thức.

```
[Citizen] POST /citizen/submissions/draft (Lưu nháp lần 1 -> Trạng thái Draft)
   │
   ├─► GET /citizen/submissions/{id}/download-docx (Tải về điền thêm)
   │
   ▼
[Citizen] PUT /citizen/submissions/{id}/draft (Lưu nháp lần 2, 3... -> Trạng thái Draft)
   │
   ├─► GET /citizen/submissions (Xem danh sách hồ sơ của mình)
   ├─► GET /citizen/submissions/{id} (Xem chi tiết hồ sơ)
   │
   ▼
[Citizen] POST /citizen/submissions/{id}/submit (NỘP CHÍNH THỨC -> Chuyển sang Submitted)
```

---

### 2.1 `POST /api/v1/citizen/submissions/draft` — Tạo bản nháp hồ sơ đầu tiên
- **Actor**: Người dân (Citizen).
- **Mục đích**: Người dân tải phôi mẫu về, điền một phần thông tin vào file `.docx` và lưu nháp để hôm sau làm tiếp.
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/draft`
- **Headers**: `Content-Type: multipart/form-data`
- **Form Fields**:
  - `templateId`: `<GUID_CỦA_PHÔI_MẪU_Ở_CỤM_1>`
  - `applicantId`: `11111111-1111-1111-1111-111111111111` (ID tài khoản người dân)
  - `file`: Chọn file `.docx` đã điền một phần từ máy của bạn.
- **Response Thành công (201 Created)**:
```json
{
  "submissionId": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "status": "Draft",
  "fileName": "don_cu_tru_nhap_v1.docx",
  "fileSizeBytes": 15420,
  "updatedAt": "2026-10-06T15:48:44Z",
  "message": "Draft created successfully."
}
```
👉 **Lưu lại `submissionId` để dùng cho toàn bộ các bước sau.**

---

### 2.2 `GET /api/v1/citizen/submissions` — Lấy danh sách hồ sơ của người dân
- **Actor**: Người dân (Citizen).
- **Mục đích**: Hiển thị bảng "Hồ sơ của tôi" trên giao diện công dân.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/citizen/submissions?applicantId=11111111-1111-1111-1111-111111111111&page=1&pageSize=10`
- **Query Params**:
  - `applicantId`: `11111111-1111-1111-1111-111111111111` (Bắt buộc).
  - `status`: (tùy chọn) `Draft`, `Submitted`, `RevisionRequested`, `Approved`.
- **Response Thành công (200 OK)**: Trả về danh sách hồ sơ của công dân đó.

---

### 2.3 `GET /api/v1/citizen/submissions/{submissionId}` — Xem chi tiết 1 hồ sơ
- **Actor**: Người dân (Citizen).
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/citizen/submissions/{submissionId}`
- **Path Param**: `submissionId` = GUID hồ sơ.
- **Response Thành công (200 OK)**:
```json
{
  "id": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "templateId": "31eacf66-b1d6-4d67-8d5e-af823e6e967f",
  "applicantId": "11111111-1111-1111-1111-111111111111",
  "fileName": "don_cu_tru_nhap_v1.docx",
  "fileSizeBytes": 15420,
  "status": "Draft",
  "officerComment": null,
  "submittedAt": null,
  "reviewedAt": null
}
```

---

### 2.4 `GET /api/v1/citizen/submissions/{submissionId}/download-docx` — Tải file DOCX đang lưu nháp
- **Actor**: Người dân (Citizen).
- **Mục đích**: Người dân tải file nháp mà mình đã lưu hôm qua về máy để mở bằng Word và điền tiếp.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/{submissionId}/download-docx`
- **Path Param**: `submissionId`
- **Response Thành công (200 OK)**: Tải trực tiếp file `.docx` nháp về máy.

---

### 2.5 `PUT /api/v1/citizen/submissions/{submissionId}/draft` — Cập nhật bản nháp (Điền tiếp)
- **Actor**: Người dân (Citizen).
- **Mục đích**: Sau khi điền thêm thông tin vào file `.docx`, người dân upload file mới lên để đè bản nháp cũ.
- **Method & URL**: `PUT https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/{submissionId}/draft`
- **Path Param**: `submissionId`
- **Headers**: `Content-Type: multipart/form-data`
- **Form Fields**:
  - `applicantId`: `11111111-1111-1111-1111-111111111111`
  - `file`: Chọn file `.docx` phiên bản mới hơn.
- **Response Thành công (200 OK)**:
```json
{
  "submissionId": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "status": "Draft",
  "fileName": "don_cu_tru_nhap_v2.docx",
  "fileSizeBytes": 18200,
  "message": "Draft updated successfully."
}
```

---

### 2.6 `POST /api/v1/citizen/submissions/{submissionId}/submit` — Nộp hồ sơ chính thức
- **Actor**: Người dân (Citizen).
- **Mục đích**: Khi đã điền hoàn tất và kiểm tra đầy đủ, người dân bấm **"Nộp hồ sơ"**. Trạng thái chuyển từ `Draft` ➔ `Submitted`.
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/citizen/submissions/{submissionId}/submit`
- **Path Param**: `submissionId`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
```json
{
  "applicantId": "11111111-1111-1111-1111-111111111111"
}
```
- **Response Thành công (200 OK)**:
```json
{
  "submissionId": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "status": "Submitted",
  "submittedAt": "2026-10-06T15:52:10Z",
  "message": "Application submitted successfully."
}
```

---

# CỤM 3: 🏢 Officer — Xét duyệt hồ sơ (Front-Desk / Cán bộ)

> **Mục đích**: Cán bộ một cửa xem danh sách hồ sơ người dân đã nộp (`Submitted`), mở file DOCX để thẩm định, yêu cầu người dân chỉnh sửa (`RevisionRequested`) hoặc duyệt hồ sơ (`Approved`).

```
[Officer] GET /officer/submissions?status=Submitted (Xem hàng đợi chờ duyệt)
   │
   ├─► GET /officer/submissions/{id} (Xem chi tiết hồ sơ)
   ├─► GET /officer/submissions/{id}/download-docx (Tải file DOCX về đọc)
   │
   ├──────► [Nhánh 1: Yêu cầu sửa] POST /officer/submissions/{id}/request-revision
   │           │
   │           ▼ (Người dân sửa file & nộp lại)
   │        [Citizen] PUT /draft ➔ POST /submit
   │           │
   ▼           ▼
[Officer] POST /officer/submissions/{id}/approve (PHÊ DUYỆT HOÀN TẤT -> Trạng thái Approved)
```

---

### 3.1 `GET /api/v1/officer/submissions` — Cán bộ lấy danh sách hồ sơ cần duyệt
- **Actor**: Cán bộ một cửa (Officer / Front-Desk).
- **Mục đích**: Hiển thị danh sách hồ sơ đang chờ duyệt trong ca làm việc.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/officer/submissions?status=Submitted&page=1&pageSize=10`
- **Query Params**:
  - `status`: `Submitted` (lọc hồ sơ đang chờ duyệt) | `RevisionRequested` (hồ sơ đã trả về) | `Approved` (hồ sơ đã duyệt).
- **Response Thành công (200 OK)**:
```json
{
  "items": [
    {
      "id": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
      "applicantId": "11111111-1111-1111-1111-111111111111",
      "status": "Submitted",
      "submittedAt": "2026-10-06T15:52:10Z"
    }
  ],
  "totalCount": 1
}
```

---

### 3.2 `GET /api/v1/officer/submissions/{submissionId}` — Cán bộ xem chi tiết một hồ sơ
- **Actor**: Cán bộ (Officer).
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/v1/officer/submissions/{submissionId}`
- **Path Param**: `submissionId`
- **Response Thành công (200 OK)**: Thông tin chi tiết người nộp, thời gian nộp, ghi chú cán bộ trước đó (nếu có).

---

### 3.3 `GET /api/v1/officer/submissions/{submissionId}/download-docx` — Cán bộ tải file DOCX về thẩm định
- **Actor**: Cán bộ (Officer).
- **Mục đích**: Cán bộ tải file DOCX người dân đã điền về máy để kiểm tra tính hợp lệ của từng trường thông tin.
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/officer/submissions/{submissionId}/download-docx`
- **Path Param**: `submissionId`
- **Response Thành công (200 OK)**: Tải binary file `.docx`.

---

### 3.4 (Nhánh A) `POST /api/v1/officer/submissions/{submissionId}/request-revision` — Cán bộ trả hồ sơ, yêu cầu sửa
- **Actor**: Cán bộ (Officer).
- **Mục đích**: Khi phát hiện thông tin khai chưa đúng hoặc thiếu giấy tờ, cán bộ gửi trả hồ sơ kèm hướng dẫn cụ thể.
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/officer/submissions/{submissionId}/request-revision`
- **Path Param**: `submissionId`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
```json
{
  "officerId": "22222222-2222-2222-2222-222222222222",
  "comment": "Ảnh chụp CCCD mặt sau bị mờ, vui lòng chụp lại rõ nét hơn rồi nộp lại."
}
```
- **Response Thành công (200 OK)**:
```json
{
  "submissionId": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "status": "RevisionRequested",
  "officerComment": "Ảnh chụp CCCD mặt sau bị mờ, vui lòng chụp lại rõ nét hơn rồi nộp lại.",
  "reviewedAt": "2026-10-06T15:53:00Z",
  "message": "Revision requested."
}
```
*Ghi chú cho người dân sau bước này:* Người dân vào gọi `PUT /api/v1/citizen/submissions/{id}/draft` (upload file đã sửa) ➔ gọi `POST /api/v1/citizen/submissions/{id}/submit` để gửi lại cho cán bộ.

---

### 3.5 (Nhánh B) `POST /api/v1/officer/submissions/{submissionId}/approve` — Cán bộ phê duyệt hồ sơ
- **Actor**: Cán bộ (Officer).
- **Mục đích**: Khi hồ sơ đã đầy đủ và hợp lệ, cán bộ bấm duyệt hoàn tất. Trạng thái chuyển sang `Approved`.
- **Method & URL**: `POST https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/api/v1/officer/submissions/{submissionId}/approve`
- **Path Param**: `submissionId`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
```json
{
  "officerId": "22222222-2222-2222-2222-222222222222"
}
```
- **Response Thành công (200 OK)**:
```json
{
  "submissionId": "a9b7d43f-2043-4ef1-bd83-f6544001e8e6",
  "status": "Approved",
  "reviewedAt": "2026-10-06T15:55:00Z",
  "message": "Submission approved successfully."
}
```

---

# CỤM 4: ⚙️ WardMate.Services.DocumentForm.API (Root & Health)

> **Mục đích**: Kiểm tra tình trạng hoạt động (liveness/readiness) của service DocumentForm.

---

### 4.1 `GET /` — Root Service Info
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/`
- **Response Thành công (200 OK)**:
```json
{
  "service": "WardMate.Services.DocumentForm",
  "version": "1.0.0",
  "status": "Running"
}
```

---

### 4.2 `GET /health` — Health Check
- **Method & URL**: `GET https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/document-form/health`
- **Response Thành công (200 OK)**:
```text
Healthy
```
