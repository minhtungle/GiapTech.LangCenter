using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemChucVuVaDoiTenChucNang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "chuc_vu_id",
                table: "NGUOI_DUNG",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CHUC_VU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    dang_dung = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chuc_vu", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_chuc_vu_id",
                table: "NGUOI_DUNG",
                column: "chuc_vu_id");

            migrationBuilder.CreateIndex(
                name: "ix_chuc_vu_tenant_id",
                table: "CHUC_VU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_chuc_vu_tenant_id_ten",
                table: "CHUC_VU",
                columns: new[] { "tenant_id", "ten" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoi_dung_chuc_vu_chuc_vu_id",
                table: "NGUOI_DUNG",
                column: "chuc_vu_id",
                principalTable: "CHUC_VU",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ---------- CHUYỂN DỮ LIỆU (quy tắc #1) ----------
            //
            // Cột `HO_SO_NHAN_VIEN.chuc_vu` (chuỗi) có dữ liệu THẬT — đếm 09/09/2026: 40 hàng,
            // hai giá trị "Quản trị hệ thống" (39) và "Trưởng phòng nhân sự" (1). Xoá thẳng là
            // mất dữ liệu, nên phải: sinh danh mục từ chính các giá trị đang có → nối người vào
            // danh mục → mới xoá cột.
            //
            // EF sinh `DropColumn` NGAY ĐẦU `Up()`; đã chuyển xuống sau khối SQL này bằng tay.

            // 1. Sinh chức vụ từ các giá trị đang có, theo TỪNG tenant (tên chỉ duy nhất trong
            //    tenant). `gen_random_uuid()` có sẵn từ PostgreSQL 13.
            migrationBuilder.Sql("""
                INSERT INTO "CHUC_VU" (id, ten, mo_ta, thu_tu, dang_dung, ngay_tao, tenant_id)
                SELECT gen_random_uuid(), t.chuc_vu, NULL, 0, true, now(), t.tenant_id
                FROM (
                    SELECT DISTINCT hs.tenant_id, btrim(hs.chuc_vu) AS chuc_vu
                    FROM "HO_SO_NHAN_VIEN" hs
                    WHERE hs.chuc_vu IS NOT NULL AND btrim(hs.chuc_vu) <> ''
                ) t
                ON CONFLICT DO NOTHING;
                """);

            // 2. Nối người dùng vào chức vụ vừa sinh (khớp theo tenant + tên).
            migrationBuilder.Sql("""
                UPDATE "NGUOI_DUNG" nd
                SET chuc_vu_id = cv.id
                FROM "HO_SO_NHAN_VIEN" hs
                JOIN "CHUC_VU" cv
                  ON cv.tenant_id = hs.tenant_id AND cv.ten = btrim(hs.chuc_vu)
                WHERE hs.nguoi_dung_id = nd.id
                  AND hs.chuc_vu IS NOT NULL AND btrim(hs.chuc_vu) <> '';
                """);

            // 3. Đổi tên chức năng trong dữ liệu phân quyền — KHÔNG chỉ trong code.
            //    `GiaoVienNhanSu` → `NhanSu` (đổi tên vì nó gác cả ba vai trò nhân sự).
            //    `NhanVienKinhDoanh` → `ChucVu` (bỏ màn riêng, quyền cũ chuyển sang danh mục
            //    chức vụ để nhóm quyền đã cấp không mất tác dụng).
            //
            //    Bỏ trùng trước khi đổi: nếu một nhóm quyền đã có CẢ HAI thì đổi tên sẽ đụng
            //    UNIQUE(quyen_id, ten_chuc_nang, hanh_dong).
            migrationBuilder.Sql("""
                DELETE FROM "QUYEN_CHUC_NANG" q
                WHERE q.ten_chuc_nang = 'NhanVienKinhDoanh'
                  AND EXISTS (
                    SELECT 1 FROM "QUYEN_CHUC_NANG" x
                    WHERE x.quyen_id = q.quyen_id AND x.hanh_dong = q.hanh_dong
                      AND x.ten_chuc_nang = 'GiaoVienNhanSu');
                """);
            migrationBuilder.Sql("""
                UPDATE "QUYEN_CHUC_NANG" SET ten_chuc_nang = 'NhanSu'
                WHERE ten_chuc_nang = 'GiaoVienNhanSu';
                """);
            migrationBuilder.Sql("""
                UPDATE "QUYEN_CHUC_NANG" SET ten_chuc_nang = 'ChucVu'
                WHERE ten_chuc_nang = 'NhanVienKinhDoanh';
                """);

            // 4. Giờ mới xoá cột chuỗi — dữ liệu đã sang bảng danh mục.
            migrationBuilder.DropColumn(
                name: "chuc_vu",
                table: "HO_SO_NHAN_VIEN");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_nguoi_dung_chuc_vu_chuc_vu_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropTable(
                name: "CHUC_VU");

            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_chuc_vu_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "chuc_vu_id",
                table: "NGUOI_DUNG");

            migrationBuilder.AddColumn<string>(
                name: "chuc_vu",
                table: "HO_SO_NHAN_VIEN",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
