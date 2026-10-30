# Đặc tả cho BE: Ngày nghỉ của phường và ngày lễ toàn quốc dừng deadline 45 giờ

*Viết ngày 30/10/2026 (Asia/Saigon). Người viết: Claude. **Chưa triển khai.** Tài liệu này là yêu cầu để BE phụ trách lịch làm/lịch nghỉ (STAFFING-01) làm tiếp.*

## 1. Mục tiêu

Deadline tiền kiểm 45 giờ làm việc (thứ Hai–thứ Sáu 07:30–16:30 UTC+07) hiện **không trừ ngày lễ**: `ReviewDeadline:Holidays` trong `appsettings.json` đang rỗng. Chủ dự án quyết định không dùng danh sách cố định mà cho người dùng khai báo:

- **Ngày lễ toàn quốc** (Tết, 30/4, 1/5, 2/9, nghỉ bù...): **IT_ADMIN** khai báo một lần, áp dụng cho mọi phường.
- **Ngày nghỉ của phường** (quầy đóng, mất điện, nghỉ bù riêng...): **MANAGER** khai báo cho phường của mình.

Ngày đã khai báo không tính là giờ làm việc. Hồ sơ đang trong tiền kiểm được **lùi hạn** đúng bằng số giờ làm việc bị mất.

## 2. Hiện trạng code cần biết

| Chỗ | Nội dung |
|---|---|
| `ApplicationWorkflow.Domain/ReviewDeadline.cs` | `ReviewDeadlinePolicy` (giờ làm, ngày làm, `Holidays`) và các hàm `AddWorkingTicks`, `WorkingTicks`, `Start`, `Pause`, `Resume`, `CancelPause`. Hàm `Working(d, p)` quyết định một ngày có phải ngày làm việc. |
| `Start` | Lúc nộp lần đầu, policy được **chụp vào hồ sơ** (`applications.deadline_policy_snapshot` jsonb) và tính `DueAtUtc`. Đổi cấu hình sau đó không ảnh hưởng hồ sơ cũ. **Giữ nguyên cơ chế snapshot này, không sửa snapshot cũ.** |
| `Resume` | Khi công dân nộp lại sau NEED_REVISION: `DueAtUtc += WorkingTicks(pausedAt, now)`. |
| Chỗ gọi | `Application/WorkflowRequests.cs:116` (`Start`, trong `store.WithLock` async); `Application/ReviewHandlers.cs:115` (`Resume`, trong `store.Mutate` **sync**); `ReviewHandlers.cs:89` (`Pause`); `CounterReceiptRequests.cs:44` (`CancelPause`). |
| Staffing | `Infrastructure/StaffingService.cs`, `API/Controllers/StaffingController.cs` route `api/v1/staffing`, khóa phường `StaffingLocks.Ward` (`pg_advisory_xact_lock(hashtextextended(ward, 301))`). Gateway đã có route `/api/v1/staffing/{**catch-all}` (cả `appsettings.json` và `appsettings.Production.json`): **không cần thêm route**. |
| Quyền | Policy trong `API/WorkflowSecurity.cs`. `workflow.staffing.manage` (MANAGER) đã có trong IAM (migration `20261029120000_ManagerStaffingPermissions`). IT_ADMIN **chưa có** quyền nào của Workflow. |
| Analytics | Đọc `DueAtUtc` trực tiếp từ Workflow qua `/internal/analytics/v1/deadlines` (ANA-DEADLINE-01). **Không cần sửa Analytics**: hạn mới tự được dùng. |

## 3. Dữ liệu

**Bảng mới `calendar_closures`** (DB Workflow, có migration EF + script `docs/sql/workflow-calendar-closures.sql` idempotent cho Supabase, theo mẫu `docs/sql/workflow-staffing-tables.sql`):

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | uuid PK | `pk_calendar_closures` |
| `ward_code` | varchar(50) NULL | **NULL = ngày lễ toàn quốc** |
| `closure_date` | date | Ngày theo giờ Việt Nam (UTC+07) |
| `reason` | varchar(500) NOT NULL | Bắt buộc, trim |
| `created_by`, `created_at_utc` | uuid, timestamptz | |
| `cancelled_by`, `cancelled_at_utc`, `cancellation_reason` | NULL | Hủy mềm, không xóa dòng |

