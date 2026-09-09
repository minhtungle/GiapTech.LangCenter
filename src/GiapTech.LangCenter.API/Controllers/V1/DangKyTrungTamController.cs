using Asp.Versioning;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.API.RateLimit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// Đăng ký trung tâm mới — tạo tenant kèm tài khoản admin mặc định.
///
/// **MỞ Ở MỌI MÔI TRƯỜNG** — endpoint ẩn danh, ai cũng tự tạo được trung tâm của mình.
///
/// ⚠️ **Rate limit ở tầng reverse proxy là BẮT BUỘC** trước khi mở ra Internet: một script gọi
/// endpoint này liên tục sẽ sinh tenant rác không giới hạn.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dang-ky-trung-tam")]
public class DangKyTrungTamController(ITenantSeeder seeder) : ControllerBase
{
    /// <summary>Chỉ cần tên trung tâm — mã trung tâm do hệ thống sinh (7 ký tự).</summary>
    public record DangKyRequest(string TenTrungTam);

    // Endpoint ẩn danh và GHI dữ liệu (tạo tenant + tài khoản admin) — thiếu hạn mức thì một
    // script sinh tenant rác không giới hạn. `AuthController` đã gắn hạn mức này từ 21/08 mà
    // endpoint đăng ký thì bị bỏ sót; phát hiện 08/09 khi soát lại tài liệu. Đây là lớp TRONG,
    // rate limit ở reverse proxy vẫn bắt buộc (nợ N3).
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
    public async Task<IActionResult> DangKy([FromBody] DangKyRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TenTrungTam))
            return BadRequest(new { errorCode = "DU_LIEU_KHONG_HOP_LE" });

        try
        {
            var tenant = await seeder.TaoTenantMoiAsync(body.TenTrungTam.Trim(), ct: ct);
            return Ok(new
            {
                tenant.Id,
                tenant.MaTrungTam,
                tenant.TenTrungTam,
                username = "admin",
                matKhau = "123456",
                luuY = "Ghi lại mã trung tâm — cần nó để đăng nhập. Bắt buộc đổi mật khẩu lần đầu."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "LOI_HE_THONG", chiTiet = ex.Message });
        }
    }
}
