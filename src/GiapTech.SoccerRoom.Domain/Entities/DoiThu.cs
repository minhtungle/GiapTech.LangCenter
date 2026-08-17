using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>DOI_THU — đội đối thủ trong sổ của CLB.</summary>
public class DoiThu : TenantEntity
{
    public string TenDoi { get; set; } = null!;
    public string? LienHe { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>
    /// Mã đội của CLB đối thủ nếu họ CŨNG dùng hệ thống này. Null khi đội chỉ tồn tại trong
    /// sổ của ta (nhập tay).
    ///
    /// Lưu **mã đội** chứ không phải FK tới TENANT: FK sẽ cho phép join xuyên tenant và biến
    /// một truy vấn vô tình thành rò rỉ dữ liệu chéo CLB (quy tắc #2). Mã đội chỉ là chuỗi 7
    /// ký tự để hiển thị và tra lại khi cần, không mở đường đọc dữ liệu của họ.
    ///
    /// Cũng không dùng FK vì CLB kia có thể xoá tài khoản — lịch sử đối đầu của ta phải giữ
    /// nguyên, không bị Cascade theo.
    /// </summary>
    public string? MaDoiHeThong { get; set; }

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
