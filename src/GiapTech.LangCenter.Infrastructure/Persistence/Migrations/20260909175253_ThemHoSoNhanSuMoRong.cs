using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHoSoNhanSuMoRong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tep_dinh_kem_dung_mot_chu",
                table: "TEP_DINH_KEM");

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_dung_id",
                table: "TEP_DINH_KEM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cccd",
                table: "NGUOI_DUNG",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ghi_chu",
                table: "NGUOI_DUNG",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "so_tai_khoan",
                table: "NGUOI_DUNG",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ten_ngan_hang",
                table: "NGUOI_DUNG",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LIEN_KET_MXH",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loai = table.Column<int>(type: "integer", nullable: false),
                    duong_dan = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lien_ket_mxh", x => x.id);
                    table.ForeignKey(
                        name: "fk_lien_ket_mxh_nguoi_dungs_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_nguoi_dung_id",
                table: "TEP_DINH_KEM",
                column: "nguoi_dung_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tep_dinh_kem_dung_mot_chu",
                table: "TEP_DINH_KEM",
                sql: "(CASE WHEN bai_tap_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_nop_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_kiem_tra_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_lam_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN tai_lieu_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN nguoi_dung_id IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_lien_ket_mxh_nguoi_dung_id",
                table: "LIEN_KET_MXH",
                column: "nguoi_dung_id");

            migrationBuilder.CreateIndex(
                name: "ix_lien_ket_mxh_tenant_id",
                table: "LIEN_KET_MXH",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_nguoi_dung_id",
                table: "TEP_DINH_KEM",
                column: "nguoi_dung_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_nguoi_dung_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropTable(
                name: "LIEN_KET_MXH");

            migrationBuilder.DropIndex(
                name: "ix_tep_dinh_kem_nguoi_dung_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tep_dinh_kem_dung_mot_chu",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropColumn(
                name: "nguoi_dung_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropColumn(
                name: "cccd",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "ghi_chu",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "so_tai_khoan",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "ten_ngan_hang",
                table: "NGUOI_DUNG");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tep_dinh_kem_dung_mot_chu",
                table: "TEP_DINH_KEM",
                sql: "(CASE WHEN bai_tap_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_nop_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_kiem_tra_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_lam_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN tai_lieu_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
        }
    }
}
