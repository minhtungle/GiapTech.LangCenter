using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Đổi ma_doi từ varchar(50) người dùng tự đặt sang char(7) sinh tự động.
    ///
    /// ⚠️ MẤT DỮ LIỆU: mã dài hơn 7 ký tự sẽ bị cắt cụt và có thể va nhau ở ràng buộc UNIQUE.
    /// An toàn ở đây vì chưa có dữ liệu thật (mới chỉ có CLB thử nghiệm). Nếu về sau cần chạy
    /// trên DB đã có tenant thật, PHẢI viết migration khác: thêm cột mới → sinh mã cho từng
    /// tenant → thông báo mã mới cho họ → mới bỏ cột cũ.
    /// </summary>
    /// <inheritdoc />
    public partial class DoiMaDoiThanhMa7KyTu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ma_doi",
                table: "TENANT",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ma_doi",
                table: "TENANT",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(7)",
                oldFixedLength: true,
                oldMaxLength: 7);
        }
    }
}
