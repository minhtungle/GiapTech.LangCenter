namespace GiapTech.LangCenter.LMS.Domain.Common;

/// <summary>
/// Danh mục chức năng dùng trong phân quyền động (FR-05).
///
/// Đây là danh mục ĐÓNG — người dùng chọn từ danh sách này, không tự nhập chuỗi tùy ý,
/// nếu không sẽ sinh ra quyền gõ sai không bao giờ khớp với endpoint nào.
///
/// Thêm module mới: thêm hằng số ở đây VÀ cập nhật bảng trong
/// docs/backend/phan-quyen-dong.md trong cùng PR.
///
/// ⚠️ Thêm hằng mới KHÔNG tự cấp quyền cho nhóm "Quản trị viên" của tenant ĐÃ TỒN TẠI —
/// seeder chỉ chạy lúc tạo tenant. Xem <c>BoKhuyetQuyenQuanTri</c> ở tầng Infrastructure.
/// </summary>
public static class ChucNang
{
    // ---------- Quản trị hệ thống ----------

    public const string TaiKhoan = nameof(TaiKhoan);
    public const string PhanQuyen = nameof(PhanQuyen);
    public const string ThietLapChung = nameof(ThietLapChung);

    /// <summary>
    /// Tải / đọc / xóa ảnh (logo, ảnh bìa, ảnh QR).
    ///
    /// Là chức năng RIÊNG chứ không ghép vào <see cref="ThietLapChung"/>: endpoint đọc ảnh
    /// dùng chung cho mọi loại ảnh, nên nó cần một quyền không phụ thuộc loại đối tượng —
    /// gác bằng quyền của một module cụ thể sẽ khiến người thiếu quyền đó không xem được
    /// cả logo, một cách âm thầm.
    /// </summary>
    public const string Anh = nameof(Anh);

    /// <summary>
    /// Đổi mật khẩu cho tài khoản KHÁC — đặc quyền chỉ Admin có (FR-03).
    /// Biểu diễn bằng một chức năng riêng thay vì ngoại lệ hard-code trong code,
    /// để một cơ chế duy nhất quyết định mọi truy cập.
    /// </summary>
    public const string DoiMatKhauNguoiKhac = nameof(DoiMatKhauNguoiKhac);

    // ---------- Nghiệp vụ đào tạo ----------

    public const string LopHoc = nameof(LopHoc);
    public const string BuoiHoc = nameof(BuoiHoc);
    public const string DiemDanh = nameof(DiemDanh);

    /// <summary>
    /// Giao bài tập trong buổi học. TÁCH khỏi <see cref="BaiKiemTra"/> vì ma trận phân quyền
    /// của hai thứ này khác nhau: trợ giảng được toàn quyền với bài tập nhưng chỉ xem được
    /// bài kiểm tra. Gộp lại thì không diễn đạt nổi sự khác biệt đó.
    /// </summary>
    public const string BaiTap = nameof(BaiTap);

    /// <summary>
    /// Bài học viên nộp cho bài tập. Tách khỏi <see cref="BaiTap"/> vì học viên được TẠO
    /// bài nộp nhưng không được tạo bài tập.
    /// </summary>
    public const string BaiNopBaiTap = nameof(BaiNopBaiTap);

    public const string BaiKiemTra = nameof(BaiKiemTra);

    /// <summary>Bài học viên làm cho bài kiểm tra — tách cùng lý do với <see cref="BaiNopBaiTap"/>.</summary>
    public const string BaiLamKiemTra = nameof(BaiLamKiemTra);

    public const string TaiLieu = nameof(TaiLieu);
    public const string HocPhi = nameof(HocPhi);
    public const string ThongKe = nameof(ThongKe);

    /// <summary>
    /// Thấy MỌI lớp của trung tâm, không chỉ lớp mình phụ trách.
    ///
    /// Đây là cách hệ thống nhận ra "người quản trị" mà KHÔNG hard-code vai trò: giáo viên có
    /// <see cref="LopHoc"/> nhưng không có chức năng này nên chỉ thấy lớp mình dạy; admin có
    /// cả hai nên thấy hết. Suy từ dữ liệu quyền chứ không từ TÊN nhóm quyền — tên là chuỗi
    /// người dùng tự sửa được, đổi tên nhóm không được phép làm mất quyền quản trị.
    ///
    /// Phân biệt theo thao tác: cấp <c>Xem</c> mà không cấp <c>Sua</c> nghĩa là xem được mọi
    /// lớp nhưng chỉ sửa lớp mình phụ trách.
    /// </summary>
    public const string LopHocToanTrungTam = nameof(LopHocToanTrungTam);

    public static readonly IReadOnlyList<string> TatCa =
    [
        TaiKhoan, PhanQuyen, ThietLapChung, Anh, DoiMatKhauNguoiKhac,
        LopHoc, BuoiHoc, DiemDanh, BaiTap, BaiNopBaiTap, BaiKiemTra, BaiLamKiemTra,
        TaiLieu, HocPhi, ThongKe, LopHocToanTrungTam
    ];
}
