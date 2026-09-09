using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

/// <summary>
/// KHACH_HANG — người quan tâm tới khoá học (FR-17).
///
/// **Bảng riêng, không dùng `NGUOI_DUNG`.** Khách hàng chưa chắc thành học viên: nhồi họ vào
/// `NGUOI_DUNG` thì danh sách học viên bên LMS lẫn người chưa học, và mọi cột của `NGUOI_DUNG`
/// (`trang_thai_nhan_su`, `loai_nguoi_dung`, ba bảng hồ sơ vai trò) đều vô nghĩa với một người
/// mới để lại số điện thoại.
/// </summary>
public class KhachHang : TenantEntity
{
    public string HoTen { get; set; } = null!;

    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }

    /// <summary>Link Facebook — kênh liên hệ chính của phần lớn khách ở thị trường này.</summary>
    public string? LinkFacebook { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Hình thức thanh toán mặc định của khách này — từng đăng ký vẫn ghi riêng.</summary>
    public PhuongThucThanhToan PhuongThucThanhToan { get; set; } = PhuongThucThanhToan.ChuyenKhoan;

    /// <summary>
    /// Trỏ tới hồ sơ học viên khi khách đã THẬT SỰ vào học; `null` = chưa vào học.
    ///
    /// Nối bằng khoá ngoại chứ **không copy** họ tên/email sang `NGUOI_DUNG`: copy thì hai bên
    /// trôi khỏi nhau và không biết bên nào đúng — đúng lỗi hai nguồn sự thật đã gặp với
    /// tài khoản/người dùng (07/09/2026).
    /// </summary>
    public Guid? NguoiDungId { get; set; }
    public NguoiDung? NguoiDung { get; set; }

    public ICollection<DangKyKhoaHoc> DangKys { get; set; } = [];

    public ICollection<LichSuChamSoc> LichSuChamSocs { get; set; } = [];
}

/// <summary>
/// LICH_SU_CHAM_SOC — từng lần liên hệ với khách (FR-17).
///
/// `TrangThaiSau` của dòng MỚI NHẤT chính là trạng thái hiện tại của khách trong phễu bán hàng
/// — xem <see cref="Enums.TrangThaiKhachHang"/> về việc vì sao không lưu thành cột riêng.
/// </summary>
public class LichSuChamSoc : TenantEntity
{
    public Guid KhachHangId { get; set; }
    public KhachHang KhachHang { get; set; } = null!;

    public DateTimeOffset ThoiDiem { get; set; }

    public HinhThucChamSoc HinhThuc { get; set; } = HinhThucChamSoc.GoiDien;

    public string NoiDung { get; set; } = null!;

    /// <summary>Trạng thái khách SAU lần liên hệ này — nguồn của phễu bán hàng.</summary>
    public TrangThaiKhachHang TrangThaiSau { get; set; } = TrangThaiKhachHang.DangTuVan;

    /// <summary>
    /// Người thực hiện — lấy từ TOKEN, không nhận từ client (không có tham số để ghi hộ).
    ///
    /// SetNull: xoá hồ sơ nhân sự không được cuốn theo lịch sử chăm sóc; dòng cũ vẫn còn nội
    /// dung, chỉ mất tên người phụ trách.
    /// </summary>
    public Guid? NguoiPhuTrachId { get; set; }
    public NguoiDung? NguoiPhuTrach { get; set; }
}

/// <summary>
/// THU_TIEN_DANG_KY — một lần khách đóng tiền cho một đăng ký (FR-18).
///
/// Tách khỏi `DANG_KY_KHOA_HOC` vì hai thứ khác nhau (chốt 08/09/2026):
/// **đăng ký là CAM KẾT**, còn đây là **tiền thật đã nhận** — khách thường đóng nhiều đợt.
/// Còn thiếu = cam kết − tổng đã thu, **tính động, không lưu cột**.
/// </summary>
public class ThuTienDangKy : TenantEntity
{
    public Guid DangKyId { get; set; }
    public DangKyKhoaHoc DangKy { get; set; } = null!;

    /// <summary>Ghi cùng ĐƠN VỊ TIỀN của đăng ký — lẫn đơn vị thì phép trừ "còn thiếu" vô nghĩa.</summary>
    public decimal SoTien { get; set; }

