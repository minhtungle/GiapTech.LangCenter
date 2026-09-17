using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChamRiengGiaoVienTroGiangVaChuyenLich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_diem_tieu_chi_nhan_xet_buoi_hoc_id_tieu_chi_id",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI",
                column: "nguoi_duoc_cham_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_nhan_xet_buoi_hoc_id_tieu_chi_id_nguoi_duoc_c",
                table: "DIEM_TIEU_CHI",
                columns: new[] { "nhan_xet_buoi_hoc_id", "tieu_chi_id", "nguoi_duoc_cham_id" },
                unique: true,
                filter: "nhan_xet_buoi_hoc_id IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_diem_tieu_chi_nguoi_cham_chi_cua_buoi",
                table: "DIEM_TIEU_CHI",
                sql: "nguoi_duoc_cham_id IS NULL OR nhan_xet_buoi_hoc_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_diem_tieu_chi_nguoi_dungs_nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI",
                column: "nguoi_duoc_cham_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_diem_tieu_chi_nguoi_dungs_nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.DropIndex(
                name: "ix_diem_tieu_chi_nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.DropIndex(
                name: "ix_diem_tieu_chi_nhan_xet_buoi_hoc_id_tieu_chi_id_nguoi_duoc_c",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.DropCheckConstraint(
                name: "ck_diem_tieu_chi_nguoi_cham_chi_cua_buoi",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.DropColumn(
                name: "nguoi_duoc_cham_id",
                table: "DIEM_TIEU_CHI");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_nhan_xet_buoi_hoc_id_tieu_chi_id",
                table: "DIEM_TIEU_CHI",
                columns: new[] { "nhan_xet_buoi_hoc_id", "tieu_chi_id" },
                unique: true,
                filter: "nhan_xet_buoi_hoc_id IS NOT NULL");
        }
    }
}
