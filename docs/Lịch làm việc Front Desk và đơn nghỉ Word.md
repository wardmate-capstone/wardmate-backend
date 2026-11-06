# Lịch làm việc Front Desk và đơn nghỉ Word

**Bàn giao FE · ApplicationWorkflow · 05/11/2026.** Phạm vi gồm **47 API: 22 cũ và 25 mới**. “Mới” là thao tác trong source local của đợt WF-WS. Antigravity đang nghiệm thu Azure. Khi tích hợp, lấy base URL của môi trường đang test và kiểm tra Swagger đúng service ApplicationWorkflow.

## Cách đọc và gọi API

- Mỗi hàng là một cặp **method + route**. Ghép **`/api/v1`** trước đường dẫn ở mọi bảng. Ví dụ hàng 01 gọi `GET /api/v1/staffing/capabilities`. Nếu dùng Gateway theo prefix service thì URL là `{gateway}/api/application-workflow/api/v1/staffing/capabilities`. Không ghép prefix hai lần.
- Gửi `Authorization: Bearer <access token IAM>`. Backend xác minh danh tính và phường. Các nút “đơn của tôi” không nhận `officerId` hoặc `wardCode` từ người dùng. Khi Manager xếp ca cho người khác, `officerId` là người được xếp.
- Các thao tác thành công trong bảng trả **HTTP 200**. Body ghi bằng danh sách tên trường là JSON, trừ chỗ ghi rõ query, header, multipart hoặc binary. Khi tải Word, đọc response dưới dạng Blob/ArrayBuffer. Không parse JSON khi tải file thành công.
- `{month}` là **ngày đầu tháng** theo giờ Việt Nam, ví dụ `2026-12-01`. Các giờ ca và giờ nghỉ gửi ISO 8601 kèm offset. Ca giao nhau theo khoảng `[bắt đầu, kết thúc)`.
- **Manager** là `MANAGER`; **cán bộ** là `FRONT_DESK_OFFICER`. Cán bộ chính và chuyên viên được mời là hai trách nhiệm trên một hồ sơ, không phải hai role IAM riêng.

| Màn hình | Ai dùng và quyền cần kiểm tra |
|---|---|
| Lịch, đơn nghỉ | Manager hoặc cán bộ có `workflow.staffing.read`. Ghi lịch cần Manager + `workflow.staffing.manage`. Tạo/nộp nháp, xác nhận lịch và đề nghị đổi ca cần cán bộ + `workflow.leave.request`. |
| Quyết định đơn nghỉ | Manager có quyền quản lý lịch; chủ đơn có quyền xin nghỉ chỉ được hủy đơn đang chờ của mình. `workflow.staffing.decide` là tên policy phía BE. |
| Điều phối hồ sơ | Manager có `workflow.dispatch.read`; giao/chuyển cần thêm `workflow.dispatch.write`. |
| Tham vấn | Cần `workflow.consultation.read` và đúng phạm vi hồ sơ. Mời/đóng cần thêm `workflow.dispatch.write`; trả lời/kiểm tra quyền đọc tài liệu cần `workflow.consultation.respond` và đúng người được mời. |
| Ngày đóng cửa | Manager quản lý phường của mình; IT_ADMIN có `workflow.calendar.manage` quản lý ngày toàn quốc. |

## A. Mở màn hình và thiết lập ngày đóng cửa — 5 API

