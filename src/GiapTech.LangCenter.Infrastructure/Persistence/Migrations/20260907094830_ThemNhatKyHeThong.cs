using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemNhatKyHeThong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NHAT_KY_HE_THONG",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten_lenh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    chuc_nang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hanh_dong = table.Column<int>(type: "integer", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ho_ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    thanh_cong = table.Column<bool>(type: "boolean", nullable: false),
                    ma_loi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tham_so = table.Column<string>(type: "text", nullable: true),
                    chi_tiet = table.Column<string>(type: "text", nullable: true),
                    so_ban_ghi_anh_huong = table.Column<int>(type: "integer", nullable: false),
                    dia_chi_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    so_mili_giay = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nhat_ky_he_thong", x => x.id);
                    table.ForeignKey(
                        name: "fk_nhat_ky_he_thong_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_nguoi_dung_id",
                table: "NHAT_KY_HE_THONG",
                column: "nguoi_dung_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_tenant_id_chuc_nang",
                table: "NHAT_KY_HE_THONG",
                columns: new[] { "tenant_id", "chuc_nang" });

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_tenant_id_ngay_tao",
                table: "NHAT_KY_HE_THONG",
                columns: new[] { "tenant_id", "ngay_tao" });

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_tenant_id_nguoi_dung_id",
                table: "NHAT_KY_HE_THONG",
                columns: new[] { "tenant_id", "nguoi_dung_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NHAT_KY_HE_THONG");
        }
    }
}
