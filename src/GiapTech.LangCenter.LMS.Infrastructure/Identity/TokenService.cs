using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GiapTech.LangCenter.LMS.Infrastructure.Identity;

public class TokenService(IConfiguration config) : ITokenService
{
    public CapToken PhatHanh(ThongTinToken tt)
    {
        var secret = config["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "Thiếu JWT_SECRET. Xem docs/ha-tang/bien-moi-truong.md.");

        // Khoá ngắn làm chữ ký HS256 yếu đi đáng kể; chặn ở đây thay vì để chạy được với
        // cấu hình không an toàn.
        if (secret.Length < 32)
            throw new InvalidOperationException("JWT_SECRET phải có tối thiểu 32 ký tự.");

        var phut = int.TryParse(config["JWT_EXPIRY_MINUTES"], out var p) ? p : 60;
        var hetHan = DateTimeOffset.UtcNow.AddMinutes(phut);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, tt.Username),
            new(ClaimTenant.TaiKhoanId, tt.TaiKhoanId.ToString()),
            new(ClaimTenant.TenantId, tt.TenantId.ToString()),
            new(ClaimTenant.MaTrungTam, tt.MaTrungTam),
            new(ClaimTenant.TenTrungTam, tt.TenTrungTam)
        };

        // NameIdentifier = id NGƯỜI, không phải id tài khoản — xem ClaimTenant.TaiKhoanId.
        // Bỏ hẳn claim khi tài khoản không gắn người, thay vì phát Guid.Empty: chuỗi rỗng sẽ
        // parse thành Guid.Empty ở phía đọc và có thể khớp nhầm một hàng dữ liệu.
        if (tt.NguoiDungId is { } nd)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nd.ToString()));

        var khoa = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));

        var token = new JwtSecurityToken(
            issuer: config["JWT_ISSUER"] ?? "langcenter-lms-api",
            audience: config["JWT_ISSUER"] ?? "langcenter-lms-api",
            claims: claims,
            expires: hetHan.UtcDateTime,
            signingCredentials: new SigningCredentials(khoa, SecurityAlgorithms.HmacSha256));

        return new CapToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            // Refresh token ngẫu nhiên bằng RNG mật mã — không dùng Guid, vốn không được
            // thiết kế để khó đoán.
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            hetHan);
    }
}
