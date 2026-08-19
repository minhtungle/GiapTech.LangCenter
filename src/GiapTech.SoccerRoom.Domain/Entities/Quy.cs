using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>QUY — một đợt quỹ (FR-15, FR-16).</summary>
public class Quy : TenantEntity
{
    public string TenQuy { get; set; } = null!;
    public DateOnly? ThoiHan { get; set; }
    public string? GhiChu { get; set; }
    public TrangThaiQuy TrangThai { get; set; } = TrangThaiQuy.DangMo;

    /// <summary>
    /// Có hiện thông tin chuyển khoản của CLB trên đợt quỹ này hay không.
    ///
    /// Theo đợt chứ không phải bật/tắt toàn cục: có đợt thu tiền mặt tại sân (đóng ngay sau
    /// trận), có đợt thu chuyển khoản. Hiện QR cho đợt thu tiền mặt chỉ làm người ta chuyển
    /// khoản trong khi thủ quỹ đang đứng chờ nhận tiền tươi.
    ///
    /// Mặc định <c>false</c>: đợt quỹ cũ (tạo trước tính năng này) không tự nhiên hiện số tài
    /// khoản lên — thủ quỹ chủ động bật khi cần.
    /// </summary>
    public bool HienThongTinChuyenKhoan { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<DongGopQuy> DongGops { get; set; } = [];
}

/// <summary>
/// DONGGOP_QUY — khoản đóng của một cầu thủ trong một đợt quỹ.
///
/// Gắn với CAU_THU chứ không phải NGUOI_DUNG: cầu thủ chưa có tài khoản vẫn phải đóng quỹ.
/// Cho phép đóng từng phần — tiến độ = SoTienDaDong / SoTienCanDong.
/// </summary>
public class DongGopQuy : TenantEntity
{
    public Guid QuyId { get; set; }
    public Quy Quy { get; set; } = null!;

    public Guid CauThuId { get; set; }
    public CauThu CauThu { get; set; } = null!;

    public decimal SoTienCanDong { get; set; }
    public decimal SoTienDaDong { get; set; }

    public DateTimeOffset? NgayDong { get; set; }
    public string? GhiChu { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public bool DaDongDu => SoTienDaDong >= SoTienCanDong;

    /// <summary>
    /// Quá hạn hay chưa. Nhận thời hạn qua tham số thay vì đọc <c>Quy.ThoiHan</c> để hàm
    /// không phụ thuộc việc navigation đã được load — gọi trên entity chưa Include sẽ ném
    /// NullReference ở chỗ khó lần ra.
    /// </summary>
    public bool QuaHan(DateOnly homNay, DateOnly? thoiHan) =>
        !DaDongDu && thoiHan is { } han && homNay > han;
}
