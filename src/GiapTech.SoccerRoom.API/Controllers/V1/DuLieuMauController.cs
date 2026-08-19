using Asp.Versioning;
using GiapTech.SoccerRoom.Application.DuLieuMau;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Dựng bộ dữ liệu mẫu để test tay.
///
/// CHỈ BẬT Ở DEVELOPMENT, cùng lý do với <see cref="DangKyClbController"/> — và ở đây còn nặng
/// hơn: endpoint này có tuỳ chọn XOÁ SẠCH mọi dữ liệu. Mở ở production là đưa cho bất kỳ ai
/// biết URL một cái nút xoá toàn bộ hệ thống.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/du-lieu-mau")]
public class DuLieuMauController(ISender sender, IWebHostEnvironment env) : ControllerBase
{
    /// <param name="xoaDuLieuCu">
    /// true = xoá TOÀN BỘ dữ liệu hiện có trước khi seed. Mặc định false (quy tắc #1: xoá dữ
    /// liệu phải do người dùng chọn tường minh).
    /// </param>
    [HttpPost("seed")]
    [AllowAnonymous]
    public async Task<IActionResult> Seed(
        [FromQuery] bool xoaDuLieuCu = false, CancellationToken ct = default)
    {
        if (!env.IsDevelopment()) return NotFound();

        var kq = await sender.Send(new SeedDuLieuMauCommand(xoaDuLieuCu), ct);
        return Ok(kq);
    }
}
