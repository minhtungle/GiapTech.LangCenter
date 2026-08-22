using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// LOI_MOI_LINK — lời mời thách đấu gửi qua link/QR tới đối thủ **chưa liên kết** (FR-18).
///
/// Khác <see cref="LoiMoiThachDau"/> ở điểm cốt lõi: bảng đó cần CẢ HAI tenant, còn bảng này
/// chưa biết bên nhận là tenant nào — nó trỏ tới một <see cref="DoiThu"/> trong sổ của người gửi
/// (một cái tên gõ tay). Tenant bên nhận chỉ xuất hiện khi họ bấm link và chấp nhận.
///
/// Vì sao bảng riêng chứ không cho `LoiMoiThachDau.TenantNhanId` nullable: xem
/// <c>docs/kien-truc/adr/0005-loi-moi-qua-link.md</c>. Tóm lại — nullable làm mọi truy vấn hiện
/// có phải xử lý null, và ràng buộc "một lời mời đang chờ mỗi cặp CLB" mất nghĩa.
///
/// Bảng này CÓ <c>tenant_id</c> (của người GỬI) nên được Global Query Filter bảo vệ như mọi bảng
/// nghiệp vụ khác. Chỗ đọc ngoài tenant là truy vấn theo TOKEN — nó phải bỏ filter, và đó là
/// điểm cần canh chặt nhất của tính năng này.
/// </summary>
public class LoiMoiLink : TenantEntity
{
    /// <summary>
    /// Đối thủ trong sổ của người gửi — cái tên sẽ được "nâng cấp" thành CLB có ID khi chấp nhận.
    /// </summary>
    public Guid DoiThuId { get; set; }
    public DoiThu DoiThu { get; set; } = null!;

    /// <summary>
    /// Trận đã lên lịch mà lời mời này gắn vào. Null = mời làm quen, chưa có trận cụ thể.
    ///
    /// Không Cascade khi xoá trận: lời mời đã gửi ra ngoài rồi, người nhận vẫn có thể đang mở
    /// link. Xoá trận thì lời mời chuyển sang trạng thái "trận không còn" chứ không biến mất.
    /// </summary>
    public Guid? TranDauId { get; set; }
    public TranDau? TranDau { get; set; }

    /// <summary>
    /// SHA-256 của token thô. Lưu hash, không lưu token — cùng cơ chế token đặt lại mật khẩu:
    /// DB bị đọc lén thì kẻ đọc vẫn không dựng lại được link nào.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// Hạn dùng. Mặc định = ngày trận + 1, hoặc 30 ngày nếu chưa hẹn giờ.
    ///
    /// Hết hạn phải nói rõ "đã hết hiệu lực" chứ không trả 404: người nhận sẽ tưởng link sai và
    /// bỏ luôn, thay vì liên hệ lại bên mời.
    /// </summary>
    public DateTimeOffset HetHan { get; set; }

    public DateTimeOffset? ThoiGianDeXuat { get; set; }
    public string? DiaDiem { get; set; }
    public string? LoiNhan { get; set; }

    public TrangThaiLoiMoi TrangThai { get; set; } = TrangThaiLoiMoi.ChoPhanHoi;

    public string? PhanHoi { get; set; }
    public DateTimeOffset? ThoiGianPhanHoi { get; set; }

    /// <summary>
    /// Người gửi thu hồi lúc nào. Thu hồi rồi thì token vô hiệu ngay, dù chưa hết hạn.
    ///
    /// Đánh dấu chứ không xoá hàng: người nhận đang mở link cần thấy "đã được thu hồi" thay vì
    /// một trang lỗi không giải thích gì.
    /// </summary>
    public DateTimeOffset? ThuHoiLuc { get; set; }

    /// <summary>
    /// Tenant đã chấp nhận. Null cho tới lúc đó.
    ///
    /// Lưu lại để người gửi thấy AI đã nhận — link chia sẻ được nên có thể sai người, và họ cần
    /// biết để huỷ liên kết (xem <see cref="DaHuyLienKet"/>).
    /// </summary>
    public Guid? TenantNhanId { get; set; }

    /// <summary>
    /// Người gửi đã huỷ liên kết vì sai người nhận.
    ///
    /// Không xoá hàng và không xoá trận: huỷ liên kết chỉ gỡ `MaDoiHeThong` khỏi đối thủ, đưa
    /// đối thủ về dạng tên gõ tay như trước. Trận vẫn còn — bạn vẫn đá với ai đó hôm đó.
    /// </summary>
    public bool DaHuyLienKet { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
