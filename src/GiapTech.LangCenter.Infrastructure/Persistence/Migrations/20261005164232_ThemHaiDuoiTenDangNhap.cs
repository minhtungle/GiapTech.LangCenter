using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHaiDuoiTenDangNhap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "duoi_ten_dang_nhap_2",
                table: "TENANT",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "duoi_ten_dang_nhap_3",
                table: "TENANT",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duoi_ten_dang_nhap_2",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "duoi_ten_dang_nhap_3",
                table: "TENANT");
        }
    }
}
