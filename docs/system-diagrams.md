# WardMate — Sơ đồ phân tích hệ thống

ERD đã cập nhật 24/10/2026: [Conceptual](system-erd-conceptual.md), [Logical hợp nhất](system-unified-erd.md), [Physical đầy đủ](system-erd-physical.md), [ERD tương tác 3 mức](diagrams/erd/wardmate-erd-interactive.html), [draw.io tiếng Anh](diagrams/erd/wardmate-erd-en.drawio). Có 41 bảng EF ứng dụng của 6 service; 6 bảng tương lai tách riêng. Chỉ đối chiếu source, không xác nhận Azure.

Các mục Activity/State/Structure bên dưới giữ nguyên từbộtrước, không thuộc lượt cậpnhậtERD này; không dùng thôngtin lịchsử ở đó để phủđịnh ERD mới. [Bản cũ đầyđủ](diagrams/erd/archive-system-diagrams-20261015.md).

## 1. ERD — ba trang toàn hệ thống

[Conceptual](system-erd-conceptual.md) · [Logical](system-unified-erd.md) · [Physical](system-erd-physical.md) · [File draw.io 3 trang](diagrams/erd/wardmate-erd.drawio). Mỗi trang nối toàn bộ 6 service. 41 bảng hiện có; 6 bảng FUTURE được đánh dấu riêng trong cùng hình. Bản tương tác: [wardmate-erd-interactive.html](diagrams/erd/wardmate-erd-interactive.html); draw.io tiếng Anh: [wardmate-erd-en.drawio](diagrams/erd/wardmate-erd-en.drawio).

## 2. Activity Diagram — Luồng hoạt động

Đã bổ sung [**bản vẽ UML tiếng Anh theo ảnh mẫu**](diagrams/activities/index.html), [file draw.io chỉnh sửa được](diagrams/activities/wardmate-activities.drawio) và [Mermaid source](diagrams/activities/README.md). Các bản phân tích hiện trạng bên dưới được giữ nguyên để đối chiếu nghiệp vụ.

Các sơ đồ dùng flowchart Mermaid để biểu diễn hoạt động, điểm quyết định và vùng trách nhiệm; không phải bản xuất UML/XMI.

### 2.1. PDF → đối soát → thủ tục chính thức

```mermaid
flowchart TD
    Start((Bắt đầu)) --> Upload["Procedure Manager chọn PDF"]
    Upload --> Mode{"Chỉ xem trước?"}
    Mode -- Có --> Preview["POST drafts/extract-preview: bóc tách đồng bộ"]
    Preview --> Temp["Trả payload, extractedText, warnings; không tạo draft lưu DB"]
    Temp --> Choice["Đối soát rồi publish payload trực tiếp hoặc tải lên để lưu draft"]
    Mode -- Không --> Validate{"PDF hợp lệ và Blob được cấu hình?"}
    Validate -- Không --> Error["Trả lỗi; sửa file hoặc cấu hình"]
    Validate -- Có --> Store["Lưu PDF vào Blob và draft vào database"]
    Store --> Enabled{"Bật extraction?"}
    Enabled -- Có --> Queue["202 Queued → worker lấy bản nháp"]
    Queue --> Extract["AIOCR đọc text; gọi AI/OCR theo cấu hình"]
    Extract --> Result{"Có kết quả hợp lệ để đối soát?"}
    Result -- Có --> Review["NeedsReview: payload có thể thiếu và có cảnh báo"]
    Result -- Không --> Failed["Failed: retry hoặc nhập dữ liệu thủ công"]
    Failed --> Review
    Enabled -- Không --> Review
    Review --> Edit["Manager chọn lĩnh vực, sửa nội dung, checklist và biểu mẫu"]
    Edit --> Confirm{"Đã đối soát, đúng revision và dữ liệu hợp lệ?"}
    Confirm -- Không --> Edit
    Confirm -- Có --> Publish["POST drafts/id/publish: Confirmed và Revision"]
    Publish --> Tx["Transaction: tạo/cập nhật Procedure và lưu version"]
    Tx --> Done["Draft Published; công dân tra cứu thủ tục đang active"]
    Done --> End((Kết thúc))
```

Không tự xuất bản kết quả AI. Nhánh preview không tự tạo `draftId`; không dùng ID tưởng tượng để gọi draft publish. `POST procedures/publish` là luồng nhận payload đã đối soát trực tiếp, khác với publish draft đã lưu.

### 2.2. Prepare Documents and Visit Ward Office — thiết kế mới

