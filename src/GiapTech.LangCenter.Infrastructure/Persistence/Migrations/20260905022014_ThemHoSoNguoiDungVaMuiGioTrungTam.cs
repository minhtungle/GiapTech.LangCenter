using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHoSoNguoiDungVaMuiGioTrungTam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mui_gio",
                table: "TENANT",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                // Trung tâm đã tồn tại phải nhận múi giờ thật, không phải chuỗi rỗng —
                // NgayHoc tính từ nó, rỗng là mọi buổi học lệch ngày.
                defaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.AddColumn<int>(
                name: "so_ngay_canh_bao_no_hoc_phi",
                table: "TENANT",
                type: "integer",
                nullable: false,
                // 0 nghĩa là cảnh báo nợ ngay hôm khai giảng — không phải ý định.
                defaultValue: 14);

            migrationBuilder.AddColumn<string>(
                name: "anh_dai_dien_url",
                table: "NGUOI_DUNG",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ho_ten",
                table: "NGUOI_DUNG",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "loai_nguoi_dung",
                table: "NGUOI_DUNG",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ngay_sinh",
                table: "NGUOI_DUNG",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_tenant_id_loai_nguoi_dung",
                table: "NGUOI_DUNG",
                columns: new[] { "tenant_id", "loai_nguoi_dung" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_tenant_id_loai_nguoi_dung",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "mui_gio",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "so_ngay_canh_bao_no_hoc_phi",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "anh_dai_dien_url",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "ho_ten",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "loai_nguoi_dung",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "ngay_sinh",
                table: "NGUOI_DUNG");
        }
    }
}
