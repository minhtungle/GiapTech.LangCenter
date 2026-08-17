using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemBangVideoTran : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // THỨ TỰ QUAN TRỌNG: tạo bảng → chép dữ liệu → mới xóa cột.
            // EF sinh mặc định là xóa cột TRƯỚC, làm mất sạch link đã nhập. Đây chính là kiểu
            // mất dữ liệu mà quy tắc #1 cấm.
            migrationBuilder.CreateTable(
                name: "VIDEO_TRAN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    mo_ta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_video_tran", x => x.id);
                    table.ForeignKey(
                        name: "fk_video_tran_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_video_tran_tran_dau_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_video_tran_tenant_id",
                table: "VIDEO_TRAN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_video_tran_tran_dau_id",
                table: "VIDEO_TRAN",
                column: "tran_dau_id");

            // Chép link cũ sang bảng mới trước khi xóa cột. Hiện DB chưa có hàng nào có link,
            // nhưng migration phải đúng kể cả khi ai đó nhập link ngay trước khi triển khai.
            migrationBuilder.Sql("""
                INSERT INTO "VIDEO_TRAN" (id, tran_dau_id, ten, url, mo_ta, thu_tu, ngay_tao, tenant_id)
                SELECT gen_random_uuid(), id, 'Video trận', link_video, NULL, 0, now(), tenant_id
                FROM "TRAN_DAU"
                WHERE link_video IS NOT NULL AND btrim(link_video) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "link_video",
                table: "TRAN_DAU");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VIDEO_TRAN");

            migrationBuilder.AddColumn<string>(
                name: "link_video",
                table: "TRAN_DAU",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
