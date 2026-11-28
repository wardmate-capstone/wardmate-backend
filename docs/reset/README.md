# RESET-DATA-1128 — Xóa toàn bộ dữ liệu nghiệp vụ để nhập lại từ đầu

Quyết định của chủ dự án (28/11/2026):
- Chọn cách 2: xóa hết dữ liệu nghiệp vụ.
- Không giữ thủ tục nào.
- Xóa cả tệp trên blob.

**Không hoàn tác được.** Chỉ khôi phục được từ bản sao lưu ở bước 1.

## Phạm vi

| Dịch vụ (Container App) | Xóa | Giữ |
|---|---|---|
| ProcedureCatalog (`wardmate-procedure-catalog`) | Thủ tục, phiên bản, bản nháp, lĩnh vực | Cấu trúc bảng, lịch sử migration |
| DocumentForm (`wardmate-documentform`) | Biểu mẫu và phiên bản mẫu, bản khai, giấy tờ người dân, PDF, QR, tệp theo lần gửi | — |
| ApplicationWorkflow (`wardmate-application-workflow`) | Hồ sơ và mọi dữ liệu con (giấy tờ, lịch sử, phân công, nhận xét, tham vấn, lần gửi), hàng đợi sự kiện | Ca làm, lịch tuần, nghỉ phép và mẫu đơn nghỉ, ngày lễ/ngày đóng cửa |
| AIOCR (`wardmate-aiocr`) | Kho kiến thức chatbot, phiên chat | Nhật ký lượt gọi AI (hạn mức) |
| AnalyticsSystem (`wardmate-analytics-system`) | Thống kê, đánh giá, thông báo | Nhật ký kiểm toán, lịch sử sức khỏe, sao lưu, cấu hình hệ thống, thiết bị nhận push |
| IAM (`wardmate-iam`) | Lĩnh vực đã gán cho cán bộ (đặt về rỗng) | Tài khoản, vai trò, quyền, phường |
| Blob `stwardmate6ll4` | Mọi tệp trong container `procedure-sources` và `form-templates` | `wardmate-files`, `workflow-leave-documents`, `wardmate-backups`; không xóa container |

## 0. Chuẩn bị

- Chọn lúc không ai dùng hệ thống và báo nhóm FE ngừng thử nghiệm.
- Cách làm:
  1. Mở Azure Cloud Shell (bash).
  2. Đăng nhập đúng subscription.
  3. Tải repo nhánh `kha` (hoặc chỉ thư mục `docs/reset`).
  4. Chạy `source docs/reset/pg-env.sh`.
- `pg_names <app>` liệt kê tên chuỗi kết nối của app, chỉ in tên, không in giá trị. Tên dự kiến:

| App | Khóa |
|---|---|
| `wardmate-procedure-catalog` | `ProcedureDatabase` |
| `wardmate-documentform` | `DocumentDb` |
| `wardmate-application-workflow` | `WorkflowDatabase` |
| `wardmate-aiocr` | `AiOcrDatabase` |
| `wardmate-analytics-system` | `AnalyticsDatabase` và `NotificationsDatabase` |
| `wardmate-iam` | `Database` (kiểm bằng `pg_names`) |

- Nếu hai khóa trỏ cùng một DB, chỉ cần sao lưu một lần.
- Không chép chuỗi kết nối ra màn hình, tài liệu hay chat.

## 1. Sao lưu (bắt buộc)

Thực hiện cho từng cặp app/khóa ở trên:

```bash
mkdir -p ~/reset-20261128 && cd ~/reset-20261128
pg_from_app wardmate-procedure-catalog ProcedureDatabase
pg_dump -Fc -f catalog.dump && pg_restore -l catalog.dump | wc -l   # số mục > 0 là có dữ liệu
pg_clear
```

Đặt tên tệp lần lượt:
- `documentform.dump`
- `workflow.dump`
- `aiocr.dump`
- `analytics.dump`
- `notifications.dump` (nếu khác DB với analytics)
- `iam.dump`

**Lỗi thường gặp:**
- `server version mismatch`: `pg_dump` của Cloud Shell cũ hơn Supabase. Khi đó chạy `pg_dump` trên máy dự án bằng Docker: `docker run --rm -e PGHOST -e PGPORT -e PGDATABASE -e PGUSER -e PGPASSWORD -e PGSSLMODE -v ${PWD}:/out postgres:17 pg_dump -Fc -f /out/catalog.dump`. Nạp biến PG* trước, không gõ mật khẩu vào lệnh.
- Supabase dùng pooler cổng 6543 (transaction): đổi sang cổng 5432 (session) cho `pg_dump`.

**Sao lưu blob** (khuyến nghị, tốn rất ít dung lượng):

