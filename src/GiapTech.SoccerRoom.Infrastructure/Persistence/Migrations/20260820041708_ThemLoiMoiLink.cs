using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLoiMoiLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOI_MOI_LINK",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doi_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    thoi_gian_de_xuat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dia_diem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    loi_nhan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    phan_hoi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thoi_gian_phan_hoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    thu_hoi_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_nhan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    da_huy_lien_ket = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loi_moi_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_loi_moi_link_doi_thu_doi_thu_id",
                        column: x => x.doi_thu_id,
                        principalTable: "DOI_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_loi_moi_link_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_loi_moi_link_tran_daus_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_link_doi_thu_id",
                table: "LOI_MOI_LINK",
                column: "doi_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_link_tenant_id",
                table: "LOI_MOI_LINK",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_link_token_hash",
                table: "LOI_MOI_LINK",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_link_tran_dau_id",
                table: "LOI_MOI_LINK",
                column: "tran_dau_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOI_MOI_LINK");
        }
    }
}
