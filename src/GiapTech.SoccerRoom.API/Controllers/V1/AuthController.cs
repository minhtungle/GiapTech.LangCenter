using Asp.Versioning;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DangNhap;
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
}
