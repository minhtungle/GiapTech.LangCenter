namespace GiapTech.SoccerRoom.Application.Common.Exceptions;

/// <summary>
/// Danh mục mã lỗi API. Quy tắc bất di bất dịch #3: API trả MÃ LỖI, không trả text một
/// ngôn ngữ — frontend tra bảng dịch qua react-i18next.
///
/// Thêm mã mới ở đây thì phải thêm bản dịch tương ứng ở frontend trong cùng PR, nếu không
/// người dùng sẽ thấy chính chuỗi mã lỗi trên màn hình.
/// </summary>
public static class MaLoi
{
    // --- Đăng nhập (FR-01, FR-02) ---
    /// <summary>Sai mã trung tâm, sai username hoặc sai mật khẩu — cùng một mã, không phân biệt,
    /// để không tiết lộ tenant/tài khoản nào tồn tại.</summary>
    public const string DangNhapThatBai = "DANG_NHAP_THAT_BAI";

    public const string TaiKhoanBiVoHieuHoa = "TAI_KHOAN_BI_VO_HIEU_HOA";
    public const string PhaiDoiMatKhau = "PHAI_DOI_MAT_KHAU";
    public const string MatKhauCuKhongDung = "MAT_KHAU_CU_KHONG_DUNG";
    public const string TokenDatLaiKhongHopLe = "TOKEN_DAT_LAI_KHONG_HOP_LE";

    // --- Xác thực / phân quyền ---
    public const string ChuaXacThuc = "CHUA_XAC_THUC";
    public const string KhongDuQuyen = "KHONG_DU_QUYEN";
    public const string TokenThieuTenant = "TOKEN_THIEU_TENANT";

    // --- Chung ---
    public const string KhongTimThay = "KHONG_TIM_THAY";
    public const string DuLieuKhongHopLe = "DU_LIEU_KHONG_HOP_LE";
    public const string LoiHeThong = "LOI_HE_THONG";
}
