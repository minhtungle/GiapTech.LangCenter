using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemTagVaiTroPhongBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tag_vai_tro",
                table: "PHONG_BAN",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_tenant_id_tag_vai_tro",
                table: "PHONG_BAN",
                columns: new[] { "tenant_id", "tag_vai_tro" },
                filter: "tag_vai_tro IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_phong_ban_tenant_id_tag_vai_tro",
                table: "PHONG_BAN");

            migrationBuilder.DropColumn(
                name: "tag_vai_tro",
                table: "PHONG_BAN");
        }
    }
}
