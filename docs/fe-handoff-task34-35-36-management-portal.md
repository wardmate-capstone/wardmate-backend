# Tài liệu Bàn giao Frontend: Phân hệ Quản trị Hệ thống (TASK-34, TASK-35, TASK-36)
**Dự án:** WardMate — Hệ thống Hỗ trợ Dịch vụ Công Phường Xã  
**Đối tượng bàn giao:** Đội ngũ Phát triển Frontend (Web Admin / Management Portal)  
**Ngày phát hành:** 2026-11-03  
**Phiên bản Backend:** Release 2026-11-03 (HEAD: `1c44510`)  

---

## 📌 1. TỔNG QUAN PHÂN HỆ QUẢN TRỊ (MANAGEMENT PORTAL)

Phân hệ Quản trị Hệ thống phục vụ riêng cho vai trò **Quản trị viên Kỹ thuật (`IT_ADMIN`)**. Phân hệ này tập hợp các chức năng cốt lõi được xây dựng từ Sprint 6:
* **TASK-34:** Quản trị cấu hình động (System Settings), Nhật ký kiểm toán cấu hình (Audit Logs) và Thu thập sự kiện kiểm toán nghiệp vụ tập trung (Business Audit Events).
* **TASK-35:** Giám sát sức khỏe thời gian thực các Microservices (Service Health Checks) và Hệ thống Sao lưu/Xác minh Database tự động (DB Backup & Verification Engine).
* **TASK-36:** Tối ưu hóa chỉ mục DB, Tra cứu Vector HNSW và Bộ sưu tập Postman Collection xuất chuẩn phục vụ test tích hợp.

### 1.1 Thông tin Kết nối & Endpoint
* **API Gateway URL (Khuyến nghị cho FE):**  
  `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`
* **Analytics System URL (Trực tiếp):**  
  `https://wardmate-analytics-system.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`
* **Swagger UI OpenAPI 3.0:**  
  `https://wardmate-analytics-system.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/swagger/index.html`
* **Bộ sưu tập Postman kèm Environment:**  
  Nằm trong thư mục mã nguồn: `docs/postman/wardmate-api-collection.json` và `docs/postman/wardmate-environment.json`.

### 1.2 Yêu cầu Xác thực & Phân quyền (RBAC)
* Mọi request từ FE gửi lên các API quản trị đều **bắt buộc** đính kèm Header:
  ```http
  Authorization: Bearer <ACCESS_TOKEN>
  Content-Type: application/json
  ```
* **Quyền hạn bắt buộc:** Token phải có claim vai trò **`IT_ADMIN`**.
* **Xử lý lỗi phân quyền:**
  * `401 Unauthorized`: Token hết hạn hoặc không có token ➔ Điều hướng về trang Login.
  * `403 Forbidden`: Người dùng đăng nhập thành công nhưng không có vai trò `IT_ADMIN` (ví dụ: `CITIZEN`, `WARD_OFFICER`) ➔ Hiển thị màn hình 403 "Bạn không có quyền truy cập trang quản trị này".

---

## 🖥️ 2. ĐẶC TẢ GIAO DIỆN & CÁC MÀN HÌNH QUẢN TRỊ (FE SCREENS)

