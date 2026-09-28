# Hướng dẫn triển khai WardMate Backend lên Azure Portal

Tài liệu này cung cấp hướng dẫn chi tiết từng bước (Step-by-step) trên giao diện **Azure Portal** (GUI) để triển khai toàn bộ hệ thống backend WardMate (.NET 8 Microservices + PostgreSQL + YARP Gateway). 

Mục tiêu: Nhánh `deploy` trên GitHub tự động build và cập nhật API trên Azure để đội ngũ Frontend (FE) có thể gọi trực tiếp API thật mà không cần chạy code hay Docker ở máy local.

---

## 1. Mô hình kiến trúc trên Azure

```
+-----------------------------------------------------------------------------------+
| Azure Resource Group: rg-wardmate-prod (Location: Southeast Asia)                |
|                                                                                   |
|  +-----------------------------------------------------------------------------+  |
|  | Azure Container Apps Environment (cae-wardmate-prod)                       |  |
|  |                                                                             |  |
|  |  +------------------------------------+                                     |  |
|  |  | wardmate-gateway (YARP Gateway)    | <=== Ingress: External (Port 8080)  |  |
|  |  | Public FQDN (HTTPS URL cho FE)     |      (Nhận request từ Frontend)     |  |
|  |  +-----------------+------------------+                                     |  |
|  |                    | (Mạng nội bộ DNS)                                      |  |
|  |                    v                                                        |  |
|  |  +------------------------------------+                                     |  |
|  |  | wardmate-iam (IAM Service)         | <=== Ingress: Internal (Port 8080)  |  |
|  |  | http://wardmate-iam                |      (Chỉ Gateway truy cập được)    |  |
|  |  +-----------------+------------------+                                     |  |
|  +--------------------|--------------------------------------------------------+  |
|                       | (Kết nối SSL Port 5432)                                   |
|                       v                                                           |
|  +-----------------------------------------------------------------------------+  |
|  | Azure Database for PostgreSQL Flexible Server (ps-wardmate-db)              |  |
|  | Database: wardmate_iam_db                                                   |  |
|  +-----------------------------------------------------------------------------+  |
|                                                                                   |
|  +-----------------------------------------------------------------------------+  |
|  | Azure Container Registry (acrwardmate.azurecr.io)                           |  |
|  | Chứa Docker Images: wardmate-gateway, wardmate-iam                          |  |
|  +-----------------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------------+
```

---

## 2. Chuẩn bị trước khi thao tác trên Azure Portal

1. Tài khoản Azure đã kích hoạt (Free Trial, Azure for Students, hoặc Pay-As-You-Go).
2. Repository GitHub `wardmate-backend` đã có:
   - Nhánh `deploy` chứa code mới nhất.
   - Dockerfile cho Gateway: `src/Gateways/WardMate.YarpGateway/Dockerfile`.
   - Dockerfile cho IAM: `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Dockerfile`.
   - File `.dockerignore` tại thư mục gốc.

---

## 3. Các bước thực hiện chi tiết trên Azure Portal (GUI)

