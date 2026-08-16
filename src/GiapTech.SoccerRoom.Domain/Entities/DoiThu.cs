using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>DOI_THU — đội đối thủ trong sổ của CLB.</summary>
public class DoiThu : TenantEntity
{
    public string TenDoi { get; set; } = null!;
    public string? LienHe { get; set; }
    public string? GhiChu { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<TranDau> TranDaus { get; set; } = [];
    public ICollection<LoiMoiDoiThu> LoiMois { get; set; } = [];
}

/// <summary>
/// LOI_MOI_DOI_THU — lời mời giao hữu (FR-09).
/// Chấp nhận → sinh TRAN_DAU mới trạng thái "đã lên lịch"; từ chối → đóng lời mời.
/// </summary>
public class LoiMoiDoiThu : TenantEntity
{
    public Guid DoiThuId { get; set; }
    public DoiThu DoiThu { get; set; } = null!;

    public DateTimeOffset ThoiGianDeXuat { get; set; }
    public TrangThaiLoiMoi TrangThai { get; set; } = TrangThaiLoiMoi.ChoPhanHoi;
    public string? GhiChu { get; set; }

    /// <summary>Trận được sinh ra khi chấp nhận — giữ vết để không tạo trùng.</summary>
    public Guid? TranDauId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
