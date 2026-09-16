namespace GiapTech.LangCenter.Application.Common.Exceptions;

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

    // --- Tệp hồ sơ nhân sự (FR-23) ---
    /// <summary>
    /// Hồ sơ nhân sự chỉ nhận PDF/Word/Excel — HẸP HƠN whitelist của kho lưu trữ dùng chung
    /// (kho còn nhận ảnh, mp3, zip, pptx cho học liệu LMS). Cần mã riêng chứ không dùng lại
    /// <c>LOAI_TEP_KHONG_HO_TRO</c> của kho: hai thông điệp liệt kê hai danh sách khác nhau,
    /// dùng chung thì người dùng HRM đọc được "chấp nhận cả ảnh và file nén" rồi thử và bị từ
    /// chối. → <see cref="NhanSu.LoaiTepHoSo"/>
    /// </summary>
    public const string LoaiTepHoSoKhongHoTro = "LOAI_TEP_HO_SO_KHONG_HO_TRO";

    /// <summary>
    /// Mỗi hồ sơ nhân sự tối đa <see cref="NhanSu.TepHoSo.SoTepToiDa"/> tệp (yêu cầu
    /// 16/09/2026).
    ///
    /// Hạn mức tính **theo từng hồ sơ**, không phải toàn trung tâm: người này tải nhiều không
    /// được làm người kia hết chỗ.
    /// </summary>
    public const string VuotSoTepHoSo = "VUOT_SO_TEP_HO_SO";

    /// <summary>
    /// Tên hiển thị người dùng đặt bị rỗng sau khi làm sạch (chỉ gồm dấu cách, ký tự điều
    /// khiển…). Không im lặng quay về tên máy quét: người dùng chủ động gõ tên thì họ cần biết
    /// tên đó không dùng được.
    /// </summary>
    public const string TenTepKhongHopLe = "TEN_TEP_KHONG_HOP_LE";

    // --- Chung ---
    public const string KhongTimThay = "KHONG_TIM_THAY";
    public const string DuLieuKhongHopLe = "DU_LIEU_KHONG_HOP_LE";
    public const string LoiHeThong = "LOI_HE_THONG";
}
