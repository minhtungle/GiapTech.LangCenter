using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Thêm cờ "người thu xác nhận đơn đã thu đủ" vào `THU_TIEN_DANG_KY` (05/10/2026).
    ///
    /// CHỈ THÊM CỘT, mặc định `false` — mọi lần thu đang có giữ nguyên nghĩa "chỉ là một lần
    /// đóng tiền", công nợ vẫn suy từ phép trừ như trước. Không migration dữ liệu, không reset.
    ///
    /// Vì sao cần cột thay vì tính: phép trừ trả lời "còn thiếu bao nhiêu TIỀN", không trả lời
    /// "trung tâm còn đòi nữa không" — hai câu lệch nhau khi miễn phần lẻ, giảm giá sau khi
    /// chốt đơn, hoặc xoá nợ. Xem `ThuTienDangKy.XacNhanDuTien`.
    /// </summary>
    public partial class XacNhanDuTienChoLanThu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "xac_nhan_du_tien",
                table: "THU_TIEN_DANG_KY",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xac_nhan_du_tien",
                table: "THU_TIEN_DANG_KY");
        }
    }
}
