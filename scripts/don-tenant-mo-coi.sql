-- Dọn hàng MỒ CÔI: thuộc một tenant không còn tồn tại.
--
-- Vì sao cần: script dọn tenant trước đây liệt kê từng bảng bằng tay, nên mỗi lần thêm bảng
-- mới mà quên cập nhật là bỏ lại rác. Đã xảy ra 13/09/2026 với 4 bảng elearning — 36 hàng nằm
-- lại sau khi xoá tenant E2E, vô hình với người dùng (Global Query Filter lọc sạch) nhưng vẫn
-- chiếm chỗ và làm sai mọi phép đếm khi rà soát.
--
-- Cách viết dưới đây KHÔNG liệt kê bảng: nó tự tìm mọi bảng có cột `tenant_id`. Thêm bảng mới
-- không phải sửa gì.
--
-- An toàn: chỉ xoá hàng mà `tenant_id` KHÔNG khớp tenant nào đang tồn tại. Dữ liệu của tenant
-- đang dùng không bị đụng tới.
--
-- Chạy:  docker exec -i lms-pg psql -U langcenter -d langcenter -f - < scripts/don-tenant-mo-coi.sql
DO $$
DECLARE
    r        RECORD;
    so_dong  BIGINT;
    tong     BIGINT := 0;
BEGIN
    -- Lặp nhiều vòng: xoá bảng con trước mới xoá được bảng cha (FK Restrict). Bốn vòng là đủ
    -- cho độ sâu quan hệ hiện tại; vòng nào không xoá thêm được gì thì dừng.
    FOR i IN 1..4 LOOP
        so_dong := 0;
        FOR r IN
            SELECT table_name FROM information_schema.columns
            WHERE column_name = 'tenant_id' AND table_schema = 'public'
            ORDER BY table_name
        LOOP
            BEGIN
                EXECUTE format(
                    'DELETE FROM %I x WHERE NOT EXISTS '
                    '(SELECT 1 FROM "TENANT" t WHERE t.id = x.tenant_id)', r.table_name);
                GET DIAGNOSTICS so_dong = ROW_COUNT;
                tong := tong + so_dong;
            EXCEPTION WHEN foreign_key_violation THEN
                -- Bảng cha còn bị con tham chiếu — vòng sau sẽ xoá được.
                NULL;
            END;
        END LOOP;
        EXIT WHEN so_dong = 0;
    END LOOP;

    RAISE NOTICE 'Đã xoá % hàng mồ côi', tong;
END $$;

-- Kiểm chứng thủ công sau khi chạy:
--   SELECT table_name FROM information_schema.columns WHERE column_name='tenant_id';
-- rồi đếm hàng mồ côi của từng bảng — phải là 0 hết.
