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

    // ----- FR-19: link/QR cho người KHÔNG có tài khoản -----
    //
    // Đặt ngay trong bảng này chứ không tạo bảng riêng: mỗi lời mời có **nhiều nhất một** link
    // đang hiệu lực, nên bảng riêng chỉ thêm một join mà không thêm khả năng nào. Khác FR-18 —
    // ở đó lời mời qua link là một *loại lời mời khác*, còn ở đây link chỉ là một *cách vào* của
    // cùng một lời mời.

    /// <summary>
    /// SHA-256 của token thô. Null = chưa sinh link. Lưu hash chứ không lưu token, cùng cơ chế
    /// với token đặt lại mật khẩu và FR-18: DB bị đọc lén thì kẻ đọc không dựng lại được link.
    /// </summary>
    public string? LinkTokenHash { get; set; }

    /// <summary>
    /// Hạn của link. Trưởng nhóm chọn một trong bốn mốc — xem <c>HanLinkDangKy</c>.
    /// Null khi chưa sinh link.
    /// </summary>
    public DateTimeOffset? LinkHetHan { get; set; }

    /// <summary>
    /// Trưởng nhóm thu hồi link lúc nào. Đánh dấu chứ không xoá hash: người đang mở link cần
    /// thấy "đã bị thu hồi" thay vì một trang lỗi không giải thích gì.
    ///
    /// Thu hồi được nghĩa là không cần hạn cực ngắn để an toàn — đó là lý do bốn mốc hạn có cả
    /// mốc 7 ngày.
    /// </summary>
    public DateTimeOffset? LinkThuHoiLuc { get; set; }

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

    /// <summary>
    /// Số lần câu trả lời bị **đổi** (FR-19). 0 = trả lời một lần rồi thôi.
    ///
    /// Có vì quyết định "cho sửa lại, không khoá cứng": khoá cứng thì người mở link đầu tiên có
    /// thể chọn hộ người khác và khoá luôn họ, mà không ai biết. Cho sửa thì không ai bị khoá
    /// oan, nhưng phải để lại **dấu vết** — "người này sửa 6 lần" là thứ trưởng nhóm nhìn thấy
    /// được, còn "bị khoá oan" thì không.
    /// </summary>
    public int SoLanSua { get; set; }

    /// <summary>
    /// true = câu trả lời đến từ link ẩn danh, không phải từ tài khoản đã đăng nhập (FR-19).
    ///
    /// Trưởng nhóm cần phân biệt: câu trả lời qua link **không** xác thực được là ai bấm, nên
    /// khi có tranh chấp ("tôi không đăng ký mà") thì đây là chỗ nhìn.
    /// </summary>
    public bool QuaLink { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
