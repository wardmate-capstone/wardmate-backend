# Vận hành Notifications — TASK-26 / TASK-27

## Phạm vi và giới hạn

Đã có luồng Workflow transaction -> workflow_outbox -> RabbitMQ -> notification_inbox -> notifications + notification_deliveries -> SignalR/SMTP adapter. SMS chưa có nhà cung cấp, luôn Blocked qua adapter mặc định. Không có gửi Email/SMS thật trong kiểm thử. Redis backplane/Azure SignalR chưa triển khai; AnalyticsSystem chạy tối đa 1 replica để realtime đến đúng connection. Các worker DB dùng FOR UPDATE SKIP LOCKED để tránh cùng xử lý một hàng.

Event version 1 gồm EventId, ApplicationId, ApplicantId, AssignedOfficerId, WardCode, ApplicationCode, Kind, Status, OccurredAt UTC. Không chứa token, nội dung biểu mẫu/nhận xét, Email hoặc số điện thoại. Không đổi schema payload v1 mà không có kế hoạch tương thích producer/consumer.

## Triển khai theo thứ tự

1. IAM: deploy endpoint nội bộ recipient directory; đặt Notifications__ServiceKey bằng secret riêng >=32 ký tự.
2. Workflow: áp dụng migration 20261012010910_WorkflowNotificationOutbox trước khi chạy code mới. Outbox được ghi kể cả RabbitMq__Enabled=false để không mất thay đổi mới; không backfill lịch sử đã có trước migration.
3. AnalyticsSystem: áp dụng 20261012010837_ReliableNotificationDelivery (sau InitialNotifications). Có Inbox, deliveries, source_event_id và recipient_ward_code.
4. Chuẩn bị RabbitMQ có lưu trữ bền, tài khoản/vhost riêng và ACL phù hợp. Producer/consumer hiện khai báo topology, nên cần quyền configure tương ứng. Exchange wardmate.workflow.v1, routing key workflow.changed; queue wardmate.notifications.workflow.v1; queue lỗi wardmate.notifications.workflow.v1.dead. RabbitMQ single-node không phải HA.
5. Bật các workers khi IAM và broker đã sẵn sàng. Cấu hình IAM/Workflow JWT giữ như hiện tại.

Workflow và AnalyticsSystem:

- RabbitMq__Enabled=true
- RabbitMq__Host=<broker-host>, RabbitMq__Port=5671 nếu dùng TLS
- RabbitMq__User=<user>, RabbitMq__Password=<secret>, RabbitMq__VirtualHost=<vhost>
- RabbitMq__UseTls=true trên kết nối cloud cần TLS; server name lấy từ Host, không tắt kiểm tra chứng chỉ.

AnalyticsSystem:

- Notifications__WorkersEnabled=true
- Notifications__ServiceKey=<cùng secret của IAM>
- Notifications__RevalidateSeconds=15
- Iam__BaseUrl=https://<iam-origin>/
- ConnectionStrings__NotificationsDatabase=<secret database riêng>
- Email__Enabled=false, Sms__Enabled=false cho đến khi cấu hình nhà cung cấp.

SMTP khi đã có nhà cung cấp: Email__Enabled=true, Email__Host, Email__Port=587, Email__From, Email__Username, Email__Password qua secret. Adapter dùng STARTTLS, không hỗ trợ implicit TLS 465. Sms__Enabled vẫn false cho đến khi có adapter thật. Bật kênh chỉ áp dụng các sự kiện được xử lý sau đó; không tự gửi lại hàng loạt lịch sử.

## Local — chủ dự án tự build/chạy

