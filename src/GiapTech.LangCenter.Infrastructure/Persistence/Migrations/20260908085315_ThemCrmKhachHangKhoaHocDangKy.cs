using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemCrmKhachHangKhoaHocDangKy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KHACH_HANG",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ho_ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    so_dien_thoai = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    link_facebook = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    phuong_thuc_thanh_toan = table.Column<int>(type: "integer", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khach_hang", x => x.id);
                    table.ForeignKey(
                        name: "fk_khach_hang_nguoi_dungs_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KHOA_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    gia_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    don_vi_tien = table.Column<int>(type: "integer", nullable: false),
                    so_buoi = table.Column<int>(type: "integer", nullable: false),
                    dang_ban = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khoa_hoc", x => x.id);
                    table.CheckConstraint("ck_khoa_hoc_gia_khong_am", "gia_tien >= 0");
                });

            migrationBuilder.CreateTable(
                name: "DANG_KY_KHOA_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khach_hang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoa_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gia_goc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    so_tien = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    don_vi_tien = table.Column<int>(type: "integer", nullable: false),
                    ty_gia_ve_vnd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ngay_dang_ky = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    phuong_thuc = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dang_ky_khoa_hoc", x => x.id);
                    table.CheckConstraint("ck_dang_ky_gia_goc_khong_am", "gia_goc >= 0");
                    table.CheckConstraint("ck_dang_ky_so_tien_khong_am", "so_tien >= 0");
                    table.CheckConstraint("ck_dang_ky_ty_gia_duong", "ty_gia_ve_vnd > 0");
                    table.ForeignKey(
                        name: "fk_dang_ky_khoa_hoc_khach_hangs_khach_hang_id",
                        column: x => x.khach_hang_id,
                        principalTable: "KHACH_HANG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dang_ky_khoa_hoc_khoa_hocs_khoa_hoc_id",
                        column: x => x.khoa_hoc_id,
                        principalTable: "KHOA_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_khach_hang_id",
                table: "DANG_KY_KHOA_HOC",
                column: "khach_hang_id");

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_khoa_hoc_id",
                table: "DANG_KY_KHOA_HOC",
                column: "khoa_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_ngay_dang_ky",
                table: "DANG_KY_KHOA_HOC",
                column: "ngay_dang_ky");

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_tenant_id",
                table: "DANG_KY_KHOA_HOC",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_ho_ten",
                table: "KHACH_HANG",
                column: "ho_ten");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_nguoi_dung_id",
                table: "KHACH_HANG",
                column: "nguoi_dung_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_tenant_id",
                table: "KHACH_HANG",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_tenant_id_so_dien_thoai",
                table: "KHACH_HANG",
                columns: new[] { "tenant_id", "so_dien_thoai" },
                unique: true,
                filter: "so_dien_thoai IS NOT NULL AND so_dien_thoai <> ''");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_hoc_tenant_id",
                table: "KHOA_HOC",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_hoc_tenant_id_ten",
                table: "KHOA_HOC",
                columns: new[] { "tenant_id", "ten" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropTable(
                name: "KHACH_HANG");

            migrationBuilder.DropTable(
                name: "KHOA_HOC");
        }
    }
}
