using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.Crm;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>FR-17 — khách hàng (CRM).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/khach-hang")]
public class KhachHangController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhachHangDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] bool? daMua,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachKhachHangQuery(
            timKiem, daMua, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuKhachHangCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuKhachHangCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.KhachHang, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhachHangCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-19 — danh mục khoá học bán ra (CRM).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/khoa-hoc")]
public class KhoaHocController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhoaHocDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] bool? dangBan,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachKhoaHocQuery(
            timKiem, dangBan, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuKhoaHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuKhoaHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.KhoaHoc, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhoaHocCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// FR-18 — doanh thu: đăng ký khoá học (CRM).
///
/// Gác bằng `DoanhThu` chứ không `KhachHang`: người trực tổng đài nhập khách mới cần quyền
/// khách hàng mà **không** nên thấy số tiền của mọi đơn hàng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doanh-thu")]
public class DoanhThuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<DangKyDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] Guid? khachHangId,
        [FromQuery] Guid? khoaHocId,
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDoanhThuQuery(
            timKiem, khachHangId, khoaHocId, tuNgay, denNgay,
            new ThamSoTrang(trang, soDong)), ct));

    /// <summary>
    /// Tổng hợp trên TOÀN BỘ tập đã lọc, không chỉ trang đang xem — endpoint riêng vì cộng
    /// trên trang hiện tại là số vô nghĩa mà người dùng rất dễ tin là tổng thật.
    /// </summary>
    [HttpGet("tong-hop")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xem)]
    public async Task<ActionResult<TongHopDoanhThuDto>> TongHop(
        [FromQuery] string? timKiem,
        [FromQuery] Guid? khachHangId,
        [FromQuery] Guid? khoaHocId,
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayTongHopDoanhThuQuery(
            timKiem, khachHangId, khoaHocId, tuNgay, denNgay), ct));

    [HttpPost]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuDangKyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuDangKyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.DoanhThu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaDangKyCommand(id), ct);
        return NoContent();
    }
}
