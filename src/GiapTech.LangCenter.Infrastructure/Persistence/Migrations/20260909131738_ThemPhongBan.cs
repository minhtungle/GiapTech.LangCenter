using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemPhongBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "phong_ban_id",
                table: "NGUOI_DUNG",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PHONG_BAN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phong_ban_cha_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguoi_quan_ly_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mo_ta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phong_ban", x => x.id);
                    table.ForeignKey(
                        name: "fk_phong_ban_nguoi_dung_nguoi_quan_ly_id",
                        column: x => x.nguoi_quan_ly_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_phong_ban_phong_ban_phong_ban_cha_id",
                        column: x => x.phong_ban_cha_id,
                        principalTable: "PHONG_BAN",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_phong_ban_id",
                table: "NGUOI_DUNG",
                column: "phong_ban_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_nguoi_quan_ly_id",
                table: "PHONG_BAN",
                column: "nguoi_quan_ly_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_phong_ban_cha_id",
                table: "PHONG_BAN",
                column: "phong_ban_cha_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_tenant_id",
                table: "PHONG_BAN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_tenant_id_phong_ban_cha_id_ten",
                table: "PHONG_BAN",
                columns: new[] { "tenant_id", "phong_ban_cha_id", "ten" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_phong_ban_goc_ten",
                table: "PHONG_BAN",
                columns: new[] { "tenant_id", "ten" },
                unique: true,
                filter: "phong_ban_cha_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_nguoi_dung_phong_bans_phong_ban_id",
                table: "NGUOI_DUNG",
                column: "phong_ban_id",
                principalTable: "PHONG_BAN",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_nguoi_dung_phong_bans_phong_ban_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropTable(
                name: "PHONG_BAN");

            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_phong_ban_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "phong_ban_id",
                table: "NGUOI_DUNG");
        }
    }
}
