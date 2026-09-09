using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLopHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOP_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    giao_vien_chinh_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hinh_thuc = table.Column<int>(type: "integer", nullable: false),
                    phong_hoc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    link_hoc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hoc_phi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    suc_chua_toi_da = table.Column<int>(type: "integer", nullable: true),
                    ngay_khai_giang = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_ket_thuc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nhan_ban_tu_lop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguoi_tao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lop_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_lop_hoc_lop_hoc_nhan_ban_tu_lop_id",
                        column: x => x.nhan_ban_tu_lop_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lop_hoc_nguoi_dungs_giao_vien_chinh_id",
                        column: x => x.giao_vien_chinh_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lop_hoc_nguoi_dungs_nguoi_tao_id",
                        column: x => x.nguoi_tao_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LOP_HOC_HOC_VIEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_vao_lop = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_roi_lop = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    hoc_phi_ap_dung = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lop_hoc_hoc_vien", x => x.id);
                    table.ForeignKey(
                        name: "fk_lop_hoc_hoc_vien_lop_hoc_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lop_hoc_hoc_vien_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LOP_HOC_TRO_GIANG",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tro_giang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lop_hoc_tro_giang", x => x.id);
                    table.ForeignKey(
                        name: "fk_lop_hoc_tro_giang_lop_hoc_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lop_hoc_tro_giang_nguoi_dungs_tro_giang_id",
                        column: x => x.tro_giang_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_giao_vien_chinh_id",
                table: "LOP_HOC",
                column: "giao_vien_chinh_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_nguoi_tao_id",
                table: "LOP_HOC",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_nhan_ban_tu_lop_id",
                table: "LOP_HOC",
                column: "nhan_ban_tu_lop_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tenant_id_ten",
                table: "LOP_HOC",
                columns: new[] { "tenant_id", "ten" },
                unique: true,
                filter: "trang_thai <> 0");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tenant_id_trang_thai",
                table: "LOP_HOC",
                columns: new[] { "tenant_id", "trang_thai" });

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_hoc_vien_hoc_vien_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_hoc_vien_lop_hoc_id_hoc_vien_id",
                table: "LOP_HOC_HOC_VIEN",
                columns: new[] { "lop_hoc_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_hoc_vien_tenant_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tro_giang_lop_hoc_id_tro_giang_id",
                table: "LOP_HOC_TRO_GIANG",
                columns: new[] { "lop_hoc_id", "tro_giang_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tro_giang_tenant_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tro_giang_tro_giang_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "tro_giang_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropTable(
                name: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropTable(
                name: "LOP_HOC");
        }
    }
}
