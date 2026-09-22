using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// REFRESH_TOKEN — token làm mới phiên đăng nhập.
///
/// Lưu **hash** chứ không lưu token thô: người đọc được DB (backup rò rỉ, SQL injection,
/// lập trình viên xem bảng) sẽ không mạo danh được ai. Cùng lý do với password_hash.
/// </summary>
public class RefreshToken : TenantEntity
{
    public Guid TaiKhoanId { get; set; }
    public TaiKhoan TaiKhoan { get; set; } = null!;

    /// <summary>SHA-256 của token thô. Token thô chỉ tồn tại trong response trả về client.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset HetHan { get; set; }

    /// <summary>Thời điểm bị thu hồi (đăng xuất, hoặc đã dùng để xoay vòng). Null = còn hiệu lực.</summary>
    public DateTimeOffset? ThuHoiLuc { get; set; }

    /// <summary>
    /// **Vì sao** token bị thu hồi (22/09/2026, ADR-0007). Null với token còn hiệu lực, và với
    /// token thu hồi trước ngày này.
    ///
    /// Cần vì `ThuHoiLuc` không nói được lý do, mà hai lý do đòi hai cách xử lý **ngược nhau**
    /// khi ai đó dùng lại token đã thu hồi:
    ///
    /// - <see cref="LyDoThuHoi.XoayVong"/> → nghi **bị đánh cắp**: kẻ tấn công dùng bản sao cũ
    ///   sau khi chủ tài khoản đã xoay vòng ⇒ thu hồi TOÀN BỘ phiên.
    /// - <see cref="LyDoThuHoi.BiDayRa"/> → chuyện **bình thường**: người kia vừa đăng nhập.
    ///   Thu hồi toàn bộ ở đây sẽ giết luôn token của chính người vừa đăng nhập ⇒ hai người
    ///   cùng bị đá ra, không ai vào được.
    ///
    /// Trước ADR-0007 không phân biệt được cũng không sao: phiên cũ nhận 401 ở endpoint nghiệp
    /// vụ và frontend **không** gọi làm mới. Nay refresh token đi bằng cookie nên phiên cũ chạm
    /// vào `lam-moi-token` trước, và sự nhập nhằng thành lỗi thật.
    /// </summary>
    public LyDoThuHoi? LyDo { get; set; }

    public bool ConHieuLuc(DateTimeOffset bayGio) => ThuHoiLuc is null && bayGio < HetHan;
}

/// <summary>Lý do một refresh token bị thu hồi — xem <see cref="RefreshToken.LyDo"/>.</summary>
public enum LyDoThuHoi
{
    /// <summary>Đã dùng để đổi lấy cặp token mới. Dùng lại = nghi bị đánh cắp.</summary>
    XoayVong = 0,

    /// <summary>Bị lần đăng nhập mới đẩy ra (một phiên mỗi tài khoản). Dùng lại là bình thường.</summary>
    BiDayRa = 1,

    /// <summary>Người dùng tự đăng xuất, hoặc đổi/đặt lại mật khẩu.</summary>
    DangXuatHoacDoiMatKhau = 2,
}

/// <summary>
/// TOKEN_DAT_LAI_MK — token đặt lại mật khẩu qua email (FR-02).
/// Cũng lưu hash, dùng một lần, có thời hạn ngắn.
/// </summary>
public class TokenDatLaiMatKhau : TenantEntity
{
    public Guid TaiKhoanId { get; set; }
    public TaiKhoan TaiKhoan { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public DateTimeOffset HetHan { get; set; }
    public DateTimeOffset? DaDungLuc { get; set; }

    public bool ConHieuLuc(DateTimeOffset bayGio) => DaDungLuc is null && bayGio < HetHan;
}
