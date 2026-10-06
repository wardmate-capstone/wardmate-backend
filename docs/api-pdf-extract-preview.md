# Hướng dẫn API đọc thử PDF thủ tục — WardMate

Cập nhật: 04/10/2026 (Asia/Saigon). Đối tượng: Frontend, QA và Procedure Manager.

## 1. API dùng để làm gì?

API nhận một PDF thủ tục, đọc lớp văn bản và trả nội dung để cán bộ kiểm tra trước khi nhập vào hệ thống. Khi bật AI và có cấu hình phù hợp, hệ thống có thể đề xuất dữ liệu thủ tục có cấu trúc.

**Đọc thử không lưu PDF vào Blob, không tạo bản nháp và không xuất bản thủ tục.** HTTP 200 không có nghĩa mọi trường nghiệp vụ đã được điền hoặc PDF đã được đọc đầy đủ; luôn kiểm tra warnings.

## 2. Endpoint và quyền truy cập

| Thuộc tính | Giá trị |
| --- | --- |
| Method | POST |
| Route | `/api/v1/procedure-manager/drafts/extract-preview` |
| Local trực tiếp | `http://localhost:5002/api/v1/procedure-manager/drafts/extract-preview` |
| Local qua Gateway | `http://localhost:5000/api/v1/procedure-manager/drafts/extract-preview` |
| Swagger Procedure | `http://localhost:5002/swagger` (Development) |
| Authentication | `Authorization: Bearer <access_token>` |
| Role được phép | `PROCEDURE_MANAGER` hoặc `IT_ADMIN` |
| Content-Type | `multipart/form-data` với boundary do HTTP client tạo |
| Response | JSON; `Cache-Control: no-store` |

URL deploy dùng domain Procedure/Gateway thực tế do đội vận hành cung cấp; không dùng domain IAM cho route này. Tài liệu không xác nhận phiên bản đã có trên Azure.

## 3. Điều kiện để gọi được

Procedure Catalog và AIOCR phải đang chạy. Procedure cần cấu hình database/JWT hiện có; API preview không ghi database và không cần cấu hình Azure Blob.

| Service | Biến môi trường | Giá trị cho chế độ chỉ đọc chữ |
| --- | --- | --- |
| AIOCR | `Extraction__Enabled` | `true` |
| AIOCR | `Extraction__UseAI` | `false` |
| AIOCR | `Extraction__OcrFallbackEnabled` | `false` |
| AIOCR | `ServiceAuthentication__Key` | Secret ngẫu nhiên tối thiểu 32 ký tự |
| Procedure | `ProcedureDrafts__AiOcrUrl` | `http://localhost:5005` khi chạy hai service trên máy |
| Procedure | `ProcedureDrafts__ServiceKey` | Cùng secret với AIOCR |

Service key chỉ nằm ở backend, không gửi cho FE. Trong Docker, dùng địa chỉ nội bộ `http://ai-ocr:8080`, không dùng localhost để gọi container khác. Khởi động lại service khi đổi cấu hình. Preview không phụ thuộc cờ hàng đợi `ProcedureDrafts__ExtractionEnabled`.

## 4. Request

| Trường form-data | Kiểu | Bắt buộc | Quy định |
| --- | --- | --- | --- |
| `file` | File binary | Có | Tên kết thúc bằng .pdf; chữ ký đầu file PDF hợp lệ; từ 5 byte đến 20 MiB |

Reader hỗ trợ tối đa 100 trang và 100.000 ký tự tính cả dấu phân trang. Không gửi JSON hoặc base64 thay cho trường file. PDF có thể copy chữ phù hợp nhất; PDF scan/trang thiếu chữ trả cảnh báo khi OCR tắt. PDF hỏng, khóa hoặc quá giới hạn có thể trả lỗi.

## 5. Gọi bằng Swagger

1. Đăng nhập IAM bằng tài khoản PROCEDURE_MANAGER hoặc IT_ADMIN, lấy accessToken từ response. Nếu gọi login thủ công, dùng header `X-CSRF-Protection: 1` theo hợp đồng IAM.
2. Mở Swagger Procedure tại cổng 5002.
3. Bấm Authorize, nhập access token theo Bearer scheme của Swagger.
4. Mở nhóm ProcedureDrafts, chọn POST `/api/v1/procedure-manager/drafts/extract-preview`.
5. Bấm Try it out, chọn file PDF ở trường file và bấm Execute.
6. Đọc extractedText và warnings. Khi AI tắt, payload là khung dữ liệu chưa điền — đây là hành vi đúng.

