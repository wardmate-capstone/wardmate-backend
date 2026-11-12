# Bàn giao Backend → Frontend/Mobile: ảnh đại diện, ảnh điện thoại, thông báo đẩy, chatbot hỏi lại (12/11/2026)

Người viết: Backend (Claude), 12/11/2026. Hợp đồng chi tiết: `docs/api-guide.md` mục "Mobile, ảnh đại diện và chatbot hỏi lại — 12/11/2026 (MOBILE-1112)". Quy tắc: `docs/system-constraints.md` PROFILE-AVATAR-01, DOC-HEIC-01, PUSH-01, MOBILE-01, CHAT-QUALITY-01, CHAT-FEEDBACK-01.

## 0. Trạng thái

**Chưa deploy.** Cần chạy 3 SQL (IAM, AnalyticsSystem, AIOCR) rồi deploy IAM, DocumentForm, AnalyticsSystem, AIOCR. Thông báo đẩy tắt cho tới khi có dự án Firebase. BE báo tag image sau khi deploy.

## 1. Ảnh đại diện (Web và Mobile)

| Việc | API |
|---|---|
| Lấy URL ảnh | `GET /api/v1/users/me` → `avatarUrl` (null = chưa có ảnh, hiện ảnh mặc định) |
| Đổi ảnh | `PUT /api/v1/users/me/avatar`, multipart trường `file` (JPEG/PNG/WebP/HEIC, ≤ 10 MB) → 200 `{avatarUrl, updatedAt}` |
| Xóa ảnh | `DELETE /api/v1/users/me/avatar` → 204 |
| Hiện ảnh | `GET {gateway}{avatarUrl}` kèm Bearer token; 404 → ảnh mặc định |

- Web: `<img src>` không gửi token → `fetch` có header Authorization, rồi `URL.createObjectURL(blob)`.
- Mobile: truyền header Authorization vào widget ảnh. Server tự cắt vuông, không cần màn cắt ảnh (nếu có thì càng tốt).
- Sau khi đổi ảnh, dùng `avatarUrl` mới trả về (tham số `v` đổi nên ảnh cũ trong cache không bị dùng lại).
- Lỗi hiển thị: `iam.avatar_too_large`, `iam.image_unsupported`, `iam.image_unreadable` — dùng `title` của ProblemDetails.

## 2. Ảnh chụp từ điện thoại

- Ô tải giấy tờ: thêm `.heic, .heif, .webp` vào `accept`. Server trả về tệp `.jpg` (tên và `contentType` đổi).
- Đọc CCCD bằng AI: nhận được ảnh HEIC/WebP.
- Khuyến nghị app nén ảnh (cạnh dài ~2000–3000 px) trước khi gửi để tải nhanh trên 4G.

## 3. Thông báo đẩy (Mobile)

1. Sau đăng nhập và khi Firebase đổi token: `PUT /api/v1/system/push-devices` `{token, platform: "ANDROID"|"IOS", appVersion}`.
2. Khi đăng xuất, **trước** khi xóa token đăng nhập: `POST /api/v1/system/push-devices/unregister` `{token}`.
3. Nhận tin: `data.actionUrl` để mở màn hình, `data.notificationId` để gọi `MarkRead` qua hub. Android tạo kênh `wardmate_default`.
4. `GET /api/v1/system/app-config` → `pushEnabled` cho biết server đã bật đẩy chưa (đang `false`).

## 4. Cấu hình app

`GET /api/v1/system/app-config` (không cần đăng nhập) khi mở app: so phiên bản với `minVersion` (bắt cập nhật) và `latestVersion` (gợi ý), hiện `maintenanceMessage` khi `maintenance = true`. Deep link: FE đặt `/.well-known/assetlinks.json` và `/.well-known/apple-app-site-association` trên tên miền Web để link trong thông báo/QR mở thẳng app.

## 5. Chatbot hỏi lại, gợi ý và đánh giá (Web và Mobile)

Response `POST /api/v1/ai/chat/completions` có thêm:

- `needsClarification: true` → câu trả lời đang hỏi lại; hiện `suggestedQuestions` thành nút chọn nhanh.
- `suggestedQuestions` (≤ 4) khi đã trả lời → nút "Câu hỏi tiếp theo". Bấm nút = gửi đúng chữ đó làm `message`, giữ `conversationId`.
- `messageId` → dưới mỗi câu trả lời hiện 👍/👎. 👎 mở lựa chọn lý do: Sai thông tin (`WRONG_INFO`), Không đúng điều tôi hỏi (`NOT_WHAT_I_ASKED`), Khó hiểu (`HARD_TO_UNDERSTAND`), Thiếu thông tin (`MISSING_INFO`), Khác (`OTHER`) + ô góp ý. Gọi `POST /api/v1/ai/chat/messages/{messageId}/feedback` `{helpful, reason, comment}` → 204.
- Lịch sử tin nhắn có `needsClarification`, `suggestedQuestions`, `feedbackHelpful` để hiện lại trạng thái.
- Trang quản lý kho kiến thức (PROCEDURE_MANAGER): thêm tab "Đánh giá chatbot" gọi `GET /api/v1/ai/knowledge/chat-feedback?helpful=false` (câu hỏi, câu trả lời, lý do, góp ý).

## 6. Kiểm tra sau deploy

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Người dân tải ảnh HEIC (iPhone) làm ảnh đại diện | 200; `GET /users/me` có `avatarUrl`; ảnh vuông, đúng chiều |
| 2 | Người dân A mở `avatarUrl` của người dân B | 404 |
| 3 | Người dân mở ảnh cán bộ phụ trách hồ sơ | 200 |
| 4 | Xóa ảnh hai lần | 204 cả hai lần; `avatarUrl` null |
| 5 | Tải giấy tờ `.heic` | 201, `fileName` đuôi `.jpg`, xem trực tiếp được |
| 6 | `GET /system/app-config` không token | 200 |
| 7 | Đăng ký token đẩy rồi gỡ | 204 / 204 |
| 8 | Hỏi chatbot "làm giấy khai sinh" | Thường hỏi lại trường hợp (`needsClarification: true`) kèm lựa chọn |
| 9 | Hỏi tiếp "còn lệ phí thì sao" | Trả lời lệ phí đúng thủ tục đang nói |
| 10 | Bấm 👎 với lý do | 204; quản lý thấy trong `chat-feedback` |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi.
