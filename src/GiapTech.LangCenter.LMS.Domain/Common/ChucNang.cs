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

    // ---------- Nhân sự (HRM) ----------

    /// <summary>
    /// Hồ sơ nhân viên kinh doanh — chỉ tiêu, khách hàng phụ trách, hoa hồng.
    /// </summary>
    public const string NhanVienKinhDoanh = nameof(NhanVienKinhDoanh);

    /// <summary>
    /// Giáo viên dưới góc nhìn NHÂN SỰ — hợp đồng, lương, chấm công.
    ///
    /// Vẫn là chính con người trong `NGUOI_DUNG` + `HO_SO_GIAO_VIEN` mà LMS dùng để phân công
    /// lớp, **không** phải bảng nhân sự thứ hai: hai nguồn sự thật cho cùng một người thì đổi
    /// tên một bên là bên kia sai (đúng lỗi đã gặp với tài khoản/người dùng).
    ///
    /// Tách khỏi <see cref="TaiKhoan"/> vì hai việc khác nhau: `TaiKhoan` là ai đăng nhập
    /// được, `GiaoVienNhanSu` là điều kiện làm việc của một người. Trưởng phòng nhân sự cần
    /// cái sau mà không cần cái trước.
    /// </summary>
    public const string GiaoVienNhanSu = nameof(GiaoVienNhanSu);

    // ---------- Khách hàng (CRM) ----------

    /// <summary>
    /// Doanh thu — tổng hợp theo kỳ, theo lớp, theo nhân viên.
    ///
    /// Tách khỏi <see cref="HocPhi"/>: `HocPhi` là **sổ thu từng khoản** của một lớp (ai đóng
    /// bao nhiêu, còn nợ bao nhiêu), `DoanhThu` là **số tổng hợp** để ban giám đốc xem. Người
    /// xem doanh thu toàn trung tâm không nhất thiết được xem công nợ từng học viên, và ngược
    /// lại — kế toán lớp không cần thấy doanh thu toàn công ty.
    /// </summary>
    public const string DoanhThu = nameof(DoanhThu);

    /// <summary>
    /// Khách hàng — người quan tâm khoá học (FR-17).
    ///
    /// Tách khỏi <see cref="DoanhThu"/>: người trực tổng đài nhập khách mới cần quyền này mà
    /// **không** cần thấy số tiền của mọi đơn hàng.
    /// </summary>
    public const string KhachHang = nameof(KhachHang);

    /// <summary>
    /// Danh mục khoá học bán ra (FR-19).
    ///
    /// Tách khỏi <see cref="DoanhThu"/> vì ma trận khác: người bán **xem** giá niêm yết để báo
    /// giá, nhưng chỉ quản lý mới **sửa** được giá — gộp lại thì không diễn đạt nổi.
    /// </summary>
    public const string KhoaHoc = nameof(KhoaHoc);

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

    /// <summary>
    /// Xem nhật ký thao tác hệ thống (FR-16).
    ///
    /// Chỉ `Xem` có nghĩa thực chất: nhật ký do hệ thống tự ghi (`Them`), và **không bao giờ**
    /// sửa hay xoá — nhật ký sửa được thì không còn là nhật ký. Seeder cấp cả 4 hành động cho
    /// nhóm quản trị theo vòng lặp chung, nhưng không có endpoint nào cho Sua/Xoa.
    /// </summary>
    public const string NhatKyHeThong = nameof(NhatKyHeThong);

    public static readonly IReadOnlyList<string> TatCa =
    [
        TaiKhoan, PhanQuyen, ThietLapChung, Anh, DoiMatKhauNguoiKhac,
        NhanVienKinhDoanh, GiaoVienNhanSu, DoanhThu, KhachHang, KhoaHoc,
        LopHoc, BuoiHoc, DiemDanh, BaiTap, BaiNopBaiTap, BaiKiemTra, BaiLamKiemTra,
        TaiLieu, HocPhi, ThongKe, LopHocToanTrungTam, NhatKyHeThong
    ];

    /// <summary>
    /// Chức năng nào thuộc hệ thống nào — nguồn sự thật DUY NHẤT cho việc nhóm.
    ///
    /// Chỉ liệt kê chức năng thuộc ĐÚNG MỘT hệ thống. Nhóm quản trị hệ thống (`TaiKhoan`,
    /// `PhanQuyen`, `ThietLapChung`, `Anh`, `DoiMatKhauNguoiKhac`, `NhatKyHeThong`) **cố ý
    /// không có ở đây**: chúng dùng chung cho cả ba hệ thống, xem <see cref="DungChung"/>.
    ///
    /// Ép chúng vào một hệ thống nào đó sẽ sai theo cả hai hướng: người quản trị nhân sự cần
    /// sửa tài khoản nhưng không cần vào LMS, còn nhật ký thì ghi thao tác của cả ba.
    /// </summary>
    private static readonly Dictionary<string, HeThong> TheoHeThong = new()
    {
        [NhanVienKinhDoanh] = HeThong.Hrm,
        [GiaoVienNhanSu] = HeThong.Hrm,

        [DoanhThu] = HeThong.Crm,
        [KhachHang] = HeThong.Crm,
        [KhoaHoc] = HeThong.Crm,

        [LopHoc] = HeThong.Lms,
        [BuoiHoc] = HeThong.Lms,
        [DiemDanh] = HeThong.Lms,
        [BaiTap] = HeThong.Lms,
        [BaiNopBaiTap] = HeThong.Lms,
        [BaiKiemTra] = HeThong.Lms,
        [BaiLamKiemTra] = HeThong.Lms,
        [TaiLieu] = HeThong.Lms,
        [HocPhi] = HeThong.Lms,
        [ThongKe] = HeThong.Lms,
        [LopHocToanTrungTam] = HeThong.Lms
    };

    /// <summary>
    /// Chức năng quản trị dùng chung cho cả ba hệ thống — hiện ở sidebar của hệ thống nào cũng
    /// được, miễn người dùng có quyền.
    /// </summary>
    public static readonly IReadOnlyList<string> DungChung =
    [
        TaiKhoan, PhanQuyen, ThietLapChung, Anh, DoiMatKhauNguoiKhac, NhatKyHeThong
    ];

    /// <summary>
    /// Hệ thống của một chức năng; `null` = dùng chung cho cả ba.
    ///
    /// Trả `null` thay vì ném lỗi cho chức năng không có trong bảng: thêm hằng mới mà quên khai
    /// hệ thống thì nó thành "dùng chung" — hiện ở mọi sidebar, tức là **thấy quá nhiều**, dễ
    /// phát hiện. Ném lỗi sẽ làm sập màn phân quyền của mọi tenant. Có test canh việc khai đủ.
    /// </summary>
    public static HeThong? HeThongCua(string chucNang) =>
        TheoHeThong.TryGetValue(chucNang, out var ht) ? ht : null;

    /// <summary>Các chức năng của một hệ thống, KHÔNG gồm nhóm dùng chung.</summary>
    public static IReadOnlyList<string> ChucNangCua(HeThong heThong) =>
        TatCa.Where(cn => HeThongCua(cn) == heThong).ToList();
}
