using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemNgonNgu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ngon_ngu_id",
                table: "SAN_PHAM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ngon_ngu_id",
                table: "KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NGON_NGU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_ngon_ngu", x => x.id);
                    table.ForeignKey(
                        name: "fk_ngon_ngu_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ngon_ngu_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_san_pham_ngon_ngu_id",
                table: "SAN_PHAM",
                column: "ngon_ngu_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_hoc_ngon_ngu_id",
                table: "KHOA_HOC",
                column: "ngon_ngu_id");

            migrationBuilder.CreateIndex(
                name: "ix_ngon_ngu_created_by_id",
                table: "NGON_NGU",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ngon_ngu_tenant_id",
                table: "NGON_NGU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ngon_ngu_updated_by_id",
                table: "NGON_NGU",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_ngon_ngu_tenant_ten",
                table: "NGON_NGU",
                columns: new[] { "tenant_id", "ten" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_khoa_hoc_ngon_ngus_ngon_ngu_id",
                table: "KHOA_HOC",
                column: "ngon_ngu_id",
                principalTable: "NGON_NGU",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_san_pham_ngon_ngu_ngon_ngu_id",
                table: "SAN_PHAM",
                column: "ngon_ngu_id",
                principalTable: "NGON_NGU",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_khoa_hoc_ngon_ngus_ngon_ngu_id",
                table: "KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_san_pham_ngon_ngu_ngon_ngu_id",
                table: "SAN_PHAM");

            migrationBuilder.DropTable(
                name: "NGON_NGU");

            migrationBuilder.DropIndex(
                name: "ix_san_pham_ngon_ngu_id",
                table: "SAN_PHAM");

            migrationBuilder.DropIndex(
                name: "ix_khoa_hoc_ngon_ngu_id",
                table: "KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "ngon_ngu_id",
                table: "SAN_PHAM");

            migrationBuilder.DropColumn(
                name: "ngon_ngu_id",
                table: "KHOA_HOC");
        }
    }
}
