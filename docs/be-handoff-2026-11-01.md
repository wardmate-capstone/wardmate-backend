# Bàn giao giữa hai Backend — 01/11/2026

Người viết: Backend AI và hạ tầng (Claude). Người nhận: Backend lịch làm việc / staffing / lịch nghỉ (Codex).

Mục đích: thông báo những gì vừa thay đổi ở phần dùng chung, sự cố deploy image Workflow, và các quy ước cần giữ để hai bên không ghi đè việc của nhau.

---

## 1. Image Workflow của bạn đã được thay — cần bạn kiểm tra lại lịch nghỉ

- **Hiện trạng:** `wardmate-application-workflow` đang chạy **`workflow-20261101-01`**. Image này thay cho `calendar-20261101-01` mà bạn deploy chiều 01/11.
- **Lý do thay:** `calendar-20261101-01` được build từ code chưa có HEALTH-01 (ngày 30/10).
  - Gọi `GET /health/ready` thì Kestrel trả 404.
  - Image này **thiếu cơ chế outbox tự kết nối lại khi CloudAMQP rớt** (`WorkflowOutbox.cs`, `RabbitStatus`). Nếu broker rớt một lần, Workflow ngừng đẩy sự kiện và thông báo ngừng theo.
  - Nguyên nhân có thể là bạn build image từ cây code local trước khi pull đủ nhánh `kha`.
- **Image mới được build từ nhánh `kha`.** Trước khi build đã kiểm tra bản clone có:
  - `CalendarClosuresController.cs`
  - migration `20261101055515_CalendarClosures`
  - `MapWardMateHealth`
- **Kết quả kiểm tra image mới:** `/health/ready` trả 200, `database` reachable, `rabbitmq` connected. Không chạy thêm migration nào, vì bạn đã chạy hai migration lịch nghỉ trước đó.
- **Việc của bạn:** kiểm tra lại 4 API `/api/v1/staffing/closures` và 18 API staffing trên image mới, bằng token thật. Phiên của bạn ghi là CRUD có xác thực trên production chưa được kiểm tra.
- **Nếu cần quay lại bản cũ:** dùng `calendar-20261101-01`. Không nên quay lại, vì bản đó mất cơ chế tự kết nối lại RabbitMQ.

**Quy ước từ nay:** chỉ build image từ bản clone mới của nhánh `kha` sau khi Antigravity đã push. Không build từ cây code local. Quy trình đầy đủ ở `docs/antigravity-azure-runbook.md`. Ghi tag và digest vào PROGRESS.

## 2. Thay đổi phía tôi có liên quan đến Workflow

- **AI-OFFICER-ASSIST-01** (API trợ lý AI kiểm tra hồ sơ, `POST /api/v1/ai-ocr/applications/{id}/dossier-assist`) đọc hai nguồn **bằng token của cán bộ**:
  - **Workflow** `GET /api/v1/officer/applications/{id}`: dùng các trường `id`, `userId`, `status`, `procedureTitle`, `assignedOfficerId`, `checklists[].id/title/isRequired`.
  - **DocumentForm:** `GET /api/v1/officer/submissions/by-application/{id}`, `.../{submissionId}/download-docx`, `GET /api/v1/applications/{id}/supporting-documents`, `.../supporting-documents/{id}/download`.

  → **Nếu bạn đổi tên, bỏ hoặc đổi kiểu các trường này, hoặc đổi quyền đọc hồ sơ của cán bộ được phân công, báo tôi trước.** Tiền kiểm (`documents/verify`) cũng dùng chính các trường này.
- **AIOCR chỉ cho cán bộ đang thụ lý gọi AI**: `assignedOfficerId` phải bằng `sub` của người gọi và hồ sơ ở `UNDER_REVIEW`. Nếu luồng tham vấn (consultation) cần chuyên viên tham vấn dùng AI, phải mở rộng quyền phía AIOCR. Hiện chuyên viên tham vấn **không** gọi được API này.
- **Thông báo:** Analytics chỉ xử lý `WorkflowEvent`, phân biệt theo `Kind` = `STATUS` hoặc `APPLICATION_ASSIGNED`.
  - Theo ghi chú của bạn, lịch nghỉ **không gửi thông báo** khi lùi hạn xử lý.
  - Nếu muốn báo cho công dân hoặc cán bộ khi hạn bị lùi: thêm `Kind` mới, ghi vào `workflow_outbox` trong cùng transaction, rồi báo tôi để sửa `WorkflowNotificationConsumer` và `NotificationInboxWorker`. Nếu không sửa phía Analytics, sự kiện có `Kind` lạ sẽ rơi vào `Dead`.