Ràng buộc:
- Unique khi còn hiệu lực: `(ward_code, closure_date) WHERE cancelled_at_utc IS NULL AND ward_code IS NOT NULL` và `(closure_date) WHERE cancelled_at_utc IS NULL AND ward_code IS NULL`.
- Check: hủy thì phải đủ 3 cột hủy, chưa hủy thì cả 3 NULL.
- Index `(closure_date)` để tra theo khoảng ngày.

**Quyền IAM mới** `workflow.calendar.manage` gán cho **IT_ADMIN** (migration IAM + script `docs/sql/iam-calendar-permission.sql`, theo mẫu `docs/sql/iam-staffing-permissions.sql`: advisory lock, `setval` sequence, ghi `__EFMigrationsHistory`).

## 4. API (cùng route `api/v1/staffing`)

| Method & route | Ai | Mô tả |
|---|---|---|
| `GET /api/v1/staffing/closures?from=&to=` | MANAGER, FRONT_DESK_OFFICER (`workflow.staffing.read`); IT_ADMIN (`workflow.calendar.manage`) | Ngày lễ toàn quốc + ngày nghỉ của phường người gọi (IT_ADMIN chỉ thấy toàn quốc). `from`/`to` là ngày, tối đa 400 ngày. Có cả dòng đã hủy kèm cờ `cancelled`. |
| `POST /api/v1/staffing/closures` | MANAGER (`workflow.staffing.manage`) | Body `{ "date": "2026-11-20", "reason": "..." }`. Phường lấy từ access-context, **không nhận từ body**. |
| `POST /api/v1/staffing/closures/national` | IT_ADMIN (`workflow.calendar.manage`) | Body như trên, tạo dòng `ward_code = NULL`. |
| `POST /api/v1/staffing/closures/{id}/cancel` | MANAGER cho dòng của phường mình; IT_ADMIN cho dòng toàn quốc | Body `{ "reason": "..." }`. |

Lưu ý: controller hiện tại có `[Authorize(Policy = "workflow.staffing.read")]` ở cấp class, nên IT_ADMIN không vào được. Nên tạo **controller riêng** (ví dụ `CalendarClosuresController`) với policy từng action, hoặc thêm policy gộp `workflow.closure.read`.

Kết quả 200/201 trả dòng closure và **số hồ sơ đã lùi hạn** (`rescheduledApplications`), để Manager thấy tác động.

## 5. Quy tắc nghiệp vụ

1. **Không khai báo lùi ngày:** `date` phải ≥ hôm nay (UTC+07). Nếu là hôm nay thì chỉ phần giờ làm việc còn lại từ thời điểm khai báo được trừ. Không thể "xóa quá hạn" bằng cách khai báo ngày đã qua.
2. **Không khai báo quá xa:** `date` ≤ hôm nay + 366 ngày.
3. **Bỏ qua cuối tuần:** ngày không phải ngày làm việc (thứ Bảy, Chủ nhật) vẫn lưu được nhưng không đổi hạn; trả `rescheduledApplications = 0`.
4. **Hủy:** chỉ hủy được khi `closure_date` > hôm nay (trước khi ngày đó bắt đầu). Hủy thì tính lại hạn theo chiều ngược lại.
5. **Trùng:** cùng phường cùng ngày (hoặc toàn quốc cùng ngày) đang hiệu lực → 409. Ngày phường trùng với ngày lễ toàn quốc → cho lưu, nhưng không trừ hai lần (tập ngày nghỉ là hợp của hai loại).
6. **Hồ sơ đã quá hạn giữ nguyên quá hạn.**

## 6. Thuật toán

**Lịch hiệu lực của một hồ sơ** = policy trong snapshot (ngày làm, giờ làm, `Holidays` cũ) **cộng** các closure còn hiệu lực có `ward_code IS NULL` hoặc `ward_code = applications.ward_code`.

