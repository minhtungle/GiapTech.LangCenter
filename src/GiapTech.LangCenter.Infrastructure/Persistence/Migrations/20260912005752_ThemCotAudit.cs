using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemCotAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "YEU_CAU_XEP_LOP",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "YEU_CAU_XEP_LOP",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "THU_TIEN_DANG_KY",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "THU_TIEN_DANG_KY",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TEP_DINH_KEM",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TEP_DINH_KEM",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TENANT",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TENANT",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TAI_LIEU_LOP_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TAI_LIEU_LOP_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TAI_LIEU",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TAI_LIEU",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "TAI_KHOAN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "TAI_KHOAN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "SAN_PHAM",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "SAN_PHAM",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "REFRESH_TOKEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "REFRESH_TOKEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "QUYEN_CHUC_NANG",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "QUYEN_CHUC_NANG",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "QUYEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "QUYEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "PHONG_BAN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "PHONG_BAN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "NHAT_KY_HE_THONG",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "NHAT_KY_HE_THONG",
                newName: "updated_at");

            migrationBuilder.RenameIndex(
                name: "ix_nhat_ky_he_thong_tenant_id_ngay_tao",
                table: "NHAT_KY_HE_THONG",
                newName: "ix_nhat_ky_he_thong_tenant_id_created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "NHAN_XET_BUOI_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "NHAN_XET_BUOI_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "NGUOIDUNG_QUYEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "NGUOIDUNG_QUYEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "NGUOI_DUNG",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "NGUOI_DUNG",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LOP_HOC_TRO_GIANG",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LOP_HOC_TRO_GIANG",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LOP_HOC_KHOA_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LOP_HOC_KHOA_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LOP_HOC_HOC_VIEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LOP_HOC_HOC_VIEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LOP_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LOP_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LIEN_KET_MXH",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LIEN_KET_MXH",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "LICH_SU_CHAM_SOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "LICH_SU_CHAM_SOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "KHOAN_THU_HOC_PHI",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "KHOAN_THU_HOC_PHI",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "KHOA_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "KHOA_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "KHACH_HANG",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "KHACH_HANG",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "HO_SO_NHAN_VIEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "HO_SO_NHAN_VIEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "HO_SO_HOC_VIEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "HO_SO_HOC_VIEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "HO_SO_GIAO_VIEN",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "HO_SO_GIAO_VIEN",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "DIEM_DANH",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "DIEM_DANH",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "DANG_KY_KHOA_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "DANG_KY_KHOA_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "CHUC_VU",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "CHUC_VU",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "BUOI_HOC",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "BUOI_HOC",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "BAI_TAP",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "BAI_TAP",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "BAI_NOP",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "BAI_NOP",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "BAI_LAM",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "BAI_LAM",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ngay_tao",
                table: "BAI_KIEM_TRA",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ngay_cap_nhat",
                table: "BAI_KIEM_TRA",
                newName: "updated_at");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "YEU_CAU_XEP_LOP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "YEU_CAU_XEP_LOP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "THU_TIEN_DANG_KY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "THU_TIEN_DANG_KY",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TEP_DINH_KEM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TEP_DINH_KEM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TENANT",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TENANT",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TAI_LIEU_LOP_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TAI_LIEU_LOP_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TAI_LIEU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TAI_LIEU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "TAI_KHOAN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "TAI_KHOAN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "SAN_PHAM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "SAN_PHAM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "REFRESH_TOKEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "REFRESH_TOKEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "QUYEN_CHUC_NANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "QUYEN_CHUC_NANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "QUYEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "QUYEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "PHONG_BAN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "PHONG_BAN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "NHAT_KY_HE_THONG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "NHAT_KY_HE_THONG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "NHAN_XET_BUOI_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "NHAN_XET_BUOI_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "NGUOIDUNG_QUYEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "NGUOIDUNG_QUYEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "NGUOI_DUNG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "NGUOI_DUNG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LOP_HOC_TRO_GIANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LOP_HOC_TRO_GIANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LOP_HOC_KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LOP_HOC_KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LOP_HOC_HOC_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LOP_HOC_HOC_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LOP_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LOP_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LIEN_KET_MXH",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LIEN_KET_MXH",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "LICH_SU_CHAM_SOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "LICH_SU_CHAM_SOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "KHOAN_THU_HOC_PHI",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "KHOAN_THU_HOC_PHI",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "KHACH_HANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "KHACH_HANG",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "HO_SO_NHAN_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "HO_SO_NHAN_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "HO_SO_HOC_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "HO_SO_HOC_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "HO_SO_GIAO_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "HO_SO_GIAO_VIEN",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "DIEM_DANH",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "DIEM_DANH",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "DANG_KY_KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "DANG_KY_KHOA_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "CHUC_VU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "CHUC_VU",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "BUOI_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "BUOI_HOC",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "BAI_TAP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "BAI_TAP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "BAI_NOP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "BAI_NOP",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "BAI_LAM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "BAI_LAM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "BAI_KIEM_TRA",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_id",
                table: "BAI_KIEM_TRA",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "YEU_CAU_XEP_LOP");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "THU_TIEN_DANG_KY");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TEP_DINH_KEM");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TAI_LIEU_LOP_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TAI_LIEU");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "TAI_KHOAN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "SAN_PHAM");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "QUYEN_CHUC_NANG");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "QUYEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "QUYEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "PHONG_BAN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "NHAT_KY_HE_THONG");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "NHAN_XET_BUOI_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "NGUOI_DUNG");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LOP_HOC_TRO_GIANG");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LOP_HOC_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LOP_HOC_HOC_VIEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LOP_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LIEN_KET_MXH");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "LICH_SU_CHAM_SOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "KHOAN_THU_HOC_PHI");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "KHACH_HANG");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "HO_SO_HOC_VIEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "DIEM_DANH");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "DANG_KY_KHOA_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "CHUC_VU");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "BUOI_HOC");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "BAI_TAP");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "BAI_NOP");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "BAI_LAM");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.DropColumn(
                name: "updated_by_id",
                table: "BAI_KIEM_TRA");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "YEU_CAU_XEP_LOP",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "YEU_CAU_XEP_LOP",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "THU_TIEN_DANG_KY",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "THU_TIEN_DANG_KY",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TEP_DINH_KEM",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TEP_DINH_KEM",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TENANT",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TENANT",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TAI_LIEU_LOP_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TAI_LIEU_LOP_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TAI_LIEU",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TAI_LIEU",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "TAI_KHOAN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TAI_KHOAN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "SAN_PHAM",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "SAN_PHAM",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "REFRESH_TOKEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "REFRESH_TOKEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "QUYEN_CHUC_NANG",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "QUYEN_CHUC_NANG",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "QUYEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "QUYEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "PHONG_BAN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "PHONG_BAN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "NHAT_KY_HE_THONG",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "NHAT_KY_HE_THONG",
                newName: "ngay_tao");

            migrationBuilder.RenameIndex(
                name: "ix_nhat_ky_he_thong_tenant_id_created_at",
                table: "NHAT_KY_HE_THONG",
                newName: "ix_nhat_ky_he_thong_tenant_id_ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "NHAN_XET_BUOI_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "NHAN_XET_BUOI_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "NGUOIDUNG_QUYEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "NGUOIDUNG_QUYEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "NGUOI_DUNG",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "NGUOI_DUNG",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LOP_HOC_TRO_GIANG",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LOP_HOC_TRO_GIANG",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LOP_HOC_KHOA_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LOP_HOC_KHOA_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LOP_HOC_HOC_VIEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LOP_HOC_HOC_VIEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LOP_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LOP_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LIEN_KET_MXH",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LIEN_KET_MXH",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "LICH_SU_CHAM_SOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "LICH_SU_CHAM_SOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "KHOAN_THU_HOC_PHI",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "KHOAN_THU_HOC_PHI",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "KHOA_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "KHOA_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "KHACH_HANG",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "KHACH_HANG",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "HO_SO_NHAN_VIEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "HO_SO_NHAN_VIEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "HO_SO_HOC_VIEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "HO_SO_HOC_VIEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "HO_SO_GIAO_VIEN",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "HO_SO_GIAO_VIEN",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "DIEM_DANH",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "DIEM_DANH",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "DANG_KY_KHOA_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "DANG_KY_KHOA_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "CHUC_VU",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "CHUC_VU",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "BUOI_HOC",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "BUOI_HOC",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "BAI_TAP",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "BAI_TAP",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "BAI_NOP",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "BAI_NOP",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "BAI_LAM",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "BAI_LAM",
                newName: "ngay_tao");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "BAI_KIEM_TRA",
                newName: "ngay_cap_nhat");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "BAI_KIEM_TRA",
                newName: "ngay_tao");
        }
    }
}
