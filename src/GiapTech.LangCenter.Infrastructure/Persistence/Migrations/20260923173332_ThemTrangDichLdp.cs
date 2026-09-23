using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemTrangDichLdp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LIEN_HE_LANDING",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ho_ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    so_dien_thoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quan_tam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    loi_nhan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    khach_hang_id = table.Column<Guid>(type: "uuid", nullable: true),
                    da_xu_ly = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lien_he_landing", x => x.id);
                    table.ForeignKey(
                        name: "fk_lien_he_landing_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lien_he_landing_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TRANG_DICH",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    da_xuat_ban = table.Column<bool>(type: "boolean", nullable: false),
                    tieu_de_seo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mo_ta_seo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trang_dich", x => x.id);
                    table.ForeignKey(
                        name: "fk_trang_dich_nguoi_dung_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_trang_dich_nguoi_dung_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KHOI_LDP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trang_dich_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loai = table.Column<int>(type: "integer", nullable: false),
                    hien = table.Column<bool>(type: "boolean", nullable: false),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    khoa_anh = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nhan_nut = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    duong_dan_nut = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_khoi_ldp", x => x.id);
                    table.ForeignKey(
                        name: "fk_khoi_ldp_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_khoi_ldp_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_khoi_ldp_trang_diches_trang_dich_id",
                        column: x => x.trang_dich_id,
                        principalTable: "TRANG_DICH",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MUC_LDP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoi_ldp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phu_de = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mo_ta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    khoa_anh = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gia_niem_yet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    duong_dan = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_muc_ldp", x => x.id);
                    table.ForeignKey(
                        name: "fk_muc_ldp_khoi_ldp_khoi_ldp_id",
                        column: x => x.khoi_ldp_id,
                        principalTable: "KHOI_LDP",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_muc_ldp_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_muc_ldp_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_khoi_ldp_created_by_id",
                table: "KHOI_LDP",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoi_ldp_trang_dich_id_loai",
                table: "KHOI_LDP",
                columns: new[] { "trang_dich_id", "loai" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_khoi_ldp_updated_by_id",
                table: "KHOI_LDP",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lien_he_landing_created_by_id",
                table: "LIEN_HE_LANDING",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lien_he_landing_tenant_id_da_xu_ly",
                table: "LIEN_HE_LANDING",
                columns: new[] { "tenant_id", "da_xu_ly" });

            migrationBuilder.CreateIndex(
                name: "ix_lien_he_landing_updated_by_id",
                table: "LIEN_HE_LANDING",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_muc_ldp_created_by_id",
                table: "MUC_LDP",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_muc_ldp_khoi_ldp_id_thu_tu",
                table: "MUC_LDP",
                columns: new[] { "khoi_ldp_id", "thu_tu" });

            migrationBuilder.CreateIndex(
                name: "ix_muc_ldp_updated_by_id",
                table: "MUC_LDP",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_trang_dich_created_by_id",
                table: "TRANG_DICH",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_trang_dich_tenant_id",
                table: "TRANG_DICH",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trang_dich_updated_by_id",
                table: "TRANG_DICH",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LIEN_HE_LANDING");

            migrationBuilder.DropTable(
                name: "MUC_LDP");

            migrationBuilder.DropTable(
                name: "KHOI_LDP");

            migrationBuilder.DropTable(
                name: "TRANG_DICH");
        }
    }
}
