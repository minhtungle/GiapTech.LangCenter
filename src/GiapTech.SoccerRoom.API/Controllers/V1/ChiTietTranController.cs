using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.LichThiDau.ChiTietTran;
using GiapTech.SoccerRoom.Application.LichThiDau.LoiMoi;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>FR-09 — lời mời giao hữu từ đối thủ.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/loi-moi")]
public class LoiMoiController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<LoiMoiDto>>> DanhSach(
        [FromQuery] TrangThaiLoiMoi? trangThai, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachLoiMoiQuery(trangThai), ct));

    [HttpPost]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuLoiMoiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuLoiMoiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    /// <summary>Chấp nhận → tự sinh trận đấu "đã lên lịch". Trả về id trận vừa tạo.</summary>
    [HttpPost("{id:guid}/chap-nhan")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> ChapNhan(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new ChapNhanLoiMoiCommand(id), ct));

    [HttpPost("{id:guid}/tu-choi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> TuChoi(Guid id, CancellationToken ct)
    {
        await sender.Send(new TuChoiLoiMoiCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaLoiMoiCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-10 — chi tiết trận: đội hình (a), sơ đồ (b), đánh giá + vote MVP (c).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tran-dau/{tranDauId:guid}")]
public class ChiTietTranController(ISender sender) : ControllerBase
{
    // ----- Tab (a): đội hình -----

    [HttpGet("doi-hinh")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<DoiHinhDto>>> DoiHinh(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LayDoiHinhQuery(tranDauId), ct));

    [HttpPut("doi-hinh")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuDoiHinh(
        Guid tranDauId, [FromBody] LuuDoiHinhCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    // ----- Tab (b): sơ đồ chiến thuật -----

    [HttpGet("so-do")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<SoDoDto>> SoDo(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LaySoDoQuery(tranDauId), ct));

    [HttpPut("so-do")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuSoDo(
        Guid tranDauId, [FromBody] LuuSoDoCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    // ----- Tab (c): đánh giá + vote MVP -----

    [HttpGet("danh-gia")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<DanhGiaDto>>> DanhGia(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhGiaQuery(tranDauId), ct));

    [HttpPut("danh-gia")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuDanhGia(
        Guid tranDauId, [FromBody] LuuDanhGiaCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    /// <summary>
    /// Thả tim MVP. Chỉ cần quyền **Xem** lịch thi đấu: cầu thủ (Player) phải bình chọn được
    /// mà không cần quyền sửa trận — xem docs/nghiep-vu/lich-thi-dau.md.
    /// </summary>
    [HttpPost("vote-mvp/{cauThuId:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<IActionResult> VoteMvp(
        Guid tranDauId, Guid cauThuId, CancellationToken ct)
    {
        await sender.Send(new VoteMvpCommand(tranDauId, cauThuId), ct);
        return NoContent();
    }
}
