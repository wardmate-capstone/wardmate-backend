# PDF-001 — PDF nguồn, bóc tách và đối soát bản nháp thủ tục

**Cập nhật PDF-002:** ưu tiên đọc lớp văn bản PDF bằng PdfPig, không cần OCR. AI và OCR có hai cờ bật riêng, mặc định tắt. Có API extract-preview không cần Blob. Xem [hướng dẫn test PDF trực tiếp](procedure-pdf-text.md); phần adapter Azure bên dưới mô tả nhánh tùy chọn, không còn là bước bắt buộc cho mọi PDF.

## Phạm vi và trạng thái

Backend đã có luồng lưu PDF → bản nháp → sửa/đối soát → xác nhận xuất bản. Procedure Catalog quản lý dữ liệu nghiệp vụ và hàng đợi PostgreSQL; AIOCR chỉ đọc PDF và đề xuất JSON qua HTTP, không truy cập database của Procedure. Nghiệp vụ bản nháp nằm ở Application, EF/Blob/HTTP/worker nằm ở Infrastructure.

Chủ dự án chưa có OCR. Vì vậy AI/OCR **mặc định tắt**, không có tài khoản cloud, model hoặc dịch vụ trả phí nào được tạo/gọi tự động. Adapter Azure Document Intelligence + Azure OpenAI đã có để cấu hình sau. Tests dùng PostgreSQL thật tạm thời, Blob và phản hồi nhà cung cấp mô phỏng; chưa kiểm chứng chất lượng OCR/AI trên PDF thật.

Repo này chỉ chứa backend. FE cần xây màn hình Split-view dựa trên các API dưới đây; không có màn hình đối soát mới trong repo này.

## Cấu hình tối thiểu: upload và đối soát thủ công

- Áp dụng migration `20261002072134_ProcedureDrafts` vào `wardmate_procedure_db` theo quy trình vận hành. Migration không tự được áp dụng vào Local Dev hoặc Azure trong task này.
- Procedure Catalog cần `AzureBlob__ConnectionString` từ secret/environment, dùng Azure Storage shared key để client hiện tại sinh read SAS. Không đưa connection string vào source hoặc tài liệu.
- Container Blob riêng `procedure-sources`, tạo private khi chưa tồn tại. Không đổi container này thành public.
- Giữ `ProcedureDrafts__ExtractionEnabled=false`. PDF upload thành công đi thẳng vào `NeedsReview`, payload trống và cảnh báo nhập thủ công.
- Thiếu Blob configuration: upload/source trả 503 `draft.storage_not_configured`; các API thủ tục cũ vẫn hoạt động.
- Swagger local Procedure: `http://localhost:5002/swagger`; Gateway đã có route `/api/v1/procedure-manager/{**catch-all}` nên không cần đổi route.
- Cần role `PROCEDURE_MANAGER` hoặc `IT_ADMIN`, Bearer access token từ IAM. Không dùng refresh cookie để xác thực Procedure API.

## API bản nháp (Manager)

| Method | Route | Body | Thành công |
| --- | --- | --- | --- |
| POST | `/api/v1/procedure-manager/drafts` | multipart/form-data, trường `file` là PDF | 202 DraftDto, Location |
| GET | `/api/v1/procedure-manager/drafts?page=1&pageSize=10` | Không | 200 mảng DraftSummaryDto, tối đa 50/trang |
| GET | `/api/v1/procedure-manager/drafts/{id}` | Không | 200 DraftDto |
| GET | `/api/v1/procedure-manager/drafts/{id}/source` | Không | 200 `{url,expiresInSeconds:600}` |
| PUT | `/api/v1/procedure-manager/drafts/{id}` | `{revision,payload}` | 200 DraftDto với revision mới |
| POST | `/api/v1/procedure-manager/drafts/{id}/retry` | `{revision}` | 200 DraftDto trạng thái Queued |
| POST | `/api/v1/procedure-manager/drafts/{id}/publish` | `{revision,confirmed:true}` | 200 ProcedureDetailDto |

DraftDto: `id`, `status`, `pdfFileName`, `payload` (JSON object), `warnings` (mảng thông báo), `extractedText`, `failureCode?`, `revision`, `publishedProcedureId?`, `createdAt`, `updatedAt`. DraftSummaryDto bỏ payload, warnings và extractedText để không tải JSON lớn trên trang danh sách. List dùng page/pageSize, chưa trả tổng số bản nháp.

