using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.SanDoiThu;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// SÀN ĐỐI THỦ — xem các CLB khác trong hệ thống và gửi lời mời bắt đối.
///
/// ⚠️ Toàn bộ controller này đọc dữ liệu NGOÀI tenant hiện tại. Xem `SanDoiThuDtos` để biết
/// những gì cố ý lộ và những gì cố ý không.
///
/// Dùng chung quyền `LichThiDau`: bắt đối là việc của người sắp lịch. Tách thành chức năng
/// riêng sẽ khiến mọi nhóm quyền đang có mất quyền vào sàn cho tới khi admin đi cấp lại —
/// thêm chức năng vào `QUYEN_CHUC_NANG` không tự gán cho nhóm nào.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/san-doi-thu")]
public class SanDoiThuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<ClbTrenSanDto>>> DanhSach(
        [FromQuery] string? tuKhoa,
        [FromQuery] string? khuVuc,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachSanQuery(tuKhoa, khuVuc, trang, soDong), ct));

    [HttpGet("khu-vuc")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<string>>> KhuVuc(CancellationToken ct)
        => Ok(await sender.Send(new LayKhuVucQuery(), ct));

    /// <summary>Lời mời bắt đối — cả ta gửi lẫn ta nhận, trong một danh sách.</summary>
    [HttpGet("loi-moi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<ThuBatDoiDto>>> LoiMoi(CancellationToken ct)
        => Ok(await sender.Send(new LayThuBatDoiQuery(), ct));

    /// <summary>Gửi lời mời tới CLB khác. Cần quyền THÊM: nó tạo dữ liệu ở hòm thư bên kia.</summary>
    [HttpPost("loi-moi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Gui(
        [FromBody] GuiLoiMoiBatDoiCommand body, CancellationToken ct)
        => Ok(await sender.Send(body, ct));

    /// <summary>
    /// Đồng ý hoặc từ chối. Cần quyền THÊM vì đồng ý sẽ TẠO trận trong lịch của cả hai bên —
    /// người chỉ có quyền Xem không được tự ý thêm trận vào lịch đội.
    /// </summary>
    [HttpPost("loi-moi/{id:guid}/tra-loi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<IActionResult> TraLoi(
        Guid id, [FromBody] TraLoiBatDoiBody body, CancellationToken ct)
    {
        await sender.Send(new TraLoiBatDoiCommand(id, body.ChapNhan, body.PhanHoi), ct);
        return NoContent();
    }

    public record TraLoiBatDoiBody(bool ChapNhan, string? PhanHoi);

    /// <summary>Bên gửi rút lại lời mời chưa được trả lời.</summary>
    [HttpDelete("loi-moi/{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Huy(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyLoiMoiBatDoiCommand(id), ct);
        return NoContent();
    }
}
