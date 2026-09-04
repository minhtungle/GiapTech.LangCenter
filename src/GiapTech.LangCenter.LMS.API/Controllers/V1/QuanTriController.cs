using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.QuanTri.Quyen;
using GiapTech.LangCenter.LMS.Application.QuanTri.TaiKhoan;
using GiapTech.LangCenter.LMS.Application.QuanTri.ThietLap;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>FR-03 — tài khoản người dùng.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tai-khoan")]
public class TaiKhoanController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<TaiKhoanDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachTaiKhoanQuery(timKiem, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoTaiKhoanCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatTaiKhoanCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>
    /// Đổi mật khẩu cho tài khoản khác — đặc quyền riêng của Admin (FR-03), dùng chức năng
    /// <c>DoiMatKhauNguoiKhac</c> chứ không phải quyền Sửa tài khoản thông thường.
    /// </summary>
    [HttpPost("{id:guid}/dat-lai-mat-khau")]
    [RequirePermission(ChucNang.DoiMatKhauNguoiKhac, HanhDong.Sua)]
    public async Task<IActionResult> DatLaiMatKhau(
        Guid id, [FromBody] DatLaiMatKhauRequest body, CancellationToken ct)
    {
        await sender.Send(new DatLaiMatKhauCommand(id, body.MatKhauMoi), ct);
        return NoContent();
    }

    public record DatLaiMatKhauRequest(string MatKhauMoi);

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaTaiKhoanCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-05 — nhóm quyền.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/quyen")]
public class QuyenController(ISender sender) : ControllerBase
{
    /// <summary>Danh mục chức năng × thao tác để frontend dựng ma trận phân quyền.</summary>
    [HttpGet("danh-muc")]
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.Xem)]
    public async Task<ActionResult<DanhMucChucNangDto>> DanhMuc(CancellationToken ct)
        => Ok(await sender.Send(new LayDanhMucChucNangQuery(), ct));

    [HttpGet]
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.Xem)]
    public async Task<ActionResult<List<QuyenDto>>> DanhSach(CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachQuyenQuery(), ct));

    [HttpPost]
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuQuyenCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuQuyenCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaQuyenCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-06 — thiết lập chung của trung tâm.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/thiet-lap")]
public class ThietLapController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Xem)]
    public async Task<ActionResult<ThietLapDto>> Lay(CancellationToken ct)
        => Ok(await sender.Send(new LayThietLapQuery(), ct));

    [HttpPut]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        [FromBody] CapNhatThietLapCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