```mermaid
flowchart TD
    Start((" ")) --> Create("Create Application")
    Create --> Fill("Complete Forms and Attach Documents")
    Fill --> Submit("Submit for Document Review")
    Submit --> Merge{" "}
    Merge --> Review("Review Documents")
    Review --> Complete{"Documents Complete?"}
    Complete -->|No| Revision("Request Revision")
    Revision --> Resubmit("Revise and Resubmit")
    Resubmit --> Merge
    Complete -->|Yes| Ready("Confirm Document Readiness")
    Ready --> Download("Download Prepared Documents")
    Download --> Visit("Visit Ward Office")
    Visit --> Finish(((" ")))
    classDef proposed fill:#7ed0eb,stroke:#45525e,stroke-dasharray:6 4;
    classDef external fill:#eeeeee,stroke:#666;
    class Ready,Download proposed;
    class Visit external;
```

Nhánh No yêu cầu sửa/bổ sung rồi kiểm tra lại; không phải từ chối giải quyết. Nhánh Yes chỉ xác nhận giấy tờ sẵn sàng. Visit Ward Office là hành động ngoài hệ thống.

### 2.3. Thông báo bất đồng bộ

```mermaid
flowchart LR
    Change["Workflow đổi trạng thái / thêm comment"] --> Save["Cùng transaction: dữ liệu + Outbox"]
    Save --> Worker["Outbox publisher"]
    Worker --> Broker["RabbitMQ"]
    Broker --> Inbox["Persist Inbox theo EventId rồi ACK"]
    Inbox --> Process["Tra IAM: người nhận còn hợp lệ"]
    Process --> Notify["Lưu Notification + Delivery"]
    Notify --> Send{"Kênh gửi"}
    Send --> Hub["IN_APP: SignalR user group"]
    Send --> Other["Email/SMS: phụ thuộc adapter và cấu hình"]
    Hub --> UI["FE nhận thông báo và số chưa đọc"]
    Worker -. "lỗi: giữ Outbox và retry" .-> Worker
    Process -. "lỗi: retry có giới hạn" .-> Inbox
```

Email/SMS thật chưa được coi là hoàn tất. Event ID và unique index chống xử lý trùng ở mức lưu dữ liệu; không khẳng định toàn hệ thống gửi đúng một lần tuyệt đối.

## 3. State Machine — Vòng đời

### 3.1. Application — trạng thái đề xuất cho kiểm tra giấy tờ

```mermaid
stateDiagram-v2
    [*] --> DRAFT : Create Application
    DRAFT --> DRAFT : Complete Forms and Attach Documents
    DRAFT --> SUBMITTED : Submit for Document Review
    SUBMITTED --> UNDER_REVIEW : Review Documents
    UNDER_REVIEW --> NEED_REVISION : Request Revision
    NEED_REVISION --> SUBMITTED : Revise and Resubmit
    UNDER_REVIEW --> DOCUMENTS_READY : Confirm Document Readiness
    DOCUMENTS_READY --> [*]
    note right of DOCUMENTS_READY
        Proposed state; backend unchanged.
        Citizen downloads prepared documents.
        Ward visit occurs outside WardMate.
        This is not an administrative decision.
    end note
```

### 3.2. ProcedureDraft — bản nháp bóc tách PDF

```mermaid
stateDiagram-v2
    [*] --> Queued : Upload / extraction bat
    [*] --> NeedsReview : Upload / extraction tat
    Queued --> Processing : Worker lay lease
    Processing --> Processing : Lease het han / thu lai neu con luot
    Processing --> NeedsReview : Ket qua hop le / co the kem warnings
    Processing --> Failed : Loi xu ly hoac het luot
    Failed --> Queued : Retry / extraction bat va dung Revision
    Failed --> NeedsReview : Luu payload thu cong
    NeedsReview --> NeedsReview : Luu chinh sua / dung Revision
    NeedsReview --> Published : Confirmed va payload hop le / transaction
    Published --> [*]
```

Worker dùng lease để tránh nhiều worker ghi cùng draft; tối đa 3 lần nhận xử lý trong chu kỳ trước khi `Failed`. Retry thủ công reset số lần. Preview đồng bộ không đi qua state machine này.

### 3.3. UserSubmission — biểu mẫu chuẩn bị, thiết kế mới

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Draft : Save Form
    Draft --> Submitted : Submit for Document Review
    Submitted --> RevisionRequested : Request Revision
    RevisionRequested --> Draft : Revise Form
    Submitted --> ReadyForDownload : Confirm Document Readiness
    ReadyForDownload --> [*]
