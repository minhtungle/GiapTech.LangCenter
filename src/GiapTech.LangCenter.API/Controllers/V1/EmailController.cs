using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.QuanTri.Email;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// FR-31 — cấu hình gửi email và mẫu nội dung. Thuộc nhóm quản trị hệ thống.
///
/// Mọi endpoint ở đây đều **sau đăng nhập và có gác quyền**. Không có endpoint ẩn danh nào:
/// cấu hình SMTP và nội dung mẫu là chuyện nội bộ của trung tâm.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/email")]
public class EmailController(ISender sender) : ControllerBase
{
    // ---------------------------------------------------------------------------------
    // Cấu hình gửi
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Cấu hình SMTP hiện tại. **Không bao giờ trả mật khẩu** — chỉ cờ `CoMatKhau`.
    /// </summary>
    [HttpGet("thiet-lap")]
    [RequirePermission(ChucNang.ThietLapEmail, HanhDong.Xem)]
    [ProducesResponseType<ThietLapEmailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ThietLapEmailDto>> ThietLap(CancellationToken ct)
        => Ok(await sender.Send(new LayThietLapEmailQuery(), ct));

    [HttpPut("thiet-lap")]
    [RequirePermission(ChucNang.ThietLapEmail, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LuuThietLap(
        [FromBody] LuuThietLapEmailCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Xoá cấu hình riêng — trung tâm quay về dùng SMTP chung của VPS.</summary>
    [HttpDelete("thiet-lap")]
    [RequirePermission(ChucNang.ThietLapEmail, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> XoaThietLap(CancellationToken ct)
    {
        await sender.Send(new XoaThietLapEmailCommand(), ct);
        return NoContent();
    }

    /// <summary>
    /// Gửi một email thử. Quyền RIÊNG `GuiThu` — nó bắn thư thật ra ngoài.
    ///
    /// Không có rate limit riêng: endpoint này đã sau đăng nhập và sau kiểm quyền, nên nó
    /// không phải bề mặt tấn công như endpoint ẩn danh. Người có quyền lạm dụng nó là chuyện
    /// nội bộ của trung tâm.
    /// </summary>
    [HttpPost("gui-thu")]
    [RequirePermission(ChucNang.ThietLapEmail, HanhDong.GuiThu)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuiThu(
        [FromBody] GuiEmailThuCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    // ---------------------------------------------------------------------------------
    // Mẫu nội dung
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Danh sách mẫu — **luôn đủ mọi loại**, kể cả loại trung tâm chưa soạn (lúc đó trả nội
    /// dung mặc định kèm cờ `DaSoan = false`).
    /// </summary>
    [HttpGet("mau")]
    [RequirePermission(ChucNang.MauEmail, HanhDong.Xem)]
    [ProducesResponseType<IReadOnlyList<MauEmailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MauEmailDto>>> DanhSachMau(CancellationToken ct)
        => Ok(await sender.Send(new DanhSachMauEmailQuery(), ct));

    [HttpPut("mau/{loai}")]
    [RequirePermission(ChucNang.MauEmail, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LuuMau(
        LoaiMauEmail loai, [FromBody] LuuMauEmailCommand command, CancellationToken ct)
    {
        await sender.Send(command with { Loai = loai }, ct);
        return NoContent();
    }

    /// <summary>Xoá mẫu đã soạn — quay về mẫu mặc định trong mã.</summary>
    [HttpDelete("mau/{loai}")]
    [RequirePermission(ChucNang.MauEmail, HanhDong.Xoa)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> XoaMau(LoaiMauEmail loai, CancellationToken ct)
    {
        await sender.Send(new XoaMauEmailCommand(loai), ct);
        return NoContent();
    }
}