Sửa `ReviewDeadline`: thêm tham số tập ngày nghỉ (ví dụ `IReadOnlySet<DateOnly> closed`) cho `Working`, `AddWorkingTicks`, `WorkingTicks`, `Start`, `Resume`. **Không** đưa closures vào JSON snapshot (snapshot chỉ là policy gốc).

**Khi khai báo hoặc hủy một closure** (trong một transaction):
1. Khóa: dùng một advisory lock chung cho mọi thao tác closure (ví dụ `hashtextextended('calendar', 302)`); closure của phường thì lấy thêm `StaffingLocks.Ward`.
2. Chọn hồ sơ bị ảnh hưởng, khóa `FOR UPDATE ORDER BY id`: `deadline_status = 'KNOWN'`, `due_at_utc IS NOT NULL`, `documents_ready_at IS NULL`, status ∈ {SUBMITTED, UNDER_REVIEW, NEED_REVISION}, đúng phường (hoặc mọi phường nếu là ngày toàn quốc), và `due_at_utc` > mốc tính (xem bước 3).
3. Với mỗi hồ sơ, `C_old` = lịch trước thay đổi, `C_new` = lịch sau thay đổi:
   - **Đang chạy:** mốc = `now`. Nếu `due ≤ now` thì bỏ qua (đã quá hạn). `remaining = WorkingTicks(now, due, C_old)`; `due' = AddWorkingTicks(now, remaining, C_new)`.
   - **Đang tạm dừng** (`DeadlinePausedAtUtc = P`): mốc = `P`. Nếu `due ≤ P` thì bỏ qua (quá hạn trước khi dừng). `remaining = WorkingTicks(P, due, C_old)`; `due' = AddWorkingTicks(P, remaining, C_new)`. Khi nộp lại, `Resume` phải dùng **cùng lịch `C_new`** để cộng thời gian tạm dừng, nếu không sẽ lùi hạn hai lần.
4. Nếu `due'` khác `due`: cập nhật `DueAtUtc`, ghi một sự kiện vào `deadline_history` với action mới `CLOSURE` (khai báo) hoặc `CLOSURE_CANCEL` (hủy), actor là người khai báo, `DueBeforeUtc`/`DueAfterUtc` như sự kiện hiện có.
5. Đếm số hồ sơ đã đổi để trả về.

**Khi nộp hồ sơ (`Start`)** và **khi nộp lại (`Resume`)**: nạp closure hiệu lực của phường + toàn quốc từ ngày cần thiết trở đi (từ hôm nay cho `Start`, từ ngày `P` cho `Resume`) và truyền vào hàm. `Resume` đang chạy trong `store.Mutate` dạng sync: cần nạp closure **trước** khi gọi `Mutate` (ví dụ thêm method store đọc closure, lọc theo phường bên trong lambda), hoặc thêm overload `Mutate` async.

## 7. Giới hạn chấp nhận và cần ghi vào tài liệu

- **Tranh chấp hẹp:** nếu công dân nộp hồ sơ đúng lúc Manager đang khai báo ngày nghỉ, hồ sơ đó có thể không được lùi hạn (vì lúc khai báo hồ sơ còn DRAFT, lúc nộp chưa đọc thấy closure). Muốn tránh hoàn toàn thì `Start` phải lấy khóa phường trước khi khóa dòng hồ sơ; cẩn thận deadlock với code phân công đang dùng `StaffingLocks.Ward`. Nếu không xử lý thì ghi rõ thành giới hạn.
- Ngày lễ toàn quốc khai báo sẽ khóa và cập nhật hồ sơ của **mọi phường** trong một transaction; với dữ liệu đồ án là nhỏ, nhưng nên giới hạn và ghi log số dòng.
- Hạn tiền kiểm chỉ là mốc nội bộ, không phải hạn hành chính. Không gửi thông báo cho công dân khi hạn đổi (trừ khi chủ dự án yêu cầu sau).
- `ReviewDeadline:Holidays` trong cấu hình vẫn giữ để tương thích, nhưng hướng dẫn từ nay dùng API.

