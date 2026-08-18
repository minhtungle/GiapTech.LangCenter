using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemSanDoiThuVaLoiMoiBatDoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "khu_vuc",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lien_he_cong_khai",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "san_nha",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LOI_MOI_BAT_DOI",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_gui_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_nhan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thoi_gian_de_xuat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dia_diem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    loi_nhan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    phan_hoi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thoi_gian_phan_hoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tran_dau_nhan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tran_dau_gui_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loi_moi_bat_doi", x => x.id);
                    table.ForeignKey(
                        name: "fk_loi_moi_bat_doi_tenants_tenant_gui_id",
                        column: x => x.tenant_gui_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loi_moi_bat_doi_tenants_tenant_nhan_id",
                        column: x => x.tenant_nhan_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_bat_doi_tenant_gui_id",
                table: "LOI_MOI_BAT_DOI",
                column: "tenant_gui_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_bat_doi_tenant_nhan_id",
                table: "LOI_MOI_BAT_DOI",
                column: "tenant_nhan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOI_MOI_BAT_DOI");

            migrationBuilder.DropColumn(
                name: "khu_vuc",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "lien_he_cong_khai",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "san_nha",
                table: "TENANT");
        }
    }
}
