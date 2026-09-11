using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLopHocKhoaHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOP_HOC_KHOA_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoa_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lop_hoc_khoa_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_lop_hoc_khoa_hoc_khoa_hoc_khoa_hoc_id",
                        column: x => x.khoa_hoc_id,
                        principalTable: "KHOA_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lop_hoc_khoa_hoc_lop_hoc_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_khoa_hoc_khoa_hoc_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "khoa_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_khoa_hoc_lop_hoc_id_khoa_hoc_id",
                table: "LOP_HOC_KHOA_HOC",
                columns: new[] { "lop_hoc_id", "khoa_hoc_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_khoa_hoc_tenant_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOP_HOC_KHOA_HOC");
        }
    }
}