Giới hạn: file .pdf có chữ ký `%PDF-`, tối đa 20 MiB; tên file tối đa 255 ký tự, được bỏ phần đường dẫn. Đây là kiểm tra định dạng đầu vào, không phải quét virus hay xác nhận cấu trúc PDF hoàn chỉnh. Payload nháp tối đa 500.000 ký tự, HTTP PUT tối đa 1 MiB. Mỗi PDF dành cho một thủ tục; nhiều trường hợp xử lý trong một thủ tục được giữ thành các cases.

`revision` là GUID phiên bản hiện tại, phải lấy từ GET/PUT mới nhất. Thiếu/sai revision hoặc trạng thái không cho phép trả 409 `draft.conflict`, FE tải lại thay vì ghi đè. PUT chấp nhận bản nháp chưa hoàn chỉnh để lưu tiến độ; validation nghiệp vụ đầy đủ chạy khi publish. Giá trị số/boolean thiếu hoặc null cần cán bộ bổ sung, không tự áp dụng mặc định 0, miễn phí hay 1 ngày.

Các trạng thái:

- `Queued`: đợi worker (chỉ khi đã bật extraction).
- `Processing`: worker đã nhận và đang gọi AIOCR.
- `NeedsReview`: có đề xuất AI hoặc nội dung nhập thủ công; chưa công khai.
- `Failed`: bóc tách lỗi; có thể PUT để nhập thủ công hoặc POST retry nếu đã bật extraction.
- `Published`: đã xuất bản, không cho sửa/xuất bản lại cùng bản nháp. Khi có PDF mới, tạo bản nháp mới; cùng ProcedureCode sẽ cập nhật thủ tục và lưu snapshot cũ.

Lỗi trả ProblemDetails tiếng Việt với `code`, `traceId`, `errors` nếu có: 400 validation/file/payload/thiếu xác nhận; 401/403 xác thực; 404 không tìm thấy; 409 revision/trạng thái/xung đột; 503 thiếu cấu hình. File quá giới hạn HTTP có thể bị server/ingress chặn 413 trước controller. Không thay đổi token contract IAM.

## Luồng FE Split-view

1. Upload PDF, nhận draft id và status. Khi status là Queued/Processing, poll GET mỗi 3–5 giây; dừng khi NeedsReview/Failed/Published.
2. Gọi source để lấy read SAS 10 phút. Hiển thị PDF bằng iframe/viewer bên trái; khi hết hạn gọi source lại. Không ghi SAS vào payload, analytics hoặc localStorage.
3. Bên phải render form từ `payload`, cho phép thêm/sửa cases, steps, checklist và forms. Hiển thị warnings và extractedText như nội dung không tin cậy; không render HTML trực tiếp từ OCR/AI. Chọn CategoryId thực tế trong hệ thống; AI không được tự tạo ID danh mục.
4. PUT `{revision,payload}` để lưu. Giữ nguyên null cho trường chưa rõ; chỉ bổ sung sau khi đối chiếu PDF. Giữ revision mới từ response.
5. Cán bộ xác nhận đã kiểm tra, POST publish với `confirmed:true` và revision mới nhất. Server sử dụng PDF của bản nháp, bỏ qua URL/tên PDF do payload hoặc AI tự đưa vào.
6. Publish thực thi trong transaction chung với cập nhật trạng thái bản nháp. Hai lần xác nhận đồng thời chỉ một lần thành công, lần còn lại 409. Payload hợp lệ dùng handler publish hiện có: mã mới tạo version 1; mã đã có chụp snapshot trước khi cập nhật; không tự mở lại thủ tục đang đóng.

## Xem PDF sau xuất bản

`GET /api/v1/procedures/{id}/source` là public, chỉ cấp read URL khi thủ tục đang active và PDF hiện tại tương ứng một bản nháp Published. Trả 200 `{url,expiresInSeconds:600}`, header `Cache-Control: no-store`; còn lại 404. Với URL thủ tục cũ được nhập bằng API khác, endpoint này không fetch tùy ý URL đó.

