# TASK-34 — Bàn giao hoàn tất local cho Antigravity

Ngày cập nhật cuối: 2026-11-03, Asia/Saigon. Chỉ source local; Codex không chạy Git, không push, không migrate/deploy Azure. Phần “snapshot 19 commit” phía dưới là mốc cũ trước khi audit liên service hoàn tất; dùng trạng thái và manifest cuối ở cuối tài liệu.

## Phạm vi đã làm

5 API trong SystemAdministrationController: GET settings, GET settings/{key}, PUT settings/{key}, GET audit-logs, GET audit-logs/{id}; tiền tố `/api/v1/system`. Xem hợp đồng và lỗi trong `docs/api-guide.md`, rule SYS-ADMIN-01 trong `docs/system-constraints.md`.

Hai bảng trong schema analytics/database AnalyticsSystem: system_settings và audit_logs. Khóa duy nhất hỗ trợ hiện tại là analytics.lookbackDays. Audit và thay đổi cấu hình chung transaction. IP là peer kết nối, không phải bảo đảm IP browser sau proxy. IAM xác nhận IT_ADMIN. Tắt mặc định; chạy migration trước khi bật SystemAdministration:Enabled.

## Kiểm tra local

- 25/25 kiểm tra trong console tạm `logs/task34-check` với PostgreSQL16 Docker dùng một lần: migration, giá trị mặc định, validation, version conflict, concurrent writes, rollback khi audit lỗi, phân trang, IP header giả, role IT_ADMIN/Manager/Citizen, IAM unavailable, trigger append-only và SQL migration idempotent.
- Không thêm test project vào solution; console và SQL trong logs không bàn giao để commit. Đây là kiểm tra service và authorization filter với IAM giả lập, không phải test end-to-end HTTP/JWT/IAM thật.
- Không chạm database dự án, không dùng dữ liệu thật. Database container chỉ bind loopback.
- Build solution và kiểm tra model được ghi ở PROGRESS.md; chỉ merge khi kết quả cuối đạt 0 errors/0 warnings.

## Snapshot cũ trước khi hoàn tất audit liên service (không dùng để bàn giao cuối)

- Chưa audit CRUD/transition của Workflow, DocumentForm, IAM, Catalog; chưa có outbox/consumer audit liên service. Không dùng consumer notification để nhận audit vì contract khác.
- Chưa có chính sách lưu trữ/archival audit, trusted proxy IP thực, phân quyền DB runtime riêng khỏi migration owner.
- Chưa chạy 5 API với IAM thật qua Gateway/Docker application image; chưa deploy/migrate Azure.
- TASK-35 và TASK-36 chưa triển khai theo yêu cầu tập trung TASK-34 trước.

## Danh sách commit đề xuất — 19 commit cho Antigravity

Đây là danh sách đề xuất theo file, không phải commit đã tồn tại. Giữ các thay đổi khác của người dùng; chỉ stage hunks TASK-34. Tiền tố `AS/` dưới đây là `src/Services/WardMate.Services.AnalyticsSystem/`. Các commit trung gian có thể chưa build riêng; kiểm tra toàn bộ sau khi ghép đủ.

| # | File | Conventional Commit |
|---|---|---|
| 1 | AS/WardMate.Services.AnalyticsSystem.Domain/SystemAdministration.cs (mới) | feat(analytics): add system setting and audit entities |
| 2 | AS/WardMate.Services.AnalyticsSystem.Application/SystemAdministration.cs (mới) | feat(analytics): define system administration contracts |
| 3 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/SystemAdministration.cs (mới) | feat(analytics): persist settings with atomic audit |
| 4 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsDbContext.cs | feat(analytics): map settings and audit storage |
| 5 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsRegistration.cs | feat(analytics): register system administration service |
| 6 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsAggregationJob.cs | feat(analytics): consume configured aggregation lookback |
| 7 | AS/WardMate.Services.AnalyticsSystem.API/SystemAuditContextMiddleware.cs (mới) | feat(analytics): capture audit request metadata |
| 8 | AS/WardMate.Services.AnalyticsSystem.API/Controllers/SystemAdministrationController.cs (mới) | feat(analytics): expose protected settings and audit endpoints |
| 9 | AS/WardMate.Services.AnalyticsSystem.API/Program.cs | feat(analytics): wire audit metadata and migration startup guard |
| 10 | AS/WardMate.Services.AnalyticsSystem.API/appsettings.json | chore(analytics): default system administration to disabled |
| 11 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102021643_SystemAdministrationAudit.cs (mới) | feat(analytics): migrate settings and append-only audit tables |
| 12 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102021643_SystemAdministrationAudit.Designer.cs (mới) | chore(analytics): add administration migration metadata |
| 13 | AS/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/AnalyticsDbContextModelSnapshot.cs | chore(analytics): update administration model snapshot |
| 14 | src/Gateways/WardMate.YarpGateway/appsettings.json | feat(gateway): route local system administration APIs |
| 15 | src/Gateways/WardMate.YarpGateway/appsettings.Production.json | feat(gateway): route production system administration APIs |
| 16 | docs/api-guide.md | docs(api): describe system administration contracts |
| 17 | docs/system-constraints.md | docs(system): record settings and audit constraints |
| 18 | PROGRESS.md | docs(progress): record task34 local implementation evidence |
| 19 | docs/task34-commit-handoff.md (mới) | docs(task34): add granular commit handoff and remaining work |

