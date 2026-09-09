using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemLanGuiVaTuChoiXepLop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_yeu_cau_xep_lop_dang_ky_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.RenameColumn(
                name: "thoi_diem_xep",
                table: "YEU_CAU_XEP_LOP",
                newName: "thoi_diem_xu_ly");

            // defaultValue 1, KHÔNG phải 0 (EF sinh mặc định 0): các yêu cầu đã có trong DB đều
            // là lần gửi THỨ NHẤT. Để 0 thì lịch sử hiện "lần 0", và lần gửi lại tiếp theo tính
            // MAX+1 = 1 sẽ đụng `UNIQUE(dang_ky_id, lan_gui)` với chính dòng cũ.
            migrationBuilder.AddColumn<int>(
                name: "lan_gui",
                table: "YEU_CAU_XEP_LOP",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ly_do_tu_choi",
                table: "YEU_CAU_XEP_LOP",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_dang_ky_id_lan_gui",
                table: "YEU_CAU_XEP_LOP",
                columns: new[] { "dang_ky_id", "lan_gui" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_yeu_cau_xep_lop_dang_ky_dang_cho",
                table: "YEU_CAU_XEP_LOP",
                column: "dang_ky_id",
                unique: true,
                filter: "trang_thai = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_yeu_cau_xep_lop_dang_ky_id_lan_gui",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropIndex(
                name: "ux_yeu_cau_xep_lop_dang_ky_dang_cho",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropColumn(
                name: "lan_gui",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropColumn(
                name: "ly_do_tu_choi",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.RenameColumn(
                name: "thoi_diem_xu_ly",
                table: "YEU_CAU_XEP_LOP",
                newName: "thoi_diem_xep");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_dang_ky_id",
                table: "YEU_CAU_XEP_LOP",
                column: "dang_ky_id",
                unique: true);
        }
    }
}