Workflow dùng **một mẫu Word chung cho mọi phường** khi operator cấu hình một lần `WorkSchedule__SharedLeaveTemplate__TemplateVersionId` và `WorkSchedule__SharedLeaveTemplate__TemplateSha256`. Upload mẫu vào container private `workflow-leave-documents`, tên object `templates/{UUID không gạch ngang}.docx`. Có thể đặt `WorkSchedule__SharedLeaveTemplate__RequireWord=true` để yêu cầu nộp Word cho mọi phường. FE tiếp tục gọi 01, 17 và 19 bằng token hiện tại, không cần gửi mã phường hay URL Blob. Khi thay mẫu, dùng UUID mới; nháp đã tạo có bản sao riêng.

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 01 | Mới | GET `/staffing/capabilities` | Mở màn hình nghỉ → đọc `wordRequired, templateConfigured, maxDocxBytes`. Mẫu có cấu hình vẫn cần tải thử để biết file đọc được. |
| 02 | Cũ | GET `/staffing/closures` | Lọc `from,to` dạng ngày, tối đa 400 ngày kể cả hai đầu → danh sách ngày đóng cửa theo quyền. Kiểm tra trạng thái hủy trước khi hiển thị ngày đang có hiệu lực. |
| 03 | Cũ | POST `/staffing/closures` | Manager khai báo ngày đóng cửa phường: `date,reason` → `closure,rescheduledApplications` (số hồ sơ được tính lại hạn). |
| 04 | Cũ | POST `/staffing/closures/national` | IT_ADMIN khai báo ngày toàn quốc với cùng body → closure có `wardCode=null`. |
| 05 | Cũ | POST `/staffing/closures/{id}/cancel` | Manager/IT_ADMIN hủy theo phạm vi với `reason` → trả thay đổi và số hồ sơ tính lại hạn. Chỉ hủy ngày chưa bắt đầu. |

## B. Manager xếp ca, lập lịch tháng và cán bộ xác nhận — 11 API

**Hai nút có tác dụng khác nhau.** “Thêm ca” tạo ca chính thức ngay. “Lưu nháp tháng” giữ thay đổi cho Manager rà soát; chỉ “Công bố” mới ghi cả đợt thành ca chính thức. Mẫu tuần là quy luật sinh ca cho tháng được chọn.

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 06 | Cũ | GET `/staffing/shifts` | Xem ngày/tuần với query `from,to` kèm offset, tối đa 93 ngày → ca của phường hoặc của chính cán bộ. Chú ý `cancelledAtUtc`. |
| 07 | Cũ | POST `/staffing/shifts` | Manager thêm một ca với `officerId,startsAt,endsAt` → bản ghi ca và ID. Ca tương lai, tối đa 24 giờ. Cùng cán bộ có thể có nhiều ca không trùng trong ngày. |
| 08 | Cũ | POST `/staffing/shifts/{id}/cancel` | Hủy ca với `reason`; dùng shiftId. Tải lại lịch chính thức và nháp tháng sau khi lưu. |
| 09 | Mới | GET `/staffing/schedules/{month}` | Mở lịch tháng → `slots,baseFingerprint,draft,leaves,closures,confirmations`. Cán bộ nhận lịch cá nhân; Manager nhận thêm nháp của phường. |
| 10 | Mới | GET `/staffing/week-pattern` | Manager mở cấu hình mẫu tuần → `version,entries`. Phiên bản 0 nếu chưa có mẫu. |
| 11 | Mới | PUT `/staffing/week-pattern` | Gửi `expectedVersion,entries`; mỗi dòng gồm `day,start,end,officerId`. Day từ 0 (Chủ nhật) đến 6 (thứ Bảy). Giờ dạng `08:00:00`. Tối đa 200 dòng → mẫu và version mới. |
| 12 | Mới | GET `/staffing/schedules/{month}/preview` | Sinh từ mẫu tuần; thêm query `copyFrom=2026-11-01` để sao chép tháng nguồn theo ngày trong tháng. Trả `slots,closures,conflicts,baseFingerprint`. Không ghi ca. |
| 13 | Mới | PUT `/staffing/schedules/{month}/draft` | `expectedVersion,baseFingerprint,slots,reason`. Gửi **toàn bộ** danh sách dự kiến, tối đa 500 ca → revision nháp. Giữ ID/version ca cũ. Ca tương lai bị bỏ khỏi danh sách sẽ bị hủy khi công bố. |
| 14 | Mới | POST `/staffing/schedules/{month}/publish` | `revisionId,expectedVersion,allowClosure,reason` → revision đã công bố. Nếu cho phép ca ngoại lệ ngày đóng cửa, hiển thị xác nhận riêng cho Manager. 409 thì tải lại lịch trước khi công bố. |
| 15 | Mới | GET `/staffing/schedules/{month}/history` | Manager xem các revision `PUBLISHED` và `BASELINE`. BASELINE là ca chuyển từ dữ liệu cũ; actorId toàn 0 không phải một Manager. |
| 16 | Mới | POST `/staffing/schedules/{month}/confirm` | Cán bộ gửi `fingerprint` lấy từ **baseFingerprint lịch cá nhân** ở 09 → biên nhận. Lịch cá nhân đổi thì tải lại rồi xác nhận. |