    public DateTimeOffset NgayThu { get; set; }

    public PhuongThucThanhToan PhuongThuc { get; set; } = PhuongThucThanhToan.ChuyenKhoan;

    public string? GhiChu { get; set; }

    public Guid? NguoiThuId { get; set; }
    public NguoiDung? NguoiThu { get; set; }
}

/// <summary>
/// KHOA_HOC — danh mục SẢN PHẨM bán ra (FR-19).
///
/// Khác <see cref="LopHoc"/>: khoá học là sản phẩm bán đi bán lại, lớp học là một **lần mở** cụ
/// thể có giáo viên và lịch. Gộp hai thứ thì không bán được trước khi mở lớp, và mỗi lần mở lại
/// phải khai giá lại.
/// </summary>
public class KhoaHoc : TenantEntity
{
    public string Ten { get; set; } = null!;

    public string? GhiChu { get; set; }

    /// <summary>Giá niêm yết. Đăng ký **chụp lại** giá này chứ không đọc động.</summary>
    public decimal GiaTien { get; set; }

    public DonViTien DonViTien { get; set; } = DonViTien.VND;

    /// <summary>Số buổi NIÊM YẾT — không ràng buộc số buổi lớp thật sinh ra.</summary>
    public int SoBuoi { get; set; }

    /// <summary>false = ngừng bán. Không xoá khoá đã bán — đơn cũ phải giữ được tên khoá.</summary>
    public bool DangBan { get; set; } = true;

    public ICollection<DangKyKhoaHoc> DangKys { get; set; } = [];
}

/// <summary>
/// SAN_PHAM — vật phẩm bán kèm: sách, học cụ, đồng phục… (FR-20).
///
/// Bảng RIÊNG chứ không gộp vào <see cref="KhoaHoc"/> kèm cột `loai`: gộp thì `so_buoi` luôn
/// NULL cho sách, và mọi query khoá học phải nhớ `WHERE loai = ...` — quên một lần là sách lọt
/// vào danh sách khoá. Cùng lý do `LOP_HOC_HOC_VIEN` và `LOP_HOC_TRO_GIANG` là hai bảng.
/// </summary>
public class SanPham : TenantEntity
{
    public string Ten { get; set; } = null!;

    public string? GhiChu { get; set; }

    /// <summary>Giá niêm yết cho MỘT đơn vị. Đơn hàng chụp lại giá này.</summary>
    public decimal GiaTien { get; set; }

    public DonViTien DonViTien { get; set; } = DonViTien.VND;

    /// <summary>Đơn vị tính hiển thị: "quyển", "bộ", "cái"… Chỉ để đọc, không tính toán.</summary>
    public string? DonViTinh { get; set; }

    public bool DangBan { get; set; } = true;

    public ICollection<DangKyKhoaHoc> DonHangs { get; set; } = [];
}

/// <summary>
/// DANG_KY_KHOA_HOC — một **đơn hàng**: khách mua một khoá học HOẶC một sản phẩm (FR-18/FR-20).
///
/// Tên bảng giữ nguyên dù nay chứa cả sản phẩm: đổi tên bảng đang có dữ liệu là việc rủi ro
/// (EF dễ sinh drop-and-recreate), mà lợi ích chỉ là cái tên đẹp hơn. Đọc `DangKyKhoaHoc` theo
/// nghĩa "đơn hàng" — ghi vào nợ kỹ thuật để đổi tên khi nào có dịp migration lớn.
///
/// **Hai FK nullable loại trừ nhau** (`KhoaHocId` / `SanPhamId`), ràng buộc bằng `CHECK` ở tầng
/// DB chứ không chỉ validate ở handler — cùng khuôn `TEP_DINH_KEM` đã dùng cho 5 loại đính kèm.
///
/// Một khách mua nhiều thứ → nhiều dòng. **Không gộp** dòng của cùng một khách: mỗi lần mua là
/// một sự kiện doanh thu riêng, có ngày và mức giá riêng.
/// </summary>
public class DangKyKhoaHoc : TenantEntity
{
    public Guid KhachHangId { get; set; }
    public KhachHang KhachHang { get; set; } = null!;

    /// <summary>Mua KHOÁ HỌC — null nếu đơn này mua sản phẩm.</summary>
    public Guid? KhoaHocId { get; set; }
    public KhoaHoc? KhoaHoc { get; set; }

