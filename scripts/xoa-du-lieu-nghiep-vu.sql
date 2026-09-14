-- Xoá sạch DỮ LIỆU NGHIỆP VỤ của một trung tâm, GIỮ hạ tầng đăng nhập.
--
-- Dùng khi dựng lại bộ dữ liệu mẫu trên chính trung tâm đang test, thay vì tạo trung tâm mới
-- mỗi lần (mã đổi liên tục, phải nhớ mã mới — phiền khi test tay).
--
-- GIỮ LẠI: TENANT · TAI_KHOAN · NGUOI_DUNG của các tài khoản · QUYEN · QUYEN_CHUC_NANG ·
--          NGUOIDUNG_QUYEN · THIET_LAP_CHUNG · CHUC_VU · PHONG_BAN
-- XOÁ HẾT: lớp · buổi học · điểm danh · bài tập · bài nộp · tài liệu · học phí · khách hàng ·
--          đơn hàng · khoá học · sản phẩm · khoá online · nhật ký · người dùng KHÔNG có tài khoản
--
-- ⚠️  MẤT HẲN, không hoàn tác được. Chỉ chạy trên máy local.
--
-- Chạy:  docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/xoa-du-lieu-nghiep-vu.sql
DO $$
DECLARE
    TENANT_MA CONSTANT text := 'W686AE9';

    -- Bảng hạ tầng: giữ nguyên. `NGUOI_DUNG` xử lý riêng bên dưới vì chỉ giữ người CÓ tài khoản.
    GIU CONSTANT text[] := ARRAY[
        'TENANT', 'TAI_KHOAN', 'NGUOI_DUNG', 'QUYEN', 'QUYEN_CHUC_NANG',
        'NGUOIDUNG_QUYEN', 'THIET_LAP_CHUNG', 'CHUC_VU', 'PHONG_BAN',
        'TOKEN_LAM_MOI', 'TOKEN_DAT_LAI_MAT_KHAU'
    ];

    tid      uuid;
    r        RECORD;
    so_dong  BIGINT;
    tong     BIGINT := 0;
BEGIN
    SELECT id INTO tid FROM "TENANT" WHERE ma_trung_tam = TENANT_MA;
    IF tid IS NULL THEN
        RAISE EXCEPTION 'Không thấy trung tâm %', TENANT_MA;
    END IF;

    -- Lặp nhiều vòng: FK Restrict bắt xoá bảng con trước bảng cha, độ sâu không biết trước.
    FOR i IN 1..8 LOOP
        so_dong := 0;
        FOR r IN
            SELECT table_name FROM information_schema.columns
            WHERE column_name = 'tenant_id' AND table_schema = 'public'
              AND NOT (table_name = ANY (GIU))
            ORDER BY table_name
        LOOP
            BEGIN
                EXECUTE format('DELETE FROM %I WHERE tenant_id = $1', r.table_name) USING tid;
                GET DIAGNOSTICS so_dong = ROW_COUNT;
                tong := tong + so_dong;
            EXCEPTION WHEN foreign_key_violation THEN
                NULL;   -- vòng sau sẽ xoá được
            END;
        END LOOP;
        EXIT WHEN so_dong = 0;
    END LOOP;

    -- Người dùng KHÔNG gắn tài khoản nào = hồ sơ do script mẫu sinh ra. Người CÓ tài khoản là
    -- chủ sản phẩm đang dùng để đăng nhập — giữ lại, nếu không thì lần sau không vào được.
    DELETE FROM "NGUOI_DUNG" n
    WHERE n.tenant_id = tid
      AND NOT EXISTS (SELECT 1 FROM "TAI_KHOAN" t WHERE t.nguoi_dung_id = n.id);
    GET DIAGNOSTICS so_dong = ROW_COUNT;
    tong := tong + so_dong;

    RAISE NOTICE 'Đã xoá % hàng dữ liệu nghiệp vụ của %. Tài khoản đăng nhập giữ nguyên.',
        tong, TENANT_MA;
END $$;
