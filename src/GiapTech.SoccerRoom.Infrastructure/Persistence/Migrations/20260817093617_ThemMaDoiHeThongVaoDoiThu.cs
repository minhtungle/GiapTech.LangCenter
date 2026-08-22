using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemMaDoiHeThongVaoDoiThu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ma_doi_he_thong",
                table: "DOI_THU",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ma_doi_he_thong",
                table: "DOI_THU");
        }
    }
}
