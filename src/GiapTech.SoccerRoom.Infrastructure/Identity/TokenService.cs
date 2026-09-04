using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GiapTech.SoccerRoom.Infrastructure.Identity;

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
            new(ClaimTypes.NameIdentifier, tt.NguoiDungId.ToString()),
            new(ClaimTypes.Name, tt.Username),
            new(ClaimTenant.TenantId, tt.TenantId.ToString()),
            new(ClaimTenant.MaTrungTam, tt.MaTrungTam),
            new(ClaimTenant.TenTrungTam, tt.TenTrungTam)
        };

        var khoa = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));

        var token = new JwtSecurityToken(
            issuer: config["JWT_ISSUER"] ?? "soccerroom-api",
            audience: config["JWT_ISSUER"] ?? "soccerroom-api",
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
