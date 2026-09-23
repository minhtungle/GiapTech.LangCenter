using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Infrastructure.Persistence;

/// <summary>
/// Dọn trung tâm rác của E2E bằng **vòng lặp xoá**, không dựa vào Cascade (nợ N11).
///
/// ## Vì sao không dùng Cascade của EF
///
/// Bản đầu gọi `db.Tenants.RemoveRange(...)` và tin rằng EF sẽ xoá đúng thứ tự. Nó KHÔNG:
///
///   23503: update or delete on table "NGUOI_DUNG" violates foreign key constraint
///   "fk_lop_hoc_nguoi_dungs_giao_vien_chinh_id" on table "LOP_HOC"
///
/// `LOP_HOC.giao_vien_chinh_id` là RESTRICT, nên xoá `NGUOI_DUNG` trước `LOP_HOC` là chết.
/// EF không sắp được thứ tự cho đồ thị phụ thuộc này. Gặp thật khi chạy E2E 23/09/2026 — và
/// trước đó đã gặp đúng lỗi này mỗi lần dọn tay.
///
/// ## Cách làm: để PostgreSQL tự quyết thứ tự
///
/// Lặp qua mọi bảng có `tenant_id`, bỏ qua bảng nào đang vướng khoá ngoại, lặp lại cho tới khi
/// không còn xoá được gì. Thực tế xong sau **2 vòng**.
///
/// Không tự đoán thứ tự phụ thuộc: sơ đồ 45 bảng đổi theo từng tính năng mới, và một thứ tự
/// chép cứng sẽ hỏng âm thầm vào lúc nào đó không ai để ý.
/// </summary>
public class DonTenantE2E(AppDbContext db) : IDonTenantE2E
{
    public async Task<int> XoaAsync(string tienTo, CancellationToken ct = default)
    {
        // Lấy id TRƯỚC bằng LINQ (đã tham số hoá, không có đường tiêm SQL), rồi truyền
        // mảng id xuống khối PL/pgSQL.
        //
        // Vì sao không đặt `{0}` thẳng trong khối `DO $$...$$`: PostgreSQL coi cả khối là MỘT
        // CHUỖI ký tự, nên tham số không được thay vào bên trong — chạy thật báo
        // `42703: column "p0" does not exist`. Mất một vòng thử mới ra.
        var ids = await db.Tenants
            .Where(t => t.TenTrungTam.StartsWith(tienTo))
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (ids.Count == 0) return 0;

        // Danh sách id dựng từ GUID đã parse, không phải chuỗi người dùng nhập — nối vào SQL
        // ở đây an toàn, và là cách duy nhất đưa mảng vào khối DO.
        var danhSach = string.Join(",", ids.Select(i => $"'{i}'::uuid"));

        var sql = $$"""
            DO $$
            DECLARE
              t   text;
              con bigint;
              vong int := 0;
              ids uuid[] := ARRAY[{{danhSach}}];
            BEGIN
              LOOP
                vong := vong + 1;
                con  := 0;
                IF vong > 50 THEN
                  RAISE EXCEPTION 'Quá 50 vòng — có vòng lặp phụ thuộc, dừng.';
                END IF;

                FOR t IN
                  SELECT table_name FROM information_schema.columns
                  WHERE column_name = 'tenant_id' AND table_schema = 'public'
                LOOP
                  BEGIN
                    EXECUTE format('DELETE FROM %I WHERE tenant_id = ANY($1)', t) USING ids;
                  EXCEPTION WHEN foreign_key_violation THEN
                    con := con + 1;   -- còn bảng khác trỏ vào; vòng sau xoá lại
                  END;
                END LOOP;

                EXIT WHEN con = 0;
              END LOOP;

              DELETE FROM "TENANT" WHERE id = ANY(ids);
            END $$;
            """;

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        return ids.Count;
    }
}
