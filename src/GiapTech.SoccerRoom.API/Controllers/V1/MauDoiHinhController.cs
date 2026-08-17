using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.LichThiDau.MauDoiHinh;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Quản lý đội hình mẫu — dùng lại cho nhiều trận.
///
/// Nằm dưới quyền <see cref="ChucNang.LichThiDau"/> chứ không tạo chức năng mới: mẫu chỉ là
/// công cụ phục vụ việc xếp đội hình, ai xếp được đội hình thì quản được mẫu. Thêm chức năng
/// mới vào QUYEN_CHUC_NANG sẽ bắt mọi CLB đang chạy phải cấp lại quyền.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mau-doi-hinh")]
public class MauDoiHinhController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<MauDoiHinhDto>>> DanhSach(
        [FromQuery] int? loaiSan, [FromQuery] int trang = 1, [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachMauQuery(loaiSan, new ThamSoTrang(trang, soDong)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<MauDoiHinhDto>> Chi_tiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayMauQuery(id), ct));

    [HttpPost]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuMauCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuMauCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    /// <summary>Lưu đội hình của một trận thành mẫu — dùng ngay tại màn chi tiết trận.</summary>
    [HttpPost("tu-tran/{tranDauId:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> TaoTuTran(
        Guid tranDauId, [FromBody] TaoMauTuTranCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { TranDauId = tranDauId }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaMauCommand(id), ct);
        return NoContent();
    }
}