    /// <summary>Mua SẢN PHẨM — null nếu đơn này mua khoá học.</summary>
    public Guid? SanPhamId { get; set; }
    public SanPham? SanPham { get; set; }

    /// <summary>
    /// Số lượng. Khoá học luôn là 1 (không ai mua 2 suất cùng khoá trong một đơn); sản phẩm thì
    /// mua 3 quyển sách là 1 dòng `SoLuong = 3`.
    ///
    /// `GiaGoc` và `SoTien` là **tổng của cả dòng**, không phải đơn giá — nhờ vậy mọi phép cộng
    /// doanh thu không phải nhân thêm, và người bán sửa được tổng khi có giảm giá theo lô.
    /// </summary>
    public int SoLuong { get; set; } = 1;

    /// <summary>Loại đơn — suy từ FK nào có giá trị, không lưu cột để không có gì phải đồng bộ.</summary>
    public LoaiDonHang Loai => KhoaHocId is not null ? LoaiDonHang.KhoaHoc : LoaiDonHang.SanPham;

    /// <summary>
    /// Giá niêm yết của khoá **lúc đăng ký** — snapshot, không đọc động từ `KHOA_HOC`.
    ///
    /// Trung tâm tăng giá khoá thì đơn hàng tháng trước không được đổi theo. Cùng lý do
    /// `LOP_HOC_HOC_VIEN.HocPhiApDung` là snapshot.
    /// </summary>
    public decimal GiaGoc { get; set; }

    /// <summary>Giá thực thu — người bán sửa được (miễn giảm, khuyến mãi).</summary>
    public decimal SoTien { get; set; }

    public DonViTien DonViTien { get; set; } = DonViTien.VND;

    /// <summary>
    /// Tỷ giá về VND **lúc đăng ký**. Đơn vị VND thì bằng 1.
    ///
    /// Chụp lại chứ không quy đổi động: doanh thu tháng 9 xem hôm nay và xem tuần sau phải ra
    /// **cùng một số**. Quy đổi động thì mỗi lần tỷ giá biến động là mọi báo cáo quá khứ đổi
    /// theo — kế toán không dùng được.
    /// </summary>
    public decimal TyGiaVeVnd { get; set; } = 1m;

    public DateTimeOffset NgayDangKy { get; set; }

    public PhuongThucThanhToan PhuongThuc { get; set; } = PhuongThucThanhToan.ChuyenKhoan;

    public string? GhiChu { get; set; }

    /// <summary>
    /// Quy về VND để tổng hợp doanh thu. Tính từ hai cột đã chụp nên **không đổi theo thời
    /// gian** — và không lưu thành cột thứ ba để không có gì phải đồng bộ.
    /// </summary>
    public decimal QuyDoiVnd => SoTien * TyGiaVeVnd;

    /// <summary>
    /// Phần trăm trên giá gốc — **tính động, không lưu cột**. Lưu là mở cửa cho lệch khi ai đó
    /// sửa `SoTien` hoặc `GiaGoc`; cùng nguyên tắc với công nợ học phí (FR-14).
    ///
    /// `GiaGoc = 0` → `null` (không chia cho 0), UI hiện dấu gạch.
    /// </summary>
    public decimal? PhanTramTrenGiaGoc => GiaGoc == 0 ? null : SoTien / GiaGoc * 100m;

    public ICollection<ThuTienDangKy> CacLanThu { get; set; } = [];

    /// <summary>
    /// Các lần gửi yêu cầu xếp lớp — chỉ đơn mua KHOÁ HỌC mới có.
    ///
    /// NHIỀU lần, không phải một: bị từ chối thì người bán gửi lại, và lịch sử mua hàng phải
    /// hiện đủ số lần kèm trạng thái từng lần. Chỉ đúng một lần được ở trạng thái `DangCho`
    /// (partial unique index).
    /// </summary>
    public ICollection<YeuCauXepLop> CacYeuCauXepLop { get; set; } = [];
}