**Ví dụ thao tác:** An trực ngày 07/12/2026 từ 08:00–11:00 và 13:00–16:00. Để ghi ngay, FE gọi 07 hai lần với cùng officerId. Để lên lịch cả tháng, khai báo hai dòng thứ Hai ở 11 (`day=1`), gọi 12, giữ cả hai ca trong 13 rồi gọi 14. Hai người khác nhau được làm cùng giờ. Hai ca của cùng một người có thể nối nhau tại đúng giờ kết thúc.

## C. Front Desk soạn Word và nộp đơn — 11 API

Luồng editor: **mẫu → nháp riêng → lưu Word → mở lại → nộp**. `draftId` dùng khi soạn; sau khi nộp, chuyển sang `leaveId` cho màn hình duyệt. Thời gian trong metadata là căn cứ tính lịch. Thay ngày/giờ hoặc lý do sau khi soạn thì phải xem lại và lưu revision Word tương ứng.

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 17 | Mới | GET `/staffing/leave-templates/current` | Cán bộ chọn **Xin nghỉ** → lấy `versionId,sha256` của mẫu áp dụng cho phường. |
| 18 | Mới | GET `/staffing/leave-templates/{id}/docx` | Xem trước mẫu gốc bằng versionId của 17 → binary DOCX. Nếu mẫu đã đổi, đọc lại 17. |
| 19 | Mới | POST `/staffing/leave-drafts` | Header `Idempotency-Key` (UUID); JSON `startsAt,endsAt,reason,templateVersionId` → draft có `id,version,metadataVersion,documentRevisionId`. Backend đã tạo bản sao mẫu riêng. |
| 20 | Mới | GET `/staffing/leave-drafts` | Màn **Nháp của tôi**; query `page,pageSize` (mặc định 1/20) → `items,page,pageSize,total`; chỉ nháp cá nhân chưa nộp. |
| 21 | Mới | GET `/staffing/leave-drafts/{id}` | Mở lại nháp → đọc metadata và version mới nhất trước khi sửa. Manager không được đọc nháp người khác. |
| 22 | Mới | GET `/staffing/leave-drafts/{id}/docx` | Mở **bản riêng của nháp** trong editor → binary DOCX. Dùng API này để khôi phục nội dung đã lưu. |
| 23 | Mới | PATCH `/staffing/leave-drafts/{id}` | `startsAt,endsAt,reason,expectedVersion` → version và metadataVersion mới. Sau đó gọi 24 trước khi nộp. |
| 24 | Mới | PUT `/staffing/leave-drafts/{id}/docx` | “Lưu Word”: multipart `file,expectedVersion,metadataVersion` → draft với documentRevisionId và version mới. Dùng FormData để trình duyệt đặt boundary. |
| 25 | Mới | DELETE `/staffing/leave-drafts/{id}` | “Xóa nháp”: query `expectedVersion` → 200. Chỉ xóa được nháp của mình chưa nộp. |
| 26 | Mới | POST `/staffing/leave-drafts/{id}/submit` | Header `Idempotency-Key`; JSON `expectedVersion,documentRevisionId,metadataVersion` lấy từ lần lưu Word → `leaveId,status,version,submittedAtUtc`. Trạng thái ban đầu PENDING. |
| 27 | Cũ | POST `/staffing/leaves` | Luồng gửi trực tiếp: `startsAt,endsAt,reason` → đơn PENDING. Nếu phường bắt buộc Word, BE trả 409 `staffing.word_required`; chuyển sang 17–26. |

**Xử lý mất mạng:** sinh một key cho thao tác tạo nháp, một key riêng cho thao tác nộp; giữ nguyên key **và body** khi gửi lại cùng thao tác. Sau 24, lấy version trong response mới để gọi 26. Chỉ có bản sao mẫu ban đầu thì chưa đủ điều kiện nộp. Nếu lưu lỗi, giữ nội dung đang mở trong editor.

