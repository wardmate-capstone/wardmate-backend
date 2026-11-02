# Runbook Azure cho Antigravity — kiểm tra và deploy WardMate

Người viết: Backend (Claude), 01/11/2026. Quy trình dưới đây là quy trình đã chạy thành công ngày 01/11/2026 để đưa 5 service lên bản mới mà không lỗi; chi tiết lần chạy đó ở mục `IMAGE-SYNC-1101` trong `PROGRESS.md`. Mỗi lần deploy, Anti làm đúng thứ tự trong runbook này.

> **Không bao giờ in secret.** Mọi lệnh `az containerapp show/update` phải có `--query ...` để giới hạn output. Output đầy đủ (JSON) in cả giá trị các biến môi trường **không** phải secret, trong đó có connection string. Không dán output đầy đủ vào chat, PROGRESS hay ảnh chụp.

---

## 1. Hiện trạng (01/11/2026, sau IMAGE-SYNC-1101)

| Container App | ACR repository | Image đang chạy | Dockerfile (tính từ gốc repo) | Health qua gateway |
|---|---|---|---|---|
| wardmate-iam | wardmate-iam | iam-20261030-01 | src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Dockerfile | /api/iam/health/ready |
| wardmate-procedure-catalog | wardmate-procedure-catalog | catalog-20261101-01 | src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Dockerfile | /api/procedure-catalog/health/ready |
| wardmate-application-workflow | wardmate-application-workflow | workflow-20261101-01 | src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Dockerfile | /api/application-workflow/health/ready |
| wardmate-documentform | wardmate-documentform | documentform-20261101-01 | src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Dockerfile | /api/document-form/health/ready |
| wardmate-aiocr | wardmate-aiocr | aiocr-20261101-01 | src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Dockerfile | /api/ai-ocr/health/ready |
| wardmate-analytics-system | wardmate-analyticssystem | analytics-20261101-01 | src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Dockerfile | /api/analytics-system/health/ready |
| wardmate-gateway | wardmate-gateway | gateway-20261030-02 | src/Gateways/WardMate.YarpGateway/Dockerfile | chỉ có /health |
| wardmate-gotenberg | (Docker Hub) | gotenberg/gotenberg:8 | — | — |

- Resource group: `rg-wardmate-prod`. ACR: `acrwardmate2026`.
- Gateway: `https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io`.
- `minReplicas`: Analytics = 1 (để worker thông báo chạy liên tục); các app còn lại = 0, nên lần gọi đầu khởi động lạnh mất vài giây.
- **Analytics giữ `maxReplicas` = 1**, vì job thống kê theo lịch chạy trên từng replica.

**Migration mới nhất trong code** (DB cloud phải có đủ trước khi deploy image mới):

| Service | Migration mới nhất | AutoMigrate mặc định | Script SQL |
|---|---|---|---|
| IAM | 20261101060000_WorkflowCalendarPermission | true | docs/sql/iam-*.sql |
| Workflow | 20261101055515_CalendarClosures | false | docs/sql/workflow-*.sql |
| DocumentForm | 20261015170329_AddSubmissionIdToGeneratedDocuments | true | — |
| ProcedureCatalog | 20261006145135_AlignCategoryIdentitySequence | (không có cấu hình) | — |
| AIOCR | 20261030083635_AiRequestLogs | false | docs/sql/aiocr-*.sql (`aiocr-full-setup.sql` cho DB mới) |
| Analytics (Notifications) | 20261024020118_NotificationDeliveryAdministration | — | docs/sql/notification-*.sql |
| Analytics (Analytics) | 20261021031538_AnalyticsMetricsAndCitizenFeedback | — | — |

---

## 2. Trước khi deploy: xác định service nào cần deploy lại

1. **Liệt kê image đang chạy:**
   ```bash
   az containerapp list -g rg-wardmate-prod --query "[].{app:name, image:properties.template.containers[0].image, min:properties.template.scale.minReplicas}" -o table
   ```
2. **Kiểm tra `/health/ready` của cả 6 service.** Lệnh gọi 2 lượt: lượt đầu để đánh thức app đang ở 0 replica, lượt sau mới lấy kết quả.
   ```bash
   G=https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io; for s in iam procedure-catalog application-workflow document-form ai-ocr analytics-system; do curl -s -o /dev/null --max-time 60 $G/api/$s/health/ready; done; for s in iam procedure-catalog application-workflow document-form ai-ocr analytics-system; do printf "%-22s %s\n" "$s" "$(curl -s -o /dev/null --max-time 60 -w '%{http_code}' $G/api/$s/health/ready)"; done
   ```
   Cách đọc kết quả:
   - **`200`**: OK.
   - **`503` có JSON**: phụ thuộc lỗi (database hoặc rabbitmq). Xem body bằng `curl -s <url>`.
   - **`503` rỗng**: app đang khởi động lạnh. Chạy lại.
   - **`404` với header `server: Kestrel`**: image build trước 30/10, chưa có HEALTH-01. **Phải deploy lại.**
