# WardMate Backend

Microservices .NET 8: sáu API độc lập, YARP Gateway và Global Exception Handler dùng chung. Core IAM đã có PostgreSQL, JWT và các API xác thực. Procedure Catalog đã có entities, JSONB, migration, seed và query chi tiết; xem [TASK-07](docs/procedure-catalog.md).

Hướng dẫn cấu hình/chạy IAM, DTO, lỗi và token: [Core IAM authentication](docs/iam-auth.md). Nhật ký task và kiểm thử: [PROGRESS.md](PROGRESS.md). Quy trình feature → `kha` → `deploy`: [AGENTS.md](AGENTS.md).

## Cấu trúc

```text
src/
├── BuildingBlocks/
│   ├── WardMate.SharedKernel/       # Exception handler và extension đăng ký
│   └── WardMate.EventBus/           # Class library trống, dành cho bước sau
├── Gateways/
│   └── WardMate.YarpGateway/
└── Services/
    ├── WardMate.Services.IAM/
    ├── WardMate.Services.ProcedureCatalog/
    ├── WardMate.Services.ApplicationWorkflow/
    ├── WardMate.Services.DocumentForm/
    ├── WardMate.Services.AIOCR/
    └── WardMate.Services.AnalyticsSystem/
```

Mỗi service chứa bốn project `.Domain`, `.Application`, `.Infrastructure`, `.API`. Tham chiếu: Application → Domain; Infrastructure → Application; API → Application + Infrastructure. IAM triển khai CQRS qua MediatR và FluentValidation; Procedure Catalog có query MediatR đọc chi tiết thủ tục. SharedKernel/Web được tham chiếu ở API và gateway để xử lý lỗi HTTP thống nhất; các lớp Domain không phụ thuộc ASP.NET Core. EventBus chưa tích hợp RabbitMQ.

## Build

Yêu cầu .NET SDK 8.0.4xx. TASK-07 bổ sung lại integration tests riêng cho Procedure Catalog theo yêu cầu mới; các test IAM/SharedKernel đã xóa không được khôi phục. Test mới cần Docker Linux containers và dùng PostgreSQL 16 tạm, độc lập database local. CI restore, build Release và chạy test suite hiện có.

```powershell
dotnet restore WardMate.sln
dotnet build WardMate.sln -c Release --no-restore
dotnet test WardMate.sln -c Release --no-build
```

## Chạy local

Mở terminal riêng cho mỗi service cần chạy. Với IAM, thiết lập database và `Jwt__Key` theo [hướng dẫn IAM](docs/iam-auth.md) trước. Ví dụ IAM và gateway:

```powershell
dotnet run --project src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API --launch-profile http
dotnet run --project src/Gateways/WardMate.YarpGateway --launch-profile http
```

| Service | Cổng | Prefix qua gateway |
|---|---:|---|
| IAM | 5001 | `/api/iam` |
| ProcedureCatalog | 5002 | `/api/procedure-catalog` |
| ApplicationWorkflow | 5003 | `/api/application-workflow` |
| DocumentForm | 5004 | `/api/document-form` |
| AIOCR | 5005 | `/api/ai-ocr` |
| AnalyticsSystem | 5006 | `/api/analytics-system` |

Gateway chạy tại `http://localhost:5000`. YARP loại bỏ prefix khi chuyển tiếp: `GET http://localhost:5000/api/iam/health` → IAM `/health`. Endpoint `/health` của gateway chỉ kiểm tra chính tiến trình gateway. Nếu service đích chưa chạy, YARP trả 502; lỗi chuyển tiếp này không phải exception nghiệp vụ.

## Global Exception Handler

`WardMate.SharedKernel/Web/GlobalExceptionHandler.cs` sử dụng `IExceptionHandler`, đăng ký qua `AddGlobalExceptionHandling()` và kích hoạt bằng `UseGlobalExceptionHandling()` ở cả bảy host. Exception chưa xử lý trả HTTP 500, `application/problem+json`, thông báo chung và `traceId`; chi tiết exception chỉ ghi trong log máy chủ. Cơ chế này áp dụng trong cả Development và Production. Các mã lỗi nghiệp vụ sẽ được bổ sung khi triển khai từng use case.

Không có endpoint cố tình gây lỗi trong các API thật.

`docker/docker-compose.yml` cung cấp PostgreSQL 16 cho IAM tại localhost:5433. API và gateway hiện chạy bằng .NET local. Các endpoint `/api/v1/auth/*` và `/api/v1/users/me` được gateway chuyển nguyên đường dẫn sang IAM.
