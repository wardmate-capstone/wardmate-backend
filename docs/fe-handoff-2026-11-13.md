# Bàn giao Backend → Frontend: tìm kiếm và sắp xếp danh sách điều phối; trả lời yêu cầu Mobile (13/11/2026)

Người viết: Backend (Claude), 13/11/2026. Hợp đồng: `docs/api-guide.md` mục "A. Nhóm ManagerDispatchController", phần 1. Quy tắc: `docs/system-constraints.md` DISPATCH-SEARCH-01.

## 1. Danh sách điều phối: `search`, `sort`, `status` (đã deploy 13/11, Workflow `workflow-20261113-01`)

`GET /api/v1/manager/applications?page=1&pageSize=20&search=...&sort=createdAt_desc&status=UNDER_REVIEW`

Làm đúng như đề nghị của FE:

| Tham số | Giá trị | Ghi chú |
|---|---|---|
| `search` | ≤ 200 ký tự | Không phân biệt hoa thường và dấu (kể cả đ). Tìm trong mã hồ sơ, tên thủ tục, tên cán bộ phụ trách. Nhiều từ: mọi từ đều phải khớp (tối đa 10 từ). `%`, `_` được tìm đúng chữ |
| `sort` | `createdAt_asc` (mặc định) \| `createdAt_desc` | Phân biệt hoa thường. Luôn kèm `id` cùng chiều, phân trang ổn định |
| `status` | trạng thái Workflow | Không phân biệt hoa thường |

- Response giữ `{ items, page, pageSize, total }`; `total` là số sau khi lọc.
- Sai tham số: 400 `dispatch.invalid_query`.
- Không truyền tham số mới: kết quả như cũ, FE cũ không bị ảnh hưởng.
- FE thay `applicationsNewestFirst` và `searchApplications` bằng một lần gọi mỗi trang. Đã deploy: Workflow `workflow-20261113-01` (SQL `workflow-dispatch-search.sql` đã chạy: `unaccent` + index `ix_applications_ward_code_created_at_id`).

Kiểm tra sau deploy: chạy đúng 6 mục nghiệm thu trong đề nghị của FE.

## 2. Trả lời yêu cầu MOBILE-1112 (Android)

- **SQL (mục 2.2):** đã chạy cả 3 tệp trên Azure ngày 12/11 (DEPLOY-1112i). Bốn service đã deploy: IAM `iam-20261112-m1`, DocumentForm `documentform-20261112-m1`, AnalyticsSystem `analytics-20261112-m1`, AIOCR `aiocr-20261112-m1`. FE thử được thao tác ghi.
- **Gộp vào `main` (mục 2.1):** đã fast-forward `main` và `deploy` lên `kha` ngày 13/11 (gồm MOBILE-1112 và DISPATCH-SEARCH-01).
- **`MobileApp:*` (mục 2.3):** đã đặt trên AnalyticsSystem ngày 13/11: `DeepLinkScheme=wardmate`, `Maintenance=false`, `WebBaseUrl=https://wardmate-frontend.vercel.app` (theo `Email:FrontendBaseUrl` đang dùng, bỏ dấu / cuối). Android/iOS `MinVersion`/`LatestVersion`/`StoreUrl` để trống (`null`). `GET /api/v1/system/app-config` đã kiểm: 200, `deepLinkScheme: "wardmate"`, `webBaseUrl` đúng, `pushEnabled: false`.
- **Push (mục 2.4) và package name (mục 3.1):** chủ dự án chốt package name và tạo dự án Firebase. BE chỉ bật `Push:Enabled` sau khi có bản build Android chứa `google-services.json`.
