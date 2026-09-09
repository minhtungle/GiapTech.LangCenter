using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHocLieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BAI_KIEM_TRA",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    loai = table.Column<int>(type: "integer", nullable: false),
                    thoi_luong_phut = table.Column<int>(type: "integer", nullable: true),
                    mo_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dong_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    thang_diem = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    nguoi_tao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bai_kiem_tra", x => x.id);
                    table.ForeignKey(
                        name: "fk_bai_kiem_tra_lop_hocs_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bai_kiem_tra_nguoi_dungs_nguoi_tao_id",
                        column: x => x.nguoi_tao_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAI_TAP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    buoi_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    han_nop = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nguoi_tao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bai_tap", x => x.id);
                    table.ForeignKey(
                        name: "fk_bai_tap_buoi_hocs_buoi_hoc_id",
                        column: x => x.buoi_hoc_id,
                        principalTable: "BUOI_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bai_tap_nguoi_dungs_nguoi_tao_id",
                        column: x => x.nguoi_tao_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TAI_LIEU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    loai = table.Column<int>(type: "integer", nullable: false),
                    nguoi_tai_len_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tai_lieu", x => x.id);
                    table.ForeignKey(
                        name: "fk_tai_lieu_nguoi_dung_nguoi_tai_len_id",
                        column: x => x.nguoi_tai_len_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAI_LAM",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bai_kiem_tra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thoi_diem_nop = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    han_nop_rieng = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    diem = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    nhan_xet = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    nguoi_cham_id = table.Column<Guid>(type: "uuid", nullable: true),
                    thoi_diem_cham = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bai_lam", x => x.id);
                    table.ForeignKey(
                        name: "fk_bai_lam_bai_kiem_tra_bai_kiem_tra_id",
                        column: x => x.bai_kiem_tra_id,
                        principalTable: "BAI_KIEM_TRA",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bai_lam_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bai_lam_nguoi_dungs_nguoi_cham_id",
                        column: x => x.nguoi_cham_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAI_NOP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bai_tap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lan_nop = table.Column<int>(type: "integer", nullable: false),
                    thoi_diem_nop = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    noi_dung = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    diem = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    nhan_xet = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    nguoi_cham_id = table.Column<Guid>(type: "uuid", nullable: true),
                    thoi_diem_cham = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bai_nop", x => x.id);
                    table.ForeignKey(
                        name: "fk_bai_nop_bai_taps_bai_tap_id",
                        column: x => x.bai_tap_id,
                        principalTable: "BAI_TAP",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bai_nop_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bai_nop_nguoi_dungs_nguoi_cham_id",
                        column: x => x.nguoi_cham_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TAI_LIEU_LOP_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tai_lieu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lop_hoc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tai_lieu_lop_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_tai_lieu_lop_hoc_lop_hoc_lop_hoc_id",
                        column: x => x.lop_hoc_id,
                        principalTable: "LOP_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tai_lieu_lop_hoc_tai_lieu_tai_lieu_id",
                        column: x => x.tai_lieu_id,
                        principalTable: "TAI_LIEU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TEP_DINH_KEM",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bai_tap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bai_nop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bai_kiem_tra_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bai_lam_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tai_lieu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    khoa_luu_tru = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ten_goc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    loai_noi_dung = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    kich_thuoc = table.Column<long>(type: "bigint", nullable: false),
                    nguoi_tai_len_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tep_dinh_kem", x => x.id);
                    table.CheckConstraint("ck_tep_dinh_kem_dung_mot_chu", "(CASE WHEN bai_tap_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_nop_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_kiem_tra_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN bai_lam_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN tai_lieu_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_bai_kiem_tra_bai_kiem_tra_id",
                        column: x => x.bai_kiem_tra_id,
                        principalTable: "BAI_KIEM_TRA",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_bai_lam_bai_lam_id",
                        column: x => x.bai_lam_id,
                        principalTable: "BAI_LAM",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_bai_nop_bai_nop_id",
                        column: x => x.bai_nop_id,
                        principalTable: "BAI_NOP",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_bai_tap_bai_tap_id",
                        column: x => x.bai_tap_id,
                        principalTable: "BAI_TAP",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_nguoi_dung_nguoi_tai_len_id",
                        column: x => x.nguoi_tai_len_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tep_dinh_kem_tai_lieu_tai_lieu_id",
                        column: x => x.tai_lieu_id,
                        principalTable: "TAI_LIEU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_lop_hoc_id",
                table: "BAI_KIEM_TRA",
                column: "lop_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_nguoi_tao_id",
                table: "BAI_KIEM_TRA",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_tenant_id",
                table: "BAI_KIEM_TRA",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_bai_kiem_tra_id_hoc_vien_id",
                table: "BAI_LAM",
                columns: new[] { "bai_kiem_tra_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_hoc_vien_id",
                table: "BAI_LAM",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_nguoi_cham_id",
                table: "BAI_LAM",
                column: "nguoi_cham_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_tenant_id",
                table: "BAI_LAM",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_bai_tap_id_hoc_vien_id_lan_nop",
                table: "BAI_NOP",
                columns: new[] { "bai_tap_id", "hoc_vien_id", "lan_nop" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_hoc_vien_id",
                table: "BAI_NOP",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_nguoi_cham_id",
                table: "BAI_NOP",
                column: "nguoi_cham_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_tenant_id",
                table: "BAI_NOP",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_buoi_hoc_id",
                table: "BAI_TAP",
                column: "buoi_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_nguoi_tao_id",
                table: "BAI_TAP",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_tenant_id",
                table: "BAI_TAP",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_nguoi_tai_len_id",
                table: "TAI_LIEU",
                column: "nguoi_tai_len_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_tenant_id",
                table: "TAI_LIEU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_lop_hoc_lop_hoc_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "lop_hoc_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_lop_hoc_tai_lieu_id_lop_hoc_id",
                table: "TAI_LIEU_LOP_HOC",
                columns: new[] { "tai_lieu_id", "lop_hoc_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_lop_hoc_tenant_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_bai_kiem_tra_id",
                table: "TEP_DINH_KEM",
                column: "bai_kiem_tra_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_bai_lam_id",
                table: "TEP_DINH_KEM",
                column: "bai_lam_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_bai_nop_id",
                table: "TEP_DINH_KEM",
                column: "bai_nop_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_bai_tap_id",
                table: "TEP_DINH_KEM",
                column: "bai_tap_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_khoa_luu_tru",
                table: "TEP_DINH_KEM",
                column: "khoa_luu_tru");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_nguoi_tai_len_id",
                table: "TEP_DINH_KEM",
                column: "nguoi_tai_len_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_tai_lieu_id",
                table: "TEP_DINH_KEM",
                column: "tai_lieu_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_tenant_id",
                table: "TEP_DINH_KEM",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropTable(
                name: "TEP_DINH_KEM");

            migrationBuilder.DropTable(
                name: "BAI_LAM");

            migrationBuilder.DropTable(
                name: "BAI_NOP");

            migrationBuilder.DropTable(
                name: "TAI_LIEU");

            migrationBuilder.DropTable(
                name: "BAI_KIEM_TRA");

            migrationBuilder.DropTable(
                name: "BAI_TAP");
        }
    }
}
