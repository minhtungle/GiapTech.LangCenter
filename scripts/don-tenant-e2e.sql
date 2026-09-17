-- Dọn tenant do E2E sinh ra — CHỈ DÙNG Ở DEV.
--
-- Mỗi test E2E tự tạo một trung tâm qua `/dang-ky-trung-tam` (xem `e2e/tro-giup.ts`) và không
-- dọn sau khi chạy. Sau vài trăm lượt chạy, DB dev phình lên và mọi truy vấn tay phải lọc
-- `ma_trung_tam='W686AE9'` mới đọc được — chính nó làm chậm việc rà dữ liệu thật.
--
-- AN TOÀN:
--   * Chỉ xoá tenant có tên bắt đầu bằng 'E2E ' — đúng tiền tố `taoTrungTam()` đặt.
--   * Tenant thật liệt kê TƯỜNG MINH ở `giu_lai`, không dựa vào "cái nào không phải E2E":
--     một tenant thật lỡ đặt tên bắt đầu bằng "E2E" sẽ bị xoá oan.
--   * Chạy trong MỘT transaction, in số trước/sau để đối chiếu.
--
-- Xoá theo tenant chứ không theo bảng: mọi bảng nghiệp vụ đều có `tenant_id`, và khoá ngoại
-- giữa chúng là `ON DELETE CASCADE` từ `TENANT` — nên xoá hàng ở `TENANT` là đủ. Nếu một bảng
-- nào đó KHÔNG cascade, lệnh sẽ báo lỗi khoá ngoại và cả transaction bị huỷ, chứ không xoá dở.
--
-- Chạy:
--   docker exec -i lms-pg psql -U langcenter -d langcenter < scripts/don-tenant-e2e.sql

BEGIN;

\echo '--- TRƯỚC ---'
SELECT
  (SELECT count(*) FROM "TENANT") AS tenant,
  (SELECT count(*) FROM "NGUOI_DUNG") AS nguoi_dung,
  (SELECT count(*) FROM "TAI_KHOAN") AS tai_khoan,
  (SELECT count(*) FROM "LOP_HOC") AS lop_hoc,
  (SELECT count(*) FROM "BUOI_HOC") AS buoi_hoc;

-- Danh sách tenant PHẢI GIỮ. Thêm mã mới vào đây khi có trung tâm thật thứ hai.
CREATE TEMP TABLE giu_lai(ma text);
INSERT INTO giu_lai VALUES ('W686AE9');

-- Chốt an toàn: không cho chạy nếu mã trong `giu_lai` không tồn tại (gõ sai mã ⇒ xoá sạch).
DO $$
DECLARE thieu int;
BEGIN
  SELECT count(*) INTO thieu FROM giu_lai g
  WHERE NOT EXISTS (SELECT 1 FROM "TENANT" t WHERE t.ma_trung_tam = g.ma);
  IF thieu > 0 THEN
    RAISE EXCEPTION 'Có % mã trong danh sách giữ lại KHÔNG tồn tại — dừng để khỏi xoá nhầm.', thieu;
  END IF;
END $$;

/*
  Xoá theo TỪNG BẢNG, lặp cho tới khi hết — không xoá thẳng ở `TENANT`.

  Lý do: nhiều khoá ngoại giữa các bảng nghiệp vụ là `RESTRICT` (cố ý — chặn xoá nhầm dữ liệu
  thật), nên `DELETE FROM "TENANT"` sẽ đứng ngay ở `LOP_HOC -> NGUOI_DUNG`. Đã gặp thật lúc
  dry-run trên DB bản sao, transaction rollback sạch.

  Vòng lặp thay cho một danh sách thứ tự viết tay: thứ tự đúng phụ thuộc đồ thị khoá ngoại, mà
  đồ thị đó đổi mỗi lần thêm bảng — danh sách tay sẽ lạc hậu im lặng. Mỗi vòng thử xoá mọi bảng;
  bảng nào còn bị tham chiếu thì bỏ qua, vòng sau thử lại. Hết vòng mà vẫn còn hàng = có chu
  trình hoặc khoá ngoại thiếu, và ta RAISE để biết chứ không xoá dở.
*/
DO $$
DECLARE
  ten_bang text;
  con_lai int;
  vong int := 0;
  ids uuid[];
BEGIN
  SELECT array_agg(id) INTO ids FROM "TENANT"
  WHERE ten_trung_tam LIKE 'E2E %' AND ma_trung_tam NOT IN (SELECT ma FROM giu_lai);

  IF ids IS NULL THEN RAISE NOTICE 'Không có tenant E2E nào.'; RETURN; END IF;
  RAISE NOTICE 'Sẽ xoá % tenant E2E.', array_length(ids, 1);

  FOR vong IN 1..20 LOOP
    FOR ten_bang IN
      SELECT table_name FROM information_schema.columns
      WHERE column_name = 'tenant_id' AND table_schema = 'public'
    LOOP
      BEGIN
        EXECUTE format('DELETE FROM %I WHERE tenant_id = ANY($1)', ten_bang) USING ids;
      EXCEPTION WHEN foreign_key_violation THEN
        NULL;   -- còn bảng con tham chiếu; vòng sau thử lại
      END;
    END LOOP;

    SELECT count(*) INTO con_lai FROM "NGUOI_DUNG" WHERE tenant_id = ANY(ids);
    EXIT WHEN con_lai = 0;
  END LOOP;

  IF con_lai > 0 THEN
    RAISE EXCEPTION 'Còn % người dùng của tenant E2E sau % vòng — kiểm lại khoá ngoại.',
      con_lai, vong;
  END IF;

  DELETE FROM "TENANT" WHERE id = ANY(ids);
  RAISE NOTICE 'Xong sau % vòng.', vong;
END $$;

\echo '--- SAU ---'
SELECT
  (SELECT count(*) FROM "TENANT") AS tenant,
  (SELECT count(*) FROM "NGUOI_DUNG") AS nguoi_dung,
  (SELECT count(*) FROM "TAI_KHOAN") AS tai_khoan,
  (SELECT count(*) FROM "LOP_HOC") AS lop_hoc,
  (SELECT count(*) FROM "BUOI_HOC") AS buoi_hoc;

-- Chốt cuối: tenant thật phải còn, và còn đủ dữ liệu.
DO $$
DECLARE con int;
BEGIN
  SELECT count(*) INTO con FROM "TENANT" WHERE ma_trung_tam = 'W686AE9';
  IF con <> 1 THEN RAISE EXCEPTION 'MẤT tenant thật W686AE9 — huỷ toàn bộ.'; END IF;

  SELECT count(*) INTO con FROM "NGUOI_DUNG" n
  JOIN "TENANT" t ON t.id = n.tenant_id WHERE t.ma_trung_tam = 'W686AE9';
  IF con < 70 THEN RAISE EXCEPTION 'W686AE9 chỉ còn % người dùng (chờ ~75) — huỷ.', con; END IF;
END $$;

COMMIT;

VACUUM (ANALYZE);
