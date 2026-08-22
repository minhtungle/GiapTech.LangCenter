using Asp.Versioning;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Đăng ký CLB mới — tạo tenant kèm tài khoản admin mặc định.
///
/// **MỞ Ở MỌI MÔI TRƯỜNG** từ 20/08/2026 (nợ N4 đã xử lý). Trước đó chỉ bật ở Development.
///
/// Lý do phải mở: luồng lời mời qua link (FR-18) có ca phổ biến nhất là "đối thủ CHƯA có tài
/// khoản" — họ bấm link, tạo đội ngay tại đó, rồi chấp nhận. Chặn đăng ký thì ca đó không chạy
/// được và tính năng mất phần lớn giá trị.
///
/// ⚠️ **Rate limit ở tầng Caddy là BẮT BUỘC** trước khi mở ra Internet: một script gọi endpoint
/// này liên tục sẽ sinh CLB rác không giới hạn, và chúng hiện hết lên Cộng đồng của mọi người —
/// đúng thứ đã xảy ra với 292 tenant rác từ E2E. Xem nợ N3 trong docs/ke-hoach.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dang-ky-clb")]
public class DangKyClbController(ITenantSeeder seeder) : ControllerBase
{
    /// <summary>Chỉ cần tên đội — mã đội do hệ thống sinh (7 ký tự).</summary>
    public record DangKyRequest(string TenDoi);

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> DangKy([FromBody] DangKyRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TenDoi))
            return BadRequest(new { errorCode = "DU_LIEU_KHONG_HOP_LE" });

        try
        {
            var tenant = await seeder.TaoTenantMoiAsync(body.TenDoi.Trim(), ct: ct);
            return Ok(new
            {
                tenant.Id,
                tenant.MaDoi,
                tenant.TenDoi,
                username = "admin",
                matKhau = "123456",
                luuY = "Ghi lại mã đội — cần nó để đăng nhập. Bắt buộc đổi mật khẩu lần đầu."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "LOI_HE_THONG", chiTiet = ex.Message });
        }
    }
}
