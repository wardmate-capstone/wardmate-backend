# Hướng dẫn FE — 18 API mới: Staffing, phân công và tham vấn

Base URL:
```text
https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io
```

Tất cả API cần `Authorization: Bearer <accessToken>` của IAM. POST gửi `Content-Type: application/json`. Các API mới trả **200 khi thành công**. Thời gian gửi ISO8601 có offset (`2026-11-02T08:00:00+07:00`), response lưu thời gian UTC. Thay các chuỗi `<...>` bằng UUID thật; ngày giờ mẫu phải đổi theo thời điểm sử dụng.

### ManagerDispatchController — màn hình điều phối hồ sơ

Manager cần `workflow.dispatch.read`, thao tác ghi cần thêm `workflow.dispatch.write`.

#### 1. Danh sách hồ sơ
`GET /api/v1/manager/applications?page=1&pageSize=20`

Gọi khi mở màn hình, đổi trang hoặc tải lại. PageSize từ 1–100. Response:
```json
{
  "items": [{
    "id": "<applicationId>",
    "applicationCode": "<mã hồ sơ>",
    "procedureId": "<procedureId>",
    "procedureTitle": "<tên thủ tục>",
    "status": "SUBMITTED",
    "assignedOfficerId": null,
    "assignmentVersion": 0,
    "procedureCategoryId": 1
  }],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```
`assignedOfficerId=null` là chưa giao. Giữ `assignmentVersion` để gửi khi phân công. Danh sách gồm hồ sơ trong phường, không chỉ hồ sơ chờ nhận.

#### 2. Cán bộ phù hợp
`GET /api/v1/manager/applications/{applicationId}/assignment-candidates`

Gọi khi mở popup Phân công/Chuyển cán bộ. Response mảng:
```json
[{"officerId":"<officerId>","fullName":"<tên cán bộ>","eligible":true,"activeApplicationCount":3,"blockingReasons":[]}]
```
Cho chọn khi eligible=true; nếu false, hiển thị blockingReasons và vô hiệu hóa lựa chọn. Backend kiểm tra lại khi lưu vì điều kiện có thể thay đổi.

#### 3. Giao hoặc chuyển hồ sơ
`POST /api/v1/manager/applications/{applicationId}/assignments`
```json
{
  "officerId": "<officerId>",
  "expectedAssignmentVersion": 0,
  "reasonCode": "INITIAL_ASSIGNMENT",
  "reason": "Phân công cán bộ phụ trách đúng lĩnh vực"
}
```
Gọi khi Manager xác nhận. Chuyển vì nghỉ có thể dùng `LEAVE_HANDOVER`; reasonCode chỉ là nhãn, không kích hoạt luồng khác. Giới hạn reasonCode 50 ký tự, reason 2000 ký tự.

Response là bản ghi có `id, applicationId, fromOfficerId, toOfficerId, actorId, version, reasonCode, reason, createdAt`. Thành công: đóng popup, tải lại danh sách/lịch sử. Phân công không tự chuyển trạng thái nghiệp vụ.

#### 4. Lịch sử phân công
`GET /api/v1/manager/applications/{applicationId}/assignments`

Response mảng bản ghi như API3, theo version. Hiển thị người giao, người nhận cũ/mới, lý do và thời gian.

### StaffingController — màn hình lịch làm và nghỉ phép

Quyền đọc `workflow.staffing.read`.

#### 5. Xem lịch làm
`GET /api/v1/staffing/shifts?from=<ISO8601>&to=<ISO8601>`

Gọi khi mở/đổi khoảng ngày. Manager thấy lịch phường; cán bộ thấy lịch mình. Khoảng tối đa93 ngày. Response mảng với `id, officerId, wardCode, startsAtUtc, endsAtUtc, createdBy, createdAtUtc, cancelledBy, cancelledAtUtc, cancellationReason`. Nếu cancelledAtUtc khác null, hiển thị ca đã hủy.

