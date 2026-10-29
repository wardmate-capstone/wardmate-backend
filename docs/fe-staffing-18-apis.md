# Tài liệu Bàn giao 18 API Mới: Điều phối Hồ sơ, Ca làm, Nghỉ phép & Tham vấn Chuyên môn (Dành cho Frontend)

Tài liệu này tổng hợp chi tiết kỹ thuật bàn giao 18 API mới thuộc hệ thống **Staffing & Workflow** đã hoàn tất kiểm thử và triển khai live trên môi trường Azure. Frontend (FE) sử dụng trực tiếp tài liệu này để tích hợp màn hình Quản lý điều phối, Quản lý lịch ca làm, Xin/Duyệt nghỉ phép và Tham vấn chuyên môn.

---

## 1. Thông tin Kết nối & Xác thực Chung

- **Base URL (Gateway):**
  ```text
  https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io
  ```
- **Xác thực (Authentication):**
  - Mọi request gửi lên bắt buộc kèm Header:
    ```http
    Authorization: Bearer <accessToken>
    ```
  - Access Token do IAM cấp (chứa `sub`, `ward_code`, `roles`, `permissions`, `assigned_categories`).
- **Định dạng dữ liệu:**
  - `Content-Type: application/json` cho các request POST/PUT.
  - Các API POST/GET trả về **HTTP 200 OK** khi thành công (trả về DTO JSON trực tiếp).
  - Định dạng thời gian gửi lên: chuẩn **ISO 8601 kèm timezone offset** (ví dụ: `2026-11-02T08:00:00+07:00`).
  - Định dạng thời gian trả về: chuẩn **UTC** có đuôi `Z` (ví dụ: `2026-11-02T01:00:00Z`).
- **Cơ chế kiểm soát phiên bản (Optimistic Concurrency Control):**
  - **Phân công hồ sơ:** FE gửi `expectedAssignmentVersion` (kiểu số nguyên `long`), lấy từ trường `assignmentVersion` của hồ sơ.
  - **Duyệt nghỉ phép:** FE gửi `expectedVersion` (kiểu số nguyên `int`), lấy từ trường `version` của đơn xin nghỉ `OfficerLeave`.
  - **Tham vấn chuyên môn:** FE gửi `expectedVersion` (kiểu số nguyên `int`), lấy từ trường `version` của câu tham vấn `ApplicationConsultation`.
  - *Lưu ý sống còn:* Khi gặp lỗi **HTTP 409 Conflict**, FE phải tự động refresh tải lại dữ liệu mới nhất, hiển thị thông báo để người dùng xác nhận lại; tuyệt đối không tự ý retry ghi đè.

---

## 2. Ma trận Phân quyền & Vai trò (IAM Permissions Matrix)