```

Đây là vòng đời đề xuất của biểu mẫu, không phải quyết định hành chính. Cơ chế đồng bộ và điều kiện PDF hiện tại vẫn còn dùng trạng thái cũ; sẽ sửa code ở task sau.

## 4. Structure Analysis — Phân tích cấu trúc

Ở đây “Structure Analysis” gồm phân rã chức năng (FDD), kiến trúc service và sơ đồ dòng dữ liệu (DFD). DFD là mô hình phân tích, không phải sơ đồ gọi HTTP chi tiết từng endpoint.

### 4.1. Phân rã chức năng

```mermaid
flowchart TD
    WM["WardMate Backend"]
    WM --> IAM["1. Danh tính và quyền"]
    IAM --> I1["Đăng ký, đăng nhập, refresh/logout"]
    IAM --> I2["Tài khoản, hồ sơ cá nhân, phường, RBAC"]
    WM --> PC["2. Danh mục thủ tục"]
    PC --> P1["Lĩnh vực, tra cứu, quản lý, version"]
    PC --> P2["PDF draft, đối soát, xuất bản, checklist"]
    WM --> WF["3. Hồ sơ công dân"]
    WF --> W1["Tạo hồ sơ, checklist, submit/resubmit"]
    WF --> W2["Hàng đợi theo phường, tiếp nhận, bình luận, diff, xác nhận giấy tờ sẵn sàng (đích)"]
    WM --> DF["4. Tài liệu và biểu mẫu"]
    DF --> D1["Phôi DOCX, schema, version, biểu mẫu đã điền"]
    DF --> D2["Minh chứng, Blob, chuyển DOCX thành PDF"]
    WM --> AI["5. AI/OCR"]
    AI --> A1["Đọc PDF, bóc tách có cấu trúc, warnings"]
    WM --> NS["6. Thông báo"]
    NS --> N1["Inbox, Delivery, realtime, đã đọc"]
    NS --> N2["Adapter Email/SMS - cấu hình thực tế còn thiếu"]
```

Tên service `AnalyticsSystem` hiện không tự chứng minh đã có đầy đủ dashboard, báo cáo hay Knowledge Base/RAG. Các chức năng đó không được thêm vào sơ đồ như phần đã hoàn thành.

### 4.2. Kiến trúc service và hạ tầng

```mermaid
flowchart LR
    FE["Web công dân / cán bộ"] --> GW["YARP Gateway"]
    GW --> IAM["IAM API"]
    GW --> PC["ProcedureCatalog API"]
    GW --> WF["ApplicationWorkflow API"]
    GW --> DF["DocumentForm API"]
    GW --> NS["AnalyticsSystem API / SignalR"]
    IAM --> IDB[("IAM DB")]
    PC --> PDB[("Procedure DB")]
    WF --> WDB[("Workflow DB + Outbox")]
    DF --> DDB[("Document DB")]
    NS --> NDB[("Notification DB + Inbox")]
    PC --> AI["AIOCR nội bộ"]
    AI --> Provider["Gemini / Azure AI theo cấu hình"]
    PC --> Blob["Azure Blob / Azurite local"]
    DF --> Blob
    DF --> Convert["Gotenberg"]
    WF --> PC
    WF --> DF
    DF --> WF
    WF --> Rabbit["RabbitMQ"]
    Rabbit --> NS
    NS --> IAM
    NS -. "SignalR realtime" .-> FE
```

Đây là kiến trúc logic; URL và đường route cụ thể tùy cấu hình local/Azure. Các service không truy vấn trực tiếp database của nhau. JWT bảo vệ API người dùng; các lời gọi nội bộ dùng cơ chế xác thực nội bộ theo cấu hình. Không suy ra service đã deploy từ việc nó có mặt trong sơ đồ.

### 4.3. Clean Architecture — chiều phụ thuộc mã nguồn

```mermaid
flowchart TD
    API["API: Controllers, JWT, middleware, composition root"] --> APP["Application: Commands / Queries / Validators / Interfaces"]
    APP --> DOMAIN["Domain: Entities, trạng thái và quy tắc"]
    INFRA["Infrastructure: EF Core, Repository, Blob, HTTP, workers"] --> APP
    INFRA --> DOMAIN
    API --> INFRA
    API --> SHARED["SharedKernel / EventBus"]
    INFRA --> SHARED
