-- RESET-DATA-1128 — DocumentForm: xóa biểu mẫu, phiên bản mẫu, bản khai, giấy tờ, PDF, QR, tệp theo lần gửi
-- Chạy SAU khi đã sao lưu DB (pg_dump) theo docs/reset/README.md. KHÔNG hoàn tác được.
-- Chỉ xóa dữ liệu (TRUNCATE ... RESTART IDENTITY), giữ cấu trúc bảng và __EFMigrationsHistory.
-- Bảng chưa tồn tại trên DB thì bỏ qua (ghi NOTICE). Không dùng CASCADE: nếu bảng ngoài danh sách
-- còn khóa ngoại trỏ vào thì lệnh lỗi và cả giao dịch quay lại (không xóa gì).
BEGIN;
DO $$
DECLARE
  wanted text[] := ARRAY['document.form_templates','document.form_template_versions','document.application_forms','document.user_submissions','document.supporting_documents','document.generated_documents','document.approved_qrs','document.submission_file_versions'];
  existing text[];
  t text;
BEGIN
  FOREACH t IN ARRAY wanted LOOP
    IF to_regclass(t) IS NULL THEN RAISE NOTICE 'Bỏ qua bảng không có: %', t;
    ELSE existing := array_append(existing, t); END IF;
  END LOOP;
  IF existing IS NOT NULL THEN
    EXECUTE 'TRUNCATE TABLE ' || array_to_string(existing, ', ') || ' RESTART IDENTITY';
    RAISE NOTICE 'Đã xóa dữ liệu: %', array_to_string(existing, ', ');
  END IF;
END $$;
COMMIT;
