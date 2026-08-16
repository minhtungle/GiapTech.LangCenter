using Asp.Versioning;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DangNhap;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DoiMatKhau;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>Xác thực (FR-01, FR-02).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    /// <summary>FR-01 — đăng nhập bằng {ID đội, username, mật khẩu}.</summary>
    [HttpPost("dang-nhap")]
    [AllowAnonymous]
    [ProducesResponseType<DangNhapResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DangNhapResult>> DangNhap(
        [FromBody] DangNhapCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    /// <summary>
    /// FR-01 — người dùng tự đổi mật khẩu. Gọi được cả khi đang bị buộc đổi mật khẩu
    /// (đường dẫn này nằm trong danh sách cho phép của BuocDoiMatKhauMiddleware).
    /// </summary>
    [HttpPost("doi-mat-khau")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DoiMatKhau(
        [FromBody] DoiMatKhauCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
