using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// LOI_MOI_THAM_GIA — trưởng nhóm mời cầu thủ đăng ký đá một trận.
///
/// Mỗi trận **một lời mời**, gửi tới nhiều cầu thủ; từng người trả lời riêng trong
/// <see cref="PhanHoiThamGia"/>. Mô hình này thay cho việc trưởng nhóm gọi điện từng người
/// rồi tự ghi ra giấy ai đá được.
///
/// Tách khỏi <see cref="DoiHinhTranDau"/>: đăng ký là **ý định** của cầu thủ, đội hình là
/// **quyết định** của trưởng nhóm. Mười lăm người đăng ký nhưng chỉ mười một người được xếp —
/// gộp hai thứ vào một bảng thì mất thông tin ai đã sẵn sàng mà không được chọn.
/// </summary>
public class LoiMoiThamGia : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    /// <summary>Tài khoản trưởng nhóm đã gửi lời mời.</summary>
    public Guid NguoiGuiId { get; set; }

    public string? LoiNhan { get; set; }

    /// <summary>Hạn trả lời. Null = không đặt hạn.</summary>
    public DateTimeOffset? HanTraLoi { get; set; }

    /// <summary>Trưởng nhóm đóng lời mời khi đã chốt đội hình — không nhận trả lời mới nữa.</summary>
    public bool DaDong { get; set; }

    public ICollection<PhanHoiThamGia> PhanHois { get; set; } = [];

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// PHAN_HOI_THAM_GIA — câu trả lời của một cầu thủ cho lời mời.
///
/// Tạo sẵn một hàng "chưa trả lời" cho mọi cầu thủ ngay khi gửi lời mời, thay vì chỉ tạo khi
/// có người bấm: trưởng nhóm cần thấy **ai chưa trả lời** chứ không chỉ ai đã đồng ý.
///
/// <c>UNIQUE(loi_moi_id, cau_thu_id)</c> ở tầng DB — hai request đồng thời vẫn lọt qua kiểm
/// tra ở tầng ứng dụng, cùng lý do với ràng buộc vote MVP (quy tắc #8).
/// </summary>
public class PhanHoiThamGia : TenantEntity
{
    public Guid LoiMoiId { get; set; }
    public LoiMoiThamGia LoiMoi { get; set; } = null!;

    public Guid CauThuId { get; set; }
    public CauThu CauThu { get; set; } = null!;

    public TraLoiThamGia TraLoi { get; set; } = TraLoiThamGia.ChuaTraLoi;

    public string? GhiChu { get; set; }

    /// <summary>Thời điểm trả lời. Null khi chưa trả lời — không dùng NgayCapNhat vì hàng
    /// được tạo sẵn lúc gửi lời mời, NgayCapNhat sẽ hiểu nhầm thành "đã trả lời".</summary>
    public DateTimeOffset? ThoiGianTraLoi { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
