# Bàn giao Backend → Frontend: việc FE cần sửa sau đợt test KB-00 → KB-04 (05/11/2026)

Người viết: Backend (Claude), 05/11/2026.

Nguồn: kết quả chủ dự án test trên Azure ngày 02/11 (KB-00 → KB-03) và 05/11 (KB-04). Chi tiết từng bước nằm ở `docs/ui-test-scenarios.md`, mục 3 (cột KQ), 6.1 và 6.2. Hợp đồng API đầy đủ xem `docs/api-guide.md`.

Đây là bản bàn giao FE hiện hành; các bản 31/10 và 01/11 đã gỡ ngày 09/11/2026, nội dung hợp đồng API vẫn ở `docs/api-guide.md`.

## 1. Tóm tắt

| KB | Nội dung | Đạt | Việc của FE |
|---|---|---|---|
| KB-00 | Hạ tầng | 3/3 | — |
| KB-01 | Khách | 6/8 | Bước 6 thiếu ô chọn phường; bước 8 hoãn |
| KB-02 | Tài khoản, phiên | 13/15 | Bước 9 là tùy chọn; bước 10 chưa test được |
| KB-03 | Quản trị IT_ADMIN | 20/24 | 4 API phụ chưa dùng |
| KB-04 | Procedure Manager | 22/25 | Không liên kết được mẫu đơn; bước 22 không phát sinh |
| KB-05 | Knowledge Base (07/11) | 11/13 | Bước 7 lỗi BE (đã sửa source); bước 13 chưa có màn thống kê |

Đến KB-04 không bước nào lỗi do BE. KB-05 bước 7 là lỗi BE, đã sửa trong source và đang chờ deploy AIOCR. Các mục dưới đây là việc FE còn thiếu hoặc nên chỉnh.

**Thứ tự làm đề xuất:**
1. Ưu tiên cao: FE-01, FE-07, FE-05.
2. Trung bình: FE-02, FE-04, FE-09.
3. Thấp: FE-03, FE-06, FE-08, FE-10.

**FE-01 phải xong trước khi test KB-06** (công dân nộp hồ sơ).

## 2. Ưu tiên cao

### FE-01: Thêm ô chọn phường khi tạo hồ sơ (KB-01 #6)
- **Hiện trạng:** form tạo hồ sơ không có ô chọn phường. BE bắt buộc trường này, nên **công dân không tạo được hồ sơ**.
- **Cần làm:**
  - Thêm ô chọn **phường/xã tiếp nhận**, lấy danh sách từ `GET /api/v1/wards`. API này không cần đăng nhập.
  - Gửi `wardCode` trong body `POST /api/v1/applications`.
- **Lỗi cần hiển thị:**
  - Thiếu phường → 400 "Phải chọn phường/xã tiếp nhận.".
  - Mã phường sai → 400 `application.invalid_ward`.
  - IAM không phản hồi → 503 `application.iam_unavailable`, cho người dùng thử lại.
  - Nộp hồ sơ cũ chưa có phường → 409 `application.ward_required`. Cho người dùng chọn phường rồi nộp lại.
- **Kiểm thử:**
  - Tạo hồ sơ khi chưa chọn phường → bị chặn, có thông báo.
  - Chọn phường A → 201.

### FE-07: Liên kết mẫu đơn ở màn sửa thủ tục nhập tay (KB-04 #7, #12)
- **Hiện trạng:** màn checklist có danh sách mẫu, nhưng không lưu được liên kết. BE không lưu liên kết mẫu trong checklist. Liên kết nằm ở mảng `formDefinitions` của thủ tục.
- **Cần làm:** thêm phần **"Biểu mẫu, tờ khai"** vào màn sửa thủ tục.
  - Chọn mẫu từ `GET /api/v1/procedure-manager/document-forms`. API trả `id`, `formCode`, `formName`, `formType`.
  - Mỗi mẫu đã chọn là một phần tử `formDefinitions`:
    ```json
    { "formTemplateId": "<id>", "formCode": "<formCode>", "formName": "<formName>",
      "formType": "DOCX_TEMPLATE", "quantity": 1, "isMandatory": true, "caseCode": null }
    ```
  - Lưu bằng `PUT /api/v1/procedure-manager/procedures/{id}`, kèm `decisionNumber` và `effectiveDate`.
- **Lưu ý quan trọng:**
  - `PUT /procedures/{id}` **ghi đè toàn bộ thủ tục**, gồm cả `checklistSchema` và `formDefinitions`. FE phải lấy chi tiết hiện tại, sửa phần cần đổi rồi gửi **đầy đủ** object. Nếu gửi thiếu `checklistSchema`, checklist sẽ bị xóa.
  - `PUT .../checklist` chỉ lưu danh sách giấy tờ. Không gửi mẫu đơn qua API này.
  - Không được trùng `formCode` trong cùng một trường hợp (`caseCode`). Nếu trùng, BE trả 400.