Không dùng refresh token hoặc refresh cookie để xác thực API Procedure. API này không yêu cầu header CSRF của IAM vì dùng Bearer token.

## 6. Gọi bằng Postman hoặc curl

Postman: chọn POST và URL ở mục 2 → Authorization: Bearer Token → Body: form-data → thêm key `file`, đổi loại từ Text sang File → chọn PDF → Send. Để Postman tự tạo Content-Type và boundary.

Ví dụ PowerShell với curl.exe (thay hai giá trị mẫu):

```powershell
$accessToken = '<access-token-tu-IAM>'
$pdfPath = 'C:\duong-dan\thu-tuc.pdf'
curl.exe --request POST 'http://localhost:5002/api/v1/procedure-manager/drafts/extract-preview' `
  --header "Authorization: Bearer $accessToken" `
  --form "file=@$pdfPath;type=application/pdf"
```

Không ghi token thật vào source, tài liệu hoặc log chia sẻ.

## 7. Response thành công

Ví dụ rút gọn về nội dung văn bản, nhưng giữ cấu trúc payload của chế độ không dùng AI:

```json
{
  "payload": {
    "categoryId": 0,
    "procedureCode": "",
    "title": "",
    "levelOfImplementation": "",
    "targetAudience": "",
    "feeSummary": "",
    "processingTimeSummary": "",
    "contentPayload": {
      "decisionNumber": "",
      "receivingAddress": "",
      "cases": [],
      "submissionMethods": [],
      "legalReferences": [],
      "results": []
    },
    "checklistSchema": [],
    "formDefinitions": []
  },
  "extractedText": "--- Trang 1 ---\nCHI TIẾT THỦ TỤC HÀNH CHÍNH\nTên thủ tục Đăng ký tạm trú...",
  "warnings": [
    "Đã đọc lớp văn bản PDF trực tiếp, không dùng OCR. Kiểm tra thứ tự dòng và bảng biểu với bản gốc.",
    "Chưa bật AI: văn bản đã được trích xuất, dữ liệu thủ tục cần nhập và đối soát thủ công."
  ]
}
```

| Trường | Ý nghĩa / cách FE sử dụng |
| --- | --- |
| `extractedText` | Chữ đã đọc, có dấu phân trang. Hiển thị dạng text, giữ xuống dòng |
| `payload` | Khung nhập tay hoặc đề xuất AI; không coi là dữ liệu đã được duyệt |
| `warnings` | Cảnh báo thiếu chữ, ảnh, thứ tự bảng hoặc AI chưa bật/lỗi; hiển thị cho cán bộ |

Không có draftId trong response vì chưa tạo bản nháp. Không render HTML trực tiếp từ extractedText/payload. Nếu AI không hoạt động nhưng đọc chữ thành công, vẫn có thể trả 200 cùng cảnh báo và khung nhập tay.

## 8. Ví dụ tích hợp Frontend

