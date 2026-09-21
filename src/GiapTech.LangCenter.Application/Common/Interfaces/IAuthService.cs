namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>Băm và kiểm tra mật khẩu.</summary>
public interface IPasswordHasher
{
    string Bam(string matKhau);

    /// <summary>Kiểm tra mật khẩu. Trả về false thay vì ném exception khi hash hỏng định dạng.</summary>
    bool KiemTra(string hash, string matKhau);
}

/// <summary>Thông tin đưa vào JWT.</summary>
/// <param name="NguoiDungId">
/// Id NGƯỜI — null nếu tài khoản kỹ thuật không gắn con người nào. Đây là thứ mọi khoá ngoại
/// nghiệp vụ trỏ tới.
/// </param>
/// <param name="TaiKhoanId">Id TÀI KHOẢN — dùng cho thao tác trên chính tài khoản.</param>
public record ThongTinToken(
    Guid TenantId, string MaTrungTam, string TenTrungTam,
    Guid? NguoiDungId, Guid TaiKhoanId, string Username);

/// <summary>Cặp token trả về sau đăng nhập.</summary>
public record CapToken(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset HetHan,
    /// <summary>
    /// `jti` của access token vừa phát — định danh PHIÊN (20/09/2026).
    ///
    /// Trả ra đây thay vì để handler tự giải mã lại token: giải mã lại là làm hai lần một việc,
    /// và hai chỗ dễ trôi khỏi nhau khi đổi cách sinh `jti`.
    /// </summary>
    Guid Jti);

/// <summary>Phát hành JWT (FR-01).</summary>
public interface ITokenService
{
    CapToken PhatHanh(ThongTinToken thongTin);
}