Điền docker/.env: NOTIFICATIONS_DB_PASSWORD, NOTIFICATIONS_ENABLED=true, NOTIFICATIONS_SERVICE_KEY và RABBITMQ_PASSWORD; RABBITMQ_USER mặc định wardmate_admin. Không commit .env. Không dùng service key của FE hoặc JWT signing key.

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml --profile notifications build iam-api application-workflow analytics-system
docker compose --env-file docker/.env -f docker/docker-compose.yml --profile notifications up -d rabbitmq iam-api application-workflow analytics-system
```

Compose đã thêm volume rabbitmq-data. Nếu broker cũ có message ở layer container mà không có volume, sao lưu trước khi recreate để không mất dữ liệu. Đổi biến mật khẩu không tự đổi mật khẩu tài khoản đã lưu trong broker/DB volume. Không dùng down -v để sửa lỗi kết nối.

## Retry, chống trùng và phục hồi

- Outbox chỉ đánh dấu published_at sau publisher confirm; mandatory routing tránh coi message không có queue là đã gửi. Deadline chờ publish 10 giây. Lỗi giữ hàng và retry backoff tối đa 300 giây, không tự xóa sự kiện chưa gửi.
- Consumer manual ACK chỉ sau commit Inbox. EventId là PK, ON CONFLICT bỏ lần giao trùng; cùng event không tạo notification lần hai. Unique (source_event_id,recipient_user_id,channel) và (NotificationId,Channel) tăng bảo vệ dữ liệu.
- Message malformed/khác version được reject vào broker dead queue. Message hợp lệ nhưng xử lý IAM lỗi được lưu Inbox Pending, retry tối đa 10 lần rồi Dead trong DB.
- Delivery Pending retry tối đa 10 lần rồi Dead; Blocked nghĩa kênh/contact chưa cấu hình; Suppressed nghĩa tài khoản/phạm vi không còn hợp lệ; Sent nghĩa đã giao cho transport. Không phải xác nhận đọc hoặc exactly-once.
- Gửi SignalR/SMTP xong nhưng chết trước DB commit có thể phát lại. FE khử trùng id; SMTP không bảo đảm chống trùng. Dữ liệu notifications vẫn cho phép đọc lại sau mất mạng.
- Chưa có endpoint quản trị replay hàng đợi. DBA chỉ reset đúng bản ghi lỗi đã kiểm tra, không replay tất cả và không đổi Processed/Sent trở lại Pending. Kiểm tra contact/quyền/nhà cung cấp trước khi replay.

Ví dụ SQL trên database Notifications (thay UUID bằng hàng đã đối soát):

```sql
SELECT "Id", "State", "Attempts", "ErrorCode", "NextAttemptAt"
FROM notification_inbox WHERE "State" = 'Dead';
SELECT "Id", "NotificationId", "Channel", "State", "Attempts", "ErrorCode"
FROM notification_deliveries WHERE "State" IN ('Dead', 'Blocked');

UPDATE notification_inbox
SET "State"='Pending', "Attempts"=0, "ErrorCode"=NULL, "NextAttemptAt"=now()
WHERE "Id"='<event-uuid>' AND "State"='Dead';
UPDATE notification_deliveries
SET "State"='Pending', "Attempts"=0, "ErrorCode"=NULL, "NextAttemptAt"=now()
WHERE "Id"='<delivery-uuid>' AND "State" IN ('Dead','Blocked');
```

Outbox trên database Workflow: kiểm tra published_at IS NULL, attempts, next_attempt_at, last_error. Không chỉnh payload tùy ý. Dead queue cần điều tra schema/config trước khi đưa message về queue chính; giữ nguyên EventId. Chưa có retention job, cần lên chính sách archive Inbox/Outbox mà không mất khoảng chống trùng; không tự xóa Inbox đã xử lý khi broker còn khả năng redelivery.

## Kiểm thử deploy sau khi Antigravity/chủ dự án triển khai

- Chỉ dùng tài khoản/hồ sơ test được cho phép; ghi mã hồ sơ/EventId để đối soát. Không bật Email/SMS thật trong smoke test nếu chưa có sandbox.
- Kết nối Hub bằng token công dân và FD phường A/B.
- Nộp hồ sơ về A: outbox published, inbox Processed, công dân + FD A có notifications; FD B không nhận.
- FD A tiếp nhận, nhận xét, yêu cầu bổ sung; công dân nộp lại; FD A duyệt hoặc từ chối: kiểm tra đúng người nhận và lịch sử.
- Giao lại cùng EventId: số bản ghi thông báo không tăng. Không xóa Inbox để thử chống trùng.
- Tạm gián đoạn broker trên môi trường test riêng: API Workflow vẫn commit; bật lại broker, Outbox được chuyển tiếp. Không thử dừng broker production đang phục vụ người dùng.
- Khóa tài khoản/đổi phường/thu hồi permission: connection bị đóng qua guard; reconnect không xem được thông báo thuộc phường cũ.
- Kiểm tra CORS/WebSocket/token expiry sau ingress và khi đi qua Gateway; không ghi access_token vào log.

Codex chưa deploy image, chưa áp dụng migration vào DB đang dùng hoặc Azure, chưa kiểm chứng tích hợp trên deploy. Các kết quả local được ghi tại PROGRESS.md; không suy ra deploy đã đạt từ local tests.