### BƯỚC 1: Tạo Resource Group
1. Truy cập [Azure Portal](https://portal.azure.com/).
2. Trên thanh tìm kiếm trên cùng, gõ **Resource groups** và chọn dịch vụ.
3. Nhấn **+ Create**.
4. Điền thông tin:
   - **Subscription**: Chọn gói subscription của bạn.
   - **Resource group**: `rg-wardmate-prod`
   - **Region**: `Southeast Asia` (Singapore - độ trễ thấp nhất về Việt Nam).
5. Nhấn **Review + create** -> **Create**.

---

### BƯỚC 2: Tạo Azure Container Registry (ACR)
1. Trên thanh tìm kiếm, gõ **Container registries** -> chọn dịch vụ.
2. Nhấn **+ Create**.
3. Cấu hình:
   - **Resource group**: `rg-wardmate-prod`
   - **Registry name**: Đặt tên duy nhất viết liền không dấu, vd: `acrwardmate` (hoặc `acrwardmate<mssv>`).
   - **Location**: `Southeast Asia`
   - **SKU**: `Basic` (tiết kiệm chi phí nhất).
4. Nhấn **Review + create** -> **Create**.
5. Sau khi tạo xong, nhấn **Go to resource**:
   - Ở menu bên trái, tìm mục **Settings** -> **Access keys**.
   - Bật công tắc **Admin user** thành **Enabled**.
   - Lưu lại **Login server** (vd: `acrwardmate.azurecr.io`), **Username**, và **Password** (sẽ dùng để kết nối container apps).

---

### BƯỚC 3: Tạo Azure Database for PostgreSQL Flexible Server
1. Trên thanh tìm kiếm, gõ **Azure Database for PostgreSQL flexible servers** -> chọn dịch vụ.
2. Nhấn **+ Create** -> chọn **Flexible Server**.
3. Cấu hình tab **Basics**:
   - **Resource group**: `rg-wardmate-prod`
   - **Server name**: `ps-wardmate-db` (hoặc tên duy nhất kèm số)
   - **Region**: `Southeast Asia`
   - **PostgreSQL version**: `16`
   - **Workload type**: Chọn `Development` (hoặc `For small development projects`).
   - **Compute + storage**: Nhấn **Configure server**:
     - Compute tier: Chọn `Burstable`.
     - Compute size: Chọn `Standard_B1ms` (1 vCore, 2 GiB RAM - chi phí rẻ nhất, đủ cho bài toán đồ án).
     - Storage size: 32 GiB.
     - Nhấn **Save**.
   - **Authentication**: Chọn `PostgreSQL authentication only`.
   - **Admin username**: `wardmateadmin`
   - **Password**: Nhập mật khẩu an toàn (ví dụ: `WardMatePass2026!@#`). *Lưu ý: Không dùng ký tự `@` trong username để tránh nhầm lẫn format connection string.*
4. Chuyển sang tab **Networking**:
   - **Connectivity method**: Chọn `Public access (allowed IP addresses)`.
   - Tích chọn: **Allow public access from any Azure service within Azure to this server** *(Quan trọng: Bắt buộc chọn để Container Apps có thể kết nối vào Database)*.
   - Nhấn **+ Add current client IP address** nếu muốn dùng pgAdmin hoặc DBeaver ở máy local kết nối trực tiếp vào database này để xem dữ liệu.
5. Nhấn **Review + create** -> **Create** (quá trình tạo mất khoảng 3-5 phút).
6. Sau khi tạo xong:
   - Vào tài nguyên PostgreSQL vừa tạo.
   - Ở menu bên trái, chọn **Settings** -> **Databases**.
   - Nhấn **+ Add** để tạo database:
     - **Database name**: `wardmate_iam_db`
     - **Charset**: `UTF8`
     - **Collation**: `en_US.utf8`
     - Nhấn **Save**.

---

### BƯỚC 4: Tạo Azure Container Apps Environment
1. Trên thanh tìm kiếm, gõ **Container App Environments** -> chọn dịch vụ.
2. Nhấn **+ Create**.
3. Cấu hình:
   - **Resource group**: `rg-wardmate-prod`
   - **Environment name**: `cae-wardmate-prod`
   - **Region**: `Southeast Asia`
   - **Zone redundancy**: `Disabled` (để tối ưu chi phí).
4. Nhấn **Review + create** -> **Create**.

---

### BƯỚC 5: Tạo Container App cho IAM Service (`wardmate-iam`)
1. Trên thanh tìm kiếm, gõ **Container Apps** -> nhấn **+ Create**.
2. Tab **Basics**:
   - **Resource group**: `rg-wardmate-prod`
   - **Container App name**: `wardmate-iam`
   - **Container Apps Environment**: Chọn `cae-wardmate-prod`.
3. Tab **Container**:
   - Bỏ tích "Use quickstart image".
   - **Name**: `wardmate-iam`
   - **Image source**: Chọn `Azure Container Registry`.
   - **Registry**: Chọn registry `acrwardmate`.
   - **Image**: Chọn `wardmate-iam` (nếu chưa push image lên ACR lần đầu, bạn có thể tạm chọn quickstart image, sau đó cấu hình GitHub Actions Continuous Deployment ở bước 7 để Azure tự động build từ nhánh `deploy`).
   - **CPU and Memory**: `0.25 vCPU, 0.5 GiB` (mức tiêu thụ tài nguyên tối thiểu).
4. Tab **Ingress**:
   - **Ingress**: Bật **Enabled**.
   - **Ingress traffic**: Chọn **Limited to Container Apps Environment** *(Internal Ingress - Rất quan trọng: IAM chỉ chạy nội bộ, không mở public ra ngoài)*.
   - **Target port**: `8080`
   - **Transport**: `Auto` (hoặc `HTTP/1.1`).
5. Nhấn **Review + create** -> **Create**.
6. Cấu hình **Environment Variables** cho `wardmate-iam`:
   - Vào Container App `wardmate-iam` vừa tạo.
   - Menu bên trái -> **Application** -> **Containers** -> Nhấn **Edit and deploy** (tạo revision mới).
   - Chọn container `wardmate-iam` -> tab **Environment variables**:
     - Thêm biến `ConnectionStrings__Database`:
       `Host=ps-wardmate-db.postgres.database.azure.com;Port=5432;Database=wardmate_iam_db;Username=wardmateadmin;Password=<Mật_khẩu_PostgreSQL>;Ssl Mode=Require;Trust Server Certificate=true;`
     - Thêm biến `Jwt__Key`: Một chuỗi ngẫu nhiên tối thiểu 32 ký tự (ví dụ: `WardMateSecretKey_2026_Secure_Token_Production_Key!@#$`).
     - Thêm biến `Jwt__Issuer`: `WardMate.IAM`
     - Thêm biến `Jwt__Audience`: `WardMate.Client`
     - Thêm biến `Database__AutoMigrate`: `true` *(IAM sẽ tự động chạy EF Core migration InitialIam và tạo 7 bảng + seed 5 roles ngay khi khởi động!)*
   - Nhấn **Save** -> **Create**.

---

### BƯỚC 6: Tạo Container App cho YARP Gateway (`wardmate-gateway`)
1. Menu **Container Apps** -> nhấn **+ Create**.
2. Tab **Basics**:
   - **Resource group**: `rg-wardmate-prod`
   - **Container App name**: `wardmate-gateway`
   - **Container Apps Environment**: Chọn `cae-wardmate-prod`.
3. Tab **Container**:
   - **CPU and Memory**: `0.25 vCPU, 0.5 GiB`.
4. Tab **Ingress**:
   - **Ingress**: Bật **Enabled**.
   - **Ingress traffic**: Chọn **Accepting traffic from anywhere** *(External Ingress - Nhận request công khai từ Frontend)*.
   - **Target port**: `8080`
   - **Transport**: `Auto`.
5. Nhấn **Review + create** -> **Create**.
6. Cấu hình **Environment Variables** cho `wardmate-gateway`:
   - Vào `wardmate-gateway` -> **Containers** -> **Edit and deploy**.
   - Tab **Environment variables**:
     - Thêm biến chuyển tiếp nội bộ đến IAM Service:
       - **Name**: `ReverseProxy__Clusters__iam-cluster__Destinations__iam-api__Address`
       - **Value**: `http://wardmate-iam` *(Tên DNS nội bộ của container IAM trong cùng Environment)*.
   - Nhấn **Save** -> **Create**.
7. Lấy URL cho Frontend:
   - Vào trang **Overview** của `wardmate-gateway`.
   - Tìm mục **Application Url**. Đây là URL HTTPS chính thức cung cấp cho FE (dạng `https://wardmate-gateway.xxx.southeastasia.azurecontainerapps.io`).

---

### BƯỚC 7: Tích hợp Continuous Deployment từ GitHub nhánh `deploy`

Để bất cứ khi nào code được merge vào nhánh `deploy`, Azure tự động build Dockerfile và deploy phiên bản mới:

1. Vào Container App `wardmate-iam`:
   - Ở menu bên trái, mục **Deployment**, chọn **Continuous deployment**.
   - Nhấn **Sign in with GitHub** và ủy quyền cho tài khoản GitHub của bạn.
   - **Organization**: `wardmate-capstone`
   - **Repository**: `wardmate-backend`
   - **Branch**: `deploy`
   - **Build type**: Chọn `Dockerfile`.
   - **Dockerfile path**: `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Dockerfile`
   - **Context path**: `.` *(Dấu chấm đại diện cho thư mục gốc của repo)*.
   - Nhấn **Save**. Azure Portal sẽ tự động tạo một GitHub Actions workflow commit thẳng vào repo của bạn!
2. Thực hiện tương tự cho Container App `wardmate-gateway`:
   - Vào `wardmate-gateway` -> **Continuous deployment**.
   - **Repository**: `wardmate-backend`, **Branch**: `deploy`.
   - **Dockerfile path**: `src/Gateways/WardMate.YarpGateway/Dockerfile`.
   - **Context path**: `.`.
   - Nhấn **Save**.

---

## 4. Hướng dẫn kiểm thử nghiệm thu sau khi deploy

Khi quá trình deploy hoàn tất, đội Frontend hoặc QA có thể kiểm tra trực tiếp qua HTTPS URL của Gateway:

1. **Kiểm tra Liveness của Gateway**:
   ```
   GET https://<APPLICATION_URL_GATEWAY>/health
   ```
   *Kết quả mong đợi*: HTTP 200 `Healthy`.

2. **Kiểm tra Liveness của IAM qua Gateway**:
   ```
   GET https://<APPLICATION_URL_GATEWAY>/api/iam/health
   ```
   *Kết quả mong đợi*: HTTP 200 `Healthy`.

3. **Đăng ký tài khoản công dân mẫu**:
   ```http
   POST https://<APPLICATION_URL_GATEWAY>/api/v1/auth/register
   Content-Type: application/json

   {
     "username": "citizentest",
     "email": "citizen@test.com",
     "password": "Password123456!",
     "fullName": "Nguyen Van A"
   }
   ```
   *Kết quả mong đợi*: HTTP 201 Created kèm thông tin User Profile.

4. **Đăng nhập lấy Access Token & Refresh Token**:
   ```http
   POST https://<APPLICATION_URL_GATEWAY>/api/v1/auth/login
   Content-Type: application/json

   {
     "usernameOrEmail": "citizentest",
     "password": "Password123456!"
   }
   ```
   *Kết quả mong đợi*: HTTP 200 OK với `accessToken` và `refreshToken`.

---

## 5. Dành cho Agent / CodeX: Prompt mẫu hỗ trợ duy trì và mở rộng

Khi cần yêu cầu AI agent (CodeX/Antigravity) thực hiện các bước tiếp theo, hãy sử dụng cấu trúc prompt mẫu sau:

```text
Tôi đang phát triển hệ thống backend WardMate (.NET 8 Microservices).
Hãy tuân thủ nghiêm ngặt quy trình trong file AGENTS.md và PROGRESS.md:
1. Luôn checkout nhánh kha-feat-<tên-tính-năng> từ kha.
2. Kiểm tra chất lượng: Release build 0 error 0 warning, toàn bộ unit và integration tests đều pass.
3. Commit theo độ hạt (granularity): mỗi file tạo mới 1 commit riêng; mỗi cập nhật chức năng 1 commit riêng.
4. Trình tự merge bắt buộc: feature -> kha (push kha) -> main (push main) -> deploy (push deploy).
5. Không commit secret, mật khẩu hay file môi trường thực tế.
6. Cập nhật đầy đủ file PROGRESS.md sau khi hoàn thành.
```
