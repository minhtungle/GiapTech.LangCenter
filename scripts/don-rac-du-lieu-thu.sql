-- Dọn rác do các lượt kiểm chứng tay để lại trong tenant dev, GIỮ nguyên dữ liệu demo.
--
-- ## Vì sao cần
--
-- Các lượt rà soát 14–16/09/2026 chạy thẳng trên tenant thật `W686AE9` (chốt 12/09: không tạo
-- tenant mới). Mỗi lượt để lại một ít rác: hồ sơ trùng tên do script demo tạo mù quáng, học
-- viên mồ côi, và hơn trăm refresh token từ những lần script đăng nhập.
--
-- ## KHÔNG xoá gì thuộc nghiệp vụ
--
-- Giữ toàn bộ: 32 khách hàng · 85 đơn · 8 lớp · 12 buổi · 3 khoá online · 6 phòng ban (kèm
-- tag) · 5 khoá học · 3 sản phẩm · nhật ký. Dữ liệu demo là thứ làm biểu đồ và bộ lọc có hình
-- dạng để xem — xoá đi là mất công dựng lại.
--
-- Đã rà và KHÔNG có bất nhất logic nào: 0 học viên lọt vào cơ cấu, 0 quyền chết, 0 khoá ngoại
-- treo. Nên script này chỉ xoá đúng năm nhóm rác dưới đây.
--
--   docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/don-rac-du-lieu-thu.sql
--
-- Chạy nhiều lần vô hại (mọi lệnh đều theo điều kiện, không theo id cứng).
BEGIN;

-- ---------- 1. Hồ sơ "Cô Lan" TRÙNG TÊN ----------
--
-- Script `tao-du-lieu-mau.py` bản cũ tạo nhân sự mù quáng nên sinh hồ sơ thứ hai cùng tên
-- (đã vá 15/09: nay dùng lại hồ sơ cùng tên nếu có). Hậu quả: tài khoản `co.lan` nối hồ sơ
-- DẠY 0 LỚP, còn 4 lớp gán cho hồ sơ của `nv5` — đăng nhập `co.lan` thấy 0 lớp, trông y như
-- lỗi phân quyền.
--
-- Xoá hồ sơ CHẾT (0 lớp, 0 hồ sơ giáo viên, không phòng ban) và tài khoản của nó, rồi đổi
-- `nv5` thành `co.lan` để tên đăng nhập gợi đúng người.
CREATE TEMP TABLE ho_so_chet AS
SELECT n.id, t.id AS tai_khoan_id
FROM "NGUOI_DUNG" n
LEFT JOIN "TAI_KHOAN" t ON t.nguoi_dung_id = n.id
WHERE n.ho_ten IN (SELECT ho_ten FROM "NGUOI_DUNG" GROUP BY ho_ten HAVING count(*) > 1)
  AND n.phong_ban_id IS NULL
  AND NOT EXISTS (SELECT 1 FROM "LOP_HOC" l WHERE l.giao_vien_chinh_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "LOP_HOC_TRO_GIANG" g WHERE g.tro_giang_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "LOP_HOC_HOC_VIEN" h WHERE h.hoc_vien_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "HO_SO_GIAO_VIEN" hs WHERE hs.nguoi_dung_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "KHACH_HANG" k WHERE k.nguoi_dung_id = n.id);

SELECT 'sẽ xoá hồ sơ trùng tên: ' || count(*) FROM ho_so_chet;

DELETE FROM "REFRESH_TOKEN"   WHERE tai_khoan_id IN (SELECT tai_khoan_id FROM ho_so_chet);
DELETE FROM "NGUOIDUNG_QUYEN" WHERE tai_khoan_id IN (SELECT tai_khoan_id FROM ho_so_chet);
DELETE FROM "TAI_KHOAN"       WHERE id           IN (SELECT tai_khoan_id FROM ho_so_chet);
DELETE FROM "NGUOI_DUNG"      WHERE id           IN (SELECT id FROM ho_so_chet);

-- Đổi tên đăng nhập sang tên gợi ý người, CHỈ khi `co.lan` đã trống.
UPDATE "TAI_KHOAN" t SET username = 'co.lan'
WHERE t.username = 'nv5'
  AND NOT EXISTS (SELECT 1 FROM "TAI_KHOAN" x WHERE x.username = 'co.lan');

-- ---------- 2. Học viên MỒ CÔI ----------
--
-- Script demo tạo dư hồ sơ học viên: không tài khoản, không vào lớp nào, không nối khách hàng
-- nào. Chúng chỉ làm danh sách học viên dài ra mà không dùng được vào việc gì.
CREATE TEMP TABLE hoc_vien_mo_coi AS
SELECT n.id FROM "NGUOI_DUNG" n
WHERE n.loai_nguoi_dung = 3
  AND NOT EXISTS (SELECT 1 FROM "TAI_KHOAN"        t WHERE t.nguoi_dung_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "LOP_HOC_HOC_VIEN" h WHERE h.hoc_vien_id  = n.id)
  AND NOT EXISTS (SELECT 1 FROM "KHACH_HANG"       k WHERE k.nguoi_dung_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "GHI_DANH_KHOA_ONLINE" g WHERE g.hoc_vien_id = n.id)
  AND NOT EXISTS (SELECT 1 FROM "BAI_NOP"          b WHERE b.hoc_vien_id  = n.id)
  AND NOT EXISTS (SELECT 1 FROM "DIEM_DANH"        d WHERE d.hoc_vien_id  = n.id);

SELECT 'sẽ xoá học viên mồ côi: ' || count(*) FROM hoc_vien_mo_coi;

DELETE FROM "HO_SO_HOC_VIEN" WHERE nguoi_dung_id IN (SELECT id FROM hoc_vien_mo_coi);
DELETE FROM "NGUOI_DUNG"     WHERE id            IN (SELECT id FROM hoc_vien_mo_coi);

-- ---------- 3. Refresh token của những phiên script ----------
--
-- Mỗi lượt script kiểm chứng đăng nhập là một refresh token mới, và chúng còn hạn nên nằm lại
-- mãi. Xoá HẾT: dev không cần giữ phiên nào, đăng nhập lại là có token mới.
-- Không đụng logic — cơ chế xoay vòng và phát hiện tái sử dụng vẫn nguyên.
SELECT 'sẽ xoá refresh token: ' || count(*) FROM "REFRESH_TOKEN";
DELETE FROM "REFRESH_TOKEN";

-- ---------- 4. Token đặt lại mật khẩu đã dùng / hết hạn ----------
DELETE FROM "TOKEN_DATLAI_MATKHAU"
WHERE da_dung_luc IS NOT NULL OR het_han < now();

COMMIT;

-- ---------- Kết quả ----------
SELECT 'phòng ban'  AS bang, count(*) FROM "PHONG_BAN"
UNION ALL SELECT 'người dùng',   count(*) FROM "NGUOI_DUNG"
UNION ALL SELECT 'tài khoản',    count(*) FROM "TAI_KHOAN"
UNION ALL SELECT 'khách hàng',   count(*) FROM "KHACH_HANG"
UNION ALL SELECT 'đơn hàng',     count(*) FROM "DANG_KY_KHOA_HOC"
UNION ALL SELECT 'lớp học',      count(*) FROM "LOP_HOC"
UNION ALL SELECT 'buổi học',     count(*) FROM "BUOI_HOC"
UNION ALL SELECT 'ghi danh lớp', count(*) FROM "LOP_HOC_HOC_VIEN"
UNION ALL SELECT 'refresh token',count(*) FROM "REFRESH_TOKEN"
ORDER BY 1;
