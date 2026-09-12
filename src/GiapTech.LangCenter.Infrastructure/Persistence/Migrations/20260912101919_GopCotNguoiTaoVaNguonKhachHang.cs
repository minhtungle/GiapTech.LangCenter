using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GopCotNguoiTaoVaNguonKhachHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_nguoi_tao_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_tap_nguoi_dungs_nguoi_tao_id",
                table: "BAI_TAP");

            migrationBuilder.DropForeignKey(
                name: "fk_khach_hang_nguoi_dungs_nguoi_tao_id",
                table: "KHACH_HANG");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_nguoi_tao_id",
                table: "LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_nguoi_tao_id",
                table: "LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_khach_hang_nguoi_tao_id",
                table: "KHACH_HANG");

            migrationBuilder.DropIndex(
                name: "ix_bai_tap_nguoi_tao_id",
                table: "BAI_TAP");

            migrationBuilder.DropIndex(
                name: "ix_bai_kiem_tra_nguoi_tao_id",
                table: "BAI_KIEM_TRA");

            // ---- CHUYỂN DỮ LIỆU TRƯỚC KHI BỎ CỘT ----
            //
            // EF sinh thẳng `DropColumn` mà không chuyển gì: nó thấy hai cột độc lập, không biết
            // chúng cùng nghĩa. Bỏ thẳng là mất dấu vết "ai tạo" của mọi hàng đang có
            // (đo 13/09/2026 trên DB dev: LOP_HOC 1 hàng, BAI_TAP 1 hàng có giá trị).
            //
            // `COALESCE` chứ không gán đè: `created_by_id` có thể đã có giá trị đúng từ lúc
            // AppDbContext tự gán (12/09), và giá trị đó mới hơn nên được ưu tiên giữ.
            foreach (var bang in new[] { "LOP_HOC", "KHACH_HANG", "BAI_TAP", "BAI_KIEM_TRA" })
            {
                migrationBuilder.Sql(
                    $"""UPDATE "{bang}" SET created_by_id = COALESCE(created_by_id, nguoi_tao_id) """
                    + "WHERE nguoi_tao_id IS NOT NULL;");
            }

            migrationBuilder.DropColumn(
                name: "nguoi_tao_id",
                table: "LOP_HOC");

            migrationBuilder.DropColumn(
                name: "nguoi_tao_id",
                table: "KHACH_HANG");

            migrationBuilder.DropColumn(
                name: "nguoi_tao_id",
                table: "BAI_TAP");

            migrationBuilder.DropColumn(
                name: "nguoi_tao_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.AddColumn<int>(
                name: "nguon",
                table: "KHACH_HANG",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_created_by_id",
                table: "YEU_CAU_XEP_LOP",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_yeu_cau_xep_lop_updated_by_id",
                table: "YEU_CAU_XEP_LOP",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_created_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_thu_tien_dang_ky_created_by_id",
                table: "THU_TIEN_DANG_KY",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_thu_tien_dang_ky_updated_by_id",
                table: "THU_TIEN_DANG_KY",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_created_by_id",
                table: "TEP_DINH_KEM",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tep_dinh_kem_updated_by_id",
                table: "TEP_DINH_KEM",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_created_by_id",
                table: "TENANT",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_updated_by_id",
                table: "TENANT",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_lop_hoc_created_by_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_lop_hoc_updated_by_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_created_by_id",
                table: "TAI_LIEU",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_updated_by_id",
                table: "TAI_LIEU",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_khoan_created_by_id",
                table: "TAI_KHOAN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_khoan_updated_by_id",
                table: "TAI_KHOAN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_san_pham_created_by_id",
                table: "SAN_PHAM",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_san_pham_updated_by_id",
                table: "SAN_PHAM",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_created_by_id",
                table: "REFRESH_TOKEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_updated_by_id",
                table: "REFRESH_TOKEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_quyen_chuc_nang_created_by_id",
                table: "QUYEN_CHUC_NANG",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_quyen_chuc_nang_updated_by_id",
                table: "QUYEN_CHUC_NANG",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_quyen_created_by_id",
                table: "QUYEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_quyen_updated_by_id",
                table: "QUYEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_created_by_id",
                table: "PHONG_BAN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_phong_ban_updated_by_id",
                table: "PHONG_BAN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_created_by_id",
                table: "NHAT_KY_HE_THONG",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhat_ky_he_thong_updated_by_id",
                table: "NHAT_KY_HE_THONG",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhan_xet_buoi_hoc_created_by_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nhan_xet_buoi_hoc_updated_by_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nguoidung_quyen_created_by_id",
                table: "NGUOIDUNG_QUYEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nguoidung_quyen_updated_by_id",
                table: "NGUOIDUNG_QUYEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_created_by_id",
                table: "NGUOI_DUNG",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_updated_by_id",
                table: "NGUOI_DUNG",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tro_giang_created_by_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_tro_giang_updated_by_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_khoa_hoc_created_by_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_khoa_hoc_updated_by_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_hoc_vien_created_by_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_hoc_vien_updated_by_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_created_by_id",
                table: "LOP_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_updated_by_id",
                table: "LOP_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lien_ket_mxh_created_by_id",
                table: "LIEN_KET_MXH",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lien_ket_mxh_updated_by_id",
                table: "LIEN_KET_MXH",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_cham_soc_created_by_id",
                table: "LICH_SU_CHAM_SOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_lich_su_cham_soc_updated_by_id",
                table: "LICH_SU_CHAM_SOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_created_by_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoan_thu_hoc_phi_updated_by_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_hoc_created_by_id",
                table: "KHOA_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khoa_hoc_updated_by_id",
                table: "KHOA_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_created_by_id",
                table: "KHACH_HANG",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_updated_by_id",
                table: "KHACH_HANG",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_nhan_vien_created_by_id",
                table: "HO_SO_NHAN_VIEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_nhan_vien_updated_by_id",
                table: "HO_SO_NHAN_VIEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_hoc_vien_created_by_id",
                table: "HO_SO_HOC_VIEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_hoc_vien_updated_by_id",
                table: "HO_SO_HOC_VIEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_giao_vien_created_by_id",
                table: "HO_SO_GIAO_VIEN",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_giao_vien_updated_by_id",
                table: "HO_SO_GIAO_VIEN",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_created_by_id",
                table: "DIEM_DANH",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_diem_danh_updated_by_id",
                table: "DIEM_DANH",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_created_by_id",
                table: "DANG_KY_KHOA_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_dang_ky_khoa_hoc_updated_by_id",
                table: "DANG_KY_KHOA_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_chuc_vu_created_by_id",
                table: "CHUC_VU",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_chuc_vu_updated_by_id",
                table: "CHUC_VU",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_buoi_hoc_created_by_id",
                table: "BUOI_HOC",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_buoi_hoc_updated_by_id",
                table: "BUOI_HOC",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_created_by_id",
                table: "BAI_TAP",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_updated_by_id",
                table: "BAI_TAP",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_created_by_id",
                table: "BAI_NOP",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_nop_updated_by_id",
                table: "BAI_NOP",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_created_by_id",
                table: "BAI_LAM",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_lam_updated_by_id",
                table: "BAI_LAM",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_created_by_id",
                table: "BAI_KIEM_TRA",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_updated_by_id",
                table: "BAI_KIEM_TRA",
                column: "updated_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_created_by_id",
                table: "BAI_KIEM_TRA",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_updated_by_id",
                table: "BAI_KIEM_TRA",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_lam_nguoi_dungs_created_by_id",
                table: "BAI_LAM",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_lam_nguoi_dungs_updated_by_id",
                table: "BAI_LAM",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_nop_nguoi_dungs_created_by_id",
                table: "BAI_NOP",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_nop_nguoi_dungs_updated_by_id",
                table: "BAI_NOP",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_tap_nguoi_dungs_created_by_id",
                table: "BAI_TAP",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_tap_nguoi_dungs_updated_by_id",
                table: "BAI_TAP",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_buoi_hoc_nguoi_dungs_created_by_id",
                table: "BUOI_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_buoi_hoc_nguoi_dungs_updated_by_id",
                table: "BUOI_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_chuc_vu_nguoi_dung_created_by_id",
                table: "CHUC_VU",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_chuc_vu_nguoi_dung_updated_by_id",
                table: "CHUC_VU",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_dang_ky_khoa_hoc_nguoi_dungs_created_by_id",
                table: "DANG_KY_KHOA_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_dang_ky_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "DANG_KY_KHOA_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_diem_danh_nguoi_dungs_created_by_id",
                table: "DIEM_DANH",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_diem_danh_nguoi_dungs_updated_by_id",
                table: "DIEM_DANH",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_giao_vien_nguoi_dung_created_by_id",
                table: "HO_SO_GIAO_VIEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_giao_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_GIAO_VIEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_hoc_vien_nguoi_dung_created_by_id",
                table: "HO_SO_HOC_VIEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_hoc_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_HOC_VIEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_nhan_vien_nguoi_dung_created_by_id",
                table: "HO_SO_NHAN_VIEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ho_so_nhan_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_NHAN_VIEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khach_hang_nguoi_dungs_created_by_id",
                table: "KHACH_HANG",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khach_hang_nguoi_dungs_updated_by_id",
                table: "KHACH_HANG",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khoa_hoc_nguoi_dungs_created_by_id",
                table: "KHOA_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "KHOA_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khoan_thu_hoc_phi_nguoi_dungs_created_by_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khoan_thu_hoc_phi_nguoi_dungs_updated_by_id",
                table: "KHOAN_THU_HOC_PHI",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lich_su_cham_soc_nguoi_dungs_created_by_id",
                table: "LICH_SU_CHAM_SOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lich_su_cham_soc_nguoi_dungs_updated_by_id",
                table: "LICH_SU_CHAM_SOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lien_ket_mxh_nguoi_dung_created_by_id",
                table: "LIEN_KET_MXH",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lien_ket_mxh_nguoi_dung_updated_by_id",
                table: "LIEN_KET_MXH",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_created_by_id",
                table: "LOP_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_updated_by_id",
                table: "LOP_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_hoc_vien_nguoi_dungs_created_by_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_hoc_vien_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_HOC_VIEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_khoa_hoc_nguoi_dungs_created_by_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_KHOA_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_tro_giang_nguoi_dungs_created_by_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_tro_giang_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_TRO_GIANG",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoi_dung_nguoi_dung_created_by_id",
                table: "NGUOI_DUNG",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoi_dung_nguoi_dung_updated_by_id",
                table: "NGUOI_DUNG",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_created_by_id",
                table: "NGUOIDUNG_QUYEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_updated_by_id",
                table: "NGUOIDUNG_QUYEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nhan_xet_buoi_hoc_nguoi_dung_created_by_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nhan_xet_buoi_hoc_nguoi_dung_updated_by_id",
                table: "NHAN_XET_BUOI_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nhat_ky_he_thong_nguoi_dung_created_by_id",
                table: "NHAT_KY_HE_THONG",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_nhat_ky_he_thong_nguoi_dung_updated_by_id",
                table: "NHAT_KY_HE_THONG",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_phong_ban_nguoi_dung_created_by_id",
                table: "PHONG_BAN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_phong_ban_nguoi_dung_updated_by_id",
                table: "PHONG_BAN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_quyen_nguoi_dung_created_by_id",
                table: "QUYEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_quyen_nguoi_dung_updated_by_id",
                table: "QUYEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_quyen_chuc_nang_nguoi_dung_created_by_id",
                table: "QUYEN_CHUC_NANG",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_quyen_chuc_nang_nguoi_dung_updated_by_id",
                table: "QUYEN_CHUC_NANG",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_token_nguoi_dung_created_by_id",
                table: "REFRESH_TOKEN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_token_nguoi_dung_updated_by_id",
                table: "REFRESH_TOKEN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_san_pham_nguoi_dung_created_by_id",
                table: "SAN_PHAM",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_san_pham_nguoi_dung_updated_by_id",
                table: "SAN_PHAM",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_khoan_nguoi_dung_created_by_id",
                table: "TAI_KHOAN",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_khoan_nguoi_dung_updated_by_id",
                table: "TAI_KHOAN",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_lieu_nguoi_dung_created_by_id",
                table: "TAI_LIEU",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_lieu_nguoi_dung_updated_by_id",
                table: "TAI_LIEU",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_lieu_lop_hoc_nguoi_dung_created_by_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tai_lieu_lop_hoc_nguoi_dung_updated_by_id",
                table: "TAI_LIEU_LOP_HOC",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_nguoi_dung_created_by_id",
                table: "TENANT",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_nguoi_dung_updated_by_id",
                table: "TENANT",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_created_by_id",
                table: "TEP_DINH_KEM",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_updated_by_id",
                table: "TEP_DINH_KEM",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_thu_tien_dang_ky_nguoi_dung_created_by_id",
                table: "THU_TIEN_DANG_KY",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_thu_tien_dang_ky_nguoi_dung_updated_by_id",
                table: "THU_TIEN_DANG_KY",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_created_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_yeu_cau_xep_lop_nguoi_dung_created_by_id",
                table: "YEU_CAU_XEP_LOP",
                column: "created_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_yeu_cau_xep_lop_nguoi_dung_updated_by_id",
                table: "YEU_CAU_XEP_LOP",
                column: "updated_by_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_created_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_updated_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_lam_nguoi_dungs_created_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_lam_nguoi_dungs_updated_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_nop_nguoi_dungs_created_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_nop_nguoi_dungs_updated_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_tap_nguoi_dungs_created_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropForeignKey(
                name: "fk_bai_tap_nguoi_dungs_updated_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropForeignKey(
                name: "fk_buoi_hoc_nguoi_dungs_created_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_buoi_hoc_nguoi_dungs_updated_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_chuc_vu_nguoi_dung_created_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropForeignKey(
                name: "fk_chuc_vu_nguoi_dung_updated_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropForeignKey(
                name: "fk_dang_ky_khoa_hoc_nguoi_dungs_created_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_dang_ky_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_diem_danh_nguoi_dungs_created_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropForeignKey(
                name: "fk_diem_danh_nguoi_dungs_updated_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_giao_vien_nguoi_dung_created_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_giao_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_hoc_vien_nguoi_dung_created_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_hoc_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_nhan_vien_nguoi_dung_created_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_ho_so_nhan_vien_nguoi_dung_updated_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_khach_hang_nguoi_dungs_created_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropForeignKey(
                name: "fk_khach_hang_nguoi_dungs_updated_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropForeignKey(
                name: "fk_khoa_hoc_nguoi_dungs_created_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_khoan_thu_hoc_phi_nguoi_dungs_created_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropForeignKey(
                name: "fk_khoan_thu_hoc_phi_nguoi_dungs_updated_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropForeignKey(
                name: "fk_lich_su_cham_soc_nguoi_dungs_created_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lich_su_cham_soc_nguoi_dungs_updated_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lien_ket_mxh_nguoi_dung_created_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropForeignKey(
                name: "fk_lien_ket_mxh_nguoi_dung_updated_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_created_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_updated_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_hoc_vien_nguoi_dungs_created_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_hoc_vien_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_khoa_hoc_nguoi_dungs_created_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_khoa_hoc_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_tro_giang_nguoi_dungs_created_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropForeignKey(
                name: "fk_lop_hoc_tro_giang_nguoi_dungs_updated_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropForeignKey(
                name: "fk_nguoi_dung_nguoi_dung_created_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropForeignKey(
                name: "fk_nguoi_dung_nguoi_dung_updated_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_created_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_updated_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_nhan_xet_buoi_hoc_nguoi_dung_created_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_nhan_xet_buoi_hoc_nguoi_dung_updated_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_nhat_ky_he_thong_nguoi_dung_created_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropForeignKey(
                name: "fk_nhat_ky_he_thong_nguoi_dung_updated_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropForeignKey(
                name: "fk_phong_ban_nguoi_dung_created_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropForeignKey(
                name: "fk_phong_ban_nguoi_dung_updated_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropForeignKey(
                name: "fk_quyen_nguoi_dung_created_by_id",
                table: "QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_quyen_nguoi_dung_updated_by_id",
                table: "QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_quyen_chuc_nang_nguoi_dung_created_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropForeignKey(
                name: "fk_quyen_chuc_nang_nguoi_dung_updated_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_token_nguoi_dung_created_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_token_nguoi_dung_updated_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropForeignKey(
                name: "fk_san_pham_nguoi_dung_created_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropForeignKey(
                name: "fk_san_pham_nguoi_dung_updated_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_khoan_nguoi_dung_created_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_khoan_nguoi_dung_updated_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_lieu_nguoi_dung_created_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_lieu_nguoi_dung_updated_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_lieu_lop_hoc_nguoi_dung_created_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_tai_lieu_lop_hoc_nguoi_dung_updated_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropForeignKey(
                name: "fk_tenant_nguoi_dung_created_by_id",
                table: "TENANT");

            migrationBuilder.DropForeignKey(
                name: "fk_tenant_nguoi_dung_updated_by_id",
                table: "TENANT");

            migrationBuilder.DropForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_created_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropForeignKey(
                name: "fk_tep_dinh_kem_nguoi_dung_updated_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropForeignKey(
                name: "fk_thu_tien_dang_ky_nguoi_dung_created_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropForeignKey(
                name: "fk_thu_tien_dang_ky_nguoi_dung_updated_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_created_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropForeignKey(
                name: "fk_yeu_cau_xep_lop_nguoi_dung_created_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropForeignKey(
                name: "fk_yeu_cau_xep_lop_nguoi_dung_updated_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropIndex(
                name: "ix_yeu_cau_xep_lop_created_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropIndex(
                name: "ix_yeu_cau_xep_lop_updated_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropIndex(
                name: "ix_token_datlai_matkhau_created_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropIndex(
                name: "ix_token_datlai_matkhau_updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropIndex(
                name: "ix_thu_tien_dang_ky_created_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropIndex(
                name: "ix_thu_tien_dang_ky_updated_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropIndex(
                name: "ix_tep_dinh_kem_created_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropIndex(
                name: "ix_tep_dinh_kem_updated_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropIndex(
                name: "ix_tenant_created_by_id",
                table: "TENANT");

            migrationBuilder.DropIndex(
                name: "ix_tenant_updated_by_id",
                table: "TENANT");

            migrationBuilder.DropIndex(
                name: "ix_tai_lieu_lop_hoc_created_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_tai_lieu_lop_hoc_updated_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_tai_lieu_created_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropIndex(
                name: "ix_tai_lieu_updated_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropIndex(
                name: "ix_tai_khoan_created_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropIndex(
                name: "ix_tai_khoan_updated_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropIndex(
                name: "ix_san_pham_created_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropIndex(
                name: "ix_san_pham_updated_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropIndex(
                name: "ix_refresh_token_created_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropIndex(
                name: "ix_refresh_token_updated_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropIndex(
                name: "ix_quyen_chuc_nang_created_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropIndex(
                name: "ix_quyen_chuc_nang_updated_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropIndex(
                name: "ix_quyen_created_by_id",
                table: "QUYEN");

            migrationBuilder.DropIndex(
                name: "ix_quyen_updated_by_id",
                table: "QUYEN");

            migrationBuilder.DropIndex(
                name: "ix_phong_ban_created_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropIndex(
                name: "ix_phong_ban_updated_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropIndex(
                name: "ix_nhat_ky_he_thong_created_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropIndex(
                name: "ix_nhat_ky_he_thong_updated_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropIndex(
                name: "ix_nhan_xet_buoi_hoc_created_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropIndex(
                name: "ix_nhan_xet_buoi_hoc_updated_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropIndex(
                name: "ix_nguoidung_quyen_created_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropIndex(
                name: "ix_nguoidung_quyen_updated_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_created_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_updated_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_tro_giang_created_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_tro_giang_updated_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_khoa_hoc_created_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_khoa_hoc_updated_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_hoc_vien_created_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_hoc_vien_updated_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_created_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_lop_hoc_updated_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropIndex(
                name: "ix_lien_ket_mxh_created_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropIndex(
                name: "ix_lien_ket_mxh_updated_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropIndex(
                name: "ix_lich_su_cham_soc_created_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropIndex(
                name: "ix_lich_su_cham_soc_updated_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropIndex(
                name: "ix_khoan_thu_hoc_phi_created_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropIndex(
                name: "ix_khoan_thu_hoc_phi_updated_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropIndex(
                name: "ix_khoa_hoc_created_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_khoa_hoc_updated_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_khach_hang_created_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropIndex(
                name: "ix_khach_hang_updated_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_nhan_vien_created_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_nhan_vien_updated_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_hoc_vien_created_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_hoc_vien_updated_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_giao_vien_created_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_ho_so_giao_vien_updated_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropIndex(
                name: "ix_diem_danh_created_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropIndex(
                name: "ix_diem_danh_updated_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropIndex(
                name: "ix_dang_ky_khoa_hoc_created_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_dang_ky_khoa_hoc_updated_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropIndex(
                name: "ix_chuc_vu_created_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropIndex(
                name: "ix_chuc_vu_updated_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropIndex(
                name: "ix_buoi_hoc_created_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropIndex(
                name: "ix_buoi_hoc_updated_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropIndex(
                name: "ix_bai_tap_created_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropIndex(
                name: "ix_bai_tap_updated_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropIndex(
                name: "ix_bai_nop_created_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropIndex(
                name: "ix_bai_nop_updated_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropIndex(
                name: "ix_bai_lam_created_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropIndex(
                name: "ix_bai_lam_updated_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropIndex(
                name: "ix_bai_kiem_tra_created_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropIndex(
                name: "ix_bai_kiem_tra_updated_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropColumn(
                name: "nguon",
                table: "KHACH_HANG");

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_tao_id",
                table: "LOP_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_tao_id",
                table: "KHACH_HANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_tao_id",
                table: "BAI_TAP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "nguoi_tao_id",
                table: "BAI_KIEM_TRA",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_lop_hoc_nguoi_tao_id",
                table: "LOP_HOC",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_khach_hang_nguoi_tao_id",
                table: "KHACH_HANG",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_tap_nguoi_tao_id",
                table: "BAI_TAP",
                column: "nguoi_tao_id");

            migrationBuilder.CreateIndex(
                name: "ix_bai_kiem_tra_nguoi_tao_id",
                table: "BAI_KIEM_TRA",
                column: "nguoi_tao_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bai_kiem_tra_nguoi_dungs_nguoi_tao_id",
                table: "BAI_KIEM_TRA",
                column: "nguoi_tao_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bai_tap_nguoi_dungs_nguoi_tao_id",
                table: "BAI_TAP",
                column: "nguoi_tao_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_khach_hang_nguoi_dungs_nguoi_tao_id",
                table: "KHACH_HANG",
                column: "nguoi_tao_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lop_hoc_nguoi_dungs_nguoi_tao_id",
                table: "LOP_HOC",
                column: "nguoi_tao_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
