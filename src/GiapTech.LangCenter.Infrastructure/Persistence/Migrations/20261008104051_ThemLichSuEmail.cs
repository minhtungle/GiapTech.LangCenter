using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLichSuEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LICH_SU_EMAIL",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    khach_hang_id = table.Column<Guid>(type: "uuid", nullable: false),
                    den_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    noi_dung_html = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    thanh_cong = table.Column<bool>(type: "boolean", nullable: false),
                    ma_loi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    nguoi_gui_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lich_su_email", x => x.id);
                    table.ForeignKey(
                        name: "fk_lich_su_email_khach_hang_khach_hang_id",
                        column: x => x.khach_hang_id,
                        principalTable: "KHACH_HANG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lich_su_email_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lich_su_email_nguoi_dungs_nguoi_gui_id",
                        column: x => x.nguoi_gui_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lich_su_email_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_email_created_by_id",
                table: "LICH_SU_EMAIL",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_email_khach_hang_id",
                table: "LICH_SU_EMAIL",
                column: "khach_hang_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_email_nguoi_gui_id",
                table: "LICH_SU_EMAIL",
                column: "nguoi_gui_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_email_tenant_id_khach_hang_id_created_at",
                table: "LICH_SU_EMAIL",
                columns: new[] { "tenant_id", "khach_hang_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_email_updated_by_id",
                table: "LICH_SU_EMAIL",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LICH_SU_EMAIL");
        }
    }
}