3. **Có thay đổi code nào sau ngày trong tag image không:** đọc các mục mới trong `PROGRESS.md`. Mục nào sửa file trong `src/Services/<Service>` hoặc `src/BuildingBlocks` thì service đó cần deploy lại.

---

## 3. Deploy một service (lặp lại cho từng service, mỗi lần một service)

Chạy trên **Azure Cloud Shell (Bash)**. Cloud Shell không có Docker, nên build bằng `az acr build`. ACR Tasks tính phí theo giây build, khoảng 1–2 phút mỗi image.

**Bước 0. Lấy code mới nhất.** Luôn clone lại, không dùng bản clone cũ:
```bash
cd ~ && rm -rf wardmate-backend && git clone --branch kha --depth 1 https://github.com/wardmate-capstone/wardmate-backend.git && cd wardmate-backend
```

**Bước 1. Xác nhận bản clone có đủ tính năng sắp deploy.** Grep đúng file hoặc tên mới của lần thay đổi đó. Ví dụ đã dùng ngày 01/11:
```bash
grep -l "MapWardMateHealth" src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Program.cs; ls src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Controllers/ | grep -i calendar
```
Thiếu file nào thì **dừng lại**: code chưa được push lên `kha`.

**Bước 2. Đặt biến cho service cần deploy.** Ví dụ với Workflow (giá trị cho các service khác lấy từ bảng ở mục 1):
```bash
APP=wardmate-application-workflow; REPO=wardmate-application-workflow; TAG=workflow-20261101-02; DF=src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Dockerfile; HP=application-workflow
```

**Bước 3. Lưu image cũ để có thể quay lại:**
```bash
OLD=$(az containerapp show -n $APP -g rg-wardmate-prod --query "properties.template.containers[0].image" -o tsv); echo "$OLD"
```

**Bước 4. Build và push.** Phải thấy dòng `Run ID: ... was successful`, và ghi lại digest:
```bash
az acr build -r acrwardmate2026 -t "$REPO:$TAG" -f "$DF" .
```

**Bước 5. Đổi image.** Lệnh chỉ đổi image, giữ nguyên biến môi trường và secret; kết quả phải là `Succeeded`:
```bash
az containerapp update -n $APP -g rg-wardmate-prod --image "acrwardmate2026.azurecr.io/$REPO:$TAG" --query "properties.provisioningState" -o tsv
```

**Bước 6. Kiểm tra.** Đợi khoảng 30 giây rồi chạy:
```bash
az containerapp revision list -n $APP -g rg-wardmate-prod --query "[].{name:name, active:properties.active, health:properties.healthState, state:properties.runningState}" -o table; U=https://wardmate-gateway.blackmeadow-a2f12767.japaneast.azurecontainerapps.io/api/$HP/health/ready; curl -s -o /dev/null --max-time 60 $U; curl -s --max-time 60 -w "\n%{http_code}\n" $U
```
Kỳ vọng:
- Revision `Healthy` và `Running`.
- `/health/ready` trả `200`; mọi check `Healthy`. Riêng Workflow và Analytics, `rabbitmq` phải là `connected`.

**Bước 7. Quay lại bản cũ** nếu bước 6 lỗi quá 1 phút:
```bash
az containerapp update -n $APP -g rg-wardmate-prod --image "$OLD" --query "properties.provisioningState" -o tsv
```

**Bước 8. Ghi vào `PROGRESS.md`:** app, tag mới, digest, image cũ, kết quả bước 6. Không ghi secret.

**Thứ tự khi deploy nhiều service:** Workflow → DocumentForm → ProcedureCatalog → AIOCR → Analytics → IAM → Gateway. Deploy xong service nào thì kiểm tra service đó trước khi chuyển sang service kế tiếp.

---

## 4. Khi có migration

1. Đọc mục PROGRESS của task để biết migration và script SQL tương ứng.
2. Mở Supabase SQL Editor của **đúng DB**:
   - Workflow và DocumentForm dùng chung `wardmate-db`.
   - AIOCR: connection string `AiOcrDatabase` đang trỏ cùng `wardmate-db`.
   - IAM có DB riêng.
3. Đối chiếu bảng `__EFMigrationsHistory` để biết migration nào đã chạy. Không insert history bằng tay để "cho qua".
4. Chạy script trong `docs/sql/`. Script idempotent, tự dừng nếu chọn nhầm DB.
5. **Chạy SQL trước, deploy image sau.**

---

## 5. Lỗi hay gặp và cách xử lý

