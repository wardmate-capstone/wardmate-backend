# Bàn giao FE WardMate · Cán bộ yêu cầu tham vấn chuyên môn

**Phiên bản backend local 12/11/2026.** Controller: `ConsultationsController` thuộc ApplicationWorkflow. Khi tích hợp một môi trường, kiểm tra Swagger Workflow đã có ba thao tác mới `requests`, `specialist-candidates`, `dispatch` và IAM đã cấp quyền mới cho Front Desk.

## 1. Luồng trên giao diện

Cán bộ A đang xử lý một hồ sơ và cần ý kiến chuyên môn. A nhập câu hỏi. Manager cùng phường chọn cán bộ B. B đọc hồ sơ, trả lời và có thể đề nghị tiếp nhận. Trong cả ba bước, A vẫn phụ trách hồ sơ. Chỉ khi Manager chọn **Chuyển hồ sơ cho B** thành công thì người phụ trách mới đổi.

| Trạng thái tham vấn | A thấy | Manager thấy | B thấy |
|---|---|---|---|
| PENDING_DISPATCH | Đã gửi, chờ điều phối | Chọn người tư vấn hoặc từ chối kèm lý do | Chưa có việc được giao |
| OPEN | Đang chờ ý kiến | Người được chọn, câu hỏi | Mở hồ sơ, trả lời, chọn có sẵn sàng nhận hồ sơ hay không |
| ANSWERED | Nội dung góp ý của B | Giữ A hoặc chuyển cho B nếu B đồng ý và đủ điều kiện | Đã trả lời |
| CLOSED | Kết quả quyết định, lý do | Lịch sử tham vấn | Kết quả trong mục tham vấn được giao |

## 2. Địa chỉ, tài khoản và phiên bản

Các ví dụ dùng `C = /api/v1/applications/{applicationId}/consultations` để rút gọn. FE thay C bằng cả đường dẫn khi gọi. Dùng applicationId của **hồ sơ Workflow**. Trong C/{id}, id là **ID yêu cầu tham vấn** trả về khi tạo. Mọi request dùng Bearer JWT của người đang thao tác. Các POST gửi application/json.

- Cả controller yêu cầu `workflow.consultation.read`. A cần thêm `workflow.consultation.request`, đang được phân công hồ sơ UNDER_REVIEW và hồ sơ đã có phiên bản nộp. Manager điều phối bằng `workflow.dispatch.write`. B trả lời và kiểm tra quyền xem tài liệu bằng `workflow.consultation.respond`.
- **expectedVersion** lấy từ `version` mới nhất của yêu cầu tham vấn. **expectedAssignmentVersion** lấy từ `assignmentVersion` hiện tại của hồ sơ. `applicationVersion` là phiên bản hồ sơ đã nộp. FE lưu riêng ba giá trị này.
- Khi tạo yêu cầu, lấy ID người gửi từ phiên đăng nhập phía backend. Khi chọn B, gửi `officerId` từ danh sách ứng viên vào trường `specialistId`. Các GUID và số phiên bản bên dưới chỉ minh họa.

## 3. Các API theo thứ tự thao tác

| Màn hình / nút | Method và route | Gửi gì | Thành công (HTTP 200) |
|---|---|---|---|
| A · Yêu cầu tham vấn **(mới)** | POST C/requests | expectedAssignmentVersion, question | Một tham vấn có id, status=PENDING_DISPATCH, specialistId=null |
| Manager · Việc chờ điều phối | GET /api/v1/staffing/consultations?page=1&pageSize=20&status=PENDING_DISPATCH | Query, pageSize tối đa 100. Bỏ status để xem mọi trạng thái | Object gồm items, page, pageSize, total. Cán bộ cũng dùng hộp việc này cho các yêu cầu mình gửi hoặc được giao |
| Manager · Chọn người tư vấn **(mới)** | GET C/specialist-candidates | Không có body | Mảng gồm officerId, fullName, assignedCategories, eligible, blockingReasons |
| Manager · Gửi cho B **(mới)** | POST C/{id}/dispatch | expectedVersion, specialistId | Một tham vấn OPEN có specialistId, dispatchedBy, dispatchedAtUtc và version mới |
| Chi tiết hồ sơ · Lịch sử tham vấn | GET C | Không có body | Mảng tham vấn trong phạm vi của tài khoản |
| B · Mở tài liệu tham vấn | GET C/document-access | Không có body | Object gồm allowed, applicationId, version. Đây là bước kiểm tra quyền đọc. Dùng luồng tải tài liệu đã tích hợp để mở file |
| B · Gửi ý kiến | POST C/{id}/answer | expectedVersion, text, willingToTakeOver | Tham vấn ANSWERED, có answer và willingToTakeOver. Mặc định false |
| Manager · Quyết định | POST C/{id}/close | expectedVersion, text, decision. Thêm expectedAssignmentVersion khi chuyển hồ sơ | Tham vấn CLOSED, có decision, closeReason, closedBy. Tải lại hồ sơ để hiển thị người phụ trách hiện tại |
| Manager · Chủ động hỏi cán bộ (luồng đã có) | POST C | specialistId, expectedAssignmentVersion, question | Tạo tham vấn trực tiếp ở OPEN |

