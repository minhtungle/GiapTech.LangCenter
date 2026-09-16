-- Chuyển nhân viên kinh doanh sang vai trò riêng `NhanVienKinhDoanh` (= 4).
--
-- Bối cảnh: 16/09/2026 tách vai trò mới khỏi `NhanVien` (= 0) theo yêu cầu chủ sản phẩm
-- ("vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"). Code đã xong; script này
-- chuyển DỮ LIỆU ĐANG CÓ.
--
-- Vì sao KHÔNG làm trong EF migration: ai là nhân viên kinh doanh là **quyết định nghiệp vụ của
-- từng trung tâm**, không suy được từ schema. Migration đoán hộ sẽ gán sai cho mọi tenant khác,
-- mà đó là dữ liệu thật (quy tắc #1).
--
-- Cách nhận diện: theo TÊN (`Sale%`) chứ không theo phòng ban có tag Kinh doanh — chủ sản phẩm
-- chốt sau khi thấy "Sale Online B" đang nằm ở phòng "Đào tạo", tức phòng ban của người này mới
-- là thứ đặt sai, không phải vai trò.
--
-- Chạy:
--   docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/chuyen-vai-tro-nhan-vien-kinh-doanh.sql
--
-- An toàn: BEGIN/COMMIT một giao dịch, và in ra trước/sau để đối chiếu. Sai thì ROLLBACK.

BEGIN;

-- Trước khi đổi
\echo '--- TRƯỚC ---'
SELECT n.ho_ten, n.loai_nguoi_dung AS vai_tro, COALESCE(p.ten, '(không phòng)') AS phong_ban
FROM "NGUOI_DUNG" n
LEFT JOIN "PHONG_BAN" p ON p.id = n.phong_ban_id
WHERE n.loai_nguoi_dung IN (0, 4)
ORDER BY n.loai_nguoi_dung, n.ho_ten;

-- Chỉ đụng vai trò 0 (NhanVien) và tên bắt đầu bằng 'Sale'.
--
-- KHÔNG đổi người có chức vụ quản trị / nhân sự: "Quản trị viên" và "Chị Mai (nhân sự)" phải ở
-- lại vai trò `NhanVien`. Điều kiện tên đã loại họ, nhưng ghi rõ ra đây để lần sau đọc lại không
-- phải suy.
UPDATE "NGUOI_DUNG"
SET loai_nguoi_dung = 4
WHERE loai_nguoi_dung = 0
  AND ho_ten LIKE 'Sale%';

\echo '--- SAU ---'
SELECT n.ho_ten, n.loai_nguoi_dung AS vai_tro, COALESCE(p.ten, '(không phòng)') AS phong_ban
FROM "NGUOI_DUNG" n
LEFT JOIN "PHONG_BAN" p ON p.id = n.phong_ban_id
WHERE n.loai_nguoi_dung IN (0, 4)
ORDER BY n.loai_nguoi_dung, n.ho_ten;

-- Chốt an toàn: phải còn ít nhất một `NhanVien` (tài khoản quản trị), và không được có học viên
-- nào lọt sang vai trò mới.
DO $$
DECLARE con_nhan_vien int; hoc_vien_lot int;
BEGIN
  SELECT count(*) INTO con_nhan_vien FROM "NGUOI_DUNG" WHERE loai_nguoi_dung = 0;
  IF con_nhan_vien = 0 THEN
    RAISE EXCEPTION 'Không còn NhanVien nào — tài khoản quản trị đã bị đổi vai trò. Huỷ.';
  END IF;

  SELECT count(*) INTO hoc_vien_lot
  FROM "NGUOI_DUNG" n WHERE n.loai_nguoi_dung = 4 AND n.phong_ban_id IS NULL
    AND EXISTS (SELECT 1 FROM "HO_SO_HOC_VIEN" h WHERE h.nguoi_dung_id = n.id);
  IF hoc_vien_lot > 0 THEN
    RAISE EXCEPTION 'Có % hồ sơ học viên lọt sang vai trò nhân viên kinh doanh. Huỷ.', hoc_vien_lot;
  END IF;
END $$;

COMMIT;