| Chức năng | Endpoint Route | Quyền IAM yêu cầu | Vai trò cho phép |
|---|---|---|---|
| **Xem DS hồ sơ điều phối** | `GET /api/v1/manager/applications` | `workflow.dispatch.read` | Manager |
| **Xem cán bộ phù hợp** | `GET /api/v1/manager/applications/{id}/assignment-candidates` | `workflow.dispatch.read` | Manager |
| **Giao/Chuyển hồ sơ** | `POST /api/v1/manager/applications/{id}/assignments` | `workflow.dispatch.write` | Manager |
| **Lịch sử phân công** | `GET /api/v1/manager/applications/{id}/assignments` | `workflow.dispatch.read` | Manager |
| **Xem lịch làm việc** | `GET /api/v1/staffing/shifts` | `workflow.staffing.read` | Manager (xem cả phường), Officer (xem của mình) |
| **Tạo ca làm việc** | `POST /api/v1/staffing/shifts` | `workflow.staffing.manage` | Manager |
| **Hủy ca làm việc** | `POST /api/v1/staffing/shifts/{id}/cancel` | `workflow.staffing.manage` | Manager |
| **Xem DS nghỉ phép** | `GET /api/v1/staffing/leaves` | `workflow.staffing.read` | Manager (xem cả phường), Officer (xem của mình) |
| **Gửi yêu cầu xin nghỉ** | `POST /api/v1/staffing/leaves` | `workflow.leave.request` | Officer (Cán bộ) |
| **Quyết định duyệt/hủy nghỉ**| `POST /api/v1/staffing/leaves/{id}/decision` | `workflow.staffing.decide` | Manager (duyệt/từ chối/hủy), Officer (chỉ hủy đơn PENDING của mình) |
| **Xem hồ sơ bị ảnh hưởng** | `GET /api/v1/staffing/leaves/{id}/impact` | `workflow.staffing.manage` | Manager |
| **Lịch sử quyết định nghỉ** | `GET /api/v1/staffing/leaves/{id}/history` | `workflow.staffing.read` | Manager, Officer (chủ đơn) |
| **Hộp thư tham vấn** | `GET /api/v1/staffing/consultations` | `workflow.consultation.read` | Manager (cả phường), Chuyên viên (được gửi) |
| **DS tham vấn theo hồ sơ** | `GET /api/v1/applications/{id}/consultations` | `workflow.consultation.read` | Manager, Cán bộ chính của hồ sơ, Chuyên viên |
| **Tạo câu hỏi tham vấn** | `POST /api/v1/applications/{id}/consultations` | `workflow.dispatch.write` | Manager |
| **Trả lời tham vấn** | `POST /api/v1/applications/{id}/consultations/{cId}/answer` | `workflow.consultation.respond` | Chuyên viên được chỉ định |
| **Đóng tham vấn** | `POST /api/v1/applications/{id}/consultations/{cId}/close` | `workflow.dispatch.write` | Manager |
| **Kiểm tra quyền xem hồ sơ**| `GET /api/v1/applications/{id}/consultations/document-access`| `workflow.consultation.respond` | Chuyên viên tham vấn |

---

## 3. Chi tiết 18 API theo 3 Phân hệ Màn hình

### PHÂN HỆ 1: MÀN HÌNH ĐIỀU PHỐI HỒ SƠ (`ManagerDispatchController`)

Dành cho màn hình **Quản lý phân công hồ sơ** của Lãnh đạo / Quản lý phường.

