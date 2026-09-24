# WardMate Backend

Cấu trúc thư mục ban đầu cho backend WardMate theo kiến trúc microservices .NET.

```text
wardmate-backend/
├── src/
│   ├── BuildingBlocks/
│   │   ├── WardMate.SharedKernel/
│   │   └── WardMate.EventBus/
│   ├── Gateways/
│   │   └── WardMate.YarpGateway/
│   └── Services/
│       ├── WardMate.Services.IAM/
│       ├── WardMate.Services.ProcedureCatalog/
│       ├── WardMate.Services.ApplicationWorkflow/
│       ├── WardMate.Services.DocumentForm/
│       ├── WardMate.Services.AIOCR/
│       └── WardMate.Services.AnalyticsSystem/
├── docker/
│   └── docker-compose.yml
└── WardMate.sln
```

Các thư mục dùng `.gitkeep` để được lưu trên Git. Solution hiện chưa chứa project; Docker Compose là file giữ chỗ. Project, cấu hình hạ tầng và nghiệp vụ sẽ được bổ sung theo từng bước tiếp theo.
