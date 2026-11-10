# Bàn giao Backend → Frontend: sửa luồng thông báo và trả lại hồ sơ (10/11/2026)

Người viết: Backend (Claude), 10/11/2026. Tiếp theo `docs/fe-handoff-2026-11-10.md` (BE-H và A1–B4).

Hợp đồng chi tiết: `docs/api-guide.md` (mục API trả lại hồ sơ của cán bộ, Manager gỡ thay, `PUT .../assignee`). Quy tắc: `docs/system-constraints.md` mục **NOTIFY-COMMENT-01**, **WF-ASSIGN-REL-01**, **NOTIFY-RESUBMIT-01**.

## 0. Trạng thái

| Thay đổi | Service | Trên Azure? |
|---|---|---|
| 1. Không báo từng nhận xét cho người dân | Analytics | **Có** (`analytics-20261110-02`) |
| 2. Trả lại / gỡ cán bộ khỏi hồ sơ chờ bổ sung | Workflow (+ Analytics) | **Có** (`workflow-20261110-02`) |
| 3. Báo Manager khi hồ sơ đã gỡ cán bộ được nộp lại | Workflow + Analytics | **Có** |

Không có SQL hay đổi DTO bắt buộc. BE chưa thử các luồng này trên Azure bằng dữ liệu thử — nhờ FE test theo mục 4.

## 1. Người dân không còn nhận thông báo cho từng nhận xét

**Trước:** mỗi lần cán bộ thêm nhận xét khi hồ sơ đang kiểm tra, người dân nhận ngay "Hồ sơ có nhận xét mới" (kèm email nếu bật), kể cả nhận xét cán bộ rút lại sau đó (người dân không được xem).

**Nay:** nhận xét là việc nội bộ của cán bộ trong lúc kiểm tra. Người dân chỉ được báo **một lần** khi cán bộ bấm "Yêu cầu bổ sung" ("Hồ sơ yêu cầu bổ sung").

**FE cần làm:**

- Không dựa vào thông báo loại `INLINE_COMMENT_ADDED` nữa (không còn tạo mới; thông báo cũ vẫn còn trong danh sách).
- Màn hồ sơ của người dân chỉ hiện nhận xét khi hồ sơ ở `NEED_REVISION` (đúng như hiện tại nếu đang lọc theo trạng thái).

## 2. Trả lại hồ sơ khi đang chờ bổ sung

**Vấn đề cũ:** hồ sơ bị yêu cầu bổ sung vẫn gắn cán bộ cũ. Người dân nộp lại thì chỉ cán bộ đó nhận được; cán bộ vắng thì hồ sơ kẹt.

**Nay:**

- **Cán bộ bận** tự trả hồ sơ đang `NEED_REVISION`:

  ```
  POST /api/v1/officer/applications/{id}/release
  Body: { "reason": "Nghỉ phép 2 tuần", "expectedAssignmentVersion": 3 }
  ```

  (API cũ, trước chỉ dùng cho `UNDER_REVIEW`.)

- **Manager gỡ thay** cán bộ vắng (API mới):

  ```
  POST /api/v1/manager/applications/{id}/release
  Quyền: workflow.assign.manage (Manager cùng phường)
  Body: { "reason": "Cán bộ nghỉ dài ngày", "expectedAssignmentVersion": 3 }
  ```

  Chỉ dùng cho hồ sơ `NEED_REVISION` đang có cán bộ.

- Sau khi gỡ: trạng thái **giữ `NEED_REVISION`**, `assignedOfficerId` = null. Không ai nhận thông báo lúc gỡ. Người dân nộp lại → hồ sơ `SUBMITTED` chưa có cán bộ, vào hàng chờ chung; cả phường được báo, ai bấm nhận (`POST /officer/applications/{id}/assign`) trước thì thụ lý.
- **Không gỡ:** cán bộ cũ vẫn giữ hồ sơ, khi người dân nộp lại cán bộ đó nhận "Có hồ sơ cần tiếp nhận" và bấm nhận như trước.
- `PUT /api/v1/manager/applications/{id}/assignee` nay chuyển được hồ sơ đã nộp lại (`SUBMITTED`, vẫn gắn cán bộ cũ) sang cán bộ khác; trạng thái giữ `SUBMITTED`, cán bộ mới bấm nhận. Trước đây trả 409.

