using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.TaiChinh.KhoanChi;
using GiapTech.SoccerRoom.Application.TaiChinh.Quy;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// FR-15, FR-16 — quỹ đội.
///
/// **Dữ liệu nhạy cảm** (xem docs/nghiep-vu/tai-chinh.md): Player chỉ được **Xem** tiến độ,
/// mọi thao tác đổi tiền đều yêu cầu quyền Sửa. Đó là lý do các endpoint đọc và ghi dùng hai
/// mức quyền khác nhau chứ không gộp một.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/quy")]
public class QuyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<QuyDto>>> DanhSach(
        [FromQuery] TrangThaiQuy? trangThai,
        [FromQuery] int trang = 1, [FromQuery] int soDong = 20, CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachQuyQuery(trangThai, new ThamSoTrang(trang, soDong)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xem)]
    public async Task<ActionResult<ChiTietQuyDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayChiTietQuyQuery(id), ct));

    /// <summary>Danh sách người còn nợ — nguồn cho nút sao chép nhắc nợ.</summary>
    [HttpGet("{id:guid}/con-no")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xem)]
    public async Task<ActionResult<List<DongGopDto>>> ConNo(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachNoQuery(id), ct));

    [HttpPost]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuQuyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuQuyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    /// <summary>Ghi nhận tiền đã thu của một người. Player KHÔNG được gọi (đặc tả FR-16).</summary>
    [HttpPut("dong-gop/{dongGopId:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Sua)]
    public async Task<IActionResult> GhiNhanThu(
        Guid dongGopId, [FromBody] GhiNhanThuCommand command, CancellationToken ct)
    {
        await sender.Send(command with { DongGopId = dongGopId }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaQuyCommand(id), ct);
        return NoContent();
    }
}

/// <summary>Khoản chi và số dư quỹ — ngoài phạm vi FR-15/16 nhưng cần để biết quỹ còn bao nhiêu.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tai-chinh")]
public class TaiChinhController(ISender sender) : ControllerBase
{
    [HttpGet("tong-quan")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xem)]
    public async Task<ActionResult<TongQuanTaiChinhDto>> TongQuan(CancellationToken ct)
        => Ok(await sender.Send(new LayTongQuanQuery(), ct));

    [HttpGet("khoan-chi")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhoanChiDto>>> DanhSachChi(
        [FromQuery] Guid? quyId,
        [FromQuery] int trang = 1, [FromQuery] int soDong = 20, CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachChiQuery(quyId, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost("khoan-chi")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Them)]
    public async Task<ActionResult<Guid>> TaoChi(
        [FromBody] LuuKhoanChiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("khoan-chi/{id:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhatChi(
        Guid id, [FromBody] LuuKhoanChiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("khoan-chi/{id:guid}")]
    [RequirePermission(ChucNang.TaiChinh, HanhDong.Xoa)]
    public async Task<IActionResult> XoaChi(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhoanChiCommand(id), ct);
        return NoContent();
    }
}
