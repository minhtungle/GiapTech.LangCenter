using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemYeuCauXepLop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YEU_CAU_XEP_LOP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dang_ky_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    thoi_diem_gui = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nguoi_gui_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    thoi_diem_xep = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nguoi_duyet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_yeu_cau_xep_lop", x => x.id);
                    table.ForeignKey(
                        name: "fk_yeu_cau_xep_lop_dang_ky_khoa_hoc_dang_ky_id",
                        column: x => x.dang_ky_id,
                        principalTable: "DANG_KY_KHOA_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_yeu_cau_xep_lop_lop_hoc_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_yeu_cau_xep_lop_nguoi_dung_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_yeu_cau_xep_lop_nguoi_dung_nguoi_duyet_id",
                        column: x => x.nguoi_duyet_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_yeu_cau_xep_lop_nguoi_dung_nguoi_gui_id",
                        column: x => x.nguoi_gui_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_dang_ky_id",
                table: "YEU_CAU_XEP_LOP",
                column: "dang_ky_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_hoc_vien_id",
                table: "YEU_CAU_XEP_LOP",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_lop_hoc_id",
                table: "YEU_CAU_XEP_LOP",
                column: "lop_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_nguoi_duyet_id",
                table: "YEU_CAU_XEP_LOP",
                column: "nguoi_duyet_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_nguoi_gui_id",
                table: "YEU_CAU_XEP_LOP",
                column: "nguoi_gui_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_tenant_id",
                table: "YEU_CAU_XEP_LOP",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_trang_thai",
                table: "YEU_CAU_XEP_LOP",
                column: "trang_thai");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YEU_CAU_XEP_LOP");
        }
    }
}
