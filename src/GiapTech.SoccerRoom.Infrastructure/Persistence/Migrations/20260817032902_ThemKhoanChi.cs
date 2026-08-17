using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemKhoanChi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KHOAN_CHI",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    noi_dung = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    so_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ngay_chi = table.Column<DateOnly>(type: "date", nullable: false),
                    nguoi_chi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khoan_chi", x => x.id);
                    table.ForeignKey(
                        name: "fk_khoan_chi_quys_quy_id",
                        column: x => x.quy_id,
                        principalTable: "QUY",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_khoan_chi_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_khoan_chi_ngay_chi",
                table: "KHOAN_CHI",
                column: "ngay_chi");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_chi_quy_id",
                table: "KHOAN_CHI",
                column: "quy_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_chi_tenant_id",
                table: "KHOAN_CHI",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KHOAN_CHI");
        }
    }
}