#### 1. Lấy danh sách hồ sơ cần điều phối trong phường
- **Method & Route:** `GET /api/v1/manager/applications?page=1&pageSize=20`
- **Mô tả:** Trả về danh sách hồ sơ trong phạm vi phường của Manager, hỗ trợ phân trang (`page` >= 1, `pageSize` từ 1 đến 100).
- **Response mẫu (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "7b8e1f54-2f22-48a6-8968-380d9ad48419",
        "applicationCode": "HSO-2026-0001",
        "procedureId": "a1c5f3e9-7988-4c3d-b4b1-cf5e1e1ef8ae",
        "procedureTitle": "Đăng ký khai sinh",
        "status": "SUBMITTED",
        "assignedOfficerId": null,
        "assignmentVersion": 0,
        "procedureCategoryId": 1
      }
    ],
    "page": 1,
    "pageSize": 20,
    "total": 1
  }
  ```
- **Lưu ý FE:**
  - Nếu `assignedOfficerId == null`: hồ sơ chưa được phân công (hiển thị tag "Chưa phân công").
  - Lưu lại giá trị `assignmentVersion` để truyền vào `expectedAssignmentVersion` khi thực hiện phân công ở API 3.

#### 2. Lấy danh sách ứng viên cán bộ phù hợp để phân công
- **Method & Route:** `GET /api/v1/manager/applications/{id}/assignment-candidates`
- **Mô tả:** Gọi khi Manager bấm nút "Phân công" hoặc "Chuyển cán bộ" trên một hồ sơ để mở Modal/Dropdown chọn cán bộ.
- **Response mẫu (200 OK):**
  ```json
  [
    {
      "officerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
      "fullName": "Nguyễn Văn Cán Bộ",
      "eligible": true,
      "activeApplicationCount": 3,
      "blockingReasons": []
    },
    {
      "officerId": "44bb2a11-5311-48e2-9b22-11c4e1e2d9cd",
      "fullName": "Trần Thị Cán Bộ Nghỉ",
      "eligible": false,
      "activeApplicationCount": 5,
      "blockingReasons": [
        "NO_ACTIVE_SHIFT",
        "APPROVED_LEAVE"
      ]
    }
  ]
  ```
- **Lưu ý FE:**
  - Danh sách đã được backend sắp xếp: ưu tiên `eligible == true` lên đầu, sau đó theo `activeApplicationCount` tăng dần (ít việc nhất lên trước).
  - Nếu `eligible == false`: Disable lựa chọn cán bộ này và hiển thị tooltip/tag lý do không hợp lệ từ `blockingReasons`.
  - Các mã `blockingReasons` thường gặp:
    - `NO_ACTIVE_SHIFT`: Hiện tại không có ca làm việc hoạt động.
    - `APPROVED_LEAVE`: Đang trong đợt nghỉ phép đã được phê duyệt.
    - `CATEGORY_MISMATCH`: Không phụ trách lĩnh vực thủ tục này.
    - `WARD_MISMATCH`: Khác phường.
    - `ROLE_MISMATCH`: Không có vai trò tiếp nhận hồ sơ.
    - `ACCOUNT_INACTIVE`: Tài khoản bị khóa.

#### 3. Thực hiện Giao hoặc Chuyển hồ sơ cho cán bộ
- **Method & Route:** `POST /api/v1/manager/applications/{id}/assignments`
- **Mô tả:** Giao hồ sơ lần đầu hoặc chuyển hồ sơ sang cán bộ khác.
- **Request Body mẫu:**
  ```json
  {
    "officerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
    "expectedAssignmentVersion": 0,
    "reasonCode": "INITIAL_ASSIGNMENT",
    "reason": "Phân công xử lý hồ sơ khai sinh theo đúng lĩnh vực"
  }
  ```
- **Quy tắc Request Body:**
  - `expectedAssignmentVersion`: Bắt buộc đúng version hiện tại của hồ sơ (ngăn chặn xung đột 2 Manager cùng thao tác đồng thời).
  - `reasonCode`: **BẮT BUỘC** chỉ được chọn 1 trong 4 mã enum sau:
    1. `INITIAL_ASSIGNMENT`: Giao hồ sơ lần đầu.
    2. `OFFICER_LEAVE`: Chuyển hồ sơ do cán bộ phụ trách nghỉ phép.
    3. `WORKLOAD_BALANCE`: Điều phối cân bằng tải công việc.
    4. `SPECIALIST_REQUIRED`: Chuyển do yêu cầu cán bộ chuyên môn đặc thù.
  - `reason`: Chuỗi lý do (bắt buộc, tối đa 2000 ký tự).
- **Response mẫu (200 OK):**
  ```json
  {
    "id": "89ec2f11-9a44-48e2-9b22-11c4e1e2d9cd",
    "applicationId": "7b8e1f54-2f22-48a6-8968-380d9ad48419",
    "fromOfficerId": null,
    "toOfficerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
    "actorId": "11aa22bb-33cc-44dd-55ee-66ff77aa88bb",
    "version": 1,
    "reasonCode": "INITIAL_ASSIGNMENT",
    "reason": "Phân công xử lý hồ sơ khai sinh theo đúng lĩnh vực",
    "createdAt": "2026-10-29T16:50:00Z",
    "action": "ASSIGNED"
  }
  ```

#### 4. Xem lịch sử phân công của hồ sơ
- **Method & Route:** `GET /api/v1/manager/applications/{id}/assignments`
- **Mô tả:** Lấy danh sách toàn bộ các lần giao, chuyển hồ sơ từ trước đến nay theo thứ tự version tăng dần.
- **Response mẫu (200 OK):** Mảng các `AssignmentRecord` (cấu trúc tương tự response của API 3).

---

### PHÂN HỆ 2: MÀN HÌNH CA LÀM & NGHỈ PHÉP (`StaffingController`)

Dành cho màn hình **Lịch trực / ca làm việc** và **Quản lý nghỉ phép**.

#### 5. Xem danh sách ca làm việc (Shifts)
- **Method & Route:** `GET /api/v1/staffing/shifts?from=2026-11-01T00:00:00Z&to=2026-11-30T23:59:59Z`
- **Mô tả:** Lấy danh sách ca làm trong khoảng thời gian.
  - Manager: Xem được ca làm của toàn bộ cán bộ trong phường.
  - Cán bộ (Officer): Chỉ xem ca làm của chính mình.
  - Khoảng thời gian `to - from` không được vượt quá 93 ngày.
- **Response mẫu (200 OK):**
  ```json
  [
    {
      "id": "33cc11aa-22bb-44dd-55ee-66ff77aa88bb",
      "officerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
      "wardCode": "WARD01",
      "startsAtUtc": "2026-11-02T01:00:00Z",
      "endsAtUtc": "2026-11-02T10:00:00Z",
      "createdBy": "11aa22bb-33cc-44dd-55ee-66ff77aa88bb",
      "createdAtUtc": "2026-10-29T10:00:00Z",
      "cancelledBy": null,
      "cancelledAtUtc": null,
      "cancellationReason": null
    }
  ]
  ```
- **Lưu ý FE:** Nếu `cancelledAtUtc != null`, ca làm này đã bị hủy (hiển thị gạch ngang hoặc tag "Đã hủy").

#### 6. Tạo ca làm việc mới (Chỉ Manager)
- **Method & Route:** `POST /api/v1/staffing/shifts`
- **Request Body mẫu:**
  ```json
  {
    "officerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
    "startsAt": "2026-11-02T08:00:00+07:00",
    "endsAt": "2026-11-02T17:00:00+07:00"
  }
  ```
- **Ràng buộc kiểm tra:**
  - `startsAt` phải ở tương lai so với thời điểm gọi.
  - Độ dài ca làm (`endsAt - startsAt`) không được vượt quá 24 giờ.
  - Không được trùng với ca làm đang hoạt động khác hoặc đơn nghỉ phép `APPROVED` của cùng cán bộ đó (nếu trùng trả về `409 Conflict`).
- **Response mẫu (200 OK):** Trả về bản ghi `OfficerShift` vừa tạo.

#### 7. Hủy ca làm việc (Chỉ Manager)
- **Method & Route:** `POST /api/v1/staffing/shifts/{id}/cancel`
- **Request Body mẫu:**
  ```json
  {
    "reason": "Điều chỉnh kế hoạch công tác đột xuất"
  }
  ```
- **Ràng buộc:** Ca đã hủy hoặc ca đã kết thúc trong quá khứ sẽ bị từ chối (`409 Conflict`).
- **Response mẫu (200 OK):** Trả về bản ghi `OfficerShift` với trường `cancelledAtUtc`, `cancelledBy`, `cancellationReason` đã cập nhật.

#### 8. Xem danh sách yêu cầu nghỉ phép (Leaves)
- **Method & Route:** `GET /api/v1/staffing/leaves?from=2026-11-01T00:00:00Z&to=2026-11-30T23:59:59Z`
- **Mô tả:** Khoảng thời gian tối đa 93 ngày. Manager xem cả phường; Cán bộ xem đơn của mình.
- **Response mẫu (200 OK):**
  ```json
  [
    {
      "id": "55dd22bb-33cc-44dd-55ee-66ff77aa88bb",
      "officerId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
      "wardCode": "WARD01",
      "startsAtUtc": "2026-11-05T01:00:00Z",
      "endsAtUtc": "2026-11-06T10:00:00Z",
      "reason": "Giải quyết việc gia đình",
      "status": "PENDING",
      "version": 0,
      "createdAtUtc": "2026-10-29T10:00:00Z",
      "decidedBy": null,
      "decidedAtUtc": null,
      "decisionReason": null
    }
  ]
  ```
- **Bảng trạng thái (`status`):**
  - `PENDING`: Đang chờ duyệt (Màu vàng).
  - `APPROVED`: Đã duyệt (Màu xanh).
  - `REJECTED`: Đã từ chối (Màu đỏ).
  - `CANCELLED`: Đã hủy (Màu xám).

#### 9. Cán bộ gửi đơn xin nghỉ phép
- **Method & Route:** `POST /api/v1/staffing/leaves`
- **Request Body mẫu:**
  ```json
  {
    "startsAt": "2026-11-05T08:00:00+07:00",
    "endsAt": "2026-11-06T17:00:00+07:00",
    "reason": "Giải quyết việc gia đình"
  }
  ```
- **Lưu ý FE:** Không cần gửi `officerId`, backend tự động lấy ID cán bộ từ Access Token. Khoảng thời gian kết thúc không được ở quá khứ, tối đa 93 ngày, lý do tối đa 2000 ký tự. Không được trùng đợt nghỉ `PENDING` hoặc `APPROVED` khác của chính mình.
- **Response mẫu (200 OK):** Bản ghi `OfficerLeave` ở trạng thái `PENDING` với `version: 0`.

#### 10. Quyết định duyệt / từ chối / hủy đơn nghỉ phép
- **Method & Route:** `POST /api/v1/staffing/leaves/{id}/decision`
- **Request Body mẫu:**
  ```json
  {
    "expectedVersion": 0,
    "decision": "APPROVED",
    "reason": "Đồng ý cho cán bộ nghỉ theo quy định"
  }
  ```
- **Quy tắc phân quyền thao tác:**
  - Manager: Được ra quyết định `APPROVED` hoặc `REJECTED` cho đơn `PENDING`; hoặc `CANCELLED` cho đơn `APPROVED` nếu cần điều động lại khẩn cấp.
  - Officer: **Chỉ được** gửi `decision: "CANCELLED"` cho chính đơn `PENDING` của mình (tự hủy đơn trước khi duyệt). Officer không được tự duyệt đơn của mình.
- **Ràng buộc:** `expectedVersion` phải khớp version hiện tại của đơn nghỉ.
- **Response mẫu (200 OK):** Bản ghi `OfficerLeave` với trạng thái mới và `version` tăng thêm 1.

#### 11. Xem danh sách hồ sơ bị ảnh hưởng khi cán bộ nghỉ phép (Impact)
- **Method & Route:** `GET /api/v1/staffing/leaves/{id}/impact`
- **Mô tả:** Dành cho Manager khi xem chi tiết một đơn xin nghỉ phép. Trả về danh sách các hồ sơ đang trong quá trình xử lý (`SUBMITTED`, `UNDER_REVIEW`, `NEED_REVISION`, `DOCUMENTS_READY`) mà cán bộ này đang nắm giữ.
- **Response mẫu (200 OK):** Mảng các `DispatchApplication` (cấu trúc tương tự items của API 1).
- **Gợi ý UI:** Cung cấp nút thao tác nhanh "Bàn giao hồ sơ" ngay cạnh từng hồ sơ trong danh sách này để gọi API 2 (lấy ứng viên) và API 3 (giao hồ sơ với lý do `OFFICER_LEAVE`).

#### 12. Xem lịch sử các quyết định của đơn nghỉ phép
- **Method & Route:** `GET /api/v1/staffing/leaves/{id}/history`
- **Mô tả:** Lấy danh sách các lần thay đổi trạng thái của đơn xin nghỉ (từ PENDING sang APPROVED, CANCELLED,...).
- **Response mẫu (200 OK):**
  ```json
  [
    {
      "id": "77ee33cc-44dd-55ee-66ff-77aa88bb99cc",
      "leaveId": "55dd22bb-33cc-44dd-55ee-66ff77aa88bb",
      "actorId": "11aa22bb-33cc-44dd-55ee-66ff77aa88bb",
      "version": 1,
      "fromStatus": "PENDING",
      "toStatus": "APPROVED",
      "reason": "Đồng ý cho cán bộ nghỉ theo quy định",
      "createdAtUtc": "2026-10-29T11:00:00Z"
    }
  ]
  ```

---

### PHÂN HỆ 3: MÀN HÌNH THAM VẤN CHUYÊN MÔN (`ConsultationsController`)

Dành cho luồng **Cán bộ hoặc Lãnh đạo cần hỏi ý kiến chuyên môn từ một chuyên viên khác** cùng lĩnh vực trong phường đối với hồ sơ phức tạp.

#### 13. Hộp thư danh sách tham vấn chuyên môn
- **Method & Route:** `GET /api/v1/staffing/consultations?page=1&pageSize=20`
- **Mô tả:**
  - Manager: Xem toàn bộ các câu hỏi tham vấn trong phường.
  - Chuyên viên (Specialist): Xem các yêu cầu tham vấn được gửi trực tiếp cho mình (dùng làm trang Inbox "Hồ sơ cần tham vấn").
- **Response mẫu (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "99ff44dd-55ee-66ff-77aa-88bb99cc00dd",
        "applicationId": "7b8e1f54-2f22-48a6-8968-380d9ad48419",
        "specialistId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
        "requestedBy": "11aa22bb-33cc-44dd-55ee-66ff77aa88bb",
        "applicationVersion": 1,
        "assignmentVersion": 1,
        "question": "Nhờ chuyên viên kiểm tra tính pháp lý của giấy chứng nhận kèm theo",
        "answer": null,
        "status": "OPEN",
        "version": 0,
        "createdAtUtc": "2026-10-29T12:00:00Z",
        "answeredAtUtc": null,
        "closedAtUtc": null,
        "closedBy": null,
        "closeReason": null
      }
    ],
    "page": 1,
    "pageSize": 20,
    "total": 1
  }
  ```
