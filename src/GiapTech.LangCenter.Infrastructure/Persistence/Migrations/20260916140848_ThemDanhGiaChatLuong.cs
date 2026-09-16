using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemDanhGiaChatLuong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PHIEU_DANH_GIA_NHAN_VIEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nhan_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ky = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nhan_xet = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phieu_danh_gia_nhan_vien", x => x.id);
                    table.ForeignKey(
                        name: "fk_phieu_danh_gia_nhan_vien_nguoi_dung_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_phieu_danh_gia_nhan_vien_nguoi_dung_nhan_vien_id",
                        column: x => x.nhan_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_phieu_danh_gia_nhan_vien_nguoi_dung_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TIEU_CHI_DANH_GIA",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nhom = table.Column<int>(type: "integer", nullable: false),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    dang_dung = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tieu_chi_danh_gia", x => x.id);
                    table.ForeignKey(
                        name: "fk_tieu_chi_danh_gia_nguoi_dung_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tieu_chi_danh_gia_nguoi_dung_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DIEM_TIEU_CHI",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_chi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diem = table.Column<int>(type: "integer", nullable: false),
                    nhan_xet_buoi_hoc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phieu_danh_gia_nhan_vien_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diem_tieu_chi", x => x.id);
                    table.CheckConstraint("ck_diem_tieu_chi_dung_mot_chu", "(CASE WHEN nhan_xet_buoi_hoc_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN phieu_danh_gia_nhan_vien_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("ck_diem_tieu_chi_thang_5", "diem BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_diem_tieu_chi_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_diem_tieu_chi_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_diem_tieu_chi_nhan_xet_buoi_hocs_nhan_xet_buoi_hoc_id",
                        column: x => x.nhan_xet_buoi_hoc_id,
                        principalTable: "NHAN_XET_BUOI_HOC",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_diem_tieu_chi_phieu_danh_gia_nhan_viens_phieu_danh_gia_nhan",
                        column: x => x.phieu_danh_gia_nhan_vien_id,
                        principalTable: "PHIEU_DANH_GIA_NHAN_VIEN",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_diem_tieu_chi_tieu_chi_danh_gias_tieu_chi_id",
                        column: x => x.tieu_chi_id,
                        principalTable: "TIEU_CHI_DANH_GIA",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_created_by_id",
                table: "DIEM_TIEU_CHI",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_nhan_xet_buoi_hoc_id_tieu_chi_id",
                table: "DIEM_TIEU_CHI",
                columns: new[] { "nhan_xet_buoi_hoc_id", "tieu_chi_id" },
                unique: true,
                filter: "nhan_xet_buoi_hoc_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_phieu_danh_gia_nhan_vien_id_tieu_chi_id",
                table: "DIEM_TIEU_CHI",
                columns: new[] { "phieu_danh_gia_nhan_vien_id", "tieu_chi_id" },
                unique: true,
                filter: "phieu_danh_gia_nhan_vien_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_tenant_id",
                table: "DIEM_TIEU_CHI",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_tieu_chi_id",
                table: "DIEM_TIEU_CHI",
                column: "tieu_chi_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_tieu_chi_updated_by_id",
                table: "DIEM_TIEU_CHI",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phieu_danh_gia_nhan_vien_created_by_id",
                table: "PHIEU_DANH_GIA_NHAN_VIEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phieu_danh_gia_nhan_vien_nhan_vien_id",
                table: "PHIEU_DANH_GIA_NHAN_VIEN",
                column: "nhan_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_phieu_danh_gia_nhan_vien_tenant_id",
                table: "PHIEU_DANH_GIA_NHAN_VIEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_phieu_danh_gia_nhan_vien_tenant_id_nhan_vien_id_ky",
                table: "PHIEU_DANH_GIA_NHAN_VIEN",
                columns: new[] { "tenant_id", "nhan_vien_id", "ky" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phieu_danh_gia_nhan_vien_updated_by_id",
                table: "PHIEU_DANH_GIA_NHAN_VIEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tieu_chi_danh_gia_created_by_id",
                table: "TIEU_CHI_DANH_GIA",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tieu_chi_danh_gia_tenant_id",
                table: "TIEU_CHI_DANH_GIA",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tieu_chi_danh_gia_tenant_id_nhom_ten",
                table: "TIEU_CHI_DANH_GIA",
                columns: new[] { "tenant_id", "nhom", "ten" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tieu_chi_danh_gia_updated_by_id",
                table: "TIEU_CHI_DANH_GIA",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DIEM_TIEU_CHI");

            migrationBuilder.DropTable(
                name: "PHIEU_DANH_GIA_NHAN_VIEN");

            migrationBuilder.DropTable(
                name: "TIEU_CHI_DANH_GIA");
        }
    }
}