/// <summary>
/// YEU_CAU_XEP_LOP — cầu nối CRM → LMS (FR-21).
///
/// Sale bán một khoá học rồi **gửi yêu cầu tạo lớp**; bên đào tạo thấy học viên trong danh sách
/// chờ và xếp vào lớp.
///
/// **Bảng riêng chứ không phải một cột `trang_thai_xep_lop` trên đơn hàng** (chốt 09/09/2026):
/// bảng giữ được **vết ai gửi, ai duyệt, lúc nào** — thông tin mà một cột trạng thái làm mất.
/// Với dữ liệu tiền và bàn giao giữa hai bộ phận, biết ai chịu trách nhiệm là điều đáng một bảng.
///
/// `UNIQUE(DangKyId)`: một đơn hàng chỉ có một yêu cầu. Khách mua lại cùng khoá (học lại) là một
/// **đơn khác**, nên vẫn gửi được yêu cầu mới — chặn ở đây không cản việc đó.
/// </summary>
public class YeuCauXepLop : TenantEntity
{
    /// <summary>Đơn hàng khoá học sinh ra yêu cầu này.</summary>
    public Guid DangKyId { get; set; }
    public DangKyKhoaHoc DangKy { get; set; } = null!;

    /// <summary>
    /// Hồ sơ học viên sẽ được xếp lớp.
    ///
    /// Bắt buộc (không nullable): lúc gửi yêu cầu, nếu khách chưa có hồ sơ thì handler **tự tạo**
    /// từ dữ liệu khách. Để nullable thì danh sách chờ có dòng không xếp được vào đâu.
    /// </summary>
    public Guid HocVienId { get; set; }
    public NguoiDung HocVien { get; set; } = null!;

    public TrangThaiYeuCauXepLop TrangThai { get; set; } = TrangThaiYeuCauXepLop.DangCho;

    /// <summary>
    /// Lần gửi thứ mấy cho **cùng một đơn** — 1, 2, 3…
    ///
    /// Lưu thành cột thay vì đếm động (`COUNT(*)` theo `dang_ky_id`) vì đây là **số thứ tự của
    /// chính dòng này**, không phải một con số tổng hợp: xoá dòng giữa thì các lần sau không
    /// được đánh số lại, "lần 3" phải mãi là lần 3. `UNIQUE(dang_ky_id, lan_gui)` cũng cần cột
    /// này mới ép được.
    /// </summary>
    public int LanGui { get; set; } = 1;

    public DateTimeOffset ThoiDiemGui { get; set; }

    /// <summary>SetNull: xoá hồ sơ người gửi không được cuốn theo yêu cầu.</summary>
    public Guid? NguoiGuiId { get; set; }
    public NguoiDung? NguoiGui { get; set; }

    /// <summary>Lớp đã xếp — null khi còn `DangCho`.</summary>
    public Guid? LopHocId { get; set; }
    public LopHoc? LopHoc { get; set; }

    /// <summary>
    /// Thời điểm bên đào tạo **xử lý** — duyệt hoặc từ chối. null = còn `DangCho`.
    ///
    /// Một cột cho cả hai: hai cột `thoi_diem_xep`/`thoi_diem_tu_choi` thì luôn có đúng một cột
    /// NULL, và mọi chỗ sắp xếp theo "lúc nào xong" phải viết `COALESCE`.
    /// </summary>
    public DateTimeOffset? ThoiDiemXuLy { get; set; }

    /// <summary>Người **xử lý** — duyệt hoặc từ chối, không chỉ duyệt.</summary>
    public Guid? NguoiDuyetId { get; set; }
    public NguoiDung? NguoiDuyet { get; set; }

    /// <summary>Ghi chú của người GỬI: bối cảnh cho bên đào tạo (trình độ, nguyện vọng giờ học…).</summary>
    public string? GhiChu { get; set; }

    /// <summary>
    /// Lý do bên đào tạo từ chối — **bắt buộc** khi `TrangThai = TuChoi` (validator ép).
    ///
    /// Không ép bằng CHECK constraint ở DB: điều kiện phụ thuộc trạng thái, và
    /// `ThoiDiemXuLy`/`NguoiDuyetId` cũng đã theo cùng quy ước "nullable, do handler đặt".
    /// Từ chối mà không nói vì sao thì người bán phải đi hỏi bằng miệng — đúng thứ hệ thống
    /// này định thay thế.
    /// </summary>
    public string? LyDoTuChoi { get; set; }
}
