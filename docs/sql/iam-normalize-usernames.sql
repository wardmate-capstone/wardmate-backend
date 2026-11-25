-- IAM-USERNAME-01 (25/11/2026): chuẩn hóa tên đăng nhập bị lưu kèm khoảng trắng hoặc chữ hoa.
-- Mọi đường tạo tài khoản trong code đã cắt khoảng trắng và hạ chữ thường; đăng nhập so khớp
-- chính xác với giá trị đã chuẩn hóa. Tài khoản tạo bằng SQL tay (vd. "  test_mgr_a") vì vậy
-- không đăng nhập được bằng tên. Chạy trên DB IAM (Supabase SQL Editor), từng bước.

-- Bước 1: xem các tài khoản cần sửa và kiểm tra trùng sau khi chuẩn hóa.
SELECT u.id, '[' || u.username || ']' AS hien_tai, lower(btrim(u.username)) AS sau_chuan_hoa,
       EXISTS (SELECT 1 FROM users o WHERE o.id <> u.id AND o.username = lower(btrim(u.username))) AS bi_trung
FROM users u
WHERE u.username <> lower(btrim(u.username));

-- Bước 2: chỉ chạy khi cột bi_trung ở bước 1 đều là false. Không đụng tới tài khoản bị trùng.
BEGIN;
UPDATE users u
SET username = lower(btrim(u.username)), updated_at = now()
WHERE u.username <> lower(btrim(u.username))
  AND NOT EXISTS (SELECT 1 FROM users o WHERE o.id <> u.id AND o.username = lower(btrim(u.username)));
-- Kiểm tra lại: kết quả phải là 0 dòng (hoặc chỉ còn tài khoản bị trùng cần xử lý tay).
SELECT id, '[' || username || ']' FROM users WHERE username <> lower(btrim(username));
COMMIT;
