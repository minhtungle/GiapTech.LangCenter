using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GiapTech.LangCenter.Infrastructure.Identity;

public class TokenService(IConfiguration config) : ITokenService
{
    public CapToken PhatHanh(ThongTinToken tt)
    {
        var secret = LayKhoa();

        var phut = int.TryParse(config["JWT_EXPIRY_MINUTES"], out var p) ? p : 60;
        var hetHan = DateTimeOffset.UtcNow.AddMinutes(phut);

        // Tách ra biến để vừa ký vào token vừa trả cho handler ghi làm PHIÊN hiện tại — xem
        // `CapToken.Jti`.
        var jti = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti.ToString()),
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

        return Dung(claims, jti, hetHan, secret);
    }

    /// <summary>
    /// Token cho chủ hệ thống (ADR-0009) — **KHÔNG có claim `tenant_id`**.
    ///
    /// Sự vắng mặt đó chính là hàng rào: `TenantMiddleware` trả 401 `TOKEN_THIEU_TENANT` cho
    /// mọi token đã xác thực mà thiếu claim tenant, nên token này không gọi được endpoint
    /// nghiệp vụ nào — kể cả endpoint thêm sau này mà người viết không biết tới ADR-0009.
    /// </summary>
    public CapToken PhatHanhChoChuHeThong(ThongTinTokenChu tt)
    {
        var secret = LayKhoa();
        var phut = int.TryParse(config["JWT_EXPIRY_MINUTES"], out var p) ? p : 60;
        var hetHan = DateTimeOffset.UtcNow.AddMinutes(phut);
        var jti = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti.ToString()),
            new(ClaimTypes.Name, tt.Username),
            new(ClaimTypes.NameIdentifier, tt.QuanTriId.ToString()),
            new(ClaimTenant.LoaiDanhTinh, ClaimTenant.LoaiChuHeThong)
            // CỐ Ý không có ClaimTenant.TenantId — xem chú thích trên.
        };

        return Dung(claims, jti, hetHan, secret);
    }

    /// <summary>
    /// Khoá ký, dùng chung cho cả token tenant và token chủ hệ thống.
    ///
    /// Một chỗ đọc duy nhất để hai đường phát token không thể áp ràng buộc khác nhau — bản
    /// đầu của ADR-0009 lặp lại đoạn này và đó là chỗ hai nhánh dễ trôi khỏi nhau.
    /// </summary>
    private string LayKhoa()
    {
        var secret = config["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "Thiếu JWT_SECRET. Xem docs/07-ha-tang/bien-moi-truong.md.");

        // Khoá ngắn làm chữ ký HS256 yếu đi đáng kể; chặn ở đây thay vì để chạy được với
        // cấu hình không an toàn.
        if (secret.Length < 32)
            throw new InvalidOperationException("JWT_SECRET phải có tối thiểu 32 ký tự.");

        return secret;
    }

    private CapToken Dung(
        List<Claim> claims, Guid jti, DateTimeOffset hetHan, string secret)
    {
        var khoa = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));

        var token = new JwtSecurityToken(
            issuer: config["JWT_ISSUER"] ?? "langcenter-api",
            audience: config["JWT_ISSUER"] ?? "langcenter-api",
            claims: claims,
            expires: hetHan.UtcDateTime,
            signingCredentials: new SigningCredentials(khoa, SecurityAlgorithms.HmacSha256));

        return new CapToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            // Refresh token ngẫu nhiên bằng RNG mật mã — không dùng Guid, vốn không được
            // thiết kế để khó đoán.
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            hetHan,
            jti);
    }
}
