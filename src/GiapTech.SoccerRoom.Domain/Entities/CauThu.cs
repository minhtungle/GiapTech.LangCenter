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

    public Tenant Tenant { get; set; } = null!;

    public ICollection<DoiHinhTranDau> DoiHinhs { get; set; } = [];
    public ICollection<DanhGiaCauThu> DanhGias { get; set; } = [];
    public ICollection<DongGopQuy> DongGops { get; set; } = [];
}