`OriginalPdfUrl` lưu URL Blob bền vững không chứa SAS; Blob private nên FE dùng endpoint source để xem. Đóng thủ tục ngăn cấp link mới; link SAS đã cấp còn hiệu lực tối đa 10 phút. PDF lịch sử được giữ lại cho đối soát, không xóa file khi có phiên bản mới.

## Bật AI/OCR khi đã có dịch vụ

Procedure Catalog:

```text
ProcedureDrafts__ExtractionEnabled=true
ProcedureDrafts__AiOcrUrl=https://<internal-aiocr-host>
ProcedureDrafts__ServiceKey=<secret-ngẫu-nhiên-tối-thiểu-32-ký-tự>
AzureBlob__ConnectionString=<secret>
```

AIOCR:

```text
Extraction__Enabled=true
ServiceAuthentication__Key=<cùng-ServiceKey-của-Procedure>
DocumentIntelligence__Endpoint=https://<resource>.cognitiveservices.azure.com
DocumentIntelligence__Key=<secret>
AzureOpenAI__Endpoint=https://<resource>.openai.azure.com
AzureOpenAI__Key=<secret>
AzureOpenAI__Deployment=<deployment-hỗ-trợ-chat-completions-JSON-mode>
```

AIOCR nhận `POST /internal/v1/procedure-extractions`, body binary PDF, header `X-Service-Key`; trả 200 `{payload,extractedText,warnings}`. Không cung cấp URL để AIOCR tải file tùy ý. Triển khai AIOCR bằng internal ingress; các key chỉ trao đổi backend, không cấp cho FE. Docker internal network dùng `http://ai-ocr:8080`; cloud nên dùng HTTPS. Endpoint cần key ngay cả khi có thể truy cập qua route Gateway cũ. Không cần database riêng cho AIOCR adapter stateless này.

Adapter dùng Document Intelligence `prebuilt-layout` API `2024-11-30`, poll tối đa khoảng 4 phút; chuyển markdown OCR sang Azure OpenAI `/openai/v1/chat/completions` với JSON mode. Không mặc định model/deployment. Văn bản OCR trên 100.000 ký tự bị từ chối, yêu cầu chia file; không cắt rồi xuất bản dữ liệu thiếu. AI response bị cắt (`finish_reason` khác stop) cũng bị từ chối. Tài liệu được coi là dữ liệu, không phải chỉ dẫn thực thi; kết quả vẫn phải được người quản lý đối soát.

Worker mỗi replica xử lý một tác vụ một lần; PostgreSQL `FOR UPDATE SKIP LOCKED` và lease 12 phút phối hợp nhiều replica. Thời gian một lần xử lý tối đa 10 phút. Khi process chết, lease hết hạn cho phép nhận lại; sau 3 lần nhận mà không hoàn tất chuyển Failed. Lỗi provider thông thường chuyển Failed, không retry tính phí vô hạn; cán bộ chủ động retry. Vì nhà cung cấp ở ngoài DB transaction, khôi phục sau crash có thể gọi provider lại và phát sinh phí lặp. Không có đảm bảo exactly-once cho dịch vụ cloud.

## Docker / vận hành

Đã thêm Dockerfile AIOCR và profile Compose `ai`, các biến mẫu trong `docker/.env.example`. Chưa tự build image hoặc khởi động cụm ứng dụng. Sau khi cấu hình secret và muốn bật AI, người vận hành có thể chạy trong thư mục docker:

```powershell
docker compose --profile ai up -d --build ai-ocr procedure-catalog
```

Chế độ manual không cần bật profile ai. Tác vụ background cần ít nhất một replica Procedure hoạt động (không scale-to-zero khi còn hàng đợi). Blob upload và DB insert không cùng transaction phân tán; nếu DB insert thất bại, có thể còn file mồ côi để vận hành dọn sau khi xác minh, không tự xóa trong trường hợp commit không rõ kết quả. Upload lại tạo bản nháp khác, chưa có idempotency-key/deduplicate PDF.

Tài liệu nhà cung cấp: [Document Intelligence Analyze](https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/analyze-document?view=rest-aiservices-v4.0+%282024-11-30%29), [Azure OpenAI JSON mode](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/json-mode).
