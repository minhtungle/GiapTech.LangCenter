using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHocPhi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KHOAN_THU_HOC_PHI",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ngay_thu = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    phuong_thuc = table.Column<int>(type: "integer", nullable: false),
                    so_phieu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nguoi_thu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khoan_thu_hoc_phi", x => x.id);
                    table.CheckConstraint("ck_khoan_thu_so_tien_duong", "so_tien > 0");
                    table.ForeignKey(
                        name: "fk_khoan_thu_hoc_phi_lop_hocs_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_khoan_thu_hoc_phi_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_khoan_thu_hoc_phi_nguoi_dungs_nguoi_thu_id",
                        column: x => x.nguoi_thu_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_hoc_vien_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_lop_hoc_id_hoc_vien_id",
                table: "KHOAN_THU_HOC_PHI",
                columns: new[] { "lop_hoc_id", "hoc_vien_id" });

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_nguoi_thu_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "nguoi_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_tenant_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KHOAN_THU_HOC_PHI");
        }
    }
}
