using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemSanPhamVaDonHangHaiLoai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "khoa_hoc_id",
                table: "DANG_KY_KHOA_HOC",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "san_pham_id",
                table: "DANG_KY_KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "so_luong",
                table: "DANG_KY_KHOA_HOC",
                type: "integer",
                nullable: false,
                // SỬA TAY từ `defaultValue: 0` do EF sinh: `CHECK (so_luong > 0)` bên dưới sẽ
                // LÀM MIGRATION THẤT BẠI trên mọi dòng đang có (đơn hàng cũ đều là khoá học,
                // số lượng đúng là 1). Kiểm bằng cách áp lên DB thật có 3 dòng dữ liệu.
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "SAN_PHAM",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    gia_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    don_vi_tien = table.Column<int>(type: "integer", nullable: false),
                    don_vi_tinh = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    dang_ban = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_san_pham", x => x.id);
                    table.CheckConstraint("ck_san_pham_gia_khong_am", "gia_tien >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_san_pham_id",
                table: "DANG_KY_KHOA_HOC",
                column: "san_pham_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dang_ky_dung_mot_loai",
                table: "DANG_KY_KHOA_HOC",
                sql: "(khoa_hoc_id IS NOT NULL AND san_pham_id IS NULL) OR (khoa_hoc_id IS NULL AND san_pham_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dang_ky_so_luong_duong",
                table: "DANG_KY_KHOA_HOC",
                sql: "so_luong > 0");

            migrationBuilder.CreateIndex(
                name: "ix_san_pham_tenant_id",
                table: "SAN_PHAM",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_san_pham_tenant_id_ten",
                table: "SAN_PHAM",
                columns: new[] { "tenant_id", "ten" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_dang_ky_khoa_hoc_san_phams_san_pham_id",
                table: "DANG_KY_KHOA_HOC",
                column: "san_pham_id",
                principalTable: "SAN_PHAM",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dang_ky_khoa_hoc_san_phams_san_pham_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropTable(
                name: "SAN_PHAM");

            migrationBuilder.DropIndex(
                name: "ix_dang_ky_khoa_hoc_san_pham_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dang_ky_dung_mot_loai",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dang_ky_so_luong_duong",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "san_pham_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "so_luong",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.AlterColumn<Guid>(
                name: "khoa_hoc_id",
                table: "DANG_KY_KHOA_HOC",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