- **Bảng trạng thái tham vấn (`status`):**
  - `OPEN`: Đang chờ chuyên viên trả lời.
  - `ANSWERED`: Chuyên viên đã trả lời ý kiến.
  - `CLOSED`: Quản lý đã đóng tham vấn.

#### 14. Lấy danh sách tham vấn của một hồ sơ cụ thể
- **Method & Route:** `GET /api/v1/applications/{applicationId}/consultations`
- **Mô tả:** Gọi khi mở tab "Tham vấn chuyên môn" trong trang chi tiết hồ sơ.
- **Quyền xem:** Manager, Cán bộ chính phụ trách hồ sơ, hoặc Chuyên viên được hỏi.
- **Response mẫu (200 OK):** Mảng các `ApplicationConsultation` (cấu trúc giống items của API 13).

#### 15. Tạo câu hỏi tham vấn chuyên môn (Chỉ Manager)
- **Method & Route:** `POST /api/v1/applications/{applicationId}/consultations`
- **Request Body mẫu:**
  ```json
  {
    "specialistId": "23fa1c94-8422-47d3-9f13-68f4e1e2d8ab",
    "expectedAssignmentVersion": 1,
    "question": "Nhờ chuyên viên kiểm tra tính pháp lý của giấy chứng nhận kèm theo"
  }
  ```