#### 6. Tạo ca làm
`POST /api/v1/staffing/shifts` — Manager có `workflow.staffing.manage`.
```json
{"officerId":"<officerId>","startsAt":"2026-11-02T08:00:00+07:00","endsAt":"2026-11-02T17:00:00+07:00"}
```
Nút Thêm ca làm. Response đối tượng ca như API5. Bắt đầu phải ở tương lai, thời lượng dương và tối đa24 giờ; không trùng ca hoặc nghỉ đã duyệt của cùng cán bộ. Hai cán bộ khác nhau được làm cùng giờ.

#### 7. Hủy ca
`POST /api/v1/staffing/shifts/{shiftId}/cancel` — Manager có `workflow.staffing.manage`.
```json
{"reason":"Điều chỉnh lịch làm"}
```
Response ca đã cập nhật. Không xóa bản ghi hoặc tự chuyển hồ sơ. Ca đã hủy/đã kết thúc bị từ chối.

#### 8. Xem yêu cầu nghỉ
`GET /api/v1/staffing/leaves?from=<ISO8601>&to=<ISO8601>`

Manager xem yêu cầu trong phường, cán bộ xem của mình; khoảng tối đa93 ngày. Response mảng có `id, officerId, wardCode, startsAtUtc, endsAtUtc, reason, status, version, createdAtUtc, decidedBy, decidedAtUtc, decisionReason`.

| Status | Nhãn FE |
|---|---|
| PENDING | Chờ duyệt |
| APPROVED | Đã duyệt |
| REJECTED | Đã từ chối |
| CANCELLED | Đã hủy |

#### 9. Xin nghỉ
`POST /api/v1/staffing/leaves` — cán bộ có `workflow.leave.request`.
```json
{"startsAt":"2026-11-02T08:00:00+07:00","endsAt":"2026-11-02T17:00:00+07:00","reason":"Có việc cá nhân"}
```
Không gửi officerId; lấy người xin từ token. Response yêu cầu PENDING với id/version. Khoảng chưa kết thúc, tối đa93 ngày và không trùng PENDING/APPROVED.

#### 10. Quyết định nghỉ
`POST /api/v1/staffing/leaves/{leaveId}/decision`
```json
{"expectedVersion":0,"decision":"APPROVED","reason":"Đồng ý nghỉ"}
```
Manager duyệt/từ chối yêu cầu chờ hoặc hủy yêu cầu chờ/đã duyệt. Cán bộ chỉ hủy PENDING của mình; không tự duyệt. Response yêu cầu cập nhật với version tăng. Dùng version mới nhất từ response trước; không cố định0. Duyệt nghỉ không tự bàn giao hồ sơ.

#### 11. Hồ sơ cần cân nhắc bàn giao
`GET /api/v1/staffing/leaves/{leaveId}/impact` — Manager có `workflow.staffing.manage`.

Hiển thị mục Hồ sơ đang phụ trách trong chi tiết nghỉ. Response mảng hồ sơ giống items của API1. Đây là công việc hiện tại, không dự báo tương lai. Nút Chuyển người xử lý gọi API2 rồi API3.

#### 12. Lịch sử quyết định nghỉ
`GET /api/v1/staffing/leaves/{leaveId}/history`

Manager cùng phường hoặc chủ yêu cầu được xem. Response mảng: `id, leaveId, actorId, version, fromStatus, toStatus, reason, createdAtUtc`. Hiển thị lịch sử người quyết định, trạng thái trước/sau, lý do và thời gian.

### ConsultationsController — màn hình hỗ trợ chuyên môn

Quyền đọc `workflow.consultation.read`.

#### 13. Danh sách tham vấn
`GET /api/v1/staffing/consultations?page=1&pageSize=20`

Đường dẫn staffing nhưng thuộc ConsultationsController. Manager thấy yêu cầu trong phường; chuyên viên thấy yêu cầu của mình, gồm cả yêu cầu đã đóng/cũ. PageSize1–100.
```json
{"items":[],"page":1,"pageSize":20,"total":0}
```
Mỗi item có `id, applicationId, specialistId, requestedBy, applicationVersion, assignmentVersion, question, answer, status, version, createdAtUtc, answeredAtUtc, closedAtUtc, closedBy, closeReason`.

#### 14. Tham vấn của hồ sơ
`GET /api/v1/applications/{applicationId}/consultations`

