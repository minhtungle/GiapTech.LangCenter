using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemBuoiHocVaDiemDanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BUOI_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    bat_dau = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ket_thuc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    giao_vien_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    la_hoc_bu = table.Column<bool>(type: "boolean", nullable: false),
                    phong_hoc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    link_hoc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buoi_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_buoi_hoc_lop_hocs_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buoi_hoc_nguoi_dungs_giao_vien_id",
                        column: x => x.giao_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DIEM_DANH",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    buoi_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trang_thai_tu_khai = table.Column<int>(type: "integer", nullable: true),
                    thoi_diem_tu_check_in = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trang_thai_chinh_thuc = table.Column<int>(type: "integer", nullable: false),
                    nguon_ghi_nhan = table.Column<int>(type: "integer", nullable: false),
                    ly_do_vang = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nguoi_xac_nhan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    thoi_diem_xac_nhan = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diem_danh", x => x.id);
                    table.ForeignKey(
                        name: "fk_diem_danh_buoi_hoc_buoi_hoc_id",
                        column: x => x.buoi_hoc_id,
                        principalTable: "BUOI_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_diem_danh_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_diem_danh_nguoi_dungs_nguoi_xac_nhan_id",
                        column: x => x.nguoi_xac_nhan_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buoi_hoc_giao_vien_id_bat_dau",
                table: "BUOI_HOC",
                columns: new[] { "giao_vien_id", "bat_dau" });

            migrationBuilder.CreateIndex(
                name: "ix_buoi_hoc_lop_hoc_id_thu_tu",
                table: "BUOI_HOC",
                columns: new[] { "lop_hoc_id", "thu_tu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_buoi_hoc_tenant_id_bat_dau",
                table: "BUOI_HOC",
                columns: new[] { "tenant_id", "bat_dau" });

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_buoi_hoc_id_hoc_vien_id",
                table: "DIEM_DANH",
                columns: new[] { "buoi_hoc_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_hoc_vien_id",
                table: "DIEM_DANH",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_nguoi_xac_nhan_id",
                table: "DIEM_DANH",
                column: "nguoi_xac_nhan_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_tenant_id",
                table: "DIEM_DANH",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DIEM_DANH");

            migrationBuilder.DropTable(
                name: "BUOI_HOC");
        }
    }
}
