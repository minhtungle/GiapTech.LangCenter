using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLinkDangKyNhanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "qua_link",
                table: "PHAN_HOI_THAM_GIA",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "so_lan_sua",
                table: "PHAN_HOI_THAM_GIA",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "link_het_han",
                table: "LOI_MOI_THAM_GIA",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "link_thu_hoi_luc",
                table: "LOI_MOI_THAM_GIA",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "link_token_hash",
                table: "LOI_MOI_THAM_GIA",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_LOI_MOI_THAM_GIA_link_token",
                table: "LOI_MOI_THAM_GIA",
                column: "link_token_hash",
                unique: true,
                filter: "link_token_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_LOI_MOI_THAM_GIA_link_token",
                table: "LOI_MOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "qua_link",
                table: "PHAN_HOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "so_lan_sua",
                table: "PHAN_HOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "link_het_han",
                table: "LOI_MOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "link_thu_hoi_luc",
                table: "LOI_MOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "link_token_hash",
                table: "LOI_MOI_THAM_GIA");
        }
    }
}
