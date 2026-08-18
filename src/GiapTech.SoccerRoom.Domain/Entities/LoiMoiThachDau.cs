using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// LOI_MOI_BAT_DOI — CLB này mời CLB kia đá giao hữu, gửi qua Cộng đồng.
///
/// **Bảng DUY NHẤT trong hệ thống thuộc về HAI tenant cùng lúc.** Mọi bảng nghiệp vụ khác kế
/// thừa <see cref="TenantEntity"/> và bị Global Query Filter lọc theo một tenant; bảng này
/// không thể — một lời mời phải hiện ở hòm thư của **cả người gửi lẫn người nhận**.
///
/// Vì thế nó KHÔNG kế thừa TenantEntity mà mang hai khoá tường minh, và mọi truy vấn phải tự
/// lọc bằng <c>TenantGuiId == x || TenantNhanId == x</c>. Quên mệnh đề đó là rò rỉ dữ liệu
/// chéo CLB — xem `LoiMoiThachDauTests`, có test canh đúng chuyện này.
///
/// Không dùng <see cref="LoiMoiThamGia"/> (mời cầu thủ đăng ký) hay `DoiThu`+`TrangThaiLoiMoi`
/// (giao hữu ghi tay trong sổ riêng): cả hai đều nằm gọn trong một tenant. Lời mời thách đấu là
/// thứ khác hẳn — nó bắc cầu giữa hai CLB độc lập.
/// </summary>
public class LoiMoiThachDau : BaseEntity
{
    /// <summary>CLB gửi lời mời.</summary>
    public Guid TenantGuiId { get; set; }
    public Tenant TenantGui { get; set; } = null!;

    /// <summary>CLB nhận lời mời.</summary>
    public Guid TenantNhanId { get; set; }
    public Tenant TenantNhan { get; set; } = null!;

    /// <summary>Thời gian đề xuất đá. Null = "khi nào tiện thì báo".</summary>
    public DateTimeOffset? ThoiGianDeXuat { get; set; }

    public string? DiaDiem { get; set; }

    /// <summary>Lời nhắn kèm theo — nơi hai bên trao đổi trước khi chốt.</summary>
    public string? LoiNhan { get; set; }

    public TrangThaiLoiMoi TrangThai { get; set; } = TrangThaiLoiMoi.ChoPhanHoi;

    /// <summary>
    /// Người nhận trả lời gì. Tách khỏi <see cref="LoiNhan"/> để không ghi đè lời nhắn gốc —
    /// mất nó thì đọc lại lịch sử không hiểu bên kia đã đề nghị gì.
    /// </summary>
    public string? PhanHoi { get; set; }

    public DateTimeOffset? ThoiGianPhanHoi { get; set; }

    /// <summary>
    /// Trận được tạo khi bên nhận đồng ý. Null cho tới lúc đó.
    ///
    /// Chỉ trỏ tới trận **của bên nhận**: mỗi CLB tự quản lịch của mình, nên đồng ý sẽ tạo
    /// hai trận độc lập ở hai bên chứ không phải một trận dùng chung. Trận dùng chung sẽ buộc
    /// một trong hai CLB sửa dữ liệu nằm trong tenant của CLB kia.
    /// </summary>
    public Guid? TranDauNhanId { get; set; }

    /// <summary>Trận tương ứng bên người gửi.</summary>
    public Guid? TranDauGuiId { get; set; }
}
