using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HocTapTrucTuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KHOA_ONLINE",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khoa_online", x => x.id);
                    table.ForeignKey(
                        name: "fk_khoa_online_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_khoa_online_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAI_HOC_ONLINE",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoa_online_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    noi_dung = table.Column<string>(type: "text", nullable: true),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    cong_khai = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bai_hoc_online", x => x.id);
                    table.ForeignKey(
                        name: "fk_bai_hoc_online_khoa_onlines_khoa_online_id",
                        column: x => x.khoa_online_id,
                        principalTable: "KHOA_ONLINE",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bai_hoc_online_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bai_hoc_online_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GHI_DANH_KHOA_ONLINE",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoa_online_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_bat_dau = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ghi_danh_khoa_online", x => x.id);
                    table.ForeignKey(
                        name: "fk_ghi_danh_khoa_online_khoa_onlines_khoa_online_id",
                        column: x => x.khoa_online_id,
                        principalTable: "KHOA_ONLINE",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ghi_danh_khoa_online_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ghi_danh_khoa_online_nguoi_dungs_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ghi_danh_khoa_online_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TIEN_DO_BAI_HOC",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bai_hoc_online_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoc_vien_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hoan_thanh_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tien_do_bai_hoc", x => x.id);
                    table.ForeignKey(
                        name: "fk_tien_do_bai_hoc_bai_hoc_online_bai_hoc_online_id",
                        column: x => x.bai_hoc_online_id,
                        principalTable: "BAI_HOC_ONLINE",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tien_do_bai_hoc_nguoi_dung_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tien_do_bai_hoc_nguoi_dung_hoc_vien_id",
                        column: x => x.hoc_vien_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tien_do_bai_hoc_nguoi_dung_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bai_hoc_online_created_by_id",
                table: "BAI_HOC_ONLINE",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_hoc_online_khoa_online_id_thu_tu",
                table: "BAI_HOC_ONLINE",
                columns: new[] { "khoa_online_id", "thu_tu" });

            migrationBuilder.CreateIndex(
                name: "ix_bai_hoc_online_tenant_id",
                table: "BAI_HOC_ONLINE",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_hoc_online_updated_by_id",
                table: "BAI_HOC_ONLINE",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ghi_danh_khoa_online_created_by_id",
                table: "GHI_DANH_KHOA_ONLINE",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ghi_danh_khoa_online_hoc_vien_id",
                table: "GHI_DANH_KHOA_ONLINE",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_ghi_danh_khoa_online_khoa_online_id_hoc_vien_id",
                table: "GHI_DANH_KHOA_ONLINE",
                columns: new[] { "khoa_online_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ghi_danh_khoa_online_tenant_id",
                table: "GHI_DANH_KHOA_ONLINE",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ghi_danh_khoa_online_updated_by_id",
                table: "GHI_DANH_KHOA_ONLINE",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_online_created_by_id",
                table: "KHOA_ONLINE",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_online_tenant_id",
                table: "KHOA_ONLINE",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_online_updated_by_id",
                table: "KHOA_ONLINE",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tien_do_bai_hoc_bai_hoc_online_id_hoc_vien_id",
                table: "TIEN_DO_BAI_HOC",
                columns: new[] { "bai_hoc_online_id", "hoc_vien_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tien_do_bai_hoc_created_by_id",
                table: "TIEN_DO_BAI_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tien_do_bai_hoc_hoc_vien_id",
                table: "TIEN_DO_BAI_HOC",
                column: "hoc_vien_id");

            migrationBuilder.CreateIndex(
                name: "ix_tien_do_bai_hoc_tenant_id",
                table: "TIEN_DO_BAI_HOC",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tien_do_bai_hoc_updated_by_id",
                table: "TIEN_DO_BAI_HOC",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GHI_DANH_KHOA_ONLINE");

            migrationBuilder.DropTable(
                name: "TIEN_DO_BAI_HOC");

            migrationBuilder.DropTable(
                name: "BAI_HOC_ONLINE");

            migrationBuilder.DropTable(
                name: "KHOA_ONLINE");
        }
    }
}
