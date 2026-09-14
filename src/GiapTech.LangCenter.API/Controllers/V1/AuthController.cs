using Asp.Versioning;
using GiapTech.LangCenter.API.RateLimit;
using GiapTech.LangCenter.Application.DangNhap.Commands.DangNhap;
using GiapTech.LangCenter.Application.DangNhap.Commands.DatLaiMatKhauQuaToken;
using GiapTech.LangCenter.Application.DangNhap.Commands.DoiMatKhau;
using GiapTech.LangCenter.Application.DangNhap.Commands.LamMoiToken;
using GiapTech.LangCenter.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.LangCenter.Application.DangNhap.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>Xác thực (FR-01, FR-02).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Tra tên trung tâm theo mã trung tâm — ĐỂ HIỂN THỊ ở trang đăng nhập, ẩn danh.
    ///
    /// Mã trung tâm 7 ký tự không có nghĩa gì với người dùng; sai một chữ thì họ nhận "Sai thông tin
    /// đăng nhập" mà không biết sai ở mã hay ở mật khẩu.
    ///
    /// Trả **404 cho cả mã sai định dạng và mã không tồn tại** — phân biệt được thì người dò biết
    /// mã nào đúng định dạng, thu hẹp không gian dò. Frontend chỉ cần biết "không tìm thấy".
    ///
    /// ⚠️ Endpoint ẩn danh nhận input do người gọi kiểm soát. **Rate limit ở reverse proxy là
    /// bắt buộc** trước khi lên Internet — lớp `EnableRateLimiting` dưới đây chỉ là lớp trong.
    /// </summary>
    [HttpGet("ten-trung-tam/{maTrungTam:length(7)}")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.TraCuu)]
    [ProducesResponseType<TenTrungTamTheoMaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenTrungTamTheoMaDto>> TenTrungTam(string maTrungTam, CancellationToken ct)
        => await sender.Send(new TraTenTrungTamQuery(maTrungTam), ct) is { } dto
            ? Ok(dto)
            : NotFound();

    /// <summary>FR-01 — đăng nhập bằng {mã trung tâm, username, mật khẩu}.</summary>
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
    // Endpoint ẩn danh DUY NHẤT từng thiếu hạn mức (thêm 15/09/2026): mỗi request tốn một
    // truy vấn DB, nên là kênh gây tải rẻ nhất hệ thống. Dùng policy RIÊNG rộng hơn `XacThuc`
    // — xem `GioiHanTanSuat.LamMoiToken` để biết vì sao không dùng chung.
    [EnableRateLimiting(GioiHanTanSuat.LamMoiToken)]
    [ProducesResponseType<DangNhapResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DangNhapResult>> LamMoiToken(
        [FromBody] LamMoiTokenCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));
}
