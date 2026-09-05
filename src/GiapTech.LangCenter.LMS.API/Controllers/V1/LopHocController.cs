using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.DaoTao.LopHoc;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// FR-07 — lớp học.
///
/// Lưu ý về quyền: `[RequirePermission]` chỉ quyết định CÓ ĐƯỢC GỌI endpoint hay không, nó
/// không lọc dữ liệu. Việc giới hạn "chỉ lớp mình phụ trách" nằm ở `IPhamViLopHoc` trong từng
/// handler — bỏ nó đi thì giáo viên đọc được mọi lớp của trung tâm dù attribute vẫn đúng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lop-hoc")]
public class LopHocController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<LopHocDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] TrangThaiLopHoc? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachLopHocQuery(timKiem, trangThai, new ThamSoTrang(trang, soDong)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<LopHocDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayLopHocQuery(id), ct));

    /// <summary>Bước 1 của wizard — lưu nháp, lớp chưa hiện với người khác.</summary>
    [HttpPost]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoLopHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatLopHocCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Hoàn tất wizard: lớp rời trạng thái nháp và hiện với mọi người liên quan.</summary>
    [HttpPost("{id:guid}/hoan-tat")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> HoanTat(
        Guid id, [FromBody] HoanTatBody body, CancellationToken ct)
    {
        await sender.Send(new HoanTatLopHocCommand(id, body.NgayKhaiGiang), ct);
        return NoContent();
    }

    public record HoanTatBody(DateTimeOffset NgayKhaiGiang);

    /// <summary>Huỷ lớp — giữ toàn bộ lịch sử, khác hẳn xoá.</summary>
    [HttpPost("{id:guid}/huy")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> Huy(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyLopHocCommand(id), ct);
        return NoContent();
    }

    /// <summary>Xoá cứng — chỉ áp dụng cho lớp còn ở trạng thái nháp.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaLopHocNhapCommand(id), ct);
        return NoContent();
    }

    // ---------- Học viên trong lớp (bước 3 của wizard) ----------

    [HttpGet("{id:guid}/hoc-vien")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<List<HocVienTrongLopDto>>> HocVien(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayHocVienTrongLopQuery(id), ct));

    [HttpPost("{id:guid}/hoc-vien")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> ThemHocVien(
        Guid id, [FromBody] ThemHocVienBody body, CancellationToken ct)
    {
        await sender.Send(
            new ThemHocVienVaoLopCommand(id, body.HocVienIds, body.HocPhiApDung), ct);
        return NoContent();
    }

    public record ThemHocVienBody(List<Guid> HocVienIds, decimal? HocPhiApDung = null);

    [HttpDelete("{id:guid}/hoc-vien/{hocVienId:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> GoHocVien(Guid id, Guid hocVienId, CancellationToken ct)
    {
        await sender.Send(new GoHocVienKhoiLopCommand(id, hocVienId), ct);
        return NoContent();
    }
}