Gọi khi mở tab Tham vấn. Response mảng đối tượng như API13. Manager/cán bộ chính xem các tham vấn; chuyên viên chỉ xem tham vấn gửi cho mình.

#### 15. Tạo tham vấn
`POST /api/v1/applications/{applicationId}/consultations`

Manager cần quyền đọc tham vấn và workflow.dispatch.write.
```json
{"specialistId":"<officerId chuyên viên>","expectedAssignmentVersion":1,"question":"Nhờ kiểm tra tính phù hợp của giấy tờ bổ sung"}
```
Hồ sơ phải UNDER_REVIEW, có cán bộ chính. Chuyên viên khác người chính, cùng phường/lĩnh vực, có quyền trả lời, đang có ca và không nghỉ đã duyệt. Không có tham vấn chưa đóng cho cùng app/specialist. Response đối tượng OPEN.

API2 chỉ tham khảo khi chọn chuyên viên: quyền nhận hồ sơ khác quyền consultation.respond; backend kiểm tra riêng lúc tạo tham vấn.

#### 16. Trả lời tham vấn
`POST /api/v1/applications/{applicationId}/consultations/{consultationId}/answer`

Chuyên viên được chỉ định có workflow.consultation.respond.
```json
{"expectedVersion":0,"text":"Đề nghị đối chiếu thông tin với giấy tờ gốc"}
```
Response chuyển ANSWERED, version tăng. Chỉ trả lời OPEN, application/assignment version phải còn khớp lúc tạo. Text tối đa4000 ký tự.

#### 17. Đóng tham vấn
`POST /api/v1/applications/{applicationId}/consultations/{consultationId}/close`

Manager có workflow.dispatch.write.
```json
{"expectedVersion":1,"text":"Đã tiếp nhận ý kiến và kết thúc tham vấn"}
```
Response CLOSED, lưu người/lý do đóng. FE tải lại, vô hiệu hóa trả lời. Quyền đọc theo tham vấn hết hiệu lực.

#### 18. Quyền đọc tài liệu tham vấn
`GET /api/v1/applications/{applicationId}/consultations/document-access`

Dùng token chuyên viên có workflow.consultation.respond.
```json
{"allowed":true,"applicationId":"<applicationId>","version":1}
```
Chủ yếu phục vụ DocumentForm xác minh với Workflow. FE gọi API đọc/tải DocumentForm bằng token hiện tại; allowed=true không thay kiểm tra backend. Không cấp quyền sửa DOCX/quyết định hồ sơ. Bị từ chối khi yêu cầu đã đóng hoặc hồ sơ/phân công thay đổi.

### Quy ước chung cho FE

| Trường hợp | Xử lý |
|---|---|
| Phân công | Lấy assignmentVersion của hồ sơ, gửi expectedAssignmentVersion |
| Nghỉ/tham vấn | Lấy version của đúng đối tượng, gửi expectedVersion; không dùng lẫn với version phân công |
| 400 | Hiển thị lỗi nhập liệu |
| 401 | Xử lý phiên đăng nhập thiếu/hết hạn |
| 403 | Thông báo không đủ quyền |
| 404 | Không tìm thấy hoặc ngoài phạm vi |
| 409 | Tải lại dữ liệu và yêu cầu người dùng xem lại; không tự lặp ghi |
| 422 | Hiển thị điều kiện người nhận/hồ sơ chưa phù hợp |
| 503 | Chưa xác minh được service liên quan; cho thử lại |

Lỗi nghiệp vụ thường là ProblemDetails:
```json
{"status":409,"title":"Phân công đã thay đổi. Tải lại hồ sơ.","instance":"/api/v1/manager/applications/...","code":"dispatch.version_conflict","traceId":"..."}
```
Hiển thị title; dùng code để phân nhánh; lưu traceId khi báo lỗi. Sau khi ghi thành công tải lại từ backend. Chưa có realtime cho luồng này. Lịch sử thường trả ID, không đầy đủ tên; FE đối chiếu directory được phép truy cập. Chuyên viên tham vấn không trở thành cán bộ chính và không được tự sửa/annotate tài liệu.


