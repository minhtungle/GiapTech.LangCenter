namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>Băm và kiểm tra mật khẩu.</summary>
public interface IPasswordHasher
{
    string Bam(string matKhau);

    /// <summary>Kiểm tra mật khẩu. Trả về false thay vì ném exception khi hash hỏng định dạng.</summary>
    bool KiemTra(string hash, string matKhau);
}

/// <summary>Thông tin đưa vào JWT.</summary>
public record ThongTinToken(
    Guid TenantId, string MaTrungTam, string TenTrungTam, Guid NguoiDungId, string Username);

/// <summary>Cặp token trả về sau đăng nhập.</summary>
public record CapToken(string AccessToken, string RefreshToken, DateTimeOffset HetHan);

/// <summary>Phát hành JWT (FR-01).</summary>
public interface ITokenService
{
    CapToken PhatHanh(ThongTinToken thongTin);
}