Đề xuất FE xây dựng cụm màn hình Quản trị gồm 4 Tab / Menu chính:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        WARDMATE IT ADMIN PORTAL                        │
├───────────────┬─────────────────────────────┬─────────────┬────────────┤
│ 1. Cấu hình & │ 2. Kiểm toán Nghiệp vụ      │ 3. Giám sát │ 4. Sao lưu │
│    Audit Logs │    (Business Audit Events)  │    Sức khỏe │    Database│
└───────────────┴─────────────────────────────┴─────────────┴────────────┘
```

---

### MÀN HÌNH 1: CẤU HÌNH HỆ THỐNG & NHẬT KÝ THAY ĐỔI (TASK-34)

#### Mục đích:
Cho phép IT Admin xem và điều chỉnh các tham số vận hành của hệ thống trực tiếp trên giao diện mà không cần restart server hoặc sửa code, đồng thời theo dõi ai đã chỉnh sửa thông số gì.

#### Giao diện Đề xuất:
1. **Bảng tham số cấu hình (System Settings Table):**
   * Hiển thị danh sách Key-Value (hiện tại key: `analytics.lookbackDays` - số ngày quét dữ liệu phân tích).
   * Cột: Khóa cấu hình (`Key`), Giá trị hiện tại (`Value`), Phiên bản (`Version`), Người cập nhật gần nhất, Thời gian cập nhật.
   * Nút hành động: **[Chỉnh sửa]**.
2. **Modal Chỉnh sửa cấu hình (Edit Setting Dialog):**
   * Form gồm:
     * Giá trị mới (`value`): Input số (ràng buộc từ `1` đến `366`).
     * Lý do thay đổi (`reason`): Textarea bắt buộc (từ `3` đến `500` ký tự).
   * **Cơ chế Khóa Lạc quan (Optimistic Concurrency):** Khi submit, FE gửi kèm `expectedVersion` (lấy từ dữ liệu lúc mở modal).
     * Nếu API trả về `409 Conflict` (mã: `system.setting_conflict`): Thông báo "Cấu hình vừa được người khác cập nhật. Vui lòng tải lại trang và thực hiện lại!".
3. **Bảng Nhật ký Thay đổi Cấu hình (System Audit Trail):**
   * Hiển thị bảng phân trang (`page`, `pageSize`, lọc theo ngày `from`, `to`).
   * Cột: Thời điểm (`occurredAtUtc`), Hành động (`action`), Khóa (`entityKey`), Giá trị cũ ➔ Giá trị mới, Phiên bản, Người thực hiện, IP, Lý do.

#### Danh sách API sử dụng:
* `GET /api/v1/system/settings` — Lấy danh sách cấu hình.
* `GET /api/v1/system/settings/{key}` — Xem chi tiết 1 cấu hình.
* `PUT /api/v1/system/settings/{key}` — Cập nhật cấu hình.
  * Body:
    ```json
    {
      "value": 14,
      "expectedVersion": 0,
      "reason": "Điều chỉnh thời gian phân tích lên 14 ngày phục vụ báo cáo"
    }
    ```
* `GET /api/v1/system/audit-logs?page=1&pageSize=20` — Lấy danh sách nhật ký thay đổi cấu hình.

---

### MÀN HÌNH 2: NHẬT KÝ KIỂM TOÁN NGHIỆP VỤ TẬP TRUNG (TASK-34)

#### Mục đích:
Theo dõi mọi sự kiện thay đổi dữ liệu quan trọng phát sinh từ 6 microservices (Hồ sơ, Danh mục, Biểu mẫu, IAM...) được tự động bắt từ trigger DB và đẩy về trung tâm.

#### Giao diện Đề xuất:
* **Bộ lọc tìm kiếm đa điều kiện:**
  * Nguồn phát sinh (`source`): Dropdown (`iam`, `workflow`, `catalog`, `documentform`, `aiocr`, `analytics`, `notifications`).
  * Thực thể (`entity`): Input text (ví dụ: `applications`, `users`...).
  * Hành động (`action`): Dropdown (`INSERT`, `UPDATE`, `DELETE`).
  * Khoảng thời gian: DateRangePicker (`from`, `to`).
* **Bảng danh sách sự kiện:**
  * Cột: Mã ID, Thời điểm (`occurredAtUtc`), Nguồn (`Source`), Bảng/Thực thể (`Entity`), Hành động (`Action`), Mã băm toàn vẹn (`PayloadHash`).
  * Nút: **[Xem chi tiết JSON Payload]**.
* **Drawer / Modal Chi tiết sự kiện:**
  * Hiển thị JSON Payload gốc (dữ liệu các trường trước/sau khi đổi) dưới dạng code block JSON có nút Copy.

#### Danh sách API sử dụng:
* `GET /api/v1/system/audit-events?page=1&pageSize=20&source=workflow`
* `GET /api/v1/system/audit-events/{id}`

---

### MÀN HÌNH 3: DASHBOARD GIÁM SÁT SỨC KHỎE DỊCH VỤ (TASK-35)

#### Mục đích:
Cung cấp cái nhìn trực quan về trạng thái sống còn của 7 thành phần microservices, đo độ trễ mạng và phân tích SLA gián đoạn.

#### Giao diện Đề xuất:
1. **Thanh công cụ đầu trang:**
   * Hiển thị thời gian quét gần nhất.
   * Nút bấm: **[⚡ Kiểm tra sức khỏe ngay (Check Now)]**:
     * Khi bấm: gọi `POST /api/v1/system/health/check-now`.
     * API trả về `202 Accepted` kèm `requestId`.
     * FE hiển thị toast: *"Yêu cầu kiểm tra sức khỏe đã được gửi vào hàng đợi xử lý..."*.
     * Sau 2-3 giây, tự động gọi lại `GET /api/v1/system/health/services` để làm mới giao diện.
2. **Lưới thẻ trạng thái dịch vụ (Service Health Cards Grid):**
   * Gồm 7 thẻ đại diện cho: `iam`, `procedure-catalog`, `application-workflow`, `document-form`, `ai-ocr`, `notifications`, `analytics`.
   * Mỗi thẻ hiển thị:
     * Badge màu:
       * 🟢 **Xanh lá (Healthy):** Dịch vụ hoạt động tốt.
       * 🟡 **Vàng (Degraded):** Dịch vụ phản hồi chậm hoặc phụ thuộc gặp vấn đề nhẹ.
       * 🔴 **Đỏ (Unhealthy):** Dịch vụ không thể kết nối hoặc phụ thuộc (DB/RabbitMQ) bị ngắt.
     * HTTP Status (200, 503...).
     * Thời gian phản hồi: `responseTimeMs` (ví dụ: `45 ms`).
     * Thời gian kiểm tra: `checkedAtUtc` (format giờ địa phương).
3. **Bảng Lịch sử Sức khỏe (Health Check History):**
   * Lọc theo service và khoảng thời gian.
   * Hiển thị lịch sử downtime, mã lỗi (`errorCode`) nếu có sự cố.

#### Danh sách API sử dụng:
* `GET /api/v1/system/health/services` — Danh sách trạng thái mới nhất của tất cả service.
* `GET /api/v1/system/health/services/{serviceName}` — Chi tiết 1 service.
* `POST /api/v1/system/health/check-now` — Kích hoạt quét tức thì (202 Accepted).
* `GET /api/v1/system/health/history?service=iam&page=1&pageSize=20` — Lịch sử kiểm tra.

---

### MÀN HÌNH 4: QUẢN LÝ SAO LƯU & XÁC MINH DATABASE (TASK-35)

#### Mục đích:
Quản lý các bản sao lưu database độc lập của từng microservice, lưu trữ trên Azure Blob Storage private, và kích hoạt xác minh tính toàn vẹn (file integrity / non-corruption).

#### Giao diện Đề xuất:
1. **Nút bấm [➕ Tạo bản sao lưu mới (Create Backup)]:**
   * Mở Modal tạo yêu cầu sao lưu:
     * Chọn Database dịch vụ (`serviceName`): Dropdown (`iam`, `catalog`, `workflow`, `documentform`, `aiocr`, `notifications`, `analytics`).
     * Lý do sao lưu (`reason`): Input text (bắt buộc từ 3 đến 500 ký tự).
   * Khi submit: Gọi `POST /api/v1/system/backups`.
     * API trả về `202 Accepted` với trạng thái ban đầu `"Queued"`.
     * Xử lý lỗi `409 Conflict`: *"Dịch vụ này hiện đang có một tiến trình backup đang chờ hoặc đang chạy. Vui lòng chờ tiến trình hoàn tất!"*.
2. **Bảng Danh sách Bản sao lưu (Backup Logs Table):**
   * Bộ lọc: Dropdown Dịch vụ, Dropdown Trạng thái (`Queued`, `Running`, `Succeeded`, `Failed`, `Verified`), DateRange.
   * Các cột dữ liệu:
     * Tên dịch vụ (`serviceName`).
     * Trạng thái (`status`):
       * `Queued`: Đang chờ worker xử lý.
       * `Running`: Đang chạy `pg_dump` và upload Blob Storage.
       * `Succeeded`: Đã sao lưu và lưu file thành công.
       * `Verified`: Đã được kiểm tra toàn vẹn file thành công (Checksum & Magic bytes).
       * `Failed`: Thất bại (có tooltip hiển thị `errorCode`).
     * Kích thước file: Format từ `sizeBytes` sang KB / MB (ví dụ: `14.2 MB`).
     * Mã SHA-256: Rút gọn `a1b2...c3d4` kèm nút Copy mã băm.
     * Thời gian tạo (`createdAtUtc`), Thời gian hoàn thành (`completedAtUtc`).
     * **Cột Thao tác (Actions):**
       * Nút **[🔍 Xác minh (Verify)]**: Chỉ hiển thị hoặc kích hoạt khi bản ghi có `status == 'Succeeded'`.
         * Khi click: Gọi `POST /api/v1/system/backups/{id}/verify`.
         * Hiển thị loading spinner trên dòng đó.
         * Khi thành công: Chuyển badge sang `Verified` màu xanh ngọc và cập nhật thời gian xác minh.

#### Danh sách API sử dụng:
* `POST /api/v1/system/backups` — Tạo yêu cầu backup mới.
  * Request Body:
    ```json
    {
      "serviceName": "documentform",
      "reason": "Sao lưu định kỳ kiểm thử"
    }
    ```
* `GET /api/v1/system/backups?page=1&pageSize=20` — Danh sách backup (hỗ trợ query `service`, `status`, `from`, `to`).
* `GET /api/v1/system/backups/{id}` — Chi tiết 1 bản backup.
* `POST /api/v1/system/backups/{id}/verify` — Yêu cầu kiểm tra tính toàn vẹn của file backup.

---

## 📑 3. QUY CHUẨN XỬ LÝ LỖI (PROBLEMDETAILS FORMAT)

Mọi API quản trị trả về lỗi đều tuân thủ chuẩn RFC 7807 (`application/problem+json`). FE nên viết 1 hàm interceptor để bắt `error.response.data`:

```typescript
export interface ProblemDetails {
  status: number;       // HTTP Status code: 400, 401, 403, 404, 409, 503
  code: string;         // Mã định danh lỗi nghiệp vụ
  title: string;        // Thông báo lỗi thân thiện bằng tiếng Việt
  instance?: string;    // Endpoint gây lỗi
  traceId?: string;     // Mã vết để tra cứu log
}
```

### Bảng Mã lỗi Nghiệp vụ thường gặp cần bắt trên FE:

| HTTP Status | Mã lỗi (`code`) | Ý nghĩa & Hành động FE nên xử lý |
| :---: | :--- | :--- |
| **400** | `system.invalid_setting` | Giá trị cấu hình không nằm trong khoảng 1–366 hoặc lý do không đủ độ dài. |
| **400** | `system.invalid_service` | Tên dịch vụ yêu cầu backup không nằm trong danh sách hỗ trợ. |
| **400** | `system.backup_not_ready` | Gọi Verify trên một backup chưa hoàn thành hoặc bị lỗi. |
| **400** | `system.backup_checksum_mismatch` | File backup trên Blob bị sai lệch checksum (phát hiện file hỏng). |
| **400** | `system.backup_invalid_archive` | File backup không đúng định dạng PostgreSQL dump (`PGDMP`). |
| **404** | `system.setting_not_found` | Khóa cấu hình không tồn tại. |
| **404** | `system.service_not_found` | Service yêu cầu xem sức khỏe chưa được cấu hình. |
| **409** | `system.setting_conflict` | Xung đột phiên bản cấu hình (Optimistic Lock) ➔ Yêu cầu user reload dữ liệu. |
| **409** | `system.backup_in_progress` | Dịch vụ này đang có backup chạy ➔ Thông báo user chờ backup hiện tại xong. |
| **503** | `system.administration_disabled` | Phân hệ quản trị chưa được bật cờ trên server. |
| **503** | `system.health_disabled` | Chức năng giám sát sức khỏe chưa bật. |

---

## 🛠️ 4. HƯỚNG DẪN IMPORT & SỬ DỤNG POSTMAN COLLECTION (TASK-36)

Đội FE có thể sử dụng trực tiếp bộ sưu tập Postman đã xuất sẵn trong repository để thử nghiệm các luồng API:
1. File Collection: [`docs/postman/wardmate-api-collection.json`](file:///d:/term%209/wardmate-backend/docs/postman/wardmate-api-collection.json)
2. File Environment: [`docs/postman/wardmate-environment.json`](file:///d:/term%209/wardmate-backend/docs/postman/wardmate-environment.json)

### Các bước thực hiện:
1. Mở Postman ➔ Bấm **Import** ➔ Chọn cả 2 file trên.
2. Chọn Environment: **WardMate Production (Azure ACA)**.
3. Mở mục `01. Authentication & IAM` ➔ Chạy request `Đăng nhập hệ thống (Auth Login)`.
   * *Lưu ý:* Test script trong request này đã được viết sẵn để **tự động trích xuất `accessToken` và lưu vào biến `token` và `it_admin_token`**.
4. Sau khi đăng nhập thành công, FE có thể gọi trực tiếp bất kỳ API nào trong thư mục `06. Analytics & System Administration` mà không cần copy token thủ công.

---
*Mọi thắc mắc trong quá trình tích hợp giao diện, vui lòng đối chiếu chi tiết implementation tại mã nguồn: `src/Services/WardMate.Services.AnalyticsSystem` hoặc liên hệ Backend Team.*
