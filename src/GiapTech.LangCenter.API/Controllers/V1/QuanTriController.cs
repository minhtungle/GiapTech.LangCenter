using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.QuanTri.Quyen;
using GiapTech.LangCenter.Application.QuanTri.NguoiDung;
using GiapTech.LangCenter.Application.QuanTri.NhatKy;
using GiapTech.LangCenter.Application.QuanTri.TaiKhoan;
using GiapTech.LangCenter.Application.QuanTri.ThietLap;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// FR-03 — người dùng (hồ sơ con người).
///
/// Dùng chung chức năng phân quyền <c>TaiKhoan</c> chứ không thêm chức năng thứ 17: ai quản lý
/// được tài khoản thì quản lý được hồ sơ. Thêm hằng mới vào `ChucNang` sẽ làm admin của mọi
/// trung tâm ĐANG TỒN TẠI bị 403 trên màn mới cho tới khi chạy bổ khuyết quyền — bẫy đã gặp ở
/// giai đoạn 0.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nguoi-dung")]
public class NguoiDungController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.HoSoNguoiDung, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<NguoiDungDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] LoaiNguoiDung? loaiNguoiDung,
        [FromQuery] TrangThaiNhanSu? trangThaiNhanSu,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachNguoiDungQuery(
            timKiem, loaiNguoiDung, trangThaiNhanSu, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.HoSoNguoiDung, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoNguoiDungCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.HoSoNguoiDung, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatNguoiDungCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.HoSoNguoiDung, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaNguoiDungCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-04 — tài khoản đăng nhập.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tai-khoan")]
public class TaiKhoanController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<TaiKhoanDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] TrangThaiNguoiDung? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachTaiKhoanQuery(timKiem, trangThai, new ThamSoTrang(trang, soDong)), ct));

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

/// <summary>
/// FR-16 — nhật ký thao tác hệ thống.
///
/// **Chỉ có endpoint đọc.** Nhật ký chỉ ghi thêm; không có `POST`/`PUT`/`DELETE` nào vì nhật
/// ký sửa được thì không còn là nhật ký. Việc ghi do `NhatKyBehavior` tự làm ở pipeline.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nhat-ky")]
public class NhatKyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.NhatKyHeThong, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<NhatKyDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] string? chucNang,
        [FromQuery] HanhDongNhatKy? hanhDong,
        [FromQuery] Guid? nguoiDungId,
        [FromQuery] bool? chiThatBai,
        [FromQuery] DateTimeOffset? tuNgay,
        [FromQuery] DateTimeOffset? denNgay,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 30,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayNhatKyQuery(
            timKiem, chucNang, hanhDong, nguoiDungId, chiThatBai,
            tuNgay, denNgay, new ThamSoTrang(trang, soDong)), ct));
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
    [RequirePermission(ChucNang.PhanQuyen, HanhDong.CauHinhQuyen)]
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
