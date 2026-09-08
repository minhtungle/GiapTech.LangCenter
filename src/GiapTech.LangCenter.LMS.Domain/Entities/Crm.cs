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
/// DANG_KY_KHOA_HOC — một khách mua một khoá ở một mức giá (FR-18). Nguồn của doanh thu.
///
/// Một khách đăng ký nhiều khoá → nhiều dòng.
/// </summary>
public class DangKyKhoaHoc : TenantEntity
{
    public Guid KhachHangId { get; set; }
    public KhachHang KhachHang { get; set; } = null!;

    public Guid KhoaHocId { get; set; }
    public KhoaHoc KhoaHoc { get; set; } = null!;

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
}
