-- Cấp quyền ĐỌC danh mục tiêu chí cho nhóm "Học viên" (18/09/2026).
--
-- Vì sao cần script tay: `BoKhuyetQuyenQuanTri` lúc khởi động chỉ vá nhóm "Quản trị viên" và
-- **cố tình không đụng** ba nhóm còn lại — nới quyền cho giáo viên/học viên là quyết định của
-- admin, hệ thống tự làm là lỗ hổng (xem docs/03-backend/phan-quyen-dong.md).
--
-- Nhưng tính năng "học viên chấm giáo viên/trợ giảng theo tiêu chí" KHÔNG chạy được nếu thiếu ô
-- này: endpoint `/tieu-chi-danh-gia/de-cham` trả 403, frontend rơi về chấm sao — đúng lỗi chủ
-- sản phẩm báo. Trung tâm lập MỚI đã có sẵn ô này (`NhomQuyenMacDinh.CuaHocVien`); script này
-- dành cho trung tâm lập TRƯỚC 18/09.
--
-- An toàn:
--   · `TuLam = 16` — ĐỌC danh mục để tự đi chấm. KHÔNG phải `Xem` (màn quản lý danh mục ở HRM).
--   · `ON CONFLICT DO NOTHING` ⇒ chạy lại bao nhiêu lần cũng vậy, không nhân bản hàng.
--   · Chỉ THÊM, không xoá ô nào — admin đã cố ý bỏ ô khác thì script không đụng tới.
--
-- Dùng:  docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/cap-quyen-tieu-chi-cho-hoc-vien.sql

BEGIN;

INSERT INTO "QUYEN_CHUC_NANG" (id, tenant_id, quyen_id, ten_chuc_nang, hanh_dong, created_at)
SELECT gen_random_uuid(), q.tenant_id, q.id, 'TieuChiDanhGia', 16, now()
FROM "QUYEN" q
WHERE q.ten_quyen = 'Học viên'
  AND NOT EXISTS (
      SELECT 1 FROM "QUYEN_CHUC_NANG" x
      WHERE x.quyen_id = q.id
        AND x.ten_chuc_nang = 'TieuChiDanhGia'
        AND x.hanh_dong = 16);

-- Đối chiếu: mỗi nhóm "Học viên" phải có đúng một ô TieuChiDanhGia/TuLam.
SELECT t.ma_trung_tam, q.ten_quyen, COUNT(qc.id) AS so_o_tieu_chi
FROM "QUYEN" q
JOIN "TENANT" t ON t.id = q.tenant_id
LEFT JOIN "QUYEN_CHUC_NANG" qc
       ON qc.quyen_id = q.id AND qc.ten_chuc_nang = 'TieuChiDanhGia' AND qc.hanh_dong = 16
WHERE q.ten_quyen = 'Học viên'
GROUP BY t.ma_trung_tam, q.ten_quyen
ORDER BY t.ma_trung_tam;

COMMIT;