| Hiện tượng | Nguyên nhân | Xử lý |
|---|---|---|
| Image mới thiếu tính năng hoặc thiếu `/health/ready` | Build từ code cũ (bản clone cũ, nhánh khác, máy chưa pull). Ngày 01/11 image `calendar-20261101-01` gặp lỗi này | Luôn làm bước 0 và bước 1 |
| `az containerapp logs show` báo `KeyError: 'eventStreamEndpoint'` | Lỗi của Azure CLI trong Cloud Shell | Xem log trên Portal: App → Monitoring → **Log stream** |
| `503` không có body ngay sau deploy | Khởi động lạnh (`minReplicas=0`) | Gọi lại sau 10–30 giây |
| `/health/ready` báo `rabbitmq` `disconnected` kéo dài | Sai biến `RabbitMq__*`, hoặc CloudAMQP Free hết số kết nối | Kiểm tra biến (tên ở mục 6); xem tab Connections trên CloudAMQP |
| Thông báo không xuất hiện | Analytics tắt worker hoặc về 0 replica | `Notifications__WorkersEnabled=true`, `minReplicas=1` |
| `az acr build` từ URL GitHub lấy nhầm code | URL không ghi đúng nhánh hoặc commit | Build từ bản clone (bước 0), không build từ URL |
| Chat hoặc AI trả `503 chat.database_unavailable` | DB AIOCR thiếu bảng | Chạy `docs/sql/aiocr-full-setup.sql` trên DB mà `AiOcrDatabase` trỏ tới |

---

## 6. Biến môi trường từng service (chỉ tên, không ghi giá trị)

- **Chung (6 service):**
  - `Jwt__Key` (secretref `jwt-key`)
  - `Jwt__Issuer=WardMate.IAM`, `Jwt__Audience=WardMate.Client` (khác mặc định trong code)
  - `Iam__BaseUrl`
  - Tùy chọn: `Iam__AccessContextCacheSeconds` (mặc định 30)
- **Workflow:**
  - `ConnectionStrings__WorkflowDatabase`
  - `RabbitMq__Enabled/Host/Port/User/Password/VirtualHost/UseTls`
  - `Analytics__ServiceKey`
  - Base URL của ProcedureCatalog
- **DocumentForm:**
  - Connection string của DocumentForm
  - `Services__ApplicationWorkflow__BaseUrl`
  - `Gotenberg__Url`
  - Blob storage
- **AIOCR:**
  - `ConnectionStrings__AiOcrDatabase`
  - `Assistant__Enabled=true`
  - `Gemini__Key`, `Gemini__Model`
  - `Services__ApplicationWorkflow__BaseUrl`, `Services__DocumentForm__BaseUrl`, `Services__ProcedureCatalog__BaseUrl`
  - Tùy chọn: `Assistant__DailyRequestsPerUser`, `Assistant__DailyRequestsTotal`, `Assistant__DailyGuestRequestsTotal`, `Assistant__DailyGuestRequestsPerIp`, `Assistant__GuestRequestsPerMinute`, `Gemini__MaxOutputTokens`
- **Analytics:**
  - `ConnectionStrings__NotificationsDatabase`
  - `Notifications__WorkersEnabled=true`
  - `Notifications__ServiceKey`
  - `RabbitMq__*`
  - `Workflow__BaseUrl`
  - `Analytics__ServiceKey`, `Analytics__JobsEnabled`, `Analytics__Cron`, `Analytics__LookbackDays`
  - Tùy chọn: `Email__*`
- **IAM:**
  - Connection string
  - `Email__Enabled/Host/Port/Username/Password/From/DisplayName`
  - `Notifications__ServiceKey`
  - **Không còn `Sms__*`**: SMS đã gỡ khỏi code, xóa các biến này nếu còn.

---

## 7. Việc vận hành còn mở (Anti)

1. **Đổi JWT key** cho cả 6 service cùng lúc (người dùng phải đăng nhập lại). Key cũ đã từng ghi rõ trong `PROGRESS.md`, nay đã xóa khỏi file nhưng vẫn còn trong lịch sử Git.
2. **Thu hồi và tạo lại mật khẩu ứng dụng Gmail** cho `Email__Password` (cùng lý do), lưu dạng secret.
3. **Chuyển các biến đang để dạng thường sang secret:**
   - `Notifications__ServiceKey`, `Analytics__ServiceKey`, các `ConnectionStrings__*`.
   - Đổi `Analytics__ServiceKey` cùng lúc ở Workflow và Analytics (giá trị này đã lộ trong ảnh chụp ngày 30/10).
4. **Gotenberg** đang mở ingress public: chủ dự án đã chấp nhận, để sau.
5. **Chưa xác minh trực tiếp** Gateway `gateway-20261030-02` có `UseForwardedHeaders` (AI-GUEST-01). Nếu deploy lại Gateway thì làm theo mục 3.
