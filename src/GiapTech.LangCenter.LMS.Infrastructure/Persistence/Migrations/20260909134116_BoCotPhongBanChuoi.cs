using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoCotPhongBanChuoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phong_ban",
                table: "HO_SO_NHAN_VIEN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phong_ban",
                table: "HO_SO_NHAN_VIEN",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
