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

    /// <summary>
    /// Xoá các CLB do test E2E sinh ra (tên bắt đầu bằng <c>"E2E "</c>).
    ///
    /// Gọi ở bước teardown của Playwright. Không có bước này thì mỗi lần chạy cả bộ để lại
    /// ~44 CLB, chúng hiện lên trang Cộng đồng của mọi người, và sau vài lần chạy thì trang đó
    /// không còn dùng được để test tay — đo thật 20/08: 242 CLB rác trên 249.
    ///
    /// Không nhận tham số nào: tiền tố là cố định trong handler, không có chế độ "xoá hết".
    /// Dừng hẳn (400 `DON_TENANT_TEST_VUONG_CLB_THAT`) nếu có lời mời bắc giữa CLB test và CLB thật.
    /// </summary>
    [HttpPost("don-tenant-test")]
    [AllowAnonymous]
    public async Task<IActionResult> DonTenantTest(CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        return Ok(await sender.Send(new DonTenantTestCommand(), ct));
    }
}
