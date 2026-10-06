# PDF-002 — Đọc chữ PDF trực tiếp, không cần OCR

## Cách test nhanh nhất, không cần Blob, AI, OCR hay database

Từ thư mục gốc repo, sau khi đã build Release:

```powershell
pwsh -NoProfile -File scripts/verify-pdf-text.ps1 -PdfPath "C:\duong-dan\thu-tuc.pdf"
```

Có thể thêm `-ExpectedText "Đăng ký tạm trú"` để kiểm tra một đoạn chữ mong đợi. Script khởi động AIOCR tạm trên cổng loopback ngẫu nhiên, tự tạo service key chỉ trong process, tắt cả AI và OCR, gọi HTTP bằng PDF gốc rồi dừng process. Không ghi DB, upload Blob hoặc gửi file ra cloud. Kết quả có số ký tự, cảnh báo và 350 ký tự đầu để đối chiếu. `PASS` chỉ xác nhận HTTP trả thành công và ExpectedText (nếu truyền) khớp, không đảm bảo mọi nội dung PDF được đọc đầy đủ; luôn đọc warnings.

## Test trên Swagger Procedure

Endpoint mới: **POST `/api/v1/procedure-manager/drafts/extract-preview`**, multipart/form-data với trường `file`. Yêu cầu Bearer role PROCEDURE_MANAGER hoặc IT_ADMIN như các API quản trị khác. Không lưu Blob, không tạo bản nháp, không sửa database.

AIOCR cần chạy (local `http://localhost:5005`) với:

| Biến môi trường AIOCR | Giá trị |
| --- | --- |
| `Extraction__Enabled` | `true` |
| `Extraction__UseAI` | `false` |
| `Extraction__OcrFallbackEnabled` | `false` |
| `ServiceAuthentication__Key` | Secret ngẫu nhiên tối thiểu 32 ký tự |

Procedure Catalog cần:

| Biến môi trường Procedure | Giá trị |
| --- | --- |
| `ProcedureDrafts__AiOcrUrl` | `http://localhost:5005` |
| `ProcedureDrafts__ServiceKey` | Cùng secret với AIOCR |

Khởi động lại hai service với cấu hình trên. Procedure vẫn cần cấu hình JWT/database vốn có để chạy API; preview không cần AzureBlob. Mở `http://localhost:5002/swagger`, Authorize bằng token quản lý, chọn `ProcedureDrafts` → `extract-preview` → Try it out → chọn PDF → Execute.

Response 200:

```json
{
  "payload": { "categoryId": 0, "procedureCode": "", "title": "", "...": "các trường thủ tục chưa điền" },
  "extractedText": "--- Trang 1 ---\nNội dung PDF...",
  "warnings": ["Đã đọc lớp văn bản PDF trực tiếp, không dùng OCR...", "Chưa bật AI..."]
}
```

`payload` trong ví dụ chỉ rút gọn để minh họa; API thực tế trả khung các trường đầy đủ. Khi AI tắt, không giả vờ đã xác định trường thủ tục: kết quả chính là extractedText để cán bộ đọc/nhập thủ công. Response `Cache-Control: no-store`. Lỗi: 400 file sai, 401/403 role, 503 thiếu cấu hình kết nối AIOCR, 502 dịch vụ/file không đọc được, 504 timeout; ingress có thể trả 413 khi file quá lớn. Không thay đổi token IAM.

## Luồng lưu bản nháp

API upload `POST /api/v1/procedure-manager/drafts` vẫn cần Blob. Để worker tự đọc PDF vừa upload, bật `ProcedureDrafts__ExtractionEnabled=true`, chạy AIOCR với `Extraction__Enabled=true` và giữ UseAI/OcrFallbackEnabled=false. Worker lưu extractedText + warnings, chuyển NeedsReview. Nếu tắt ProcedureDrafts:ExtractionEnabled thì vẫn là chế độ nhập tay hoàn toàn như trước, không gọi reader.

Để AI điền các trường từ lớp văn bản: bật **riêng** `Extraction__UseAI=true` trên AIOCR và cấp cấu hình AzureOpenAI như PDF-001. PDF có chữ đi thẳng từ PdfPig đến AI, không cần Document Intelligence. Lỗi AI giữ lại văn bản với cảnh báo để nhập thủ công. Không gọi AI nếu một trang thiếu lớp văn bản theo phép kiểm tra sơ bộ.

OCR chỉ được gọi nếu phát hiện trang thiếu chữ và `Extraction__OcrFallbackEnabled=true`, đồng thời đã cấp cấu hình DocumentIntelligence. Không tự bật dịch vụ tính phí. Nhánh OCR hiện đọc lại toàn bộ tài liệu để giữ ngữ cảnh; chưa tối ưu chi phí theo từng trang. Nếu cả OCR và AI tắt thì không có HTTP ra nhà cung cấp.

Docker profile `ai` chạy AIOCR cho cả đọc chữ trực tiếp, dù không dùng AI. Trong docker/.env: `PROCEDURE_EXTRACTION_ENABLED=true`, `PROCEDURE_USE_AI=false`, `PROCEDURE_OCR_FALLBACK_ENABLED=false`, cấp `AIOCR_SERVICE_KEY`. Chỉ luồng lưu bản nháp cần `AZURE_BLOB_CONNECTION_STRING`. Không tự build/chạy Docker trong task này.

## Giới hạn và chất lượng

- PdfPig 0.1.16 + ContentOrderTextExtractor đọc lớp văn bản, thêm dấu phân trang; chuẩn hóa Unicode NFC.
- Tối đa 20 MiB, 100 trang, 100.000 ký tự (tính cả dấu phân trang). Quá giới hạn trả lỗi, không âm thầm cắt tài liệu. Parse PDF thực hiện trong process, cancellation được kiểm tra giữa các trang, không phải hard isolation cho parser bên trong một trang.
- Trang có dưới 20 chữ/số, ký tự thay thế hoặc control bất thường được đánh dấu cần kiểm tra. PDF scan/trang trắng sẽ không được coi là có đủ chữ chỉ vì trang khác có văn bản. Các phép kiểm tra này là heuristic, không chứng minh đầy đủ/đúng tuyệt đối.
- Trang có ảnh luôn có cảnh báo vì chữ nằm trong ảnh không được reader này đọc. Logo/ảnh có thể không chứa chữ; cán bộ tự đối chiếu. Thứ tự dòng, nhiều cột, bảng biểu và font đặc biệt có thể cần chỉnh lại.
- PDF hỏng/khóa/không được hỗ trợ trả lỗi có kiểm soát. Chưa triển khai nhập mật khẩu PDF.
- Văn bản và payload do AI tạo vẫn là dữ liệu không tin cậy, FE không render HTML trực tiếp. Luôn đối soát trước xuất bản.

Đã smoke test local bằng file mẫu “Đăng ký tạm trú” người dùng cung cấp: 13 trang, đọc 21.367 ký tự, tìm đúng tiêu đề có dấu và mã `1.116789`; không gọi OCR/AI. Không đưa PDF gốc hoặc nội dung trích xuất đầy đủ vào source. Chưa deploy Azure, chưa xác minh AI cloud thật.

Thư viện: [PdfPig](https://github.com/UglyToad/PdfPig), [NuGet PdfPig 0.1.16](https://www.nuget.org/packages/PdfPig/0.1.16).
