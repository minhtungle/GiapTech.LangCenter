using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemNguoiTaoKhachHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_tao_id",
                table: "KHACH_HANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_nguoi_tao_id",
                table: "KHACH_HANG",
                column: "nguoi_tao_id");

            migrationBuilder.AddForeignKey(
                name: "fk_khach_hang_nguoi_dungs_nguoi_tao_id",
                table: "KHACH_HANG",
                column: "nguoi_tao_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_khach_hang_nguoi_dungs_nguoi_tao_id",
                table: "KHACH_HANG");

            migrationBuilder.DropIndex(
                name: "ix_khach_hang_nguoi_tao_id",
                table: "KHACH_HANG");

            migrationBuilder.DropColumn(
                name: "nguoi_tao_id",
                table: "KHACH_HANG");
        }
    }
}
