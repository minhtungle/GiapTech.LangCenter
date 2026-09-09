using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemNhanXetBuoiHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nhan_xet",
                table: "DIEM_DANH",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NHAN_XET_BUOI_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    buoi_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    noi_dung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    muc_hai_long = table.Column<int>(type: "integer", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nhan_xet_buoi_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_nhan_xet_buoi_hoc_buoi_hoc_buoi_hoc_id",
                        column: x => x.buoi_hoc_id,
                        principalTable: "BUOI_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nhan_xet_buoi_hoc_nguoi_dung_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nhan_xet_buoi_hoc_buoi_hoc_id_hoc_vien_id",
                table: "NHAN_XET_BUOI_HOC",
                columns: new[] { "buoi_hoc_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nhan_xet_buoi_hoc_hoc_vien_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhan_xet_buoi_hoc_tenant_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropColumn(
                name: "nhan_xet",
                table: "DIEM_DANH");
        }
    }
}