**Kết quả:** 200 `ReviewResultDto` (hồ sơ mới, `assignedOfficerId` null, `assignmentVersion` tăng).

**Lỗi:**

| Mã | Khi nào | Xử lý |
|---|---|---|
| 400 `workflow.validation` | Lý do không đủ 5–500 ký tự | Sửa lý do |
| 403 `workflow.not_assignee` | Cán bộ không phải người đang thụ lý | Ẩn nút với cán bộ khác |
| 404 `workflow.not_found` | Hồ sơ ngoài phường | Tải lại danh sách |
| 409 `workflow.invalid_status` | Manager gỡ hồ sơ không ở `NEED_REVISION`; cán bộ trả hồ sơ không ở `UNDER_REVIEW`/`NEED_REVISION` | Tải lại hồ sơ |
| 409 `workflow.not_assigned` | Hồ sơ đã không còn cán bộ | Tải lại hồ sơ |
| 409 `workflow.assignment_conflict` | `expectedAssignmentVersion` cũ | Tải lại hồ sơ rồi thử lại |

**FE cần làm:**

- Màn chi tiết hồ sơ `NEED_REVISION`:
  - Cán bộ đang thụ lý: nút **"Trả lại hồ sơ"** (hộp nhập lý do) → API cán bộ.
  - Manager: nút **"Gỡ cán bộ"** (hộp nhập lý do) → API Manager. Chỉ hiện khi hồ sơ có `assignedOfficerId`.
- Gửi `expectedAssignmentVersion` lấy từ hồ sơ vừa tải.
- Hồ sơ `SUBMITTED` chưa có cán bộ hiện trong hàng chờ (bộ lọc `assignee=unassigned`) như hồ sơ nộp lần đầu.

## 3. Manager được báo khi hồ sơ đã gỡ cán bộ được nộp lại

Khi người dân nộp lại một hồ sơ đã bị gỡ cán bộ, ngoài cán bộ một cửa, **Manager của phường** nhận thông báo **"Hồ sơ nộp lại chưa có cán bộ thụ lý"** (in-app, email nếu bật) để theo dõi hoặc giao người. Nộp lần đầu hoặc nộp lại khi cán bộ cũ còn giữ: Manager không được báo.

**FE cần làm:** thông báo này có `type: "APPLICATION_STATUS_CHANGED"`, `actionUrl` = `/officer/applications/{id}/review`. Nếu màn Manager dùng đường dẫn khác, FE ánh xạ theo `applicationId` sang màn hồ sơ của Manager.

**Đã biết, không sửa (chủ dự án quyết định):** thông báo đơn nghỉ / đề nghị đổi ca gửi Manager chỉ hiện trong danh sách thông báo khi tải lại, không bật realtime và không có email.

## 4. Kiểm tra trên Azure

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Cán bộ thêm 2 nhận xét cho hồ sơ đang kiểm tra | Người dân **không** nhận thông báo |
| 2 | Cán bộ bấm "Yêu cầu bổ sung" | Người dân nhận 1 thông báo "Hồ sơ yêu cầu bổ sung" |
| 3 | Cán bộ thụ lý gọi API trả lại (hồ sơ `NEED_REVISION`) | 200; trạng thái vẫn `NEED_REVISION`; `assignedOfficerId` null; không ai nhận thông báo |
| 4 | Người dân sửa và nộp lại hồ sơ ở bước 3 | Hồ sơ `SUBMITTED` chưa có cán bộ; cán bộ phường và Manager nhận thông báo |
| 5 | Cán bộ khác bấm nhận | 200, hồ sơ `UNDER_REVIEW` với cán bộ mới |
| 6 | Hồ sơ `NEED_REVISION` khác: Manager gọi API gỡ thay | 200, như bước 3 |
| 7 | Manager gọi API gỡ thay cho hồ sơ `UNDER_REVIEW` | 409 `workflow.invalid_status` |
| 8 | Hồ sơ nộp lại khi **không** gỡ cán bộ | Chỉ cán bộ cũ nhận "Có hồ sơ cần tiếp nhận"; Manager không nhận |
| 9 | Manager `PUT .../assignee` cho hồ sơ ở bước 8 sang cán bộ khác | 200; trạng thái `SUBMITTED`; cán bộ mới bấm nhận được |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi. Không gửi mật khẩu/token.