- **Điều kiện ràng buộc nghiêm ngặt:**
  - Hồ sơ phải đang ở trạng thái `UNDER_REVIEW` và đã có cán bộ phụ trách chính.
  - `specialistId` phải là cán bộ khác cán bộ chính, cùng phường, phụ trách cùng lĩnh vực hồ sơ, đang có ca làm việc và không nghỉ phép.
  - `expectedAssignmentVersion` phải khớp phiên bản phân công hiện tại.
  - Không được tạo thêm tham vấn mới nếu cán bộ này đang có một câu tham vấn khác chưa đóng (`OPEN` hoặc `ANSWERED`) cho cùng hồ sơ này.
- **Response mẫu (200 OK):** Trả về bản ghi `ApplicationConsultation` vừa tạo với `status: "OPEN"`.

#### 16. Chuyên viên trả lời câu hỏi tham vấn
- **Method & Route:** `POST /api/v1/applications/{applicationId}/consultations/{id}/answer`
- **Quyền:** Chỉ chuyên viên có ID trùng với `specialistId` trong bản ghi tham vấn và có quyền `workflow.consultation.respond`.
- **Request Body mẫu:**
  ```json
  {
    "expectedVersion": 0,
    "text": "Đã đối chiếu với cơ sở dữ liệu hộ tịch: Giấy tờ hợp lệ, đủ điều kiện phê duyệt."
  }
  ```
