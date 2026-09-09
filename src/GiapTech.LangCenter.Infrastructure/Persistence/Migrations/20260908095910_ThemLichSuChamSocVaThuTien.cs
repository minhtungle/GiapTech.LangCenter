using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLichSuChamSocVaThuTien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LICH_SU_CHAM_SOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khach_hang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thoi_diem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hinh_thuc = table.Column<int>(type: "integer", nullable: false),
                    noi_dung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    trang_thai_sau = table.Column<int>(type: "integer", nullable: false),
                    nguoi_phu_trach_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lich_su_cham_soc", x => x.id);
                    table.ForeignKey(
                        name: "fk_lich_su_cham_soc_khach_hang_khach_hang_id",
                        column: x => x.khach_hang_id,
                        principalTable: "KHACH_HANG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lich_su_cham_soc_nguoi_dungs_nguoi_phu_trach_id",
                        column: x => x.nguoi_phu_trach_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "THU_TIEN_DANG_KY",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dang_ky_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ngay_thu = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    phuong_thuc = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nguoi_thu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_thu_tien_dang_ky", x => x.id);
                    table.CheckConstraint("ck_thu_tien_dang_ky_duong", "so_tien > 0");
                    table.ForeignKey(
                        name: "fk_thu_tien_dang_ky_dang_ky_khoa_hoc_dang_ky_id",
                        column: x => x.dang_ky_id,
                        principalTable: "DANG_KY_KHOA_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_thu_tien_dang_ky_nguoi_dung_nguoi_thu_id",
                        column: x => x.nguoi_thu_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_cham_soc_khach_hang_id_thoi_diem",
                table: "LICH_SU_CHAM_SOC",
                columns: new[] { "khach_hang_id", "thoi_diem" });

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_cham_soc_nguoi_phu_trach_id",
                table: "LICH_SU_CHAM_SOC",
                column: "nguoi_phu_trach_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_cham_soc_tenant_id",
                table: "LICH_SU_CHAM_SOC",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_thu_tien_dang_ky_dang_ky_id",
                table: "THU_TIEN_DANG_KY",
                column: "dang_ky_id");

            migrationBuilder.CreateIndex(
                name: "ix_thu_tien_dang_ky_nguoi_thu_id",
                table: "THU_TIEN_DANG_KY",
                column: "nguoi_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_thu_tien_dang_ky_tenant_id",
                table: "THU_TIEN_DANG_KY",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropTable(
                name: "THU_TIEN_DANG_KY");
        }
    }
}
