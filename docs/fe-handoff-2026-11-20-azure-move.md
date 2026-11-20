# Bàn giao FE — Backend chuyển sang Azure mới (20/11/2026)

## 1. Việc FE phải làm ngay

Đổi **mọi** biến địa chỉ API trên Vercel (và `.env.local` khi chạy máy) sang Gateway mới, rồi deploy lại FE:

```
VITE_API_BASE_URL=https://wardmate-gateway.bravetree-f6485744.japaneast.azurecontainerapps.io
VITE_CATALOG_API_URL=https://wardmate-gateway.bravetree-f6485744.japaneast.azurecontainerapps.io
VITE_WORKFLOW_API_URL=https://wardmate-gateway.bravetree-f6485744.japaneast.azurecontainerapps.io
VITE_DOCUMENT_FORM_API_URL=https://wardmate-gateway.bravetree-f6485744.japaneast.azurecontainerapps.io
VITE_ANALYTICS_API_URL=https://wardmate-gateway.bravetree-f6485744.japaneast.azurecontainerapps.io
```

Địa chỉ cũ `…blackmeadow-a2f12767…` **không còn hoạt động** (subscription cũ hết tín dụng, bị vô hiệu hóa). SignalR thông báo dùng cùng Gateway mới (`/hubs/notifications`).

## 2. Thay đổi người dùng sẽ thấy

- **Phải đăng nhập lại:** khóa ký JWT đã tạo mới, mọi token cũ bị từ chối (401). Tài khoản, mật khẩu, hồ sơ, thủ tục trong database **không đổi**.
- **Tệp cũ bị mất:** kho blob cũ không lấy lại được. Các đường dẫn cũ tới PDF gốc thủ tục, DOCX biểu mẫu, tệp đơn/giấy tờ đã nộp, đơn nghỉ sẽ báo không tìm thấy. Ảnh đại diện lưu trong database nên vẫn còn.
- **Lần gọi đầu sau khi để lâu có thể chậm vài giây:** các service tự tắt khi không có người dùng (trừ Analytics).

## 3. Dữ liệu cần tạo lại (người quản lý thủ tục / quản trị)

| Nội dung | Cách làm |
|---|---|
| PDF gốc thủ tục | Mở thủ tục → gắn lại PDF nguồn (`POST /api/v1/procedure-manager/procedures/{id}/source`) hoặc tải PDF tạo bản nháp mới |
| DOCX biểu mẫu | Quản lý biểu mẫu → tải lại tệp DOCX cho từng mẫu |
| Mẫu đơn nghỉ của phường | Tải lại mẫu đơn nghỉ |
| Hồ sơ thử nghiệm có tệp | Tạo lại khi kiểm thử |

## 4. Kiểm tra nhanh (đã chạy 20/11, không token)

| Gọi qua Gateway mới | Kết quả |
|---|---|
| `GET /api/v1/procedures` | 200 |
| `GET /api/v1/system/app-config` | 200 |
| `GET /api/v1/wards` | 200 |
| `GET /api/v1/citizen/submissions`, `/api/v1/ai/chat/sessions`, `/api/v1/manager/applications`, `/api/v1/users/me` | 401 (đúng, cần đăng nhập) |

Báo lỗi cho BE kèm: route, mã HTTP, `code`, `traceId`, thời điểm gọi.