```javascript
async function previewProcedurePdf(baseUrl, file, accessToken, signal) {
  const form = new FormData();
  form.append('file', file);

  const response = await fetch(
    `${baseUrl}/api/v1/procedure-manager/drafts/extract-preview`,
    {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}` },
      body: form,
      signal
    }
  );

  // Ingress có thể trả lỗi không phải JSON.
  const raw = await response.text();
  let result;
  try { result = JSON.parse(raw); } catch { result = null; }

  if (!response.ok) {
    const error = new Error(result?.title ?? `Yêu cầu thất bại (HTTP ${response.status}).`);
    error.status = response.status;
    error.code = result?.code;
    error.traceId = result?.traceId;
    throw error;
  }
  return result;
}
```

Không tự đặt header Content-Type: trình duyệt phải thêm boundary cho FormData. Không cần gửi refresh cookie đến Procedure. Nếu FE khác origin, dùng Gateway/cấu hình CORS được đội vận hành cho phép; tài liệu này không tự thêm CORS cho môi trường deploy.

Trong lúc chờ, hiển thị trạng thái đang đọc và khóa nút gửi trùng. Không tự retry vô hạn: khi AI/OCR được bật, gọi lại có thể phát sinh phí. Khi response 200, hiển thị warnings trước khi cho cán bộ dùng payload.

## 9. Mã lỗi và cách xử lý

| HTTP | code thường gặp | Xử lý |
| --- | --- | --- |
| 400 | `draft.invalid_pdf` | Kiểm tra trường file, phần mở rộng, chữ ký PDF và dung lượng |
| 401 | `auth.unauthorized` | Access token thiếu/sai/hết hạn; xử lý đăng nhập/refresh qua IAM |
| 403 | `auth.forbidden` | Tài khoản không có role quản lý yêu cầu |
| 413 | Tùy server/ingress | Giảm kích thước file; lỗi có thể không theo ProblemDetails |
| 502 | `draft.extraction_failed` | Kiểm tra AIOCR đang chạy, service key khớp, Extraction:Enabled; PDF không khóa/hỏng/quá giới hạn |
| 503 | `draft.extraction_not_configured` | Procedure thiếu địa chỉ hoặc khóa kết nối AIOCR hợp lệ |
| 504 | `draft.extraction_timeout` | Xử lý vượt thời gian; kiểm tra service/file trước khi thử lại |
| 500 | Có thể không có code nghiệp vụ | Gửi traceId cho backend kiểm tra log |

Lỗi non-2xx từ AIOCR hiện được Procedure gom thành 502, nên 502 cũng có thể là cấu hình AIOCR chưa bật hoặc khóa nội bộ không khớp; không chỉ là file lỗi.

Ví dụ ProblemDetails:

```json
{
  "title": "File không có định dạng PDF.",
  "status": 400,
  "instance": "/api/v1/procedure-manager/drafts/extract-preview",
  "code": "draft.invalid_pdf",
  "traceId": "ma-truy-vet-minh-hoa"
}
```

Các trường tùy chọn như errors/type có thể có hoặc không. FE nên xử lý theo HTTP status và code, không so khớp nguyên câu thông báo.

## 10. Sau khi đọc thử, đưa thủ tục vào hệ thống thế nào?

Preview chỉ giúp đánh giá nội dung. Muốn lưu PDF gốc và quản lý đối soát, dùng luồng riêng:

| Bước | API | Kết quả |
| --- | --- | --- |
| Upload để lưu | POST `/api/v1/procedure-manager/drafts`, multipart file | 202, draftId trong trường id; cần Blob |
| Chờ đọc xong | GET `/api/v1/procedure-manager/drafts/{id}` | 200, status/payload/extractedText/warnings/revision |
| Xem PDF nguồn | GET `/api/v1/procedure-manager/drafts/{id}/source` | 200, link xem có thời hạn |
| Lưu chỉnh sửa | PUT `/api/v1/procedure-manager/drafts/{id}`, `{revision,payload}` | 200, revision mới |
| Xác nhận | POST `/api/v1/procedure-manager/drafts/{id}/publish`, `{revision,confirmed:true}` | 200, ProcedureDetailDto |

Để tự đọc sau upload, cần bật hàng đợi ProcedureDrafts:ExtractionEnabled và chạy AIOCR. Không bật thì bản nháp được nhập thủ công. Không tự xuất bản payload đọc thử; kiểm tra đầy đủ danh mục, tên/mã thủ tục, giấy tờ và các trường bắt buộc. 409 revision conflict yêu cầu tải lại trước khi lưu. Chi tiết: [luồng bản nháp PDF](procedure-pdf-drafts.md).

## 11. Checklist kiểm thử

- Manager/IT_ADMIN + PDF có chữ: 200, extractedText có nội dung và warnings phù hợp.
- AI tắt: payload chưa điền, không kỳ vọng tự tạo thủ tục.
- PDF scan hoặc trang thiếu chữ: cảnh báo yêu cầu đối soát/OCR; không coi 200 là đủ dữ liệu.
- Không token: 401; token Citizen: 403.
- File sai định dạng: 400; PDF hỏng/khóa/quá số trang: lỗi đọc, thường 502.
- AIOCR chưa chạy/sai key: lỗi có kiểm soát, kiểm tra mục 9.
- Preview không tạo bản ghi hay lưu file Blob.

Muốn kiểm tra riêng reader mà không chạy IAM/Procedure/Blob, sau build Release dùng:

```powershell
pwsh -NoProfile -File scripts/verify-pdf-text.ps1 -PdfPath 'C:\duong-dan\thu-tuc.pdf'
```

Script chạy AIOCR tạm, tắt cloud AI/OCR và dừng sau khi kiểm tra. Hướng dẫn kỹ thuật bổ sung: [đọc PDF trực tiếp](procedure-pdf-text.md).
