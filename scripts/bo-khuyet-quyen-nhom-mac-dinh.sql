-- Bù quyền cho BA nhóm quyền mặc định của trung tâm ĐÃ TỒN TẠI.
--
-- ## Vì sao cần
--
-- `TenantSeeder` chỉ chạy đúng một lần lúc tạo trung tâm. Module thêm sau đó (FR-25→27 Học
-- trực tuyến làm 13/09/2026, và các thao tác đặc thù tách 14/09) **không có hàng nào** trong
-- `QUYEN_CHUC_NANG` cho ba nhóm Giáo viên / Trợ giảng / Học viên. Hệ quả khi chạy thật: giáo
-- viên không soạn được khoá trực tuyến, học viên không mở được bài — dù chức năng đã xong.
--
-- `BoKhuyetQuyenQuanTri` (chạy lúc khởi động) chỉ bù cho nhóm **Quản trị viên**, có chủ ý: ba
-- nhóm kia khai TƯỜNG MINH ma trận nên tự động cấp thêm là đoán ý người quản trị.
--
-- ## Vì sao chạy TAY, không tự động lúc khởi động
--
-- Đây là GHI vào dữ liệu đang có (quy tắc #1). Nếu admin đã cố ý bỏ một ô quyền thì lần khởi
-- động sau không được lặng lẽ cấp lại. Chạy tay, sau khi đã xem phần in ra ở dưới.
--
-- ## Cách chạy
--
--   docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/bo-khuyet-quyen-nhom-mac-dinh.sql
--
-- CHỈ THÊM, không xoá, không sửa: `ON CONFLICT DO NOTHING` theo cặp (nhóm, chức năng, thao tác).
-- Chạy nhiều lần vô hại.
--
-- Sinh tự động từ `NhomQuyenMacDinh.cs` + lọc qua `ChucNang.ThaoTacTheoChucNang`, nên không
-- chứa ô nào mà màn phân quyền không hiện. Sinh lại khi ma trận đổi:
--   python3 scripts/tao-sql-bo-khuyet-quyen.py
BEGIN;

CREATE TEMP TABLE quyen_can_co(ten_quyen text, ten_chuc_nang text, hanh_dong int);
INSERT INTO quyen_can_co VALUES
  ('Giáo viên','Anh',0),
  ('Giáo viên','Anh',1),
  ('Giáo viên','Anh',3),
  ('Giáo viên','BaiNopBaiTap',0),
  ('Giáo viên','BaiNopBaiTap',15),
  ('Giáo viên','BaiTap',0),
  ('Giáo viên','BaiTap',1),
  ('Giáo viên','BaiTap',2),
  ('Giáo viên','BaiTap',3),
  ('Giáo viên','BuoiHoc',0),
  ('Giáo viên','BuoiHoc',1),
  ('Giáo viên','BuoiHoc',2),
  ('Giáo viên','BuoiHoc',12),
  ('Giáo viên','DiemDanh',0),
  ('Giáo viên','DiemDanh',2),
  ('Giáo viên','DiemDanh',11),
  ('Giáo viên','GhiDanhLop',0),
  ('Giáo viên','GhiDanhLop',1),
  ('Giáo viên','GhiDanhLop',3),
  ('Giáo viên','HoSoNguoiDung',0),
  ('Giáo viên','HocOnline',0),
  ('Giáo viên','KhoaOnline',0),
  ('Giáo viên','KhoaOnline',1),
  ('Giáo viên','KhoaOnline',2),
  ('Giáo viên','KhoaOnline',3),
  ('Giáo viên','LopHoc',0),
  ('Giáo viên','NhanXetBuoiHoc',0),
  ('Giáo viên','NhanXetBuoiHoc',16),
  ('Giáo viên','TaiKhoan',0),
  ('Giáo viên','TaiLieu',0),
  ('Giáo viên','TaiLieu',1),
  ('Học viên','Anh',0),
  ('Học viên','Anh',1),
  ('Học viên','Anh',3),
  ('Học viên','BaiNopBaiTap',0),
  ('Học viên','BaiNopBaiTap',16),
  ('Học viên','BaiTap',0),
  ('Học viên','BuoiHoc',0),
  ('Học viên','DiemDanh',0),
  ('Học viên','DiemDanh',16),
  ('Học viên','GhiDanhLop',0),
  ('Học viên','HocOnline',0),
  ('Học viên','HocOnline',16),
  ('Học viên','HocPhi',0),
  ('Học viên','LopHoc',0),
  ('Học viên','NhanXetBuoiHoc',16),
  ('Học viên','TaiLieu',0),
  ('Trợ giảng','Anh',0),
  ('Trợ giảng','Anh',1),
  ('Trợ giảng','Anh',3),
  ('Trợ giảng','BaiNopBaiTap',0),
  ('Trợ giảng','BaiNopBaiTap',15),
  ('Trợ giảng','BaiTap',0),
  ('Trợ giảng','BaiTap',1),
  ('Trợ giảng','BaiTap',2),
  ('Trợ giảng','BaiTap',3),
  ('Trợ giảng','BuoiHoc',0),
  ('Trợ giảng','BuoiHoc',2),
  ('Trợ giảng','DiemDanh',0),
  ('Trợ giảng','DiemDanh',2),
  ('Trợ giảng','GhiDanhLop',0),
  ('Trợ giảng','HocOnline',0),
  ('Trợ giảng','KhoaOnline',0),
  ('Trợ giảng','LopHoc',0),
  ('Trợ giảng','NhanXetBuoiHoc',0),
  ('Trợ giảng','NhanXetBuoiHoc',16),
  ('Trợ giảng','TaiKhoan',0),
  ('Trợ giảng','TaiLieu',0);

-- Xem TRƯỚC những gì sắp thêm (không đổi dữ liệu).
SELECT c.ten_quyen, c.ten_chuc_nang, c.hanh_dong
FROM quyen_can_co c
JOIN "QUYEN" q ON q."ten_quyen" = c.ten_quyen
WHERE NOT EXISTS (
    SELECT 1 FROM "QUYEN_CHUC_NANG" x
    WHERE x.quyen_id = q.id AND x.ten_chuc_nang = c.ten_chuc_nang
      AND x.hanh_dong = c.hanh_dong)
ORDER BY 1, 2, 3;

-- `created_at` là NOT NULL (cột audit thêm 12/09/2026) — thiếu nó thì INSERT đỏ. Phát hiện
-- lúc chạy thử trên DB bản sao, đúng lý do phải chạy thử trước khi chạy thật.
-- `created_by_id` để NULL: đây là thao tác của hệ thống, không phải của người nào.
INSERT INTO "QUYEN_CHUC_NANG"
    (id, tenant_id, quyen_id, ten_chuc_nang, hanh_dong, created_at)
SELECT gen_random_uuid(), q.tenant_id, q.id, c.ten_chuc_nang, c.hanh_dong, now()
FROM quyen_can_co c
JOIN "QUYEN" q ON q."ten_quyen" = c.ten_quyen
WHERE NOT EXISTS (
    SELECT 1 FROM "QUYEN_CHUC_NANG" x
    WHERE x.quyen_id = q.id AND x.ten_chuc_nang = c.ten_chuc_nang
      AND x.hanh_dong = c.hanh_dong);

SELECT q."ten_quyen", count(*) AS so_o_sau_khi_bu
FROM "QUYEN_CHUC_NANG" qcn JOIN "QUYEN" q ON q.id = qcn.quyen_id
GROUP BY 1 ORDER BY 1;

COMMIT;
