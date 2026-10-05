-- Đối soát sau chuyển đổi — CHỈ ĐỌC (không INSERT/UPDATE/DELETE).
--   Dev: docker exec -i lms-pg psql -U langcenter -d langcenter \
--          -v ma_trung_tam=XXXXXXX < 03-doi-soat.sql
--   VPS: cd /opt/langcenter && set -a && . ./.env && set +a
--        docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
--          psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ma_trung_tam=XXXXXXX < 03-doi-soat.sql
-- So từng con số với ky-vong.json.

CREATE TEMP TABLE _ctx AS
SELECT id AS tenant_id FROM "TENANT" WHERE ma_trung_tam = :'ma_trung_tam';

\echo '=== 1. Tổng số dòng (so với ky-vong.json) ==='
SELECT 'PHONG_BAN' bang, count(*) FROM "PHONG_BAN" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'NGUOI_DUNG (kể cả admin)', count(*) FROM "NGUOI_DUNG" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'TAI_KHOAN (kể cả admin)', count(*) FROM "TAI_KHOAN" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'TAI_KHOAN hoạt động (kể cả admin)', count(*) FROM "TAI_KHOAN" JOIN _ctx USING (tenant_id) WHERE trang_thai = 0
UNION ALL SELECT 'KHOA_HOC', count(*) FROM "KHOA_HOC" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'KHACH_HANG', count(*) FROM "KHACH_HANG" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'DANG_KY_KHOA_HOC', count(*) FROM "DANG_KY_KHOA_HOC" JOIN _ctx USING (tenant_id)
UNION ALL SELECT 'THU_TIEN_DANG_KY', count(*) FROM "THU_TIEN_DANG_KY" JOIN _ctx USING (tenant_id);

\echo '=== 2. Tài khoản chưa đặt mật khẩu (sau bước 02 phải = 0 với tài khoản hoạt động) ==='
SELECT trang_thai, count(*) FROM "TAI_KHOAN" JOIN _ctx USING (tenant_id)
WHERE password_hash = 'CHUYEN-DOI:CHUA-DAT-MAT-KHAU' GROUP BY 1;

\echo '=== 3. Số tài khoản theo nhóm quyền ==='
SELECT q.ten_quyen, count(*) AS so_tai_khoan
FROM "NGUOIDUNG_QUYEN" nq JOIN "QUYEN" q ON q.id = nq.quyen_id JOIN _ctx c ON c.tenant_id = q.tenant_id
GROUP BY 1 ORDER BY 2 DESC;

\echo '=== 4. Tài khoản KHÔNG có nhóm quyền nào (phải rỗng) ==='
SELECT tk.username FROM "TAI_KHOAN" tk JOIN _ctx c ON c.tenant_id = tk.tenant_id
WHERE NOT EXISTS (SELECT 1 FROM "NGUOIDUNG_QUYEN" nq WHERE nq.tai_khoan_id = tk.id);

\echo '=== 5. Doanh thu (cam kết) và đã thu — tổng ==='
SELECT count(*) AS so_don, sum(d.so_tien * d.ty_gia_ve_vnd) AS doanh_thu,
       (SELECT sum(t.so_tien) FROM "THU_TIEN_DANG_KY" t JOIN _ctx USING (tenant_id)) AS da_thu
FROM "DANG_KY_KHOA_HOC" d JOIN _ctx USING (tenant_id);

\echo '=== 6. Doanh thu theo tháng đăng ký (giờ VN) ==='
SELECT to_char(d.ngay_dang_ky AT TIME ZONE 'Asia/Ho_Chi_Minh', 'YYYY-MM') AS thang,
       count(*) AS so_don, sum(d.so_tien * d.ty_gia_ve_vnd) AS doanh_thu
FROM "DANG_KY_KHOA_HOC" d JOIN _ctx USING (tenant_id) GROUP BY 1 ORDER BY 1;

\echo '=== 7. Doanh số theo nhân viên mang khách về (KHACH_HANG.created_by_id) ==='
SELECT COALESCE(n.ho_ten, '(không có)') AS nhan_vien, pb.ten AS doi_nhom,
       count(DISTINCT k.id) AS so_khach, count(d.id) AS so_don,
       sum(d.so_tien * d.ty_gia_ve_vnd) AS doanh_thu
FROM "DANG_KY_KHOA_HOC" d JOIN _ctx c ON c.tenant_id = d.tenant_id
JOIN "KHACH_HANG" k ON k.id = d.khach_hang_id
LEFT JOIN "NGUOI_DUNG" n ON n.id = k.created_by_id
LEFT JOIN "PHONG_BAN" pb ON pb.id = n.phong_ban_id
GROUP BY 1, 2 ORDER BY doanh_thu DESC;

\echo '=== 8. Doanh thu theo đội nhóm (phòng tag KinhDoanh; còn lại = KHAC như màn Thống kê) ==='
SELECT CASE WHEN pb.tag_vai_tro = 0 THEN pb.ten ELSE 'KHAC' END AS doi_nhom,
       sum(d.so_tien * d.ty_gia_ve_vnd) AS doanh_thu
FROM "DANG_KY_KHOA_HOC" d JOIN _ctx c ON c.tenant_id = d.tenant_id
JOIN "KHACH_HANG" k ON k.id = d.khach_hang_id
LEFT JOIN "NGUOI_DUNG" n ON n.id = k.created_by_id
LEFT JOIN "PHONG_BAN" pb ON pb.id = n.phong_ban_id
GROUP BY 1 ORDER BY 2 DESC;

\echo '=== 9. Tình trạng thu tiền theo đơn ==='
SELECT CASE WHEN COALESCE(t.da_thu, 0) = 0 THEN 'Chưa đóng'
            WHEN t.da_thu >= d.so_tien THEN 'Đã đóng đủ'
            ELSE 'Còn thiếu' END AS tinh_trang,
       count(*) AS so_don
FROM "DANG_KY_KHOA_HOC" d JOIN _ctx c ON c.tenant_id = d.tenant_id
LEFT JOIN (SELECT dang_ky_id, sum(so_tien) da_thu FROM "THU_TIEN_DANG_KY" GROUP BY 1) t ON t.dang_ky_id = d.id
GROUP BY 1 ORDER BY 2 DESC;

\echo '=== 10. Phòng ban ==='
SELECT p.ten, p.tag_vai_tro, cha.ten AS phong_cha, ql.ho_ten AS quan_ly,
       (SELECT count(*) FROM "NGUOI_DUNG" n WHERE n.phong_ban_id = p.id) AS nhan_su
FROM "PHONG_BAN" p JOIN _ctx c ON c.tenant_id = p.tenant_id
LEFT JOIN "PHONG_BAN" cha ON cha.id = p.phong_ban_cha_id
LEFT JOIN "NGUOI_DUNG" ql ON ql.id = p.nguoi_quan_ly_id
ORDER BY p.thu_tu;
