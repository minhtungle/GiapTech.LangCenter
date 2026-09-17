using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Common;

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
    /// Hồ sơ nhân sự — nhân viên · giáo viên · trợ giảng (FR-03, FR-23).
    ///
    /// Đổi tên từ `GiaoVienNhanSu` (09/09/2026) khi bỏ hai màn riêng "Nhân viên kinh doanh" và
    /// "Giáo viên": chức năng này **luôn** gác cả ba vai trò nhân sự, nên tên cũ gây hiểu sai là
    /// chỉ dành cho giáo viên.
    ///
    /// Vẫn là chính con người trong `NGUOI_DUNG` + hồ sơ vai trò mà LMS dùng để phân công lớp,
    /// **không** phải bảng nhân sự thứ hai: hai nguồn sự thật cho cùng một người thì đổi tên một
    /// bên là bên kia sai.
    ///
    /// Tách khỏi <see cref="TaiKhoan"/> vì hai việc khác nhau: `TaiKhoan` là ai đăng nhập được,
    /// `NhanSu` là hồ sơ con người. Trưởng phòng nhân sự cần cái sau mà không cần cái trước.
    /// </summary>
    public const string NhanSu = nameof(NhanSu);

    /// <summary>
    /// Danh mục chức vụ (FR-24) — "Ban quản lý", "Trưởng phòng", "Kế toán"…
    ///
    /// Tách khỏi <see cref="NhanSu"/>: sửa **danh mục** là việc thiết lập của người quản trị,
    /// còn gán chức vụ cho một người là việc thường ngày của người trực nhân sự.
    /// </summary>
    public const string ChucVu = nameof(ChucVu);

    /// <summary>
    /// Cơ cấu tổ chức — cây phòng ban, người quản lý, xếp nhân sự vào phòng (FR-22).
    ///
    /// Tách khỏi <see cref="NhanSu"/>: sửa cơ cấu tổ chức là việc của người quản trị
    /// hoặc trưởng phòng nhân sự, còn xem/sửa hồ sơ một người là việc thường ngày của người
    /// trực nhân sự. Ma trận khác nhau nên không gộp.
    ///
    /// **Không gác quyền theo `PHONG_BAN.nguoi_quan_ly_id`** — đó là thông tin tổ chức. Quyền
    /// vẫn đọc từ `QUYEN_CHUC_NANG` (quy tắc #9).
    /// </summary>
    public const string PhongBan = nameof(PhongBan);

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

    /// <summary>
    /// Danh mục sản phẩm bán kèm: sách, học cụ (FR-20).
    ///
    /// Tách khỏi <see cref="KhoaHoc"/>: người quản kho sách không nhất thiết được sửa giá khoá
    /// học, và ngược lại.
    /// </summary>
    public const string SanPham = nameof(SanPham);

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

    /// <summary>
    /// Soạn khoá học trực tuyến và bài học trong đó (FR-26). Quyền của giáo vụ.
    ///
    /// Tách khỏi <see cref="GhiDanhKhoaOnline"/> vì hai việc khác người làm: soạn nội dung là
    /// chuyên môn, cấp quyền học là điều phối. Gộp lại thì ai sửa được bài cũng cấp được quyền.
    /// </summary>
    public const string KhoaOnline = nameof(KhoaOnline);

    /// <summary>Cấp / thu quyền học một khoá trực tuyến — xem <see cref="KhoaOnline"/>.</summary>
    public const string GhiDanhKhoaOnline = nameof(GhiDanhKhoaOnline);

    /// <summary>
    /// Học viên đọc bài và đánh dấu đã học. Quyền của **chính người học**, tách khỏi hai quyền
    /// trên cùng lẽ với <see cref="BaiNopBaiTap"/> tách khỏi <see cref="BaiTap"/>.
    /// </summary>
    public const string HocOnline = nameof(HocOnline);

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

    // ==================== Chức năng tách mới (14/09/2026) ====================
    // Mỗi cái sinh ra vì một chức năng cũ đang gộp hai nhóm màn hình khác nhau, khiến không
    // cấp được quyền cho một nhóm mà không cấp luôn cho nhóm kia.

    /// <summary>
    /// Hồ sơ con người ở màn Quản trị (`/nguoi-dung`) — **mọi vai trò**, không riêng học viên.
    ///
    /// Tách khỏi <see cref="TaiKhoan"/> (14/09/2026): trước đây cùng một quyền gác ba màn rất
    /// khác nhau — hồ sơ học viên (LMS), hồ sơ mọi vai trò (Quản trị), và tài khoản đăng nhập.
    /// Hệ quả: cấp `TaiKhoan.Them` cho người tuyển sinh để họ thêm học viên thì đồng thời cho
    /// họ **tạo tài khoản đăng nhập** và **tạo người dùng vai trò bất kỳ** — `POST /nguoi-dung`
    /// không có chốt `BaoDamLaHocVien` như `/hoc-vien`.
    /// </summary>
    public const string HoSoNguoiDung = nameof(HoSoNguoiDung);

    /// <summary>
    /// Lịch sử chăm sóc khách hàng (phễu bán hàng). Tách khỏi <see cref="KhachHang"/>: ghi một
    /// lần gọi điện không cùng mức với sửa/xoá hồ sơ khách.
    /// </summary>
    public const string ChamSocKhachHang = nameof(ChamSocKhachHang);

    /// <summary>
    /// Thống kê doanh thu toàn trung tâm (FR-28) — theo cá nhân, đội nhóm, mặt hàng.
    ///
    /// Tách khỏi <see cref="DoanhThu"/>: `DoanhThu.Xem` cho xem đơn của một khách, còn đây là
    /// **doanh số của cả đội**. Sale xem đơn khách mình không nên đồng nghĩa xem doanh số đồng
    /// nghiệp. Thay cho <see cref="ThongKe"/> vốn không gác endpoint nào.
    /// </summary>
    public const string ThongKeDoanhThu = nameof(ThongKeDoanhThu);

    /// <summary>
    /// Ghi danh học viên vào lớp, kèm **học phí riêng từng người**. Tách khỏi
    /// <see cref="LopHoc"/>: thêm người vào lớp là ghi dữ liệu tiền, khác sửa tên lớp.
    /// </summary>
    public const string GhiDanhLop = nameof(GhiDanhLop);

    /// <summary>
    /// Hàng chờ xếp lớp từ CRM (FR-21) — duyệt, từ chối, huỷ yêu cầu.
    ///
    /// Tách khỏi <see cref="LopHoc"/>: trước đây `GET /lop-hoc/cho-xep-lop` phải gác bằng
    /// `LopHoc.Sua` vì DTO mang số tiền của đơn — một endpoint CHỈ ĐỌC buộc dùng quyền ghi,
    /// làm trục `Xem` mất nghĩa. Nay `XepLop.Xem` cho xem hàng chờ, `XepLop.Duyet` cho quyết.
    /// </summary>
    public const string XepLop = nameof(XepLop);

    /// <summary>
    /// Nhận xét buổi học — học viên đánh giá buổi, giáo viên đọc.
    ///
    /// Tách khỏi <see cref="DiemDanh"/>: trước đây gửi nhận xét gác bằng `DiemDanh.Them`, một
    /// quyền **vay mượn** vì nhóm Học viên tình cờ có nó. Không thể tắt tính năng nhận xét mà
    /// vẫn cho tự điểm danh.
    /// </summary>
    public const string NhanXetBuoiHoc = nameof(NhanXetBuoiHoc);

    /// <summary>
    /// Danh mục **tiêu chí đánh giá** thang 5 (FR-29, 16/09/2026) — module cấu hình trong HRM.
    ///
    /// Tách khỏi <see cref="NhanSu"/>: đây là cấu hình ảnh hưởng tới **cách cả trung tâm được
    /// đánh giá**, không phải việc sửa một hồ sơ. Người quản lý nhân sự xem hồ sơ không đồng
    /// nghĩa được đổi bộ tiêu chí mà mọi người bị chấm theo.
    /// </summary>
    public const string TieuChiDanhGia = nameof(TieuChiDanhGia);

    /// <summary>
    /// Thống kê nhân sự (FR-29) — xếp hạng nhân viên kinh doanh · giáo viên · trợ giảng, và
    /// **chấm điểm nhân viên kinh doanh theo kỳ**.
    ///
    /// Tách khỏi <see cref="NhanSu"/> cùng lý do như `ThongKeDoanhThu` tách khỏi `DoanhThu`:
    /// `NhanSu.Xem` cho xem hồ sơ một người, còn đây là **bảng xếp hạng cả trung tâm** — kèm
    /// doanh số và điểm chất lượng của từng đồng nghiệp.
    ///
    /// `Cham` = ghi phiếu đánh giá nhân viên kinh doanh; tách khỏi `Xem` vì xem bảng xếp hạng
    /// là việc của nhiều người, còn chấm điểm là việc của quản lý.
    /// </summary>
    public const string ThongKeNhanSu = nameof(ThongKeNhanSu);

    public static readonly IReadOnlyList<string> TatCa =
    [
        TaiKhoan, HoSoNguoiDung, PhanQuyen, ThietLapChung, Anh, DoiMatKhauNguoiKhac,
        NhanSu, ChucVu, PhongBan, TieuChiDanhGia, ThongKeNhanSu,
        DoanhThu, ThongKeDoanhThu, KhachHang, ChamSocKhachHang, KhoaHoc, SanPham,
        LopHoc, GhiDanhLop, XepLop, BuoiHoc, DiemDanh, NhanXetBuoiHoc,
        BaiTap, BaiNopBaiTap, BaiKiemTra, BaiLamKiemTra,
        KhoaOnline, GhiDanhKhoaOnline, HocOnline,
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
        [NhanSu] = HeThong.Hrm,
        [ChucVu] = HeThong.Hrm,
        [PhongBan] = HeThong.Hrm,
        [TieuChiDanhGia] = HeThong.Hrm,
        [ThongKeNhanSu] = HeThong.Hrm,

        [DoanhThu] = HeThong.Crm,
        [ThongKeDoanhThu] = HeThong.Crm,
        [ChamSocKhachHang] = HeThong.Crm,
        [KhachHang] = HeThong.Crm,
        [KhoaHoc] = HeThong.Crm,
        [SanPham] = HeThong.Crm,

        [LopHoc] = HeThong.Lms,
        [GhiDanhLop] = HeThong.Lms,
        [XepLop] = HeThong.Lms,
        [NhanXetBuoiHoc] = HeThong.Lms,
        [BuoiHoc] = HeThong.Lms,
        [DiemDanh] = HeThong.Lms,
        [BaiTap] = HeThong.Lms,
        [BaiNopBaiTap] = HeThong.Lms,
        [BaiKiemTra] = HeThong.Lms,
        [BaiLamKiemTra] = HeThong.Lms,
        [KhoaOnline] = HeThong.Lms,
        [GhiDanhKhoaOnline] = HeThong.Lms,
        [HocOnline] = HeThong.Lms,
        [TaiLieu] = HeThong.Lms,
        [HocPhi] = HeThong.Lms,
        [ThongKe] = HeThong.Lms,
        [LopHocToanTrungTam] = HeThong.Lms
    };

    /// <summary>Bốn thao tác cơ bản — phần lớn chức năng CRUD dùng nguyên bộ này.</summary>
    private static readonly HanhDong[] Crud =
        [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa];

    /// <summary>
    /// **Thao tác nào áp cho chức năng nào** — nguồn sự thật DUY NHẤT cho ma trận phân quyền
    /// (14/09/2026).
    ///
    /// Trước đây màn phân quyền hiện **mọi thao tác cho mọi chức năng** (`Enum.GetNames`), nên
    /// 31/108 ô bật cũng không làm gì: `NhatKyHeThong.Xoa` (nhật ký không xoá được),
    /// `ChucVu.Xem` (bị `NhanSu.Xem` thay), `HocOnline.Sua`… Người cấu hình quyền không có cách
    /// nào biết ô nào có tác dụng.
    ///
    /// **Quy tắc khi thêm chức năng mới:** khai ở đây danh sách thao tác nó THẬT SỰ có endpoint
    /// dùng. Thiếu khai thì `MoiChucNangPhaiKhaiThaoTacTests` đỏ ngay.
    /// </summary>
    private static readonly Dictionary<string, HanhDong[]> ThaoTacTheoChucNang = new()
    {
        // ---------- Dùng chung ----------
        [TaiKhoan] = Crud,
        [HoSoNguoiDung] = Crud,
        // `Sua` bỏ: endpoint PUT duy nhất của nhóm quyền vừa đổi tên vừa ghi lại ma trận, nên
        // nó phải là `CauHinhQuyen` — thao tác nguy hiểm hơn. Giữ `Sua` là khai một ô chết.
        [PhanQuyen] = [HanhDong.Xem, HanhDong.Them, HanhDong.CauHinhQuyen, HanhDong.Xoa],
        [ThietLapChung] = [HanhDong.Xem, HanhDong.Sua, HanhDong.CauHinhTien],
        // `Anh` không có `Sua`: ảnh và tệp đính kèm chỉ tải lên mới hoặc xoá, không sửa tại chỗ.
        [Anh] = [HanhDong.Xem, HanhDong.Them, HanhDong.Xoa],
        [DoiMatKhauNguoiKhac] = [HanhDong.Sua],
        // Nhật ký do hệ thống tự ghi và KHÔNG BAO GIỜ sửa/xoá — sửa được thì không còn là nhật ký.
        [NhatKyHeThong] = [HanhDong.Xem],

        // ---------- HRM ----------
        [NhanSu] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa,
                    HanhDong.DocTep, HanhDong.QuanLyTep],
        // `ChucVu` không có `Xem`: danh mục chức vụ đọc kèm màn nhân sự, gác bằng `NhanSu.Xem`.
        [ChucVu] = [HanhDong.Them, HanhDong.Sua, HanhDong.Xoa],
        [PhongBan] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa,
                      HanhDong.XepNhanSu],

        // ---------- CRM ----------
        [DoanhThu] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa,
                      HanhDong.ThuTien, HanhDong.GuiXepLop],
        [ThongKeDoanhThu] = [HanhDong.Xem],
        // Danh mục cấu hình: đủ CRUD, nhưng KHÔNG `Xoa` — tiêu chí đã có điểm thì
        // chỉ ngừng dùng (`DangDung=false`), xoá sẽ làm mọi kỳ đã chấm đổi số.
        // `TuLam` = ĐỌC danh mục tiêu chí để tự đi chấm (18/09/2026). Cần ô riêng vì học viên
        // phải đọc được tên tiêu chí mới chấm giáo viên/trợ giảng trên phiếu nhận xét buổi học,
        // nhưng `Xem` là quyền của màn QUẢN LÝ danh mục (kèm `soLanDaCham`, thấy cả tiêu chí đã
        // ngừng dùng) — cấp `Xem` cho học viên là mở cả màn quản trị HRM cho họ.
        //
        // Trước 18/09 học viên gọi endpoint này nhận 403, frontend `catch` trả rỗng rồi âm thầm
        // rơi về chấm sao — người dùng báo "chưa thay bằng tiêu chí" chính là vì chỗ này.
        [TieuChiDanhGia] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.TuLam],
        // `Cham` = ghi phiếu đánh giá nhân viên kinh doanh theo kỳ.
        [ThongKeNhanSu] = [HanhDong.Xem, HanhDong.Cham],
        [KhachHang] = Crud,
        [ChamSocKhachHang] = Crud,
        // KHÔNG tách `CauHinhTien` ở đây dù giá là dữ liệu tiền: `LuuKhoaHocCommand` ghi tên,
        // ghi chú và giá trong MỘT lệnh, nên attribute không tách được hai việc đó. Muốn tách
        // thật thì phải tách lệnh trước — ghi vào nợ kỹ thuật, đừng khai một ô không gác gì.
        [KhoaHoc] = Crud,
        [SanPham] = Crud,

        // ---------- LMS ----------
        [LopHoc] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa,
                    HanhDong.HoanTat, HanhDong.Huy, HanhDong.SinhLich],
        // Ghi danh không có `Sua`: đổi học phí áp dụng là gỡ rồi thêm lại để còn dấu vết.
        [GhiDanhLop] = [HanhDong.Xem, HanhDong.Them, HanhDong.Xoa],
        [XepLop] = [HanhDong.Xem, HanhDong.Duyet],
        [BuoiHoc] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa, HanhDong.Huy],
        // `DiemDanh` không có `Them`/`Xoa`: bản ghi điểm danh sinh ra cùng buổi học, và xoá
        // điểm danh không phải nghiệp vụ — sai thì ghi lại.
        [DiemDanh] = [HanhDong.Xem, HanhDong.Sua, HanhDong.Chot, HanhDong.TuLam],
        // `Xem` = đọc nhận xét của MỌI người trong buổi (quyền giáo viên).
        // `TuLam` = gửi và đọc lại nhận xét của CHÍNH MÌNH (quyền học viên).
        // Cấp `Xem` cho học viên là cho họ đọc phản hồi của bạn cùng lớp.
        [NhanXetBuoiHoc] = [HanhDong.Xem, HanhDong.TuLam],
        [BaiTap] = Crud,
        [BaiNopBaiTap] = [HanhDong.Xem, HanhDong.TuLam, HanhDong.Cham],
        // `Xem` ở đây KHÔNG gác endpoint nào (endpoint đọc gác bằng `HocOnline.Xem` để học
        // viên đọc được mà không soạn được) — nó là quyền PHẠM VI: `PhamViKhoaOnline.LocKhoa`
        // đọc nó để quyết định thấy MỌI khoá (kể cả nháp của người khác) hay chỉ khoá mình
        // được ghi danh. Bỏ nó đi thì không ai là người soạn nội dung nữa, và khoá vừa tạo
        // biến mất khỏi màn của chính người tạo (E2E `khoa-online.spec.ts` bắt được).
        [KhoaOnline] = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa],
        [GhiDanhKhoaOnline] = Crud,
        [HocOnline] = [HanhDong.Xem, HanhDong.TuLam],
        [TaiLieu] = Crud,
        [HocPhi] = [HanhDong.Xem, HanhDong.ThuTien, HanhDong.Sua, HanhDong.Xoa],

        // Chưa có API — giữ hằng để seeder cũ không vỡ, nhưng KHÔNG hiện trên màn phân quyền.
        [BaiKiemTra] = [],
        [BaiLamKiemTra] = [],
        [ThongKe] = [],

        // Quyền PHẠM VI, không phải quyền gọi endpoint — xem `PhamViDuLieu`.
        [LopHocToanTrungTam] = [HanhDong.Xem, HanhDong.Sua]
    };

    /// <summary>
    /// Thao tác áp dụng cho một chức năng. Rỗng = chức năng chưa có API, không hiện trên màn
    /// phân quyền.
    /// </summary>
    public static IReadOnlyList<HanhDong> ThaoTacCua(string chucNang) =>
        ThaoTacTheoChucNang.TryGetValue(chucNang, out var ds) ? ds : Crud;

    /// <summary>
    /// Cặp (chức năng, thao tác) **cần cân nhắc trước khi cấp** — màn phân quyền đánh dấu
    /// riêng và hiện mô tả hệ quả.
    ///
    /// Tiêu chí vào danh sách này, không phải "cảm giác nguy hiểm":
    /// 1. **Không đảo ngược được** — `DiemDanh.Chot` khoá sổ và sinh "Vắng mặc định";
    ///    `XepLop.Duyet` ảnh hưởng tới bên bán.
    /// 2. **Leo thang đặc quyền** — `PhanQuyen.CauHinhQuyen` cho phép tự cấp mọi quyền khác.
    /// 3. **Dính tới tiền** — `DoanhThu.ThuTien`, `HocPhi.ThuTien`, `ThietLapChung.CauHinhTien`.
    /// 4. **Mở rộng phạm vi dữ liệu** — `LopHocToanTrungTam.*` (xem <see cref="PhamViDuLieu"/>).
    ///
    /// Cố ý KHÔNG gồm `Xoa` chung chung: xoá một bài tập không cùng hạng với chốt sổ điểm danh,
    /// và đánh dấu mọi ô `Xoa` sẽ làm dấu hiệu này mất giá trị (cảnh báo đại trà = không ai đọc).
    ///
    /// Đây chỉ là **dấu hiệu cho người cấu hình**, không phải tầng bảo vệ: backend vẫn gác
    /// bằng `[RequirePermission]` như mọi ô khác.
    /// </summary>
    public static readonly IReadOnlyList<(string ChucNang, HanhDong HanhDong)> CanCanNhac =
    [
        // Không đảo ngược được
        (DiemDanh, HanhDong.Chot),
        (XepLop, HanhDong.Duyet),
        (LopHoc, HanhDong.HoanTat),
        (LopHoc, HanhDong.Huy),
        (BuoiHoc, HanhDong.Huy),
        // Leo thang đặc quyền
        (PhanQuyen, HanhDong.CauHinhQuyen),
        (DoiMatKhauNguoiKhac, HanhDong.Sua),
        // Dính tới tiền
        (DoanhThu, HanhDong.ThuTien),
        (HocPhi, HanhDong.ThuTien),
        (ThietLapChung, HanhDong.CauHinhTien),
        // Mở rộng phạm vi dữ liệu
        (LopHocToanTrungTam, HanhDong.Xem),
        (LopHocToanTrungTam, HanhDong.Sua),
        // Đọc dữ liệu cá nhân của người khác — xem chú thích cặp Xem/TuLam
        (NhanXetBuoiHoc, HanhDong.Xem),
    ];

    /// <summary>
    /// Chức năng thuộc loại **quyền phạm vi dữ liệu**, không phải quyền gọi endpoint.
    ///
    /// Khác biệt quan trọng với mọi ô còn lại: `[RequirePermission]` không bao giờ đọc chúng.
    /// Chúng được `IPhamViLopHoc` / `IPhamViHocVien` đọc để quyết định **thấy bao nhiêu hàng**
    /// — "lớp mình dạy" hay "toàn trung tâm". Cấp thiếu thì người dùng vào được màn nhưng thấy
    /// danh sách rỗng; cấp thừa thì rò rỉ dữ liệu (đúng nợ N14).
    ///
    /// Màn phân quyền hiện chúng thành nhóm riêng để người cấu hình không nhầm với quyền gọi.
    /// </summary>
    public static readonly IReadOnlyList<string> PhamViDuLieu = [LopHocToanTrungTam];

    /// <summary>
    /// Chức năng quản trị dùng chung cho cả ba hệ thống — hiện ở sidebar của hệ thống nào cũng
    /// được, miễn người dùng có quyền.
    /// </summary>
    public static readonly IReadOnlyList<string> DungChung =
    [
        TaiKhoan, HoSoNguoiDung, PhanQuyen, ThietLapChung, Anh, DoiMatKhauNguoiKhac,
        NhatKyHeThong
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

    /// <summary>
    /// Cặp (chức năng, thao tác) **không mở lối vào hệ thống** của chức năng đó (18/09/2026).
    ///
    /// Có những thao tác thuộc một hệ thống nhưng người giữ nó KHÔNG làm việc trong hệ thống
    /// ấy — họ chỉ cần một mẩu dữ liệu của nó để làm việc của mình ở nơi khác.
    ///
    /// Ca đầu tiên: `TieuChiDanhGia.TuLam` = ĐỌC danh mục tiêu chí để tự đi chấm.
    /// `TieuChiDanhGia` thuộc HRM (module cấu hình nằm ở đó), nhưng học viên giữ `TuLam` chỉ để
    /// chấm giáo viên **trên phiếu nhận xét buổi học ở LMS**. Tính nó là "vào được HRM" thì học
    /// viên bị đưa vào sidebar nhân sự và **mất luôn menu Lớp học** — đã xảy ra thật, E2E
    /// `doi-nick-khong-giu-quyen-cu` bắt được: học viên chỉ còn thấy "Tổng quan".
    ///
    /// Cùng tinh thần với <see cref="DungChung"/>: ở đó là "chức năng không thuộc hệ thống
    /// nào", ở đây là "thao tác không mở lối vào hệ thống của chính nó".
    /// </summary>
    private static readonly HashSet<(string ChucNang, HanhDong HanhDong)> KhongMoLoiVao =
    [
        (TieuChiDanhGia, HanhDong.TuLam)
    ];

    /// <summary>
    /// Cặp quyền này có mở lối vào hệ thống con không — dùng cho bộ chuyển HRM/CRM/LMS.
    ///
    /// Đặt ở `Domain` để API và màn phân quyền hỏi cùng một chỗ; suy ở tầng API sẽ sinh ra bản
    /// sao thứ hai rồi hai bên trôi khỏi nhau.
    /// </summary>
    public static bool MoLoiVaoHeThong(string chucNang, HanhDong hanhDong) =>
        !KhongMoLoiVao.Contains((chucNang, hanhDong));
}
