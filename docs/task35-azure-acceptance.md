# TASK-35 — Bàn giao sửa lỗi backup và nghiệm thu Azure

Ngày: 2026-11-03, Asia/Saigon. **Azure chưa nghiệm thu trong phiên Codex này.**
Theo AGENTS.md, Antigravity thực hiện Git/deploy. Đọc `docs/antigravity-azure-runbook.md`
trước mọi thao tác image. Không coi các mục dưới đây là kết quả đã chạy.

## Phạm vi sửa

- `SystemBackups.cs`: không Verified khi thiếu Blob/file; kiểm tra size/hash/full archive với
  stream không seek; giới hạn dung lượng/thời gian; xóa kết quả Verified cũ khi verify lỗi.
- `SystemBackupWorker.cs`: không tạo archive giả khi thiếu pg_dump, không Succeeded khi upload lỗi;
  giữ metadata khi thiếu Blob/xóa lỗi; khóa recovery và khóa chung Verify/retention.
- Không đổi route/DTO, không thêm migration cho đợt sửa này. Schema TASK-35 hiện có vẫn bắt buộc.
- Giữ maxReplicas=1. Kết nối Analytics phải hỗ trợ session advisory lock; không dùng transaction pooler.

## Điều kiện trước triển khai

1. Merge toàn bộ thay đổi đã review, theo quy trình `kha` và gate Docker của chủ dự án.
2. Lưu image cũ. Không bật đồng thời worker cũ và mới: worker cũ chưa giữ ownership lock.
3. Kiểm tra DB có migrations `20261103070217_SystemOperationsHistory`,
   `20261103070820_DurableHealthChecks` và các migration phụ thuộc trong source hiện tại.
4. Kiểm tra `pg_dump --version`, `pg_restore --version` trong image. Dockerfile hiện dùng client15
   từ Debian: nếu server target mới hơn15, phải provision client tương thích trước khi bật runner.
   Không giải quyết lỗi version bằng tạo JSON/PGDMP giả hoặc bỏ validation.
5. Secret target connection strings có quyền backup thích hợp; cấu hình SSL đúng, Blob container
   private, không anonymous read, quyền upload/read/delete đủ. Không in secrets hoặc dùng dữ liệu thật
   trong log/handoff. Blob thật và IAM thật chưa được kiểm chứng bằng fixture local.
6. Cấu hình `SystemBackups:Enabled`, `RunnerProvisioned`, `Targets`, `ContainerName` và
   `AzureBlob:ConnectionString`; giới hạn `MaxVerifyBytes` mặc định1GiB, đủ temp disk.
7. Kiểm tra `/health/ready` trước/sau. Health200 không thay thế kiểm tra backup bằng token IT_ADMIN.

## Nghiệm thu qua Swagger với quyền thật

Ghi UTC, image digest, method/path, HTTP status, backupId/requestId/traceId và kết quả;
không ghi Authorization. Chỉ dùng target kiểm thử dành riêng cho các ca lỗi có chủ ý.

| Ca | Thao tác | Tiêu chí đạt |
|---|---|---|
| A01 | IT_ADMIN gọi 4 GET health: list/detail/history; POST check-now | GET200 đúng target; POST202 queued, sau đó có observation mới. Không coi Unknown/stale là Healthy. |
| A02 | Không token; token không IT_ADMIN gọi health/backup | 401/403, không tạo job hoặc lộ metadata. |
| A03 | POST backups với serviceName target fixture, reason hợp lệ | 202 Queued; GET detail tiến tới Succeeded, size>0 và hash có giá trị. |
| A04 | GET backups lọc service/status/from/to/page | Dòng fixture đúng ID, tổng/phân trang đúng; query sai trả400. |
| A05 | POST verify trên bản A03 | 200 Verified, VerifiedAtUtc có giá trị; RestoreTestedAtUtc vẫn null nếu chưa restore drill. |
| A06 | Hai POST backup cùng service khi một job còn Queued/Running | Lần trùng409; không có hai active job. |
| A07 | Target không tồn tại; reason rỗng | 404/400, không enqueue. |
| A08 | Verify khi Queued/Running hoặc Failed | 409/400, không Verified. |
| A09 | Tạm làm mất file của backup fixture đã Verified rồi Verify | 404 file_missing; detail xóa VerifiedAtUtc, có errorCode. Không thực hiện trên backup thật. |
| A10 | Fixture hash/size sai; archive fake/truncated nhưng khớp metadata | 400, không Verified. Chỉ môi trường test cô lập được phép sửa fixture metadata. |
| A11 | Fixture thiếu pg_dump/pg_restore hoặc storage không truy cập được | Create/Verify503 hoặc workerFailed phù hợp, không fakeSucceeded/Verified. |
| A12 | Verify đồng thời retention/Verify khác | 409 verification_busy; không bị xóa file trong Verify. |
| A13 | Restart worker khi job fixture đang Running | Sau restart job cũFailed với worker_restarted_interrupted; có thể tạo job mới. Không tự retry job mơ hồ. |
| A14 | Retention fixture quá hạn | Giữ latestVerified; xóa Blob lỗi/thiếu client thì giữ metadata; chỉ xóa prefix owned. |
| A15 | Restore drill bản mới vào DB cô lập | pg_restore --exit-on-error thành công; đối chiếu schema, số bản ghi và dữ liệu mẫu. Tuyệt đối không restore lên DB production. |

Các ca A09–A14 không được cố tình phá cấu hình production dùng chung. Nếu không có môi trường
Azure test cô lập, giữ bằng chứng local và đánh dấu các ca đó chưa nghiệm thu cloud.
Không xác nhận toàn hệ thống đã đạt chỉ từ một GET200 hoặc Swagger tải được.

## Bàn giao FE

Giữ nguyên lời gọi. Create202 là xếp hàng. Verify lỗi phải reload detail; Succeeded + errorCode
không được hiển thị “đã xác minh”. Verified là toàn vẹn/đọc archive được, chưa phải restore thành công.
Thông báo timeout/503 cho phép người dùng chủ động thử lại; không tự lặp POST tạo backup.

## Commit đề xuất cho Antigravity

Phạm vi phiên sửa hiện tại gồm 6 commit đề xuất (2 fix, 4 docs); đây không phải số commit đã tạo.

1. `fix(analytics): require complete archive verification and clear stale results` — SystemBackups.cs.
2. `fix(analytics): harden backup worker ownership and retention` — SystemBackupWorker.cs.
3. `docs(api): explain backup verification failures and recovery` — docs/api-guide.md.
4. `docs(constraints): update backup integrity and lifecycle rules` — docs/system-constraints.md.
5. `docs(operations): add Azure backup acceptance handoff` — file mới này riêng một commit.
6. `docs(progress): record local backup validation and pending Azure acceptance` — PROGRESS.md,
   docs/task35-implementation-status.md.

Các thay đổi có sẵn từ phiên trước (Dockerfile/migrations/những file khác) phải kiểm kê riêng,
không suy ra đã commit hoặc nằm trong image chỉ từ danh sách trên. Không commit logs/task35-check.
