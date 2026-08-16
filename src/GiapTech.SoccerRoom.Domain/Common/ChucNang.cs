namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>
/// Danh mục chức năng dùng trong phân quyền động (FR-05).
///
/// Đây là danh mục ĐÓNG — người dùng chọn từ danh sách này, không tự nhập chuỗi tùy ý,
/// nếu không sẽ sinh ra quyền gõ sai không bao giờ khớp với endpoint nào.
///
/// Thêm module mới: thêm hằng số ở đây VÀ cập nhật bảng trong
/// docs/backend/phan-quyen-dong.md trong cùng PR.
/// </summary>
public static class ChucNang
{
    public const string LichThiDau = nameof(LichThiDau);
    public const string ThongKe = nameof(ThongKe);
    public const string TaiChinh = nameof(TaiChinh);
    public const string TaiKhoan = nameof(TaiKhoan);
    public const string CauThu = nameof(CauThu);
    public const string PhanQuyen = nameof(PhanQuyen);
    public const string ThietLapChung = nameof(ThietLapChung);

    /// <summary>
    /// Đổi mật khẩu cho tài khoản KHÁC — đặc quyền chỉ Admin có (FR-03).
    /// Biểu diễn bằng một chức năng riêng thay vì ngoại lệ hard-code trong code,
    /// để một cơ chế duy nhất quyết định mọi truy cập.
    /// </summary>
    public const string DoiMatKhauNguoiKhac = nameof(DoiMatKhauNguoiKhac);

    public static readonly IReadOnlyList<string> TatCa =
    [
        LichThiDau, ThongKe, TaiChinh, TaiKhoan,
        CauThu, PhanQuyen, ThietLapChung, DoiMatKhauNguoiKhac
    ];
}
