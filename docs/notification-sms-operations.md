# TASK-26 — Vận hành SMS thông báo hồ sơ

Cập nhật 23/10/2026. Đây là runbook vận hành; hợp đồng API vẫn ở `api-guide.md`.

## Cấu hình và triển khai

1. Áp dụng `docs/sql/notification-esms-delivery.sql` trên database **AnalyticsSystem**, đã có migration `20261012010837_ReliableNotificationDelivery`. Không chạy trên IAM. Script thêm ba cột nullable và chỉ mục; không gửi tin nhắn.
2. Deploy AnalyticsSystem mới. Giữ `Sms:Enabled=false` đến khi hoàn tất cấu hình. Workflow outbox, RabbitMQ consumer và Notifications:WorkersEnabled phải hoạt động theo TASK-27.
3. Cấu hình secret riêng cho Analytics: `Sms__ApiKey`, `Sms__SecretKey`; cấu hình `Sms__Provider=eSMS`, `Sms__Brandname`, `Sms__SmsType=2`, `Sms__Sandbox=1`. Compose ánh xạ các biến `NOTIFICATION_SMS_*` trong `.env.example`; không tự dùng khóa OTP của IAM.
4. Điền `Sms__Templates__SUBMITTED`, `UNDER_REVIEW`, `NEED_REVISION`, `DOCUMENTS_READY`, `OFFICIALLY_RECEIVED`, `CANCELLED`, `INLINE_COMMENT_ADDED`, `OFFICER_SUBMITTED`, `OFFICER_UNDER_REVIEW` theo thông báo cần gửi. Mẫu chỉ thay `{ApplicationCode}`, phải là ASCII in được, sau thay tối đa 459 ký tự. Mẫu thiếu sẽ Blocked, không gửi nội dung tùy ý. Mẫu OTP Baotrixemay không được dùng làm mẫu thông báo hồ sơ.
5. `Sms__Enabled=true` bắt đầu tạo delivery SMS cho sự kiện mới. `Sandbox=1` chỉ mô phỏng phía nhà cung cấp, không chứng minh điện thoại nhận tin. Muốn gửi thật, chủ dự án phải có Brandname/mẫu CSKH được duyệt, đặt `Sms__TemplatesApproved=true` và `Sms__Sandbox=0`. Có thể phát sinh phí; lượt triển khai code này không bật hoặc gọi gửi thật.

Các sự kiện đã xử lý lúc SMS tắt không tự tạo thêm SMS khi bật lại. Không xóa inbox để phát lại tất cả kênh.

## Trạng thái và đối soát

Luồng: Workflow outbox → RabbitMQ → inbox → notification/delivery → eSMS. Không thêm endpoint FE; lỗi SMS không làm rollback hồ sơ.

| State | Ý nghĩa và xử lý |
|---|---|
| Pending | Chờ worker xử lý. |
| Dispatching | Đã chốt quyền gửi trong DB trước gọi ngoài. Nếu bị kẹt sau crash/cancel, đối soát trước; không tự retry. |
| Sent | eSMS trả CodeResult=100 khi gửi thật; chỉ xác nhận nhà cung cấp chấp nhận, chưa xác nhận máy nhận. |
| Sandbox | Nhà cung cấp chấp nhận thử nghiệm; SentAt vẫn NULL. |
| Blocked | Thiếu cấu hình/phone/template hoặc nhà cung cấp từ chối rõ ràng; sửa nguyên nhân rồi mới cân nhắc replay riêng delivery này. |
| Unknown | Timeout, lỗi HTTP/response hoặc lỗi xử lý chưa rõ kết quả; không tự gửi lại để tránh tính phí trùng. |
| Suppressed | Người nhận không còn hợp lệ theo kiểm tra IAM/phường hiện hữu. |

`external_message_id` lưu SMSID hợp lệ do eSMS trả; `provider=eSMS`, `is_sandbox` ghi chế độ. Thành công nhưng thiếu ID vẫn Sent kèm `sms.accepted_without_id` để tránh gửi lại. `SentAt` là thời điểm ghi nhận chấp nhận, không phải thời điểm phát đến điện thoại. Không có webhook DLR trong phạm vi này.

```sql
SELECT "Id", "NotificationId", "State", "Attempts", "ErrorCode", "SentAt",
       provider, external_message_id, is_sandbox
FROM notification_deliveries
WHERE "Channel" = 'SMS'
ORDER BY "NextAttemptAt" DESC
LIMIT 100;
```

`RequestId` gửi eSMS là delivery Id dạng 32 ký tự bỏ dấu gạch. Dùng ID này cùng SMSID để đối soát với nhà cung cấp. Không coi RequestId là cam kết exactly-once. Không reset hàng Sent/Sandbox/Unknown/Dispatching hàng loạt. Chỉ operator được xử lý replay sau đối soát; chưa có API quản trị replay. Email giữ cơ chế retry hiện hữu.

Tham chiếu: [hợp đồng eSMS](https://rest.esms.vn/MainService.svc/json/help/operations/SendMultipleMessage_V4_post_json), [Sandbox](https://developers.esms.vn/en/esms-api/moi-truong-test-sandbox).