- **Ràng buộc:** Tham vấn phải còn `OPEN`. `text` tối đa 4000 ký tự. Nếu hồ sơ đã bị chuyển người khác hoặc phiên bản tờ khai thay đổi, backend sẽ từ chối để đảm bảo tính nhất quán dữ liệu.
- **Response mẫu (200 OK):** Trả về bản ghi với `status: "ANSWERED"`, cập nhật `answer`, `answeredAtUtc` và `version` tăng thêm 1.

#### 17. Quản lý đóng câu hỏi tham vấn
- **Method & Route:** `POST /api/v1/applications/{applicationId}/consultations/{id}/close`
- **Quyền:** Manager có quyền `workflow.dispatch.write`.
- **Request Body mẫu:**
  ```json
  {
    "expectedVersion": 1,
    "text": "Đã tiếp nhận ý kiến tư vấn và chuyển bước xử lý tiếp theo"
  }
  ```
- **Response mẫu (200 OK):** Bản ghi chuyển sang `status: "CLOSED"`, cập nhật `closedBy`, `closedAtUtc`, `closeReason`. Sau khi đóng, chuyên viên sẽ không còn quyền đọc tài liệu hồ sơ qua API 18.

#### 18. Kiểm tra quyền xem tài liệu chỉ đọc của Chuyên viên tham vấn
- **Method & Route:** `GET /api/v1/applications/{applicationId}/consultations/document-access`
- **Mô tả:** API nội bộ / hỗ trợ FE kiểm tra xem chuyên viên hiện tại có còn quyền xem tờ khai và giấy tờ minh chứng của hồ sơ này hay không.
- **Response mẫu (200 OK):**
  ```json
  {
    "allowed": true,
    "applicationId": "7b8e1f54-2f22-48a6-8968-380d9ad48419",
    "version": 1
  }
  ```