- **Kiểm thử:**
  - Liên kết mẫu `KB04 Tờ khai` vào thủ tục `KB04-KS` → mở lại chi tiết thấy `formDefinitions` có mẫu đó.
  - Checklist vẫn đủ 2 mục.
- **Tạm thời:** KB-06 dùng thủ tục `KB04-AI` vì thủ tục này đã có mẫu.

### FE-05: Tự làm mới token sau 15 phút (KB-02 #10, chưa test)
- **Cần test:**
  1. Đăng nhập và bật *Preserve log* trong DevTools.
  2. Để yên 16 phút (access token sống 15 phút).
  3. Bấm sang một trang có gọi API.
  4. Kỳ vọng: API trả 401 → FE tự gọi `POST /api/v1/auth/refresh-token` → 200 → gọi lại API thành công, người dùng **không bị đẩy ra màn đăng nhập**.
- **Cookie:** cookie `refreshToken` có HttpOnly, Path `/api/v1/auth` và mặc định **`SameSite=Strict`**.
  - Nếu FE chạy ở domain khác gateway, trình duyệt có thể không gửi cookie, nên refresh trả 401.
  - Khi đó báo BE/Anti để đặt `RefreshCookie__SameSite=None` trên IAM, hoặc để FE gọi qua cùng domain.
  - Request refresh phải bật `credentials: "include"` (với axios là `withCredentials: true`).
- **Nếu lỗi:** người dùng sẽ bị đăng xuất sau mỗi 15 phút.

## 3. Ưu tiên trung bình

### FE-02: Chatbot bị trình duyệt hủy request (KB-01 #8)
- **Hiện tượng:** hỏi liên tiếp thì request bị `(canceled)`.
- **Cần kiểm tra:**
  - FE có gửi câu mới khi câu trước chưa xong không.
  - Có dùng AbortController hủy request cũ không.
  - Timeout phía FE có quá ngắn không. Câu trả lời AI có thể mất 10–60 giây.
- **Khóa ô nhập** trong lúc chờ câu trả lời.
- **Lỗi 429 cần hiển thị:**
  - `ai.guest_quota_exceeded`: khách đã hết 10 câu/ngày/IP. Hiện lời mời đăng nhập, không hiện lỗi kỹ thuật.
  - `ai.guest_limit_reached`: toàn hệ thống đã hết lượt cho khách trong ngày.
- **Ghi chú:** bộ đếm theo IP nằm trong bộ nhớ AIOCR, nên reset khi service khởi động lại. Khi test, đừng coi việc này là lỗi FE.

### FE-04: Thông báo trung tính ở màn Quên mật khẩu (KB-02 #13)
- BE **luôn trả 202**, dù email có trong hệ thống hay không. Cách này chống dò tài khoản.
- **Cần làm:**
  - Hiện nguyên thông báo trung tính của BE ("Nếu tài khoản hợp lệ…, mã OTP sẽ được gửi qua Email…").
  - **Không** hiện "Đã gửi mã thành công". Không đoán email có tồn tại hay không.
- **Thêm gợi ý cho người dùng:**
  - Kiểm tra cả thư mục Spam.
  - Chờ **60 giây** mới xin lại mã. Xin sớm hơn vẫn nhận 202 nhưng không có thư mới.
  - Mã hết hạn sau **5 phút**.
  - Nhập sai **5 lần** thì phải xin mã mới.
- **Nên:** khóa nút "Gửi lại mã" trong 60 giây.

### FE-09: Tạo phôi và tải DOCX ngay trên màn bản nháp AI (góp ý KB-04)
- **Mục tiêu:** cán bộ không phải sang màn mẫu đơn để tạo từng mẫu rồi quay lại.
- **Cách làm:** với mỗi biểu mẫu AI đọc được nhưng chưa có `formTemplateId`, hiện nút **"Tạo phôi và tải DOCX"**. Nút gọi lần lượt:
  1. `POST /api/v1/form-templates`: tạo mẫu, lấy `id`.
  2. `POST /api/v1/form-templates/{id}/upload-docx`: tải file DOCX lên.
  3. Gán `formTemplateId` vào biểu mẫu đó trong bản nháp và lưu bằng `PUT /api/v1/procedure-manager/drafts/{id}`.
- Dùng API sẵn có, **BE không cần sửa**.
- **Cảnh báo mới (BE-04, đã sửa trong source, chờ deploy AIOCR):**
  - Nếu PDF có nhiều tệp `.doc/.docx` hơn số biểu mẫu AI trả về, `warnings` có dòng bắt đầu bằng `formDefinitions: Văn bản nguồn có N tệp mẫu…`.
  - FE nên hiện dòng này **nổi bật** ở phần biểu mẫu để cán bộ bổ sung mẫu còn thiếu.

## 4. Ưu tiên thấp (tùy chọn)

