# TASK-35 — Theo dõi triển khai

Ngày cập nhật: 2026-11-03, Asia/Saigon.

## Đã có và đã đọc source

- SharedKernel DependencyHealth cung cấp liveness /health và readiness /health/ready.
- Các service đã đăng ký database probes; Workflow và Analytics có RabbitMQ probe.
- Analytics dùng AnalyticsDbContext và database/schema riêng; có Hangfire cho aggregation.
- Quyền quản trị dùng AnalyticsAccess("IT_ADMIN") và IAM access-context.
- Đã có `IBlobStorageClient` và `AzureBlobStorageClient` trong `WardMate.SharedKernel.Blob`.

## Tiến độ đã hoàn thành

1. **Schema & Migrations**:
   - `analytics.service_health_logs` (check constraints `ck_health_status`, `ck_health_duration`, composite indexes).
   - `analytics.health_check_requests` (hàng đợi durable cho manual check-now).
   - `analytics.system_backup_logs` (check constraints `ck_backup_status`, `ck_backup_size`, partial index chống duplicate `Queued`/`Running`).
   - 2 Migrations: `20261103070217_SystemOperationsHistory.cs` và `20261103070820_DurableHealthChecks.cs`.

2. **Health Check Endpoints (4/4 APIs)**:
   - `GET /api/v1/system/health/services` (lấy latest probe, phát hiện stale observation).
   - `GET /api/v1/system/health/services/{serviceName}` (lấy chi tiết một target).
   - `GET /api/v1/system/health/history` (phân trang, lọc theo service, khoảng thời gian UTC).
   - `POST /api/v1/system/health/check-now` (durable queue qua advisory lock, trả 202).

3. **Health Worker**:
   - `SystemHealthWorker` chạy ngầm, đồng bộ hóa đa replica qua `pg_advisory_xact_lock(353535, 1)`, timeout 5s, xử lý `HealthCheckRequest` tồn đọng sau restart.

4. **Backup Endpoints (4/4 APIs)**:
   - `GET /api/v1/system/backups` (hỗ trợ lọc `service`, `status`, `from`/`to` UTC, phân trang).
   - `GET /api/v1/system/backups/{id}` (chi tiết metadata bản sao lưu, không rò rỉ secret).
   - `POST /api/v1/system/backups` (tạo yêu cầu sao lưu bền vững, advisory lock `353535, 3`, chống duplicate, gate `RunnerProvisioned`).
   - `POST /api/v1/system/backups/{id}/verify`: tải Blob (cả stream không seek), đối chiếu kích thước/SHA-256, dùng `pg_restore --file=-` giải mã toàn archive. Không chỉ kiểm tra header; không đồng nghĩa đã restore database.

5. **Backup Worker & Retention**:
   - `SystemBackupWorker` là hosted service trong tiến trình Analytics, giữ session advisory lock `(353535, 4)` trên kết nối không pooling suốt job. Recovery chỉ chạy khi giữ được lock. Giữ giới hạn maxReplicas=1 trong runbook; chưa nghiệm thu tình huống mất kết nối lock giữa job.
   - Chuyển `Queued` -> `Running` -> `Succeeded`/`Failed`.
   - Bắt buộc `pg_dump` CLI, không có fallback giả archive. Thiếu tool/upload lỗi -> Failed. Tính SHA-256 và SizeBytes sau dump thật.
   - Tải lên Blob thông qua `IBlobStorageClient`; operator phải xác minh container private và quyền storage khi triển khai.
   - Tự động phục hồi job bị treo khi restart sang `Failed` (`worker_restarted_interrupted`).
   - Không ghi connection string, mật khẩu hoặc raw exception vào log.
   - Retention engine tự động dọn `service_health_logs` > 30 ngày và `system_backup_logs` > 30 ngày (xóa blob kèm theo), LUÔN BẢO LƯU tối thiểu 1 bản backup đã xác minh (`Verified`) mới nhất của mỗi service.
   - Cập nhật `Dockerfile` Analytics cài đặt `postgresql-client` để hỗ trợ `pg_dump` trong container.

6. **Build Verification**:
   - Solution Release build: **0 errors, 0 warnings**.
   - 29/29 assertions trong harness tạm `logs/task35-check` trên PostgreSQL 15 Docker, Blob giả lập/nonseekable, pg_dump/pg_restore thật. Bao gồm Verify đang bận trả409. Không tạo test project trong solution.
   - Restore drill local vào DB trống thành công: 10 bảng analytics, 1 bản ghi backup fixture. Không cập nhật RestoreTestedAtUtc cho production từ kết quả fixture.
   - Chưa nghiệm thu Azure, IAM thật, Blob Azure thật, timeout/mất kết nối giữa job hoặc toàn bộ giao diện FE.

## Điều kiện vận hành môi trường Production

- Operator áp dụng migrations trên database trước khi kích hoạt `SystemHealth:Enabled=true` hoặc `SystemBackups:Enabled=true`.
- Cấu hình connection string của từng target DB trong `SystemBackups:Targets:{serviceName}` và `AzureBlob:ConnectionString`.
- Sau khi cấu hình target và storage, bật `SystemBackups:RunnerProvisioned=true` để bắt đầu nhận và xử lý job sao lưu.
- Phiên bản pg_dump phải tương thích server (image Debian mặc định hiện cài client 15; không bật runner cho server mới hơn khi chưa cung cấp client phù hợp). Dùng kết nối trực tiếp/session hỗ trợ advisory lock, không transaction-pooling cho Analytics.
- Verify tối đa 1 GiB mặc định (`SystemBackups:MaxVerifyBytes`), timeout toàn thao tác 5 phút, pg_restore 2 phút. Lỗi lần verify mới xóa VerifiedAtUtc cũ, trả Succeeded + errorCode (Succeeded chỉ là upload trước đó).
- Retention và Verify dùng chung khóa `(353535,5)`. Thiếu Blob hoặc xóa Blob lỗi giữ metadata; chỉ xóa object thuộc prefix backup/id tương ứng.
- Handoff nghiệm thu: `docs/task35-azure-acceptance.md`; Azure vẫn chờ Antigravity theo AGENTS.md.
