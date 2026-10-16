# WardMate — English UML Activity Diagrams

> Cập nhật triển khai local 16/10/2026: luồng xác nhận giấy tờ đã được code bằng DOCUMENTS_READY/documentsReadyAt và submission ReadyForDownload. Các ảnh xuất ngày 15/10 là bản thiết kế tại thời điểm đó; nhãn “backend pending” trong ảnh đã được thay thế bởi cập nhật này. Chưa deploy/migrate Azure cho thay đổi mới. ERD cũ vẫn giữ để đối chiếu; migration DocumentReadiness bổ sung documents_ready_at.

> **Điều chỉnh phạm vi ngày 15/10/2026:** WardMate hỗ trợ chuẩn bị và kiểm tra giấy tờ trước khi đến phường; không phê duyệt hoặc từ chối giải quyết hồ sơ hành chính. Các sơ đồ Application/Document/QR dưới đây là thiết kế mới, **chưa triển khai vào backend**. `DOCUMENTS_READY` và `ReadyForDownload` là tên trạng thái đề xuất, chưa phải giá trị API. ERD được giữ nguyên để đối chiếu: các cột/trạng thái APPROVED, REJECTED, ApprovedAt và thiết kế approved_qrs/metrics cũ không có nghĩa còn thuộc phạm vi nghiệp vụ đích. Việc giải quyết chính thức tại phường nằm ngoài hệ thống.


These drawings follow the supplied visual example: black initial node, rounded blue actions, decision diamonds, explicit merge diamonds and a bullseye final node. Action labels are short English verb phrases; conditions and branch labels remain short questions/guards.

## Open or edit

- [Offline gallery](index.html): open locally in a browser to view all nine diagrams.
- [Editable draw.io document](wardmate-activities.drawio): open/import in draw.io. Contains nine pages with editable shapes and connectors, not flattened images.
- `.mmd`: copy the Mermaid text into the Mermaid insertion feature of draw.io or another Mermaid editor. Auto-layout may differ from the reference; use `.drawio` for the provided coordinates and UML final symbol.
- `.svg`: vector images for reports, zooming and printing.
- `.png`: rendered images for documents or slides.
- [Overview](overview.png): contact sheet of all nine diagrams.

| Page | Mermaid source | Vector image |
| --- | --- | --- |
| IAM authentication | [01-iam.mmd](01-iam.mmd) | [SVG](01-iam.svg) |
| Procedure publishing | [02-procedure-catalog.mmd](02-procedure-catalog.mmd) | [SVG](02-procedure-catalog.svg) |
| Application review | [03-application-workflow.mmd](03-application-workflow.mmd) | [SVG](03-application-workflow.svg) |
| Prepared document PDF | [04-document-form.mmd](04-document-form.mmd) | [SVG](04-document-form.svg) |
| Knowledge and RAG | [05-ai-rag.mmd](05-ai-rag.mmd) | [SVG](05-ai-rag.svg) |
| Identity OCR | [06-ai-cccd.mmd](06-ai-cccd.mmd) | [SVG](06-ai-cccd.svg) |
| Notifications | [07-notifications.mmd](07-notifications.mmd) | [SVG](07-notifications.svg) |
| Preparation, feedback and metrics | [08-analytics-results.mmd](08-analytics-results.mmd) | [SVG](08-analytics-results.svg) |
| System operations | [09-system-operations.mmd](09-system-operations.mmd) | [SVG](09-system-operations.svg) |

## Implementation status

Dashed action borders indicate **planned functionality**. The email action has an amber border for **partial implementation**. On pages 03 and 04, solid actions are retained capabilities within a revised target flow; its readiness gate is not implemented. Visit Ward Office is an external citizen action, not a backend feature. On other pages, solid actions represent existing code; this does not confirm production integration or provider configuration. These markings keep the action labels concise.

- Pages 01–04 and 07 combine current actions with explicitly dashed future actions.
- Pages 05, 06 and 08 are planned workflows. PDF text extraction already exists, but this is not an implemented RAG or identity-card OCR pipeline.
- Page 09 shows the planned centralized operations flow. Existing IAM RBAC audit and individual health endpoints do not implement the complete centralized workflow.
- Decision/merge nodes inherit the context of their branch; their solid border does not mean the associated future condition is already implemented.

## Scope and simplifications

These are readable activity views, not a full API contract. See [detailed workflow analysis](../../system-future-flows.md) and [runtime constraints](../../system-constraints.md).

- IAM refresh/logout remain separate operations; the authentication page focuses on login and protected operations.
- The PDF upload page shows successful/manual review paths. Extraction failure, worker lease expiry and retry states remain in the detailed draft state machine.
- Application revision requires validated documents before resubmission. The short label “Revise and Resubmit Application” includes that validation. The original application code is retained.
- Status-change notifications remain a supporting capability. Their omission from the simplified preparation flow does not remove them from the backend.
- Page 04 proposes replacing the existing approval gate with document readiness. Code is unchanged. Optional reference QR is not an official result, signature or seal. “Output Type?” is a design branch, not an API parameter.
- RAG indexing and question answering are separate operations in implementation; the page illustrates preparation followed by a question using the prepared index, not mandatory re-indexing for every question.
- Provider receipts apply only where supported, not to in-app SignalR as proof that a user received or read a notification.
- Preparation-reference generation, feedback processing and metrics aggregation are independent request types, not three actions performed for every request.
- System audit/settings, health and backup are independent jobs. Backup restore verification must occur in an isolated environment.

## Regenerate

Edit the node/edge definitions and English label map in [generate-activities.cjs](generate-activities.cjs), then run:

```text
node docs/diagrams/activities/generate-activities.cjs
```

This regenerates SVG, Mermaid, draw.io XML and the offline gallery without network access or extra packages. PNG and overview images are exported from the resulting SVGs using an SVG renderer; rerender them after changing the source. No application runtime, Git or database operation is performed.
