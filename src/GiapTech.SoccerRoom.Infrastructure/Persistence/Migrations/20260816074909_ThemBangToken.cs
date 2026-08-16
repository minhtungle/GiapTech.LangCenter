using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemBangToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REFRESH_TOKEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    thu_hoi_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TOKEN_DATLAI_MATKHAU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    da_dung_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_datlai_matkhau", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_datlai_matkhau_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_nguoi_dung_id_thu_hoi_luc",
                table: "REFRESH_TOKEN",
                columns: new[] { "nguoi_dung_id", "thu_hoi_luc" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token_hash",
                table: "REFRESH_TOKEN",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_nguoi_dung_id_da_dung_luc",
                table: "TOKEN_DATLAI_MATKHAU",
                columns: new[] { "nguoi_dung_id", "da_dung_luc" });

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_token_hash",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REFRESH_TOKEN");

            migrationBuilder.DropTable(
                name: "TOKEN_DATLAI_MATKHAU");
        }
    }
}
