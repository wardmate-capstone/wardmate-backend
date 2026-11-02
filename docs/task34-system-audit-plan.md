# TASK-34 — Kế hoạch và thiết kế audit toàn hệ thống

Ngày 2026-11-02. Phạm vi TASK-34; không triển khai TASK-35/36, không Git/deploy. Kết quả thực thi cuối xem PROGRESS.md.

## Thứ tự thực hiện

1. Kiểm kê đường ghi dữ liệu của 7 DbContext. Chọn trigger thay vì chỉ SaveChanges interceptor vì code có ExecuteUpdate/ExecuteDelete/SQL trực tiếp.
2. Thêm outbox ở database từng service, trigger trên 37 bảng nghiệp vụ và metadata actor/IP/trace từ kết nối ứng dụng. Che dữ liệu nhạy cảm ngay tại nguồn.
3. Thêm relay bền vững và collector riêng trong Analytics. Khóa riêng theo nguồn; HMAC ký source/time/body; không đưa sự kiện vào notification consumer.
4. Thêm API đọc audit cho IT_ADMIN, giữ nguyên 5 API cấu hình/audit cấu hình đã có.
5. Kiểm tra migrations, khóa chính của trigger, rollback, bulk SQL, retry/dedup, dữ liệu che, xác thực và trusted proxy trong môi trường local cô lập.
6. Build Release toàn solution 0 lỗi/0 cảnh báo, cập nhật api-guide/system-constraints/PROGRESS và danh sách commit từng file cho Antigravity.

## Luồng dữ liệu

```text
JWT đã xác thực → DbConnectionInterceptor lấy sub, IP peer, trace
  → ghi nghiệp vụ trong database của service
  → trigger ghi audit_transport.outbox cùng giao dịch
  → commit thành công
  → relay đọc hàng bằng FOR UPDATE SKIP LOCKED
  → HTTPS + HMAC → Analytics /internal/audit/events
  → insert business_audit_events theo eventId duy nhất
  → nhận 204 → đánh dấu delivered_at tại nguồn
```

Rollback nghiệp vụ cũng rollback sự kiện. Nếu HTTP timeout sau khi collector đã lưu, gửi lại cùng eventId được chấp nhận mà không tạo bản sao. EventId cũ với nội dung khác trả409, không ghi đè. Collector lỗi không hủy nghiệp vụ đã commit; outbox giữ sự kiện và thử lại. Nếu chính DB không ghi được outbox thì giao dịch nghiệp vụ thất bại.

## Phạm vi bảng nguồn

| Nguồn | DbContext | Bảng nghiệp vụ được audit |
|---|---|---|
| iam | IamDbContext | public.users, user_profiles, roles, permissions, user_roles, role_permissions, wards |
| catalog | ProcedureDbContext | public.procedures, procedure_categories, procedure_drafts, procedure_versions |
| workflow | WorkflowDbContext | public.applications, application_checklists, application_comments, application_consultations, application_assignment_history, application_status_history, application_versions, officer_shifts, officer_leaves, officer_leave_decisions, calendar_closures |
| documentform | DocumentDbContext | document.application_forms, approved_qrs, form_templates, form_template_versions, generated_documents, supporting_documents, user_submissions |
| aiocr | ChatDbContext | public.chat_sessions, chat_messages, knowledge_documents, knowledge_chunks |
| analytics | AnalyticsDbContext | analytics.citizen_feedback, system_settings |
| notifications | NotificationDbContext | public.notifications, notification_deliveries |

Không audit lại audit_logs/business_audit_events, outbox/inbox, refresh_tokens/user_otps, welcome-email queue, ai_request_logs hoặc bảng metrics tổng hợp. Thay đổi password trên users vẫn ghi tên trường thay đổi nhưng giá trị là `[REDACTED]`. Audit này là thay đổi dữ liệu đã commit, không phải nhật ký mọi GET/đăng nhập thất bại; request/security logging hiện có vẫn đảm nhiệm phần đó.

## Quyền và dữ liệu riêng tư