- **Lưu ý quyền hạn đặc biệt:**
  - Quyền truy cập này là **chỉ đọc (Read-Only)**.
  - Chuyên viên tham vấn được phép tải/xem tờ khai DOCX/PDF và file đính kèm.
  - Chuyên viên **tuyệt đối không được** sửa form, không được đánh giá readiness (sẵn sàng), không được ký số hay đưa ra quyết định duyệt/từ chối hồ sơ thay cho cán bộ chính.
  - Khi tham vấn bị đóng (`CLOSED`) hoặc hồ sơ bị chuyển giao sang cán bộ khác, quyền truy cập sẽ tự động thu hồi ngay lập tức (`allowed: false` hoặc HTTP 403).

---

## 4. Xử lý Lỗi Chuẩn (ProblemDetails Guidelines for FE)

Mọi phản hồi lỗi từ hệ thống đều tuân thủ RFC 7807 (ProblemDetails):
```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Phân công đã thay đổi bởi người khác. Vui lòng tải lại trang.",
  "status": 409,
  "code": "dispatch.version_conflict",
  "instance": "/api/v1/manager/applications/7b8e1f54-2f22-48a6-8968-380d9ad48419/assignments",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
}
```

### Bảng Mã lỗi thường gặp & Hành động của FE

| HTTP Status | Error Code Pattern | Nguyên nhân | Hành động khuyến nghị cho FE |
|---|---|---|---|
| **400 Bad Request** | `*.invalid_input`, `*.invalid_range` | Dữ liệu nhập sai định dạng, ngày bắt đầu lớn hơn kết thúc, khoảng ngày > 93 ngày, vượt quá ký tự cho phép | Hiển thị thông báo đỏ ngay tại trường form nhập liệu tương ứng. |
| **401 Unauthorized** | `auth.unauthorized` | Token thiếu, hết hạn hoặc không đúng định dạng | Redirect người dùng về trang đăng nhập hoặc gọi refresh token. |
| **403 Forbidden** | `*.forbidden` | Không đủ quyền IAM, khác phường quản lý, hoặc cố tình tự duyệt đơn nghỉ của chính mình | Hiển thị thông báo Toast cảnh báo "Bạn không có quyền thực hiện thao tác này". |
| **404 Not Found** | `*.not_found` | Không tìm thấy hồ sơ, ca làm, hoặc đơn nghỉ phép trong phường | Hiển thị thông báo "Dữ liệu không tồn tại hoặc đã bị xóa". Quay về trang danh sách. |
| **409 Conflict** | `*.version_conflict`, `*.application_changed`, `*.overlap` | Xung đột phiên bản (có người khác vừa thao tác xong), hoặc ca làm bị trùng lịch / trùng ngày nghỉ phép | **Tự động gọi lại API GET** để refresh dữ liệu mới nhất lên màn hình; thông báo người dùng thao tác lại trên dữ liệu mới. |
| **422 Unprocessable**| `*.officer_ineligible`, `*.specialist_ineligible` | Cán bộ được chọn không đủ điều kiện (đang nghỉ phép, không có ca làm việc, không cùng lĩnh vực) | Disable nút submit, highlight cán bộ kèm lý do từ `blockingReasons`. |
| **503 Service Unavail**| `*.dependency_unavailable` | Tạm thời chưa kết nối được sang Catalog hoặc IAM service | Hiển thị nút "Thử lại" (Retry) cho người dùng. |

