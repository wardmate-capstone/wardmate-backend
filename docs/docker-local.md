# Chạy Local Dev bằng Docker Compose

Chạy từ thư mục gốc repository sau khi mở Docker Desktop (Linux containers):

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d --build
```

Compose build IAM/Gateway, khởi động PostgreSQL, IAM, Gateway và pgAdmin. IAM chờ PostgreSQL healthy, tự migrate khi khởi động. Gateway có thể trả 502 trong lúc IAM đang migrate; chờ log IAM báo đã lắng nghe.

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
docker compose --env-file docker/.env -f docker/docker-compose.yml logs -f iam-api gateway
```

- Swagger: http://localhost:5001/swagger
- API qua Gateway: http://localhost:5000/api/v1/...
- pgAdmin: http://localhost:5050 — email/mật khẩu trong docker/.env.
- PostgreSQL từ host: localhost:5433. Từ pgAdmin/container: host iam-db, port 5432, database wardmate_iam_db, user wardmate.

File docker/.env cần IAM_DB_PASSWORD, JWT_KEY (ít nhất 32 byte), PGADMIN_DEFAULT_EMAIL và PGADMIN_DEFAULT_PASSWORD. Sao chép .env.example và thay giá trị khi cài trên máy mới. Không commit .env; Git và Docker build đã bỏ qua file này. Khi volume database đã tồn tại, giữ mật khẩu hiện tại; đổi biến môi trường không tự đổi mật khẩu PostgreSQL.

Các cổng bind vào localhost; cấu hình Development chỉ dùng local. Các microservice ngoài IAM chưa được chạy bởi Compose này.

Dừng dịch vụ, giữ dữ liệu:

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml down
```

Không thêm `-v` nếu cần giữ dữ liệu PostgreSQL. Nếu cổng 5000/5001 đã được dotnet hoặc container thủ công sử dụng, dừng tiến trình/container đó trước khi chạy Compose.
