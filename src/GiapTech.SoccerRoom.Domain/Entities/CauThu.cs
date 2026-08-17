using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// CAU_THU — hồ sơ cầu thủ (FR-04).
///
/// Độc lập hoàn toàn với tài khoản đăng nhập: một cầu thủ có thể chưa có tài khoản
/// nhưng vẫn nằm trong đội hình, được đánh giá và có tên trong danh sách đóng quỹ.
/// </summary>
public class CauThu : TenantEntity
{
    public string HoTen { get; set; } = null!;
    public string? AnhDaiDien { get; set; }
    public DateOnly? NgaySinh { get; set; }
    public DateOnly? NgayThamGia { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>
    /// Số áo cố định của cầu thủ. Null khi chưa đặt.
    ///
    /// Nguồn mặc định cho áo trên bảng chiến thuật — trước đây phải gõ tay số áo cho từng
    /// người ở từng trận. Sơ đồ vẫn ghi đè được cho trận riêng lẻ (mượn áo, trùng số).
    /// **Không đặt UNIQUE**: CLB phong trào hay trùng số, và ràng buộc cứng sẽ chặn cả việc
    /// nhập liệu bình thường.
    /// </summary>
    public int? SoAo { get; set; }

    /// <summary>
    /// Vị trí sở trường (GK, CB, ST…). Null khi chưa đặt.
    /// Dùng làm gợi ý khi xếp sơ đồ, không ràng buộc cầu thủ chỉ được đá vị trí này.
    /// </summary>
    public string? ViTriSoTruong { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<DoiHinhTranDau> DoiHinhs { get; set; } = [];
    public ICollection<DanhGiaCauThu> DanhGias { get; set; } = [];
    public ICollection<DongGopQuy> DongGops { get; set; } = [];
}