## D. Manager duyệt nghỉ, kiểm tra ca và bàn giao hồ sơ — 11 API

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 28 | Cũ | GET `/staffing/leaves` | `from,to` kèm offset → đơn chính thức trong kỳ. Manager xem phường mình, cán bộ xem đơn mình. |
| 29 | Mới | GET `/staffing/leaves/{id}` | Chủ đơn/Manager cùng phường mở chi tiết bằng leaveId → `leave,hasSubmittedDocx,submittedRevisionId,sha256`. Đơn cũ có thể không có Word. |
| 30 | Mới | GET `/staffing/leaves/{id}/submitted-docx` | Khi `hasSubmittedDocx=true`, mở binary của **bản đã nộp**. FE có thể đối chiếu hash ở 29. |
| 31 | Mới | GET `/staffing/leaves/{id}/shift-impact` | Manager xem các ca chưa hủy giao với kỳ nghỉ → shiftId, giờ ca gốc, giờ bắt đầu/kết thúc phần giao. Nghỉ 10:00–11:00 trong ca 08:00–12:00 chỉ ảnh hưởng một giờ. |
| 32 | Cũ | GET `/staffing/leaves/{id}/impact` | Manager xem **hồ sơ cán bộ đang giữ** để quyết định cần bàn giao hồ sơ nào. Kết quả này không phải danh sách ca. |
| 33 | Cũ | POST `/staffing/leaves/{id}/decision` | `expectedVersion,decision,reason`. Manager chọn APPROVED/REJECTED/CANCELLED theo trạng thái. Cán bộ chỉ hủy đơn PENDING của mình. Lấy version của leave ở 29. |
| 34 | Cũ | GET `/staffing/leaves/{id}/history` | Manager hoặc chủ đơn xem lịch sử chuyển trạng thái đơn nghỉ. |
| 35 | Cũ | GET `/manager/applications` | Manager mở điều phối với `page,pageSize` → danh sách hồ sơ ở phường, người phụ trách và assignmentVersion. |
| 36 | Cũ | GET `/manager/applications/{id}/assignment-candidates` | Dùng applicationId → danh sách ứng viên với `eligible,activeApplicationCount,blockingReasons`. Có ca đang hiệu lực là một trong các điều kiện lựa chọn. |
| 37 | Cũ | POST `/manager/applications/{id}/assignments` | `officerId,expectedAssignmentVersion,reasonCode,reason` → bản ghi phân công. reasonCode: INITIAL_ASSIGNMENT, OFFICER_LEAVE, WORKLOAD_BALANCE hoặc SPECIALIST_REQUIRED. |
| 38 | Cũ | GET `/manager/applications/{id}/assignments` | Manager xem lịch sử giao/chuyển của một hồ sơ. |

Duyệt nghỉ ở 33 chỉ thay đổi trạng thái đơn và khả dụng trong thời gian nghỉ. Muốn chuyển hồ sơ cho Bình, Manager chọn từng applicationId ở 32, kiểm tra ứng viên qua 36 và xác nhận qua 37.

## E. Front Desk đề nghị đổi giờ ca — 3 API

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 39 | Mới | GET `/staffing/shift-change-requests` | `page,pageSize` → Manager xem yêu cầu trong phường; cán bộ xem của mình. |
| 40 | Mới | POST `/staffing/shift-change-requests` | Cán bộ chọn ca tương lai: `shiftId,expectedVersion,startsAt,endsAt,reason` → yêu cầu PENDING. Lấy expectedVersion từ ca gốc (`version` ở 06, `shiftVersion` ở 09). |
| 41 | Mới | POST `/staffing/shift-change-requests/{id}/decision` | Manager gửi `decision,reason` (APPROVED hoặc REJECTED) với **ID yêu cầu đổi ca**. Khi duyệt thành công, tải lại lịch. Nếu ca gốc thay đổi hoặc bị trùng/nghỉ, đọc lại yêu cầu thay vì tự gửi lại. |