- Chỉ IT_ADMIN được đọc audit toàn hệ thống; Manager không được đọc dữ liệu phường khác qua API này.
- Trước/sau mặc định che giá trị; chỉ mở các ID liên kết, trạng thái, cờ boolean, phiên bản, rating trong allowlist. Nội dung chat, profile, số giấy tờ, URL blob, token, password hash, JSON form, comment và file không được chép ra nguồn.
- Thay đổi nội dung đã che vẫn hiện tên trường thay đổi. Vì che toàn bộ nên không thể dùng audit này để khôi phục nội dung tài liệu; không phải backup.
- Nền/background hoặc truy cập DB không có JWT có actorId=null; không tự nhận người sở hữu bản ghi là người thao tác. Migration/SQL chạy trực tiếp không thể xác định danh tính người dùng web.
- IP mặc định là peer. Chỉ xử lý X-Forwarded-For khi Audit:TrustedProxies cấu hình IP proxy cụ thể; ForwardLimit=1, không tin mọi mạng. Multi-hop cần thiết kế riêng, không suy ra địa chỉ proxy Azure trong code.
- Trigger cấm thay payload outbox và cấm xóa sự kiện chưa gửi; collector cấm UPDATE/DELETE/TRUNCATE. DB owner/superuser có thể sửa trigger: không quảng cáo là chống giả mạo tuyệt đối.

## Cấu hình và rollout sau kiểm tra

Collector Analytics: áp dụng migrations Analytics trước, bật `SystemAdministration:Enabled=true`. Cấu hình `AuditIngestion:Keys:<source>` cho từng nguồn được phép, tối thiểu32byte. Không đưa khóa vào repo/chat.

Mỗi service: áp dụng migration TransactionalAuditSource đúng DbContext/database; trigger bắt đầu lưu outbox ngay sau migration dù relay chưa bật. Sau đó cấu hình:

```text
Audit__RelayEnabled=true
Audit__CollectorUrl=https://<analytics-host>/internal/audit/events
Audit__SigningKeys__<source>=<secret set outside source control>
```

`Audit:SigningKey` là fallback cho service chỉ có một nguồn; Analytics có hai nguồn analytics/notifications nên dùng SigningKeys riêng. Collector không nhận Bearer token người dùng thay cho HMAC. HTTP chỉ cho loopback local; remote dùng HTTPS, không redirect. Relay mặc định tắt; bật khi migration còn pending thì fail startup rõ ràng. Nếu Analytics và Notifications dùng cùng DB, outbox chung nhưng mỗi relay lọc source riêng; nếu tách DB thì mỗi DB có outbox riêng.

Giữ bản ghi collector không tự xóa. Outbox đã gửi cũng giữ nguyên trong bản này; chưa đặt lịch purge. Vận hành phải giám sát dung lượng. Có thể lập quy trình lưu trữ bản ghi đã gửi sau khi đối soát; không xóa pending để hết lỗi. Với runtime DB account, cấp DML nghiệp vụ và INSERT outbox + SELECT/UPDATE trạng thái delivery, không cấp ALTER/DROP/TRUNCATE. Migration dùng account riêng; quyền thực tế trên Azure cần người vận hành cấu hình.

Kiểm tra hàng đợi bằng SQL chỉ đọc tại DB nguồn:

```sql
SELECT source, count(*) FILTER (WHERE delivered_at IS NULL) AS pending,
       min(occurred_at) FILTER (WHERE delivered_at IS NULL) AS oldest_pending,
       max(attempts) AS max_attempts
FROM audit_transport.outbox GROUP BY source;
SELECT id, source, attempts, next_attempt_at, last_error
FROM audit_transport.outbox WHERE delivered_at IS NULL
ORDER BY occurred_at LIMIT 100;
```

`http_401`: kiểm tra cặp khóa và đồng hồ; `http_400`: hợp đồng/redaction; `http_409`: eventId trùng nội dung khác; `http_503`/transport: collector/DB chưa sẵn sàng. Không sửa payload sự kiện để ép gửi. Sửa nguyên nhân rồi retry; cần xử lý thủ công có lưu bằng chứng nếu bản phát hành tạo sự kiện không hợp lệ.

## Giới hạn và bằng chứng cần có

- Không backfill lịch sử trước migration; không tự tạo actor/thời điểm lịch sử.
- Không audit storage Blob hay hệ thống bên ngoài nếu không có thay đổi DB tương ứng. Không thay đổi semantics HTTP/file transaction hiện có.
- Audit gửi theo cơ chế ít nhất một lần; read API có độ trễ. Mỗi relay xử lý tối đa một hàng/lượt, nghỉ2giây; lỗi thử lại sau60giây. Cần đo tải trước khi nâng tốc độ.
- Chưa tuyên bố Azure đã rollout, IAM production đã test hay backup/retention hoàn tất. Chỉ ghi trạng thái đạt khi có kết quả kiểm tra trong PROGRESS.
