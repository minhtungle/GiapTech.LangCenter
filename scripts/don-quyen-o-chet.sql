-- Dọn hàng QUYEN_CHUC_NANG không còn trong bảng khai `ChucNang.ThaoTacTheoChucNang`.
--
-- Vì sao cần: trước 14/09/2026 seeder cấp MỌI thao tác cho MỌI chức năng, kể cả những ô không
-- endpoint nào đọc (`BaiKiemTra.*` — chưa có API; `ThongKe.Xem` — không endpoint nào gác bằng
-- nó; `DiemDanh.Them`, `Anh.Sua`…). Những hàng đó nay không hiện trên màn phân quyền nữa, nên
-- người quản trị THẤY KHÔNG ĐƯỢC và BỎ KHÔNG ĐƯỢC. Chúng vô hại về chức năng (không endpoint
-- nào đọc) nhưng làm màn phân quyền cảnh báo "còn giữ quyền cũ" mãi.
--
-- KHÔNG chạy tự động lúc khởi động: đây là XOÁ dữ liệu đang có (quy tắc #1). Chạy tay, sau khi
-- đã xem phần đếm ở dưới và tự sao lưu.
--
--   docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/don-quyen-o-chet.sql
--
BEGIN;

CREATE TEMP TABLE quyen_hop_le(ten_chuc_nang text, hanh_dong int);
INSERT INTO quyen_hop_le VALUES ('TaiKhoan',0),('TaiKhoan',1),('TaiKhoan',2),('TaiKhoan',3),('HoSoNguoiDung',0),('HoSoNguoiDung',1),('HoSoNguoiDung',2),('HoSoNguoiDung',3),('PhanQuyen',0),('PhanQuyen',1),('PhanQuyen',20),('PhanQuyen',3),('ThietLapChung',0),('ThietLapChung',2),('ThietLapChung',19),('Anh',0),('Anh',1),('Anh',3),('DoiMatKhauNguoiKhac',2),('NhatKyHeThong',0),('NhanSu',0),('NhanSu',1),('NhanSu',2),('NhanSu',3),('NhanSu',17),('NhanSu',18),('ChucVu',1),('ChucVu',2),('ChucVu',3),('PhongBan',0),('PhongBan',1),('PhongBan',2),('PhongBan',3),('PhongBan',22),('DoanhThu',0),('DoanhThu',1),('DoanhThu',2),('DoanhThu',3),('DoanhThu',14),('DoanhThu',23),('ThongKeDoanhThu',0),('KhachHang',0),('KhachHang',1),('KhachHang',2),('KhachHang',3),('ChamSocKhachHang',0),('ChamSocKhachHang',1),('ChamSocKhachHang',2),('ChamSocKhachHang',3),('KhoaHoc',0),('KhoaHoc',1),('KhoaHoc',2),('KhoaHoc',3),('SanPham',0),('SanPham',1),('SanPham',2),('SanPham',3),('LopHoc',0),('LopHoc',1),('LopHoc',2),('LopHoc',3),('LopHoc',21),('LopHoc',12),('LopHoc',13),('GhiDanhLop',0),('GhiDanhLop',1),('GhiDanhLop',3),('XepLop',0),('XepLop',10),('BuoiHoc',0),('BuoiHoc',1),('BuoiHoc',2),('BuoiHoc',3),('BuoiHoc',12),('DiemDanh',0),('DiemDanh',2),('DiemDanh',11),('DiemDanh',16),('NhanXetBuoiHoc',0),('NhanXetBuoiHoc',16),('BaiTap',0),('BaiTap',1),('BaiTap',2),('BaiTap',3),('BaiNopBaiTap',0),('BaiNopBaiTap',16),('BaiNopBaiTap',15),('KhoaOnline',1),('KhoaOnline',2),('KhoaOnline',3),('GhiDanhKhoaOnline',0),('GhiDanhKhoaOnline',1),('GhiDanhKhoaOnline',2),('GhiDanhKhoaOnline',3),('HocOnline',0),('HocOnline',16),('TaiLieu',0),('TaiLieu',1),('TaiLieu',2),('TaiLieu',3),('HocPhi',0),('HocPhi',14),('HocPhi',2),('HocPhi',3),('LopHocToanTrungTam',0),('LopHocToanTrungTam',2);

-- Xem TRƯỚC sẽ xoá gì (in ra, không đổi dữ liệu).
SELECT ten_chuc_nang, hanh_dong, count(*) AS so_hang
FROM "QUYEN_CHUC_NANG" q
WHERE NOT EXISTS (
    SELECT 1 FROM quyen_hop_le v
    WHERE v.ten_chuc_nang = q.ten_chuc_nang AND v.hanh_dong = q.hanh_dong)
GROUP BY 1, 2 ORDER BY 1, 2;

DELETE FROM "QUYEN_CHUC_NANG" q
WHERE NOT EXISTS (
    SELECT 1 FROM quyen_hop_le v
    WHERE v.ten_chuc_nang = q.ten_chuc_nang AND v.hanh_dong = q.hanh_dong);

COMMIT;
