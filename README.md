# WardMate Backend

Bộ khung microservices .NET 8: sáu API độc lập, YARP Gateway và Global Exception Handler dùng chung. Chưa triển khai nghiệp vụ, xác thực, database hay message broker.

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

Mỗi service chứa bốn project `.Domain`, `.Application`, `.Infrastructure`, `.API`. Tham chiếu: Application → Domain; Infrastructure → Application; API → Application + Infrastructure. Các lớp bên trong hiện để trống, API chỉ có endpoint nhận diện `/` và liveness `/health`. SharedKernel/Web được tham chiếu ở API và gateway để xử lý lỗi HTTP thống nhất; các lớp Domain không phụ thuộc ASP.NET Core. EventBus chưa tích hợp RabbitMQ.

## Build và test

Yêu cầu .NET SDK 8.0.4xx.

```powershell
dotnet restore WardMate.sln
dotnet build WardMate.sln -c Release --no-restore
dotnet test WardMate.sln -c Release --no-build
```

## Chạy local

Mở terminal riêng cho mỗi service cần chạy. Ví dụ IAM và gateway:

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

Test tạo endpoint gây lỗi trong test host để kiểm tra status, content type, trace ID và không lộ thông tin nội bộ. Không có endpoint cố tình gây lỗi trong các API thật.

`docker/docker-compose.yml` vẫn là file giữ chỗ của bước cấu trúc; bước khởi tạo này chạy bằng .NET local.
