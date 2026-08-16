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
    /// <summary>Sai ID đội, sai username hoặc sai mật khẩu — cùng một mã, không phân biệt,
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

    // --- Nghiệp vụ ---
    public const string MvpDaVoteRoi = "MVP_DA_VOTE_ROI";
    public const string TranDauDaDienRaKhongXoaDuoc = "TRAN_DAU_DA_DIEN_RA_KHONG_XOA_DUOC";
    public const string LoiMoiDaXuLy = "LOI_MOI_DA_XU_LY";
    public const string DoiThuDaTonTai = "DOI_THU_DA_TON_TAI";
    public const string DoiThuDaCoTranDau = "DOI_THU_DA_CO_TRAN_DAU";
    public const string LoiMoiDaSinhTran = "LOI_MOI_DA_SINH_TRAN";
    public const string CauThuKhongTrongDoiHinh = "CAU_THU_KHONG_TRONG_DOI_HINH";
    public const string CauThuKhongHopLe = "CAU_THU_KHONG_HOP_LE";
    public const string SoTienVuotQuaCanDong = "SO_TIEN_VUOT_QUA_CAN_DONG";
}
