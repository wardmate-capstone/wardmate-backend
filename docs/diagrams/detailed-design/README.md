# WardMate — Detailed Design (Class + Sequence Diagram)

Phần "IV. Software Design Description → 3. Detailed Design" của báo cáo. Mỗi luồng chính có một **Class Diagram** và một **Sequence Diagram**, vẽ theo code backend ngày 04/11/2026 (tên lớp, phương thức, điều kiện lỗi lấy từ source).

- [wardmate-detailed-design.drawio](wardmate-detailed-design.drawio): 26 trang, mở bằng diagrams.net để sửa.
- [png/](png/): ảnh từng trang để chèn vào báo cáo.

| # | Luồng | Service | Nguồn chính |
|---|---|---|---|
| 01 | Login | IAM | AuthController, LoginHandler, TokenIssuer, IdentityStore, JwtTokenService |
| 02 | Forgot Password OTP | IAM | PasswordOtpService, IamEmailSender, UserOtp |
| 03 | Create Application | ApplicationWorkflow | ApplicationsController, WorkflowHandlers, ProcedureCatalogClient, WorkflowDirectory, ApplicationStore |
| 04 | Submit Application | ApplicationWorkflow | ApplicationsController.Submit, DocumentContentsClient, ReviewDeadline, WorkflowOutboxCapture |
| 05 | Claim Application | ApplicationWorkflow | OfficerApplicationController, ReviewHandlers, AssignmentHandlers |
| 06 | Comment and Request Revision | ApplicationWorkflow | ReviewHandlers (AddApplicationComment, RequestRevision) |
| 07 | Confirm Documents Ready | ApplicationWorkflow | ConfirmDocumentReadinessHandler |
| 08 | Record Official Receipt | ApplicationWorkflow | ReceiveApplicationHandler |
| 09 | Generate PDF and QR | DocumentForm | GeneratedDocumentsController, GeneratePdfDocumentCommandHandler, GotenbergPdfRenderEngine, ApprovedQrService |
| 10 | Publish Procedure from PDF | ProcedureCatalog + AIOCR | ProcedureDraftService, DraftExtractionWorker, AiOcrClient, TextFirstProcedureExtractor |
| 11 | Chat with AI Assistant | AIOCR | ChatService, KnowledgeRetriever, GeminiEmbeddingProvider, GeminiAssistant |
| 12 | Scan Identity Card (OCR) | AIOCR | AssistantController.Extract, GeminiAssistant |
| 13 | Notification Delivery | Workflow + EventBus + AnalyticsSystem | WorkflowOutboxWorker, WorkflowNotificationConsumer, NotificationInboxWorker, NotificationDeliveryWorker, NotificationRealtimeListener |

Quy ước: màu xanh dương là API (controller), xanh lá là Application (handler/service), vàng là interface, cam là Infrastructure, tím là entity, đỏ nhạt là service/hệ thống bên ngoài. Trên Class Diagram: mũi tên nét đứt tam giác rỗng là "implements", nét đứt mũi mở là "uses", hình thoi đặc là "owns".

Lưu ý:
- Class Diagram chỉ ghi các thuộc tính và phương thức liên quan tới luồng, không liệt kê toàn bộ lớp.
- Luồng 09: mã QR được lưu ở bảng `approved_qrs` và trả qua API `approved-qr`; code **không** vẽ QR vào nội dung PDF.
- Đây là thiết kế theo code, không thay cho kiểm thử: sơ đồ không chứng minh runtime hoạt động đúng.
- Nguồn dựng (script sinh draw.io) nằm trong scratchpad phiên Claude, ngoài repo.