### FE-03: Xóa thông tin cá nhân (KB-02 #9)
- Nếu muốn có chức năng này: nút "Xóa thông tin cá nhân" gọi `DELETE /api/v1/users/me/profile`. Hỏi xác nhận trước khi gọi.
- Sau khi xóa, `profile` trong `GET /api/v1/users/me` là `null`. Lúc đó FE phải hiện form **tạo** (`POST /api/v1/users/me/profile`) thay cho form sửa.

### FE-06: Các API quản trị chưa dùng (KB-03 #7, #8, #12, #14)
- `GET /api/v1/accounts/{userId}`: chi tiết một tài khoản.
- `GET /api/v1/users`: danh sách người dùng.
- `POST /api/v1/users/{userId}/profile`: admin tạo hồ sơ cá nhân cho tài khoản chưa có.
- `DELETE /api/v1/users/{userId}/profile`: admin xóa hồ sơ cá nhân.

Các API này không chặn luồng chính. Chỉ cần làm nếu muốn đủ chức năng quản trị.

### FE-08: Cho xuất bản bản nháp khi chưa chọn mẫu (KB-04 #23)
- FE đang bắt chọn biểu mẫu trước khi xuất bản. BE cho phép `formTemplateId = null`, tức là liên kết mẫu sau.
- **Đề xuất:** cho xuất bản, kèm cảnh báo "Biểu mẫu chưa có file". Bước xác nhận "đã đối soát PDF" (`confirmed: true`) vẫn giữ bắt buộc.

### FE-10: Màn thống kê lượt dùng AI (KB-05 #13)
- `GET /api/v1/ai/knowledge/usage?from=yyyy-MM-dd&to=yyyy-MM-dd`. Mặc định lấy 7 ngày gần nhất.
- Dữ liệu tính theo ngày (giờ UTC+07) và theo loại thao tác, kèm hạn mức đang áp dụng.
- Chỉ PM/ADMIN dùng được. Màn này giúp theo dõi hạn mức Gemini miễn phí.

## 5. Những điểm FE đã làm đúng, giữ nguyên
- **Chặn ngay trên giao diện:**
  - KB-02 #1a: mật khẩu yếu.
  - KB-03 #3a, #4a: dữ liệu sai.
  - KB-03 #20b: vô hiệu hóa chính mình.
  - KB-04 #5a: chỉ cho chọn `.docx`.

  BE vẫn chặn lại các trường hợp này, nên FE chặn sớm là chấp nhận được.
- **Không có nút xóa vai trò hệ thống** (KB-03 #23a). BE vẫn trả 409 nếu bị gọi.
- **Bắt nhập số quyết định và ngày hiệu lực khi sửa thủ tục hoặc rollback** (KB-04 #11, #15). BE bắt buộc hai trường này.
- **Lưu ý về rollback:**
  - Ngày hiệu lực chỉ để ghi nhận. BE không so với ngày của phiên bản trước, và nội dung được áp dụng ngay.
  - FE có thể cảnh báo khi người dùng nhập ngày lùi so với phiên bản đang dùng (góp ý BE-05, chờ chủ dự án quyết).

## 6. Góp ý nghiệp vụ đang chờ chủ dự án quyết (FE chưa cần làm)

Chi tiết ở mục 6.2 của `docs/ui-test-scenarios.md`. Nếu chủ dự án đồng ý, BE sẽ sửa trước và báo lại FE.

| Mã | Nội dung | Ảnh hưởng FE nếu làm |
|---|---|---|
| BE-01 | Cho phép để trống lệ phí, thời hạn xử lý. Tạm thời nhập "Không quy định" hoặc "Không thu phí" | Bỏ dấu bắt buộc ở 2 ô này |
| BE-02 | API tải PDF lên cho thủ tục nhập tay | Thêm nút tải PDF ở màn sửa thủ tục |
| BE-03 | Tự tạo lĩnh vực khi xuất bản nếu nhập tên lĩnh vực mới | Ô lĩnh vực cho phép gõ tên mới |
| BE-05 | Kiểm tra ngày hiệu lực khi rollback | Hiển thị lỗi hoặc cảnh báo mới |

## 7. Kiểm thử lại sau khi sửa
- **FE-01:** chạy lại KB-01 #6, sau đó chạy KB-06.
- **FE-07:** chạy lại KB-04 #7 và #12 với thủ tục `KB04-KS`.
- **FE-05:** chạy KB-02 #10.
- **BE-04:** sau khi Anti deploy AIOCR, chạy lại KB-04 #17 bằng PDF thủ tục đăng ký biến động đất đai. Kỳ vọng đủ 3 biểu mẫu (Mẫu số 25, 01.LPTB, 01.TK-SDDPNN), hoặc có cảnh báo thiếu mẫu.
- **BE-06:** sau khi Anti deploy AIOCR, chạy lại KB-05 #7 (chia đoạn lại) và #5 (sửa **nội dung** tài liệu FAQ). Kỳ vọng nhận 200, không còn 503 `knowledge.database_unavailable`.
- Báo kết quả theo mẫu cũ (`1 Đ`, `2 L - …`) để BE ghi vào `docs/ui-test-scenarios.md`.