---

## 5. Hướng dẫn Kịch bản Tích hợp Luồng Màn hình (Frontend User Journeys)

### Kịch bản 1: Luồng Điều phối Hồ sơ (Manager Dispatch Flow)
1. Manager vào màn hình **Điều phối hồ sơ**: FE gọi API 1 (`GET /api/v1/manager/applications`).
2. Với hồ sơ chưa phân công (`assignedOfficerId == null`), Manager bấm **"Phân công cán bộ"**:
   - FE mở Modal, đồng thời gọi API 2 (`GET .../assignment-candidates`).
   - Hiển thị danh sách cán bộ: cán bộ `eligible: true` hiển thị sáng, cán bộ `eligible: false` hiển thị mờ kèm nhãn lý do (ví dụ: "Đang nghỉ phép").
3. Manager chọn cán bộ và chọn lý do (Dropdown 4 mã `INITIAL_ASSIGNMENT`, `WORKLOAD_BALANCE`,...):
   - FE bấm "Xác nhận", gọi API 3 (`POST .../assignments`) kèm `expectedAssignmentVersion`.
   - Nhận 200 OK: Đóng Modal, hiển thị Toast xanh thông báo thành công, reload lại danh sách API 1.

### Kịch bản 2: Luồng Xin & Duyệt Nghỉ Phép (Leave Management Flow)
1. Cán bộ vào tab **Nghỉ phép cá nhân**:
   - Bấm "Tạo đơn xin nghỉ", chọn ngày giờ và nhập lý do -> Gọi API 9 (`POST /api/v1/staffing/leaves`).
   - Nếu muốn hủy đơn đang chờ -> Gọi API 10 (`POST .../decision`) với `decision: "CANCELLED"`.
2. Manager vào tab **Duyệt nghỉ phép của phường**:
   - Gọi API 8 (`GET /api/v1/staffing/leaves`) để xem các đơn `PENDING`.
   - Bấm vào một đơn xin nghỉ: Gọi API 11 (`GET .../impact`) để kiểm tra xem cán bộ này có đang giữ hồ sơ nào cần bàn giao không.
   - Nếu có hồ sơ: Manager có thể phân công chuyển hồ sơ cho người khác trước.
   - Manager bấm "Duyệt" (`decision: "APPROVED"`) hoặc "Từ chối" (`decision: "REJECTED"`) qua API 10.

### Kịch bản 3: Luồng Tham vấn Chuyên môn (Consultation Flow)
1. Tại màn hình chi tiết hồ sơ phức tạp, Manager bấm **"Gửi yêu cầu tham vấn"**:
   - FE mở Modal, Manager chọn chuyên viên và nhập câu hỏi -> Gọi API 15 (`POST /api/v1/applications/{id}/consultations`).
2. Chuyên viên mở màn hình **Hộp thư tham vấn**:
   - FE gọi API 13 (`GET /api/v1/staffing/consultations`).
   - Chuyên viên bấm vào câu hỏi: FE điều hướng đến chi tiết hồ sơ (chuyên viên có quyền đọc tài liệu theo API 18).
   - Chuyên viên nhập ý kiến phản hồi và bấm "Gửi trả lời" -> Gọi API 16 (`POST .../answer`).
3. Manager xem câu trả lời tại tab Tham vấn của hồ sơ (API 14):
   - Khi đã nắm rõ thông tin, Manager bấm "Đóng tham vấn" -> Gọi API 17 (`POST .../close`).
   - Giao diện chuyển trạng thái sang `CLOSED`, kết thúc phiên tham vấn.
