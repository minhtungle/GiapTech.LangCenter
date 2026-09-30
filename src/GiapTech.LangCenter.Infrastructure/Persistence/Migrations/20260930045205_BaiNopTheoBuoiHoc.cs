using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BaiNopTheoBuoiHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bai_nop_bai_taps_bai_tap_id",
                table: "BAI_NOP");

            /*
              Bỏ UNIQUE cũ TRƯỚC MỌI thay đổi dữ liệu.

              Không phải dọn dẹp cho gọn — nó là điều kiện để hai lệnh UPDATE dưới chạy được.
              Ràng buộc `(bai_tap_id, hoc_vien_id, lan_nop)` kiểm ngay trong lúc UPDATE, mà
              bước gộp nhiều bài tập về một buổi đi qua trạng thái trung gian có trùng: một
              học viên nộp cho hai đầu việc của cùng một buổi sẽ thành hai dòng cùng
              `lan_nop = 1` trước khi kịp đánh số lại.

              Đặt DropIndex sau UPDATE thì migration chết giữa chừng với
              `23505: duplicate key value violates unique constraint` — đã xảy ra lúc chạy
              thử trên bản sao DB dev ngày 30/09/2026.
            */
            migrationBuilder.DropIndex(
                name: "ix_bai_nop_bai_tap_id_hoc_vien_id_lan_nop",
                table: "BAI_NOP");

            migrationBuilder.RenameColumn(
                name: "bai_tap_id",
                table: "BAI_NOP",
                newName: "buoi_hoc_id");

            // Giá trị đang là id BÀI TẬP — tra ngược sang id BUỔI HỌC.
            //
            // EF chỉ sinh `RenameColumn`, mà đổi tên thì GIÁ TRỊ ở lại nguyên: cột
            // `buoi_hoc_id` sẽ chứa id của `BAI_TAP` và trỏ sai bảng. Khoá ngoại mới sẽ từ
            // chối, hoặc tệ hơn là trùng id với một buổi có thật và dữ liệu âm thầm sai.
            migrationBuilder.Sql("""
                UPDATE "BAI_NOP" n
                SET buoi_hoc_id = t.buoi_hoc_id
                FROM "BAI_TAP" t
                WHERE t.id = n.buoi_hoc_id;
                """);

            // Cùng học viên + cùng buổi ⇒ đánh số lại `lan_nop` theo thứ tự thời gian.
            //
            // Một học viên từng nộp cho hai bài tập KHÁC NHAU trong CÙNG một buổi sẽ thành
            // hai dòng trùng `(buoi_hoc_id, hoc_vien_id, lan_nop)` và vi phạm UNIQUE. Đánh
            // số lại để giữ MỌI bản ghi — bài đã nộp là kết quả học tập của học viên, không
            // được xoá bớt cho gọn (quy tắc #1).
            migrationBuilder.Sql("""
                WITH danh_so AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY buoi_hoc_id, hoc_vien_id
                               ORDER BY thoi_diem_nop, id
                           ) AS so_moi
                    FROM "BAI_NOP"
                )
                UPDATE "BAI_NOP" n
                SET lan_nop = d.so_moi
                FROM danh_so d
                WHERE d.id = n.id;
                """);

            // Dựng UNIQUE mới SAU khi dữ liệu đã hết trùng.
            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_buoi_hoc_id_hoc_vien_id_lan_nop",
                table: "BAI_NOP",
                columns: ["buoi_hoc_id", "hoc_vien_id", "lan_nop"],
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_nop_buoi_hocs_buoi_hoc_id",
                table: "BAI_NOP",
                column: "buoi_hoc_id",
                principalTable: "BUOI_HOC",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bai_nop_buoi_hocs_buoi_hoc_id",
                table: "BAI_NOP");

            migrationBuilder.RenameColumn(
                name: "buoi_hoc_id",
                table: "BAI_NOP",
                newName: "bai_tap_id");

            // Lùi lại KHÔNG khôi phục được quan hệ cũ: chiều xuôi là nhiều-về-một, không
            // có thông tin nào để biết bài nộp vốn thuộc đầu việc nào. Gán tạm bài tập ĐẦU
            // TIÊN của buổi, và bài nộp của buổi chưa có bài tập nào sẽ bị XOÁ.
            //
            // Chạy `Down` này trên dữ liệu thật là mất mát. Nó để lùi ngay sau khi vừa áp
            // nhầm trên môi trường trống, không phải để quay về sau khi đã dùng.
            migrationBuilder.Sql("""
                DELETE FROM "BAI_NOP" n
                WHERE NOT EXISTS (SELECT 1 FROM "BAI_TAP" t WHERE t.buoi_hoc_id = n.bai_tap_id);

                UPDATE "BAI_NOP" n
                SET bai_tap_id = (
                    SELECT t.id FROM "BAI_TAP" t
                    WHERE t.buoi_hoc_id = n.bai_tap_id
                    ORDER BY t.created_at, t.id LIMIT 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "ix_bai_nop_buoi_hoc_id_hoc_vien_id_lan_nop",
                table: "BAI_NOP");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_bai_tap_id_hoc_vien_id_lan_nop",
                table: "BAI_NOP",
                columns: ["bai_tap_id", "hoc_vien_id", "lan_nop"],
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_nop_bai_taps_bai_tap_id",
                table: "BAI_NOP",
                column: "bai_tap_id",
                principalTable: "BAI_TAP",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
