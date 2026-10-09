using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemPhoiTaiLieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PHOI_TAI_LIEU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    khoa_tep = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ten_tep_goc = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    keys_json = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    dang_dung = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phoi_tai_lieu", x => x.id);
                    table.ForeignKey(
                        name: "fk_phoi_tai_lieu_nguoi_dung_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_phoi_tai_lieu_nguoi_dung_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BAN_XUAT_PHOI",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phoi_tai_lieu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    khoa_tep = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ten_tep = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    gia_tri_json = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    nguoi_xuat_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_xuat_phoi", x => x.id);
                    table.ForeignKey(
                        name: "fk_ban_xuat_phoi_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ban_xuat_phoi_nguoi_dungs_nguoi_xuat_id",
                        column: x => x.nguoi_xuat_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ban_xuat_phoi_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ban_xuat_phoi_phoi_tai_lieus_phoi_tai_lieu_id",
                        column: x => x.phoi_tai_lieu_id,
                        principalTable: "PHOI_TAI_LIEU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ban_xuat_phoi_created_by_id",
                table: "BAN_XUAT_PHOI",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_xuat_phoi_nguoi_xuat_id",
                table: "BAN_XUAT_PHOI",
                column: "nguoi_xuat_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_xuat_phoi_phoi_tai_lieu_id",
                table: "BAN_XUAT_PHOI",
                column: "phoi_tai_lieu_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_xuat_phoi_tenant_id_phoi_tai_lieu_id_created_at",
                table: "BAN_XUAT_PHOI",
                columns: new[] { "tenant_id", "phoi_tai_lieu_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ban_xuat_phoi_updated_by_id",
                table: "BAN_XUAT_PHOI",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phoi_tai_lieu_created_by_id",
                table: "PHOI_TAI_LIEU",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phoi_tai_lieu_tenant_id_ten",
                table: "PHOI_TAI_LIEU",
                columns: new[] { "tenant_id", "ten" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phoi_tai_lieu_updated_by_id",
                table: "PHOI_TAI_LIEU",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BAN_XUAT_PHOI");

            migrationBuilder.DropTable(
                name: "PHOI_TAI_LIEU");
        }
    }
}
