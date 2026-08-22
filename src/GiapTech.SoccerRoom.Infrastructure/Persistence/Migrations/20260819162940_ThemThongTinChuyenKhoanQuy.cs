using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemThongTinChuyenKhoanQuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "anh_qr_url",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chu_tai_khoan",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "so_tai_khoan",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ten_ngan_hang",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "hien_thong_tin_chuyen_khoan",
                table: "QUY",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anh_qr_url",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "chu_tai_khoan",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "so_tai_khoan",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "ten_ngan_hang",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "hien_thong_tin_chuyen_khoan",
                table: "QUY");
        }
    }
}
