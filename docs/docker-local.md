> Cập nhật 2026-10-14: chủ dự án đã yêu cầu gỡ bộ test và script kiểm thử khỏi repository. Các kết quả kiểm thử bên dưới là lịch sử; không còn chạy được các script cũ. Dùng Swagger và `docs/api-guide.md` để kiểm tra thủ công.

# Chạy Local Dev bằng Docker Compose

Chạy từ thư mục gốc repository sau khi mở Docker Desktop (Linux containers):

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d --build
```

Compose hiện có IAM, ProcedureCatalog, ApplicationWorkflow, DocumentForm và Gateway cùng database tương ứng. IAM chờ PostgreSQL healthy, tự migrate khi khởi động. Gateway có thể trả 502 trong lúc các service đang migrate; chờ log báo đã lắng nghe.

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
docker compose --env-file docker/.env -f docker/docker-compose.yml logs -f iam-api gateway
```

- Swagger: http://localhost:5001/swagger
- API qua Gateway: http://localhost:5000/api/v1/...
- pgAdmin: http://localhost:5050 — email/mật khẩu trong docker/.env.
- PostgreSQL từ host: localhost:5433. Từ pgAdmin/container: host iam-db, port 5432, database wardmate_iam_db, user wardmate.

File docker/.env cần IAM_DB_PASSWORD, JWT_KEY (ít nhất 32 byte), PGADMIN_DEFAULT_EMAIL và PGADMIN_DEFAULT_PASSWORD. Sao chép .env.example và thay giá trị khi cài trên máy mới. Không commit .env; Git và Docker build đã bỏ qua file này. Khi volume database đã tồn tại, giữ mật khẩu hiện tại; đổi biến môi trường không tự đổi mật khẩu PostgreSQL.

Database IAM/ProcedureCatalog/Workflow bind vào localhost; một số cổng API và hạ tầng hiện bind mọi interface theo Compose. Cấu hình Development chỉ dùng local.

## Kiểm thử hồ sơ với IAM thật

Điền `JWT_KEY` chung của nhóm vào `docker/.env`, không đưa khóa vào Swagger hoặc chat.
IAM, Workflow và DocumentForm dùng cùng giá trị này. Giữ nguyên mật khẩu database đã có volume;
không đổi mật khẩu chỉ để chạy kiểm tra. Cần `WORKFLOW_DB_PASSWORD`, `PROCEDURE_DB_PASSWORD`
và Blob Storage khả dụng theo `AZURE_BLOB_CONNECTION_STRING`.

Khởi động các service cần cho luồng:

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d --build iam-api procedure-catalog application-workflow document-form gateway
```

Lệnh này chạy migration theo cấu hình Compose trên database local; kiểm tra cấu hình Blob trước khi upload,
vì nếu URL trỏ Azure thì file sẽ được ghi lên storage đó. Muốn cô lập hoàn toàn, dùng script smoke bên dưới.

Đăng nhập IAM để lấy token người dân có role REGISTERED_CITIZEN và các permission workflow cần thiết.
Không dùng token tự ký để coi là đã kiểm chứng IAM thật. Chọn wardCode tồn tại trong IAM và procedureId
từ ProcedureCatalog. Thực hiện luồng tại `docs/api-guide.md`, phần “Liên kết hồ sơ – đơn DOCX – giấy tờ”.
Workflow kiểm tra quyền hiện hành qua IAM access-context; token hợp lệ nhưng thiếu permission vẫn bị từ chối.

Kiểm tra cô lập, không dùng khóa hoặc dữ liệu thật:

Bộ kiểm thử tự động trong mục này đã được gỡ theo yêu cầu chủ dự án.

Cần hai image application-link-local đã build, Docker đang chạy và các cổng 10000,15439,15032–15034 trống.
Script tự tạo rồi dọn database/storage/container tạm; IAM và ProcedureCatalog là giả lập.

Dừng dịch vụ, giữ dữ liệu:

```powershell
docker compose --env-file docker/.env -f docker/docker-compose.yml down
```

Không thêm `-v` nếu cần giữ dữ liệu PostgreSQL. Nếu cổng 5000/5001 đã được dotnet hoặc container thủ công sử dụng, dừng tiến trình/container đó trước khi chạy Compose.


### Kiểm tra token IAM local thật (đã xác minh 2026-10-14)

Khi IAM:5001, Workflow:5003 và DocumentForm:5004 đang chạy, thực hiện:

Bộ kiểm thử tự động trong mục này đã được gỡ theo yêu cầu chủ dự án.

Script tạo một tài khoản người dân tổng hợp `integration_*@example.invalid`, đăng nhập IAM thật,
kiểm tra access-context và quyền trên Workflow/DocumentForm. Tài khoản test được giữ trong database local;
script không in/lưu mật khẩu hoặc token. Đã qua13 kiểm tra HTTP; không upload file hay gọi Azure,
không thay thế kiểm thử toàn bộ thủ tục/đơn/giấy tờ với các service thật.
Nếu startup báo PostgreSQL28P01, mật khẩu .env không khớp role trong volume cũ. Đổi .env không đổi
mật khẩu PostgreSQL. Giữ nguyên volume; chỉ đồng bộ mật khẩu role khi chủ dự án cho phép.