## 8. Lỗi trả về (ProblemDetails, `code`)

| Status | code | Khi nào |
|---|---|---|
| 400 | `staffing.invalid_date` | Ngày đã qua, quá 366 ngày, sai định dạng |
| 400 | `staffing.reason_required` | Thiếu lý do hoặc quá 500 ký tự |
| 403 | `staffing.forbidden` | Sai vai trò; Manager hủy dòng toàn quốc; IT_ADMIN hủy dòng của phường |
| 404 | `staffing.not_found` | Không có closure trong phạm vi |
| 409 | `staffing.closure_exists` | Đã có ngày nghỉ còn hiệu lực trùng |
| 409 | `staffing.closure_started` | Hủy khi ngày đó đã bắt đầu hoặc đã qua |
| 503 | `staffing.iam_unavailable` | Như các API staffing hiện có |

## 9. Việc phải làm (checklist)

1. Domain: entity `CalendarClosure`; sửa `ReviewDeadline` nhận tập ngày nghỉ; thêm hàm tính lại hạn (mục 6).
2. Infrastructure: `WorkflowDbContext` (DbSet + cấu hình snake_case, index, filter); migration Workflow; service khai báo/hủy/tính lại; method đọc closure cho store.
3. Application: contract input/DTO; `IStaffingService` (hoặc service mới); sửa chỗ gọi `Start` (`WorkflowRequests.cs:116`) và `Resume` (`ReviewHandlers.cs:115`).
4. API: controller + policy (`workflow.calendar.manage` cho IT_ADMIN, dùng lại `workflow.staffing.manage`/`read` cho phường).
5. IAM: migration seed `workflow.calendar.manage` cho IT_ADMIN.
6. SQL cho Supabase: `docs/sql/workflow-calendar-closures.sql`, `docs/sql/iam-calendar-permission.sql` (idempotent, preflight chọn đúng DB).
7. Tài liệu: `docs/api-guide.md` (4 API mới, giải thích tiếng Việt), `docs/system-constraints.md` (quy tắc mới, ví dụ `DEADLINE-CLOSURE-01`; cập nhật WF-05/ANA-DEADLINE-01 rằng ngày nghỉ được khai báo qua API), `PROGRESS.md`, `docs/CLAUDE_HANDOFF.md` (dòng "Deadline calendar: Holidays rỗng" chuyển thành đã có).
8. Build: `dotnet build WardMate.sln -c Release -warnaserror -m:1` phải 0 lỗi, 0 cảnh báo.

## 10. Kiểm tra nghiệm thu gợi ý

1. Nộp hồ sơ thứ Hai 08:00 → hạn ban đầu. Manager khai báo thứ Tư là ngày nghỉ → hạn lùi đúng 9 giờ làm việc (07:30–16:30), lịch sử có `CLOSURE`.
2. Hủy ngày nghỉ đó (trước thứ Tư) → hạn quay về giá trị ban đầu, lịch sử có `CLOSURE_CANCEL`.
3. Hồ sơ đang NEED_REVISION (tạm dừng) + khai báo ngày nghỉ nằm trong thời gian tạm dừng → sau khi công dân nộp lại, hạn chỉ lùi đúng thời gian tạm dừng tính theo ngày làm việc, **không lùi hai lần**.
4. Hồ sơ đã quá hạn → khai báo ngày nghỉ không đổi hạn.
5. Khai báo ngày hôm qua → 400. Hủy ngày hôm nay → 409.
6. IT_ADMIN khai báo ngày lễ toàn quốc → hồ sơ của 2 phường khác nhau đều lùi hạn. Manager phường A không hủy được dòng đó (403).
7. Manager phường A khai báo ngày nghỉ → hồ sơ phường B không đổi.
8. Ngày phường trùng ngày lễ toàn quốc → không lùi hạn hai lần.
9. Hồ sơ nộp **sau** khi đã có ngày nghỉ trong tương lai → hạn ban đầu đã trừ ngày đó.
