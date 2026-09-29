using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemCauHinhEmailVaMauEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "smtp_host",
                table: "TENANT",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_mat_khau_ma_hoa",
                table: "TENANT",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_nguoi_gui",
                table: "TENANT",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "smtp_port",
                table: "TENANT",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_ten_nguoi_gui",
                table: "TENANT",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_user",
                table: "TENANT",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MAU_EMAIL",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loai = table.Column<int>(type: "integer", nullable: false),
                    tieu_de = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    noi_dung_html = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    dang_dung = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mau_email", x => x.id);
                    table.ForeignKey(
                        name: "fk_mau_email_nguoi_dungs_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_mau_email_nguoi_dungs_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mau_email_created_by_id",
                table: "MAU_EMAIL",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_mau_email_tenant_id_loai",
                table: "MAU_EMAIL",
                columns: new[] { "tenant_id", "loai" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mau_email_updated_by_id",
                table: "MAU_EMAIL",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MAU_EMAIL");

            migrationBuilder.DropColumn(
                name: "smtp_host",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "smtp_mat_khau_ma_hoa",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "smtp_nguoi_gui",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "smtp_port",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "smtp_ten_nguoi_gui",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "smtp_user",
                table: "TENANT");
        }
    }
}
