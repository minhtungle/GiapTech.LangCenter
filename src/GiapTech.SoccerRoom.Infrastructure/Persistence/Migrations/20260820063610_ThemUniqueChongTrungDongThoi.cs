using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemUniqueChongTrungDongThoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_loi_moi_link_doi_thu_id",
                table: "LOI_MOI_LINK");

            migrationBuilder.CreateIndex(
                name: "UQ_LOI_MOI_LINK_dang_cho",
                table: "LOI_MOI_LINK",
                column: "doi_thu_id",
                unique: true,
                filter: "trang_thai = 0 AND thu_hoi_luc IS NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_LOI_MOI_BAT_DOI_dang_cho",
                table: "LOI_MOI_BAT_DOI",
                columns: new[] { "tenant_gui_id", "tenant_nhan_id" },
                unique: true,
                filter: "trang_thai = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_DOI_THU_tenant_ma_doi_he_thong",
                table: "DOI_THU",
                columns: new[] { "tenant_id", "ma_doi_he_thong" },
                unique: true,
                filter: "ma_doi_he_thong IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_LOI_MOI_LINK_dang_cho",
                table: "LOI_MOI_LINK");

            migrationBuilder.DropIndex(
                name: "UQ_LOI_MOI_BAT_DOI_dang_cho",
                table: "LOI_MOI_BAT_DOI");

            migrationBuilder.DropIndex(
                name: "UQ_DOI_THU_tenant_ma_doi_he_thong",
                table: "DOI_THU");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_link_doi_thu_id",
                table: "LOI_MOI_LINK",
                column: "doi_thu_id");
        }
    }
}