```bash
SA=stwardmate6ll4
KEY=$(az storage account keys list -g rg-wardmate -n $SA --query "[0].value" -o tsv)
for c in procedure-sources form-templates; do
  az storage blob copy start-batch --account-name $SA --account-key "$KEY" \
    --source-container $c --destination-container wardmate-backups --destination-path "reset-20261128/$c" -o none
done
az storage blob list --account-name $SA --account-key "$KEY" -c wardmate-backups --prefix reset-20261128/ --query "length(@)" -o tsv
```

Sau đó tải các tệp `.dump` về máy (Cloud Shell → Manage files → Download), hoặc chép lên `wardmate-backups/reset-20261128/db/`.

**Chỉ sang bước 2 khi đã có đủ tệp `.dump` và bản sao blob.**

## 2. Xóa dữ liệu DB

Mỗi script tự bỏ qua bảng chưa có trên DB (in NOTICE). Script lỗi thì cả giao dịch quay lại, không xóa gì.

```bash
pg_from_app wardmate-procedure-catalog ProcedureDatabase;   psql -v ON_ERROR_STOP=1 -f docs/reset/1-catalog.sql;      pg_clear
pg_from_app wardmate-documentform DocumentDb;               psql -v ON_ERROR_STOP=1 -f docs/reset/2-documentform.sql; pg_clear
pg_from_app wardmate-application-workflow WorkflowDatabase; psql -v ON_ERROR_STOP=1 -f docs/reset/3-workflow.sql;     pg_clear
pg_from_app wardmate-aiocr AiOcrDatabase;                   psql -v ON_ERROR_STOP=1 -f docs/reset/4-aiocr.sql;        pg_clear
pg_from_app wardmate-analytics-system AnalyticsDatabase;    psql -v ON_ERROR_STOP=1 -f docs/reset/5-analytics.sql;    pg_clear
pg_from_app wardmate-analytics-system NotificationsDatabase; psql -v ON_ERROR_STOP=1 -f docs/reset/5-analytics.sql;   pg_clear   # bỏ qua nếu cùng DB với dòng trên
pg_from_app wardmate-iam Database;                          psql -v ON_ERROR_STOP=1 -f docs/reset/6-iam.sql;          pg_clear
```

Kiểm nhanh sau mỗi DB, ví dụ Catalog: `psql -c "select count(*) from procedures"` phải ra 0.

## 3. Xóa blob

Chỉ xóa tệp, giữ container:

```bash
for c in procedure-sources form-templates; do
  az storage blob delete-batch --account-name $SA --account-key "$KEY" -s $c -o none
  echo "$c: $(az storage blob list --account-name $SA --account-key "$KEY" -c $c --query 'length(@)' -o tsv) tệp còn lại"
done
unset KEY
```

## 4. Sau khi xóa

1. **Health:** `/health/ready` của 6 service trả 200.
2. **Chatbot:** AIOCR giữ chỉ mục trong bộ nhớ, có thể còn trả lời theo thủ tục cũ cho tới khi tải lại. Hỏi thử một thủ tục cũ; nếu vẫn trả lời được thì cập nhật app AIOCR để khởi động lại bản chạy. Theo runbook Azure, phần này do người vận hành làm.
3. **Kiểm qua Gateway:**
   - `GET /api/v1/procedures` trả danh sách rỗng.
   - Màn Quản lý thủ tục trống.
   - Tài khoản vẫn đăng nhập được.
4. **Nhập lại (Quản lý thủ tục):**
   1. Tạo lĩnh vực.
   2. Tạo biểu mẫu và tải tệp Word.
   3. Tạo thủ tục: tải PDF để AI trích xuất, hoặc nhập tay. Dùng giấy tờ chung + trường hợp (WF-CASE-01) nếu thủ tục có nhiều trường hợp.
   4. Xuất bản rồi công khai.
   5. "Đồng bộ thủ tục → Tất cả" để chatbot có dữ liệu.
5. **Quản lý phường:** gán lại lĩnh vực cho cán bộ (đã bị đặt về rỗng ở `6-iam.sql`).
6. **Admin:** nhập ngày lễ 2026–2027 nếu chưa có (M-14).
7. **Thống kê:** số liệu cũ đã xóa. Job tổng hợp tự chạy lại; ngày chưa có hồ sơ sẽ ra 0.

## Khôi phục (nếu cần)

Khôi phục từng DB bằng `pg_restore --clean --if-exists -d "$PGDATABASE" <tệp>.dump` sau khi nạp đúng app/khóa. Khôi phục blob bằng `az storage blob copy start-batch` từ `wardmate-backups/reset-20261128/<container>` về container gốc.
