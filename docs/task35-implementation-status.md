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
   - `POST /api/v1/system/backups/{id}/verify` (xác minh stream từ Blob private, kiểm tra SHA-256 hash và header magic bytes `PGDMP`).

5. **Backup Worker & Retention**:
   - `SystemBackupWorker` chạy ngầm, tuần tự hóa job qua `pg_advisory_xact_lock(353535, 4)`.
   - Chuyển `Queued` -> `Running` -> `Succeeded`/`Failed`.
   - Sinh archive database (hỗ trợ `pg_dump` CLI native và fallback managed snapshot `PGDMP`), tính SHA-256, dung lượng `SizeBytes`.
   - Tải lên kho lưu trữ Blob private thông qua `IBlobStorageClient`.
   - Tự động phục hồi job bị treo khi restart sang `Failed` (`worker_restarted_interrupted`).
   - Không ghi connection string, mật khẩu hoặc raw exception vào log.
   - Retention engine tự động dọn `service_health_logs` > 30 ngày và `system_backup_logs` > 30 ngày (xóa blob kèm theo), LUÔN BẢO LƯU tối thiểu 1 bản backup đã xác minh (`Verified`) mới nhất của mỗi service.
   - Cập nhật `Dockerfile` Analytics cài đặt `postgresql-client` để hỗ trợ `pg_dump` trong container.

6. **Build Verification**:
   - Solution Release build: **0 errors, 0 warnings**.

## Điều kiện vận hành môi trường Production

- Operator áp dụng migrations trên database trước khi kích hoạt `SystemHealth:Enabled=true` hoặc `SystemBackups:Enabled=true`.
- Cấu hình connection string của từng target DB trong `SystemBackups:Targets:{serviceName}` và `AzureBlob:ConnectionString`.
- Sau khi cấu hình target và storage, bật `SystemBackups:RunnerProvisioned=true` để bắt đầu nhận và xử lý job sao lưu.
