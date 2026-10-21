# TASK-32 — Đề xuất hợp đồng deadline cho nhóm ApplicationWorkflow

Trạng thái: đề xuất bàn giao 21/10/2026, chưa phải API đã triển khai.

## Phạm vi nghiệp vụ phải giữ

WardMate xác nhận bộ giấy tờ DOCUMENTS_READY, không giải quyết/phê duyệt thủ tục hành chính
chính thức tại phường. Chỉ số cần đặt tên “đúng hạn kiểm tra giấy tờ”. Nếu nhóm chưa có cam kết
thời gian cho bước này thì để UNAVAILABLE, không dùng thời hạn hành chính trên cổng quốc gia.

## Dữ liệu Workflow cần quản lý

| Trường đề xuất | Ý nghĩa |
|---|---|
| firstSubmittedAtUtc | Lần nộp đầu, bất biến; SubmittedAt hiện tại bị thay khi nộp lại nên không thay thế được |
| dueAtUtc | Thời điểm phải hoàn tất kiểm tra, UTC; nullable cho hồ sơ chưa có chính sách |
| documentsReadyAtUtc | Thời điểm thực sự xác nhận DOCUMENTS_READY; không lấy ApprovedAt lịch sử |
| deadlinePolicyId / deadlinePolicyVersion | Chính sách chụp tại lúc tiếp nhận; thay chính sách không âm thầm đổi hồ sơ cũ |
| deadlineScope | DOCUMENT_PREPARATION_REVIEW để phân biệt SLA hành chính ngoài hệ thống |
| calendarId / calendarVersion / timeZone | Lịch làm việc, ngày nghỉ/lễ và múi giờ áp dụng |
| deadlineStatus | KNOWN / UNKNOWN / NOT_APPLICABLE; không dùng thời gian 0 làm giá trị thay thế |
| updatedAtUtc / sourceVersion | Phiên bản để đồng bộ và tính lại khi deadline hoặc lịch sử được điều chỉnh |

Nếu NEED_REVISION tạm dừng đồng hồ: lưu từng khoảng pausedAtUtc/resumedAtUtc và reason;
Workflow tính dueAtUtc hiệu lực theo chính sách. Không chỉ lưu một tổng số phút mà mất lịch sử.
Nếu không tạm dừng, nêu rõ thời gian chờ công dân vẫn tính vào SLA. Không tự gia hạn khi resubmit.

## Quy tắc cần chốt và triển khai tại Workflow

1. Đồng hồ bắt đầu lúc nộp hay cán bộ tiếp nhận; dùng giờ liên tục hay giờ/ngày làm việc.
2. Giờ mở/đóng cửa, cuối tuần, lịch lễ, nộp ngoài giờ; lịch được version hóa.
3. Yêu cầu bổ sung có pause không; resume lúc nào; có giới hạn gia hạn không.
4. Xử lý hủy, chuyển phường, đổi cán bộ, mở lại hồ sơ và chỉnh deadline thủ công. Chỉnh phải có lý do/audit.
5. Khi first submit, ghi chính sách và deadline trong transaction với trạng thái; resubmit không reset lần nộp đầu.
6. Hồ sơ lịch sử không đủ căn cứ để backfill phải UNKNOWN, không suy từ mô tả “1 ngày”.

## Hợp đồng gửi sang Analytics

Giữ database riêng. Có thể mở rộng nguồn GET /internal/analytics/v1/daily thành contractVersion=2
(có phối hợp rollout hai service), hoặc thêm endpoint phiên bản mới. Không thay ngầm nghĩa contract v1.
Workflow chịu trách nhiệm tính deadline; Analytics chỉ tổng hợp kết quả.

Theo mỗi ngày đạt DOCUMENTS_READY, nhóm procedure/ward (và cán bộ thực hiện nếu cần):
- readyWithKnownDeadlineCount;
- readyOnTimeCount: documentsReadyAtUtc <= dueAtUtc;
- readyLateCount: documentsReadyAtUtc > dueAtUtc;
- readyWithUnknownDeadlineCount; readyNotApplicableCount.

OnTimeRate = 100 * OnTime / (OnTime + Late); LateRate tương tự. Mẫu số 0 trả null.
Trả số unknown/not-applicable để FE thấy độ bao phủ, không gộp unknown vào đúng hạn.
Invariant: Known = OnTime + Late; Ready = Known + Unknown + NotApplicable.

“Đang quá hạn” là snapshot riêng tại asOfUtc: hồ sơ chưa Ready/chưa hủy, có deadline và now > dueAtUtc.
Không cộng snapshot từng ngày thành tổng tháng. Đề xuất trả pendingOverdueCount, pendingWithinDeadlineCount,
pendingUnknownDeadlineCount, asOfUtc; không nhét vào metric sự kiện hiện tại.

Ví dụ hợp đồng đề xuất (không phải dữ liệu thật):
```json
{
  "contractVersion": 2,
  "date": "2026-10-21",
  "timeZone": "Asia/Ho_Chi_Minh",
  "deadlineScope": "DOCUMENT_PREPARATION_REVIEW",
  "readyCount": 12,
  "readyWithKnownDeadlineCount": 10,
  "readyOnTimeCount": 8,
  "readyLateCount": 2,
  "readyWithUnknownDeadlineCount": 2,
  "readyNotApplicableCount": 0
}
```
Kết quả: đúng hạn 80%, trễ hạn 20%, thiếu deadline 2 hồ sơ; không phải 8/12.

## Kiểm tra nghiệm thu phối hợp

- Trước/đúng/sau deadline: đúng/sai biên so sánh.
- Ngày cuối tuần, ngày lễ, nộp sau giờ; UTC và ngày báo cáo UTC+7.
- Nộp lại nhiều lần không reset lần nộp đầu; pause/resume đúng chính sách.
- Dữ liệu cũ thiếu deadline giữ UNKNOWN; APPROVED cũ không tự thành DOCUMENTS_READY.
- Hủy, chuyển phường, mở lại, điều chỉnh deadline có lịch sử/version.
- Chạy lại job không cộng trùng; sửa deadline quá lookback phải có backfill/rebuild.
- Manager chỉ xem đúng phường, nguồn nội bộ giữ khóa service; không truyền formData/CCCD.

Hiện Analytics v1 vẫn trả onTimeRate/lateRate=null và
timelinessStatus=UNAVAILABLE_NO_WORKFLOW_DEADLINE. Chỉ bỏ giới hạn sau khi hợp đồng mới được
triển khai và kiểm tra hai service; tài liệu này không tạo migration hoặc deadline giả.
