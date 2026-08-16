using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.LichThiDau.DoiThu;
using GiapTech.SoccerRoom.Application.LichThiDau.TranDau;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>Sổ đối thủ — nền cho FR-09 và FR-10.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doi-thu")]
public class DoiThuController(ISender sender) : ControllerBase
{
    // Dùng chung quyền LichThiDau: sổ đối thủ là dữ liệu phụ trợ của lịch thi đấu, tách thành
    // chức năng riêng chỉ làm ma trận phân quyền dài thêm mà không ai cần cấp lẻ.
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<DoiThuDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachDoiThuQuery(timKiem, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuDoiThuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuDoiThuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaDoiThuCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-07, FR-08, FR-10, FR-11 — lịch thi đấu.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tran-dau")]
public class TranDauController(ISender sender) : ControllerBase
{
    /// <summary>
    /// FR-07 + FR-08 — danh sách trận có lọc.
    ///
    /// Dùng POST cho truy vấn vì bộ lọc có nhiều mảng (kết quả, trạng thái); nhồi hết vào
    /// query string sẽ dài và khó đọc trong log. Không thay đổi dữ liệu.
    /// </summary>
    [HttpPost("tim-kiem")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<TranDauDto>>> TimKiem(
        [FromBody] BoLocTranDau? loc,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachTranDauQuery(loc, new ThamSoTrang(trang, soDong)), ct));

    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<TranDauDto>>> DanhSach(
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachTranDauQuery(null, new ThamSoTrang(trang, soDong)), ct));

    /// <summary>
    /// FR-08 chế độ Calendar — đủ trận của một tháng, không phân trang.
    /// Cắt trang ở đây sẽ làm mất trận khỏi ô ngày mà người dùng không biết.
    /// </summary>
    [HttpPost("theo-thang")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<TranDauDto>>> TheoThang(
        [FromQuery] int nam,
        [FromQuery] int thang,
        [FromBody] BoLocTranDau? loc,
        CancellationToken ct)
        => Ok(await sender.Send(new LayTranDauTheoThangQuery(nam, thang, loc), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<TranDauDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayTranDauQuery(id), ct));

    [HttpPost]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuTranDauCommand command, CancellationToken ct)
    {
        var id = await sender.Send(command with { Id = null }, ct);
        return CreatedAtAction(nameof(ChiTiet), new { id, version = "1.0" }, id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuTranDauCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    /// <summary>FR-11 — xóa cứng. Chỉ áp dụng cho trận chưa diễn ra.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaTranDauCommand(id), ct);
        return NoContent();
    }

    /// <summary>FR-11 — lưu trữ trận đã diễn ra, thay cho xóa cứng.</summary>
    [HttpPost("{id:guid}/luu-tru")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> LuuTru(Guid id, CancellationToken ct)
    {
        await sender.Send(new LuuTruTranDauCommand(id), ct);
        return NoContent();
    }
}