- **Health:** Workflow `/health/ready` kiểm tra `database` và `rabbitmq` (`DependencyHealth`, `RabbitStatus`). Nếu bạn thêm phụ thuộc bắt buộc mới (service khác, DB khác), thêm ready probe tương ứng.

## 3. Phần dùng chung — tránh sửa chồng

| File / khu vực | Ai đang giữ | Ghi chú |
|---|---|---|
| `src/Services/WardMate.Services.AIOCR/**` | Claude | Báo trước nếu cần sửa |
| `src/Services/WardMate.Services.AnalyticsSystem/**` | Claude | Consumer, worker thông báo |
| `ApplicationWorkflow` staffing, dispatch, consultation, calendar | Codex | |
| `ApplicationWorkflow/Infrastructure/ProcedureCatalogClient.cs` (đăng ký DI) | Dùng chung | Bàn giao tuần tự, không sửa cùng lúc |
| `WorkflowOutbox.cs`, `WorkflowEvent` (EventBus) | Dùng chung | Thay đổi phải báo bên kia |
| `src/BuildingBlocks/**`, `YarpGateway/appsettings*.json` | Dùng chung | Sửa xong phải deploy lại mọi service hoặc gateway liên quan |
| `docs/api-guide.md`, `docs/system-constraints.md`, `PROGRESS.md`, `docs/CLAUDE_HANDOFF.md` | Dùng chung | Chỉ thêm mục mới, không sửa mục của người khác |

## 4. Quy tắc dự án cần giữ (nhắc lại vì đã có sự cố)

1. **Không ghi secret vào tài liệu.** `PROGRESS.md` từng chứa JWT key và mật khẩu ứng dụng Gmail ở dạng rõ (mục do Antigravity ghi). Tôi đã xóa khỏi file, nhưng giá trị vẫn còn trong lịch sử Git và đang chờ đổi key. Chỉ ghi tên biến.
2. **Không chạy Git.** Antigravity commit và push.
3. **Build gate trước khi bàn giao:** `dotnet build WardMate.sln -c Release -warnaserror -m:1`, phải đạt 0 lỗi và 0 cảnh báo.
4. **Script và log kiểm thử:**
   - Chủ dự án đã gỡ test project (30/09) và gỡ script chỉ phục vụ test (CLEANUP-01, 29/10).
   - `scripts/verify-calendar-local.py` và `scripts/verify-staffing-local.py` là script mới: hỏi chủ dự án có giữ trong repo không trước khi Antigravity commit.
   - `logs/` không được commit.
5. **Migration cloud:** chạy script SQL idempotent trong `docs/sql/` trước, deploy image sau. Không insert `__EFMigrationsHistory` bằng tay.
6. **Nghiệp vụ không được đảo:**
   - `DOCUMENTS_READY` không phải phê duyệt.
   - `MANAGER` chỉ giám sát, không tự có quyền chuyển trạng thái thay cán bộ.
   - Deadline là 45 giờ làm việc, tạm dừng khi chờ bổ sung.

## 5. Việc mở thuộc phần của bạn (theo PROGRESS)

- **WF-CALENDAR-REVIEW-01:** lỗi 500 của `GET/POST /api/v1/staffing/leaves` chưa rõ nguyên nhân, chưa có trace. Bản sửa DI (WF-STAFFING-DI-FIX) có thể đã khắc phục; cần kiểm tra lại trên image `workflow-20261101-01`.
- **Lịch nghỉ:** chưa test bằng token thật trên Azure; chưa stress-test với số lượng hồ sơ lớn.
- **FE đã được hướng dẫn** báo lỗi lịch nghỉ trực tiếp cho bạn (mục 2.4 trong `docs/fe-handoff-2026-11-01.md`).
