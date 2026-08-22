using Asp.Versioning;
using GiapTech.SoccerRoom.API.RateLimit;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DangNhap;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DatLaiMatKhauQuaToken;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DoiMatKhau;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.LamMoiToken;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Application.DangNhap.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>Xác thực (FR-01, FR-02).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Tra tên CLB theo mã đội — ĐỂ HIỂN THỊ ở trang đăng nhập, ẩn danh.
    ///
    /// Mã đội 7 ký tự không có nghĩa gì với người dùng; sai một chữ thì họ nhận "Sai thông tin
    /// đăng nhập" mà không biết sai ở mã hay ở mật khẩu.
    ///
    /// Trả **404 cho cả mã sai định dạng và mã không tồn tại** — phân biệt được thì người dò biết
    /// mã nào đúng định dạng, thu hẹp không gian dò. Frontend chỉ cần biết "không tìm thấy".
    ///
    /// ⚠️ Endpoint ẩn danh thứ hai nhận input do người gọi kiểm soát (sau
    /// `POST /moi-qua-link/xem`). **Rate limit ở Caddy (nợ N3) là bắt buộc** trước khi lên
    /// Internet.
    /// </summary>
    [HttpGet("ten-doi/{maDoi:length(7)}")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.TraCuu)]
    [ProducesResponseType<TenDoiTheoMaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenDoiTheoMaDto>> TenDoi(string maDoi, CancellationToken ct)
        => await sender.Send(new TraTenDoiQuery(maDoi), ct) is { } dto
            ? Ok(dto)
            : NotFound();

    /// <summary>FR-01 — đăng nhập bằng {ID đội, username, mật khẩu}.</summary>
    [HttpPost("dang-nhap")]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
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

    /// <summary>
    /// FR-02 — yêu cầu đặt lại mật khẩu qua email.
    /// Luôn trả 204 dù email có tồn tại hay không, để không tiết lộ email nào đã đăng ký.
    /// </summary>
    [HttpPost("quen-mat-khau")]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> QuenMatKhau(
        [FromBody] QuenMatKhauCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>FR-02 — đặt lại mật khẩu bằng mã nhận qua email.</summary>
    [HttpPost("dat-lai-mat-khau")]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DatLaiMatKhau(
        [FromBody] DatLaiMatKhauQuaTokenCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>FR-01 — đổi refresh token lấy cặp token mới (token cũ bị thu hồi ngay).</summary>
    [HttpPost("lam-moi-token")]
    [AllowAnonymous]
    [ProducesResponseType<DangNhapResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DangNhapResult>> LamMoiToken(
        [FromBody] LamMoiTokenCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));
}
