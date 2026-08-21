using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.QuanTri.CauThu;
using GiapTech.SoccerRoom.Application.QuanTri.Quyen;
using GiapTech.SoccerRoom.Application.QuanTri.TaiKhoan;
using GiapTech.SoccerRoom.Application.QuanTri.ThietLap;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>FR-04 — hồ sơ cầu thủ.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cau-thu")]
public class CauThuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xem)]
    /// <param name="loc">
    /// `DangDa` (mặc định) · `DaNghi` · `Tatca`. Mặc định là "đang đá" vì mọi chỗ chọn người đều
    /// gọi endpoint này và không chỗ nào muốn thấy người đã nghỉ (21/08).
    /// </param>
    public async Task<ActionResult<KetQuaTrang<CauThuDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        [FromQuery] LocTrangThaiCauThu loc = LocTrangThaiCauThu.DangDa,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachCauThuQuery(timKiem, new ThamSoTrang(trang, soDong), loc), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xem)]
    public async Task<ActionResult<CauThuDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayCauThuQuery(id), ct));

    [HttpPost]
    [RequirePermission(ChucNang.CauThu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoCauThuCommand command, CancellationToken ct)
    {
        var id = await sender.Send(command, ct);
        return CreatedAtAction(nameof(ChiTiet), new { id, version = "1.0" }, id);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatCauThuCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaCauThuCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Cho cầu thủ nghỉ thi đấu — **không xoá**, lịch sử và thống kê giữ nguyên.
    ///
    /// Cần quyền SỬA chứ không XOÁ: đây là đổi trạng thái, dữ liệu không mất gì. Bắt quyền xoá
    /// thì trưởng nhóm không có quyền xoá lại phải nhờ admin cho một người nghỉ đá.
    /// </summary>
    [HttpPost("{id:guid}/nghi-thi-dau")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Sua)]
    public async Task<ActionResult<KetQuaChoNghi>> NghiThiDau(
        Guid id, [FromBody] NghiThiDauBody body, CancellationToken ct)
        => Ok(await sender.Send(
            new ChoNghiThiDauCommand(id, body.NgayNghi, body.KhoaTaiKhoan), ct));

    /// <summary>Cho đá lại — người nghỉ rồi quay lại là chuyện thường.</summary>
    [HttpPost("{id:guid}/da-lai")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Sua)]
    public async Task<IActionResult> DaLai(Guid id, CancellationToken ct)
    {
        await sender.Send(new ChoDaLaiCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Tài khoản đang hoạt động gắn với cầu thủ này — để UI hỏi "khoá luôn tài khoản?" trước khi
    /// cho nghỉ, và ẩn câu hỏi đó nếu đó là tài khoản của chính người đang đăng nhập.
    /// </summary>
    [HttpGet("{id:guid}/tai-khoan")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xem)]
    public async Task<ActionResult<TaiKhoanCuaCauThu>> TaiKhoan(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new TaiKhoanCuaCauThuQuery(id), ct));
}

public record NghiThiDauBody(DateOnly? NgayNghi = null, bool KhoaTaiKhoan = false);

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

/// <summary>FR-06 — thiết lập chung CLB.</summary>
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
