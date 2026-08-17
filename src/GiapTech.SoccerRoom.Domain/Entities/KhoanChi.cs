using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// KHOAN_CHI — khoản tiền chi ra từ quỹ đội (thuê sân, nước, trọng tài, áo đấu).
///
/// Ngoài phạm vi FR-15/16 (vốn chỉ nói tới thu quỹ) nhưng cần thiết: thủ quỹ thu tiền vào mà
/// không ghi được tiền ra thì con số "đã thu" không nói lên quỹ còn bao nhiêu.
///
/// <see cref="QuyId"/> **nullable**: chi phí có thể thuộc một đợt quỹ ("Quỹ tháng 3 chi thuê
/// sân") hoặc chi chung của CLB không gắn đợt nào. Bắt buộc gắn đợt sẽ khiến thủ quỹ tạo đợt
/// quỹ giả chỉ để ghi một khoản chi.
/// </summary>
public class KhoanChi : TenantEntity
{
    public Guid? QuyId { get; set; }
    public Quy? Quy { get; set; }

    public string NoiDung { get; set; } = null!;

    /// <summary>Số tiền chi. Luôn dương — chiều tiền đã nằm ở việc đây là bảng KHOAN_CHI.</summary>
    public decimal SoTien { get; set; }

    public DateOnly NgayChi { get; set; }

    /// <summary>Ai chi (thường là thủ quỹ ứng trước). Ghi tên tự do vì có thể không phải cầu thủ.</summary>
    public string? NguoiChi { get; set; }

    public string? GhiChu { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
