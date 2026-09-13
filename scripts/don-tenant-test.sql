-- Xoá MỌI tenant trừ tenant đang dùng thật, rồi dọn sạch dữ liệu của chúng.
--
-- Vì sao cần: mỗi lần chạy bộ E2E sinh ~23 tenant mới (nợ N15 — mỗi test tự tạo trung tâm
-- riêng). Không dọn thì DB phình ra và mọi phép đếm khi rà soát đều sai.
--
-- Cách làm: xoá dữ liệu con TRƯỚC rồi mới xoá hàng TENANT, lặp nhiều vòng cho tới khi không
-- xoá thêm được gì. Không liệt kê bảng bằng tay — thêm bảng mới không phải sửa script (bài học
-- 13/09/2026: 4 bảng elearning bị bỏ sót khỏi script cũ, để lại 36 hàng mồ côi).
--
-- AN TOÀN: sửa `TENANT_GIU` bên dưới cho đúng trung tâm đang dùng thật. Mọi tenant khác MẤT
-- HẲN cùng toàn bộ dữ liệu.
--
-- Chạy:  docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/don-tenant-test.sql
DO $$
DECLARE
    TENANT_GIU CONSTANT text := 'W686AE9';
    r        RECORD;
    so_dong  BIGINT;
    tong     BIGINT := 0;
    con_lai  BIGINT;
BEGIN
    -- Vòng 1..8: xoá dữ liệu nghiệp vụ của các tenant sắp bỏ. Nhiều vòng vì FK Restrict bắt
    -- phải xoá bảng con trước bảng cha, và độ sâu quan hệ không biết trước.
    FOR i IN 1..8 LOOP
        so_dong := 0;
        FOR r IN
            SELECT table_name FROM information_schema.columns
            WHERE column_name = 'tenant_id' AND table_schema = 'public'
              AND table_name <> 'TENANT'
            ORDER BY table_name
        LOOP
            BEGIN
                EXECUTE format(
                    'DELETE FROM %I x WHERE x.tenant_id NOT IN '
                    '(SELECT id FROM "TENANT" WHERE ma_trung_tam = %L)',
                    r.table_name, TENANT_GIU);
                GET DIAGNOSTICS so_dong = ROW_COUNT;
                tong := tong + so_dong;
            EXCEPTION WHEN foreign_key_violation THEN
                NULL;   -- vòng sau sẽ xoá được
            END;
        END LOOP;
        EXIT WHEN so_dong = 0;
    END LOOP;

    DELETE FROM "TENANT" WHERE ma_trung_tam <> TENANT_GIU;
    GET DIAGNOSTICS so_dong = ROW_COUNT;

    SELECT count(*) INTO con_lai FROM "TENANT";
    RAISE NOTICE 'Đã xoá % tenant và % hàng dữ liệu. Còn lại % tenant.',
        so_dong, tong, con_lai;
END $$;