Đây là đổi **thời gian ca của một cán bộ**. Trong lúc chờ duyệt, ca gốc vẫn có hiệu lực.

## F. Hồ sơ khó cần tham vấn chuyên môn — 6 API

| # | Loại | Method + route | Khi gọi, dữ liệu gửi → kết quả FE dùng |
|---|---|---|---|
| 42 | Cũ | POST `/applications/{applicationId}/consultations` | Manager mời cán bộ khác: `specialistId,expectedAssignmentVersion,question` → yêu cầu tham vấn. Hồ sơ đang UNDER_REVIEW và đã được phân công. |
| 43 | Cũ | GET `/staffing/consultations` | `page,pageSize` → hộp thư của Manager trong phường hoặc các yêu cầu được gửi cho cán bộ đang đăng nhập. |
| 44 | Cũ | GET `/applications/{applicationId}/consultations` | Mở tab tham vấn của hồ sơ. Manager/cán bộ chính xem trao đổi theo phạm vi; chuyên viên thấy yêu cầu của mình. |
| 45 | Cũ | GET `/applications/{applicationId}/consultations/document-access` | Chuyên viên kiểm tra quyền đọc tài liệu hồ sơ → `allowed,applicationId,version` khi hợp lệ. Không dùng để tải Word đơn nghỉ. |
| 46 | Cũ | POST `/applications/{applicationId}/consultations/{id}/answer` | Người được mời gửi `expectedVersion,text` → tham vấn chuyển sang ANSWERED. |
| 47 | Cũ | POST `/applications/{applicationId}/consultations/{id}/close` | Manager gửi `expectedVersion,text` (lý do đóng) → CLOSED. `{id}` là ID của tham vấn, không phải applicationId. |

## Xử lý lỗi và nghiệm thu FE

| HTTP | FE làm gì |
|---|---|
| 400 | Kiểm tra dữ liệu, khoảng thời gian, phân trang, định dạng tháng, UUID và key bắt buộc. |
| 401 / 403 | 401: làm mới phiên theo luồng IAM. 403: kiểm tra quyền, phường, chủ sở hữu hoặc người được chỉ định. |
| 404 | Kiểm tra loại ID và phạm vi. Không thử ID khác để tìm dữ liệu ngoài quyền. |
| 409 | Đọc `code`: nếu xung đột phiên bản thì tải lại, giữ thay đổi chưa lưu và để người dùng xác nhận; nếu thiếu revision Word thì quay về 24; nếu trùng ca thì chọn giờ khác. |
| 413 / 415 | Giới hạn file 10 MiB. Chỉ nhận DOCX hợp lệ, có kiểm tra cấu trúc và quan hệ ngoài. Giữ nội dung đang soạn khi upload thất bại. |
| 422 / 503 | 422: chọn cán bộ đủ điều kiện. 503: dependency hoặc cấu hình tạm không sẵn sàng; hiển thị message và traceId khi có. Không báo thao tác đã thành công. |

**Ba kịch bản để FE chạy cùng Antigravity:**

1. Tạo hai ca sáng/chiều → soạn lịch tháng → thay đổi ca riêng sau preview → xác nhận publish nháp cũ trả 409 → tải lại, công bố → cán bộ xác nhận đúng lịch mới.
2. Front Desk soạn/lưu Word → đăng nhập lại mở đúng nháp → nộp hai lần cùng key → nhận một leaveId → Manager tải đúng hash → duyệt → xem ca và hồ sơ bị ảnh hưởng. Thử bằng tài khoản khác phường, không được đọc file.
3. Cán bộ đề nghị đổi giờ → kiểm tra ca cũ còn hiệu lực → Manager duyệt → xem giờ mới và thông báo. Khi hồ sơ khó chỉ cần ý kiến, dùng luồng tham vấn; chỉ dùng 37 khi thực sự chuyển người phụ trách.

Thông báo staffing được backend phát qua outbox. FE đọc thông báo qua luồng thông báo hiện có; không gọi thêm một POST gửi thông báo sau khi công bố/duyệt. Đối chiếu chi tiết backend tại [hướng dẫn API](api-guide.md) và [ràng buộc WF-WS](system-constraints.md).
