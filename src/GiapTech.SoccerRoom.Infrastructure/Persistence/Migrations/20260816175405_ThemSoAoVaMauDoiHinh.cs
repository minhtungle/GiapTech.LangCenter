using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemSoAoVaMauDoiHinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "so_ao",
                table: "CAU_THU",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vi_tri_so_truong",
                table: "CAU_THU",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MAU_DOI_HINH",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    loai_san = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    noi_dung_json = table.Column<string>(type: "jsonb", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mau_doi_hinh", x => x.id);
                    table.ForeignKey(
                        name: "fk_mau_doi_hinh_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mau_doi_hinh_tenant_id",
                table: "MAU_DOI_HINH",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mau_doi_hinh_tenant_id_ten",
                table: "MAU_DOI_HINH",
                columns: new[] { "tenant_id", "ten" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MAU_DOI_HINH");

            migrationBuilder.DropColumn(
                name: "so_ao",
                table: "CAU_THU");

            migrationBuilder.DropColumn(
                name: "vi_tri_so_truong",
                table: "CAU_THU");
        }
    }
}