### Body để tích hợp hoặc thử trên Swagger

**A gửi yêu cầu** bằng POST C/requests.

```json
{"expectedAssignmentVersion":1,"question":"Nhờ đối chiếu thông tin hộ tịch trong giấy tờ đính kèm."}
```

**Manager chọn B** bằng POST C/{id}/dispatch. Trong dropdown, hiển thị fullName và dùng officerId làm giá trị. Chỉ cho chọn khi eligible=true. Với eligible=false, hiển thị lý do tương ứng, ví dụ CATEGORY_MISMATCH (khác lĩnh vực), NO_ACTIVE_SHIFT (không có ca đang làm), APPROVED_LEAVE (đã duyệt nghỉ), RESPOND_PERMISSION_MISSING (thiếu quyền phản hồi), CURRENT_ASSIGNEE (đang phụ trách hồ sơ).

```json
{"expectedVersion":0,"specialistId":"11111111-2222-4333-8444-555555555555"}
```

**B gửi ý kiến** bằng POST C/{id}/answer. Checkbox “Tôi sẵn sàng tiếp nhận hồ sơ” mặc định bỏ chọn.

```json
{"expectedVersion":1,"text":"Đã đối chiếu giấy tờ. Tôi có thể tiếp tục xử lý hồ sơ này.","willingToTakeOver":true}
```

**Manager chốt** bằng POST C/{id}/close. Mỗi lần chỉ gửi một quyết định, theo lựa chọn trên giao diện.

| Nút | Body JSON minh họa |
|---|---|
| Từ chối yêu cầu đang chờ điều phối | `{"expectedVersion":0,"decision":"DECLINE_REQUEST","text":"Hướng dẫn hiện tại đã đủ để xử lý."}` |
| Giữ A sau khi B trả lời | `{"expectedVersion":2,"decision":"KEEP_ASSIGNEE","text":"A tiếp tục xử lý theo góp ý của B."}` |
| Chuyển cho B sau khi B đồng ý | `{"expectedVersion":2,"expectedAssignmentVersion":1,"decision":"TRANSFER_TO_SPECIALIST","text":"B tiếp nhận để đối chiếu chuyên sâu."}` |

FE gửi decision rõ ràng cho luồng mới. Với dữ liệu cũ, specialistName, requestedByName hoặc decision có thể null. Dùng nhãn “Chờ chọn cán bộ” khi specialistId còn null. Nếu chỉ thiếu tên, dùng mã cán bộ để phân biệt người trùng tên.

## 4. Xử lý lỗi và nghiệm thu FE

- **400** — kiểm tra nội dung câu hỏi, ý kiến, lý do (1–4000 ký tự, không để toàn khoảng trắng) và ID đã chọn.
- **401/403** — kiểm tra phiên và quyền của người thao tác. Nếu B mất quyền đọc tài liệu sau khi đóng tham vấn, tải lại màn hình hồ sơ.
- **404** — kiểm tra applicationId, id tham vấn và phạm vi phường. **409** — giữ nội dung đang nhập, tải lại hồ sơ và yêu cầu. Nếu đã có yêu cầu đang chờ, mở yêu cầu đó. Cho người dùng xác nhận lại trước khi gửi quyết định.
- **422** — đọc ProblemDetails.code. Với consultation.takeover_not_accepted, giữ A hoặc đóng với KEEP_ASSIGNEE. Với consultation.specialist_ineligible hoặc consultation.officer_ineligible, tải lại điều kiện của cán bộ. Backend kiểm tra lại ca, quyền và lĩnh vực tại thời điểm thao tác.
- **503** — thông báo tạm thời chưa xác minh được dữ liệu từ dịch vụ phụ thuộc. Nếu không rõ lần gửi trước đã thành công hay chưa, đọc lại tham vấn trước khi thử lại. Khi báo lỗi gửi method, URL, HTTP status, code và traceId.

**Kiểm thử chấp nhận:** dùng Manager và hai tài khoản Front Desk cùng phường, B có lĩnh vực phù hợp, đúng quyền và đang trong ca. Dùng hai hồ sơ UNDER_REVIEW do A phụ trách. Với hồ sơ thứ nhất, B chỉ góp ý, Manager giữ A, mở lại hồ sơ vẫn thấy A. Với hồ sơ thứ hai, B chọn sẵn sàng nhận, Manager chuyển giao, mở lại thấy B và có lịch sử chuyển giao. Thử thêm việc Manager từ chối trước khi chọn B và hai tab cùng gửi quyết định với một version cũ.

Nguồn đối chiếu trong backend: ConsultationsController.cs, ConsultationContracts.cs, ConsultationService.cs và ApplicationConsultation.cs. Hợp đồng tổng của dự án nằm tại [api-guide.md](api-guide.md).