Loại: 12 feat, 3 chore, 4 docs. Không push/merge trước khi đối chiếu thay đổi hiện có và đạt cổng kiểm tra AGENTS.md. Không thực hiện Git trong phiên Codex này.

## Trạng thái cuối thay thế snapshot cũ

- 5 API cấu hình/audit cấu hình và 2 API đọc audit nghiệp vụ cho IT_ADMIN; 1 endpoint HMAC nội bộ ẩn Swagger.
- 37 bảng/7 DbContext dùng trigger và transactional outbox trong đúng database nguồn. Relay at-least-once sang Analytics, retry và chống nhân đôi.
- Build Release toàn solution: 0 errors, 0 warnings. Kiểm tra PostgreSQL/HTTP local: 90/90. Model hiện tại khớp snapshot trong harness. Docker 6 service được build local; xem `PROGRESS.md`.
- Chưa migrate/deploy Azure, chưa test IAM thật. Không backfill lịch sử trước migration.

Manifest cuối có đúng **63 commit file-scoped: 43 feat, 10 build, 5 chore, 5 docs**. Antigravity chỉ stage các file dưới đây, không stage `logs/`, secrets, image hoặc output build. Trước push `kha`, phải báo lại đủ 63 message và chờ người dùng xác nhận.

```text
01 WardMate.sln :: chore(solution): register auditing building block
02 src/BuildingBlocks/WardMate.Auditing/WardMate.Auditing.csproj :: feat(audit): add shared auditing project
03 src/BuildingBlocks/WardMate.Auditing/SourceAudit.sql :: feat(audit): add transactional source outbox triggers
04 src/BuildingBlocks/WardMate.Auditing/AuditSourceMigration.cs :: feat(audit): add source audit migration helper
05 src/BuildingBlocks/WardMate.Auditing/AuditConnectionInterceptor.cs :: feat(audit): propagate authenticated audit metadata
06 src/BuildingBlocks/WardMate.Auditing/AuditSignature.cs :: feat(audit): sign audit relay payloads
07 src/BuildingBlocks/WardMate.Auditing/AuditRegistration.cs :: feat(audit): register guarded audit relay
08 src/BuildingBlocks/WardMate.Auditing/AuditRelay.cs :: feat(audit): deliver source outbox events reliably
09 src/BuildingBlocks/WardMate.Auditing/AuditTrustedProxy.cs :: feat(audit): support explicit trusted proxy IPs
10 src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/WardMate.Services.IAM.Infrastructure.csproj :: build(iam): reference auditing building block
11 src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/WardMate.Services.ProcedureCatalog.Infrastructure.csproj :: build(catalog): reference auditing building block
12 src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/WardMate.Services.ApplicationWorkflow.Infrastructure.csproj :: build(workflow): reference auditing building block
13 src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/WardMate.Services.DocumentForm.Infrastructure.csproj :: build(documentform): reference auditing building block
14 src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/WardMate.Services.AIOCR.Infrastructure.csproj :: build(aiocr): reference auditing building block
15 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/WardMate.Services.AnalyticsSystem.Infrastructure.csproj :: build(analytics): reference auditing building block
16 src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/DependencyInjection.cs :: feat(iam): register transactional auditing
17 src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/DependencyInjection.cs :: feat(catalog): register transactional auditing
18 src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/ProcedureCatalogClient.cs :: feat(workflow): register transactional auditing
19 src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/DependencyInjection.cs :: feat(documentform): register transactional auditing
20 src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/AssistantRegistration.cs :: feat(aiocr): register transactional auditing
21 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsRegistration.cs :: feat(analytics): register analytics audit source and collector
22 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/DependencyInjection.cs :: feat(notifications): register transactional auditing
23 src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Program.cs :: feat(iam): apply trusted proxy audit middleware
24 src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Program.cs :: feat(catalog): apply trusted proxy audit middleware
25 src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Program.cs :: feat(workflow): apply trusted proxy audit middleware
26 src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Program.cs :: feat(documentform): apply trusted proxy audit middleware
27 src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Program.cs :: feat(aiocr): apply trusted proxy audit middleware
28 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Program.cs :: feat(analytics): wire system and business audit pipeline
29 src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Infrastructure/Persistence/Migrations/20261102100000_TransactionalAuditSourceIamDbContext.cs :: feat(iam): migrate transactional audit source
30 src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Infrastructure/Persistence/Migrations/20261102100001_TransactionalAuditSourceProcedureDbContext.cs :: feat(catalog): migrate transactional audit source
31 src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Infrastructure/Migrations/20261102100002_TransactionalAuditSourceWorkflowDbContext.cs :: feat(workflow): migrate transactional audit source
32 src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Infrastructure/Migrations/20261102100003_TransactionalAuditSourceDocumentDbContext.cs :: feat(documentform): migrate transactional audit source
33 src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.Infrastructure/Migrations/20261102100004_TransactionalAuditSourceChatDbContext.cs :: feat(aiocr): migrate transactional audit source
34 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102100005_TransactionalAuditSourceAnalyticsDbContext.cs :: feat(analytics): migrate transactional audit source
35 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/20261102100006_TransactionalAuditSourceNotificationDbContext.cs :: feat(notifications): migrate transactional audit source
36 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Domain/SystemAdministration.cs :: feat(analytics): add system setting and audit entities
37 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Application/SystemAdministration.cs :: feat(analytics): define system administration contracts
38 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/SystemAdministration.cs :: feat(analytics): persist settings with atomic audit
39 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/SystemAuditContextMiddleware.cs :: feat(analytics): capture system audit request metadata
40 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Controllers/SystemAdministrationController.cs :: feat(analytics): expose protected system administration APIs
41 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsDbContext.cs :: feat(analytics): map settings and business audit storage
42 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/AnalyticsAggregationJob.cs :: feat(analytics): read configured aggregation lookback
43 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/appsettings.json :: chore(analytics): disable system administration by default
44 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102021643_SystemAdministrationAudit.cs :: feat(analytics): migrate settings and immutable audit logs
45 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102021643_SystemAdministrationAudit.Designer.cs :: chore(analytics): add system administration migration metadata
46 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/AnalyticsDbContextModelSnapshot.cs :: chore(analytics): update audit model snapshot
47 src/Gateways/WardMate.YarpGateway/appsettings.json :: feat(gateway): route system administration APIs locally
48 src/Gateways/WardMate.YarpGateway/appsettings.Production.json :: feat(gateway): route system administration APIs in production
49 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Domain/BusinessAuditEvent.cs :: feat(analytics): add business audit event entity
50 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Application/BusinessAuditContracts.cs :: feat(analytics): define business audit contracts
51 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/BusinessAuditStore.cs :: feat(analytics): validate and persist business audit events
52 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Controllers/BusinessAuditController.cs :: feat(analytics): expose audit read and ingestion endpoints
53 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102024750_BusinessAuditCollector.cs :: feat(analytics): migrate immutable business audit collector
54 src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/Migrations/Analytics/20261102024750_BusinessAuditCollector.Designer.cs :: chore(analytics): add business audit migration metadata
55 src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Dockerfile :: build(iam): include auditing project in image restore
56 src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Dockerfile :: build(catalog): include auditing project in image restore
57 src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Dockerfile :: build(documentform): include auditing project in image restore
58 src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Dockerfile :: build(aiocr): include auditing project in image restore
59 docs/task34-system-audit-plan.md :: docs(task34): document system audit architecture and rollout
60 docs/task34-commit-handoff.md :: docs(task34): provide granular Antigravity handoff
61 docs/api-guide.md :: docs(api): describe system administration and audit APIs
62 docs/system-constraints.md :: docs(system): record system audit constraints
63 PROGRESS.md :: docs(progress): record completed local task34 evidence
```
