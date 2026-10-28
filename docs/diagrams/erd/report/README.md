# WardMate ERD — bản dùng cho báo cáo

Theo yêu cầu chủ dự án (26/10/2026), bản này vẽ **toàn bộ thiết kế hệ thống**: 41 bảng đã có trong code và 6 bảng thiết kế theo đặc tả gốc (`ai_request_logs`, `ocr_scan_logs`, `audit_logs`, `system_settings`, `service_health_logs`, `system_backup_logs`) được vẽ như bảng thường — không nhãn FUTURE, không viền nét đứt, không đường chấm.

**Lưu ý trung thực:** 6 bảng trên **chưa có entity, migration hay bảng nào trong code/database** (đối chiếu EF model ngày 24/10/2026, kiểm tra lại 26/10/2026 không đổi). Kiểu dữ liệu và NOT NULL của 6 bảng là đề xuất. Nếu báo cáo cần nêu tình trạng triển khai, dùng bản chính xác.

| File | Nội dung |
|---|---|
| [conceptual.png](conceptual.png) | Conceptual, 35 khái niệm, 44 quan hệ, nền sáng |
| [logical.png](logical.png) | Logical, 47 bảng, thuộc tính và khóa |
| [physical.png](physical.png) | Physical, 47 bảng, schema, kiểu dữ liệu, NN/NULL |
| [../wardmate-erd-report-en.drawio](../wardmate-erd-report-en.drawio) | draw.io 3 trang tiếng Anh, sửa được |

Bản chính xác (đánh dấu FUTURE): [../wardmate-erd-en.drawio](../wardmate-erd-en.drawio), [../wardmate-erd-interactive.html](../wardmate-erd-interactive.html), [../../../erd-drawing-guide.txt](../../../erd-drawing-guide.txt).

Bản báo cáo được dựng từ cùng nguồn với trang tương tác (cờ `REPORT`, nguồn dựng nằm trong scratchpad của phiên Claude, ngoài repo). Khi schema đổi, dựng lại cả hai bản từ `../model-inventory.json`.
