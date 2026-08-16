using Asp.Versioning;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Đăng ký CLB mới — tạo tenant kèm tài khoản admin mặc định.
///
/// CHỈ BẬT Ở MÔI TRƯỜNG DEVELOPMENT. Trên production, mở endpoint ẩn danh tạo tenant là mở
/// cửa cho bất kỳ ai sinh CLB rác không giới hạn. Quy trình thật cần duyệt thủ công hoặc
/// xác thực cấp hệ thống — sẽ bổ sung khi triển khai.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dang-ky-clb")]
public class DangKyClbController(
    ITenantSeeder seeder, IWebHostEnvironment env) : ControllerBase
{
    public record DangKyRequest(string MaDoi, string TenDoi);

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> DangKy([FromBody] DangKyRequest body, CancellationToken ct)
    {
        if (!env.IsDevelopment())
            return NotFound();

        try
        {
            var tenant = await seeder.TaoTenantMoiAsync(body.MaDoi, body.TenDoi, ct: ct);
            return Ok(new
            {
                tenant.Id,
                tenant.MaDoi,
                tenant.TenDoi,
                username = "admin",
                matKhau = "123456",
                luuY = "Bắt buộc đổi mật khẩu ở lần đăng nhập đầu tiên."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "MA_DOI_DA_TON_TAI", chiTiet = ex.Message });
        }
    }
}