```

Mũi tên biểu diễn phụ thuộc, không phải thứ tự xử lý request. API đăng ký implementation Infrastructure vào DI; Application gọi qua interface. Cách tổ chức file chi tiết khác nhau giữa các service, sơ đồ mô tả ranh giới trách nhiệm chung.

### 4.4. DFD mức ngữ cảnh

```mermaid
flowchart LR
    Citizen["Công dân"] -->|"Thông tin, hồ sơ, giấy tờ"| System(("WardMate"))
    System -->|"Thủ tục, trạng thái, lỗi bổ sung, PDF, thông báo"| Citizen
    Officer["Cán bộ Một cửa"] -->|"Kiểm tra giấy tờ, yêu cầu bổ sung, xác nhận sẵn sàng"| System
    System -->|"Hàng đợi đúng phường, dữ liệu hồ sơ"| Officer
    Manager["Manager / IT Admin"] -->|"Quản lý tài khoản và quyền theo phạm vi"| System
    ProcedureManager["Procedure Manager"] -->|"PDF, bản đối soát, checklist, phôi"| System
    System -->|"Nội dung cần bóc tách"| AI["Nhà cung cấp AI/OCR"]
    AI -->|"Dữ liệu có cấu trúc hoặc lỗi"| System
```

### 4.5. DFD mức 1 — xử lý hồ sơ

```mermaid
flowchart LR
    C["Công dân"] -->|"Yêu cầu tạo, điền, nộp"| P1(("1. Chuẩn bị hồ sơ"))
    Catalog[("D1 Thủ tục và checklist")] -->|"Yêu cầu giấy tờ"| P1
    P1 -->|"Draft và checklist snapshot"| Apps[("D2 Hồ sơ")]
    P1 -->|"Biểu mẫu và file"| Docs[("D3 Tài liệu")]
    Apps --> P2(("2. Kiểm tra và nộp"))
    Docs -->|"Metadata và kiểm tra tồn tại file"| P2
    P2 -->|"SUBMITTED, version, lịch sử"| Apps
    Apps --> P3(("3. Kiểm tra giấy tờ"))
    O["Cán bộ đúng phường"] -->|"Bình luận và xác nhận giấy tờ"| P3
    P3 -->|"Lỗi, trạng thái, lịch sử"| Apps
    P3 -->|"Yêu cầu bổ sung"| C
    Apps -->|"DOCUMENTS_READY (đề xuất)"| P4(("4. Kết xuất giấy tờ chuẩn bị"))
    Docs -->|"DOCX đã điền"| P4
    P4 -->|"PDF"| Docs
    P4 -->|"Giấy tờ mang đến phường"| C
    Apps -->|"Sự kiện qua Outbox"| P5(("5. Thông báo"))
    P5 -->|"Nội dung và trạng thái gửi"| N[("D4 Notifications")]
    P5 -->|"Realtime"| C
```

## 5. Nguồn đối chiếu và giới hạn

- IAM: `src/Services/WardMate.Services.IAM/WardMate.Services.IAM.Domain/Entities/` và `WardMate.Services.IAM.Infrastructure/Persistence/Configurations/`.
- Catalog: `src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.Domain/Entities/`, `Application/Drafts/ProcedureDraftService.cs` trong project Application và `Infrastructure/Drafts/DraftExtractionWorker.cs` trong project Infrastructure.
- Workflow: `src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.Domain/`, project Application: `WorkflowRequests.cs`, `ReviewHandlers.cs`, `FinalDecisionRequests.cs`; project Infrastructure: `WorkflowDbContext.cs`, `WorkflowOutbox.cs`.
- DocumentForm: `src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.Domain/Entities/`, project Infrastructure: `Persistence/Configurations/`; project API: `Security/ApplicationAccess.cs`.
- Notifications: `src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.Infrastructure/NotificationDbContext.cs` và các worker cùng service.
- Hướng dẫn sử dụng và quy tắc chi tiết: [API guide](api-guide.md), [System constraints](system-constraints.md), [Progress](../PROGRESS.md).

Tài liệu không xác nhận độ chính xác tuyệt đối của AI, giao dịch nguyên tử xuyên service, gửi thông báo exactly-once, SMS/Email production, hay kiểm thử toàn luồng Azure. Khi bổ sung entity, FK hoặc chuyển trạng thái mới, cần sửa sơ đồ tương ứng thay vì coi sơ đồ này là đặc tả bất biến.

## 6. ERD tương lai

6 bảng chưa triển khai được giữ riêng trong [Logical ERD](system-unified-erd.md). Chat, Knowledge Base, metrics, feedback và QR đã có, không còn thuộc phần tương lai. Xem bản lưu lịch sử nếu cần thiết kế trước đây.

## 7. Activity, State Machine và Structure Analysis

Xem [bộ sơ đồ luồng](system-future-flows.md); phần này không thay đổi trong lượt cập nhật ERD.

## 8. ERD hợp nhất

[41 bảng hiện có và 6 bảng tương lai](system-unified-erd.md); FK vật lý và REF logic được phân biệt.
