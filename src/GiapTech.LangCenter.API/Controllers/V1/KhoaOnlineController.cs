using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.DaoTao.HocTapTrucTuyen;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// FR-26 — khoá học trực tuyến, kênh học tập thứ hai của LMS.
///
/// **Ba chức năng phân quyền, đừng gộp:**
///
/// | Chức năng | Ai | Việc |
/// |---|---|---|
/// | `KhoaOnline` | giáo vụ | soạn khoá và bài học |
/// | `GhiDanhKhoaOnline` | điều phối | cấp / thu quyền học |
/// | `HocOnline` | học viên | đọc bài, đánh dấu đã học |
///
/// Tách vì ba việc khác người làm. Gộp lại thì ai sửa được bài cũng cấp được quyền học — kể cả
/// cấp cho chính mình.
///
/// **`IPhamViKhoaOnline` lọc hàng**, `[RequirePermission]` chỉ gác cửa. Endpoint đọc bài gác
/// bằng `HocOnline.Xem` — quyền mà mọi học viên đều có — nên nếu tầng phạm vi hỏng thì một
/// người mua một khoá sẽ đọc được mọi khoá. Đó là chỗ rò rỉ nặng nhất của FR-26.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/khoa-online")]
public class KhoaOnlineController(ISender sender) : ControllerBase
{
    // ---------- Khoá ----------

    /// <summary>
    /// Gác bằng `HocOnline.Xem` chứ không `KhoaOnline.Xem`: học viên phải liệt kê được khoá
    /// mình học, mà họ không có quyền soạn. `IPhamViKhoaOnline.LocKhoa` lọc về đúng phạm vi.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.HocOnline, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhoaOnlineDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] TrangThaiKhoaOnline? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachKhoaOnlineQuery(timKiem, trangThai, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.KhoaOnline, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(TaoKhoaOnlineCommand lenh, CancellationToken ct)
        => Ok(await sender.Send(lenh, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.KhoaOnline, HanhDong.Sua)]
    public async Task<IActionResult> Sua(Guid id, SuaKhoaOnlineCommand lenh, CancellationToken ct)
    {
        if (id != lenh.Id) return BadRequest();
        await sender.Send(lenh, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.KhoaOnline, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhoaOnlineCommand(id), ct);
        return NoContent();
    }

    // ---------- Bài học ----------

    [HttpGet("{id:guid}/bai-hoc")]
    [RequirePermission(ChucNang.HocOnline, HanhDong.Xem)]
    public async Task<ActionResult<List<BaiHocOnlineDto>>> DanhSachBaiHoc(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachBaiHocQuery(id), ct));

    /// <summary>Nội dung bài — lọc ở mức BÀI, xem `IPhamViKhoaOnline.LocBaiHoc`.</summary>
    [HttpGet("bai-hoc/{baiHocId:guid}")]
    [RequirePermission(ChucNang.HocOnline, HanhDong.Xem)]
    public async Task<ActionResult<ChiTietBaiHocDto>> BaiHoc(Guid baiHocId, CancellationToken ct)
        => Ok(await sender.Send(new LayBaiHocQuery(baiHocId), ct));

    [HttpPost("bai-hoc")]
    [RequirePermission(ChucNang.KhoaOnline, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> LuuBaiHoc(LuuBaiHocCommand lenh, CancellationToken ct)
        => Ok(await sender.Send(lenh, ct));

    [HttpDelete("bai-hoc/{baiHocId:guid}")]
    [RequirePermission(ChucNang.KhoaOnline, HanhDong.Xoa)]
    public async Task<IActionResult> XoaBaiHoc(Guid baiHocId, CancellationToken ct)
    {
        await sender.Send(new XoaBaiHocCommand(baiHocId), ct);
        return NoContent();
    }

    /// <summary>
    /// Học viên tự đánh dấu đã học xong. `Them` chứ không `Sua`: đây là tạo một bản ghi tiến
    /// độ của chính mình, cùng khuôn với tự điểm danh (FR-10).
    /// </summary>
    [HttpPost("bai-hoc/{baiHocId:guid}/da-hoc")]
    [RequirePermission(ChucNang.HocOnline, HanhDong.Them)]
    public async Task<IActionResult> DanhDauDaHoc(Guid baiHocId, CancellationToken ct)
    {
        await sender.Send(new DanhDauDaHocCommand(baiHocId), ct);
        return NoContent();
    }

    // ---------- Ghi danh ----------

    [HttpGet("{id:guid}/ghi-danh")]
    [RequirePermission(ChucNang.GhiDanhKhoaOnline, HanhDong.Xem)]
    public async Task<ActionResult<List<GhiDanhKhoaOnlineDto>>> DanhSachGhiDanh(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachGhiDanhQuery(id), ct));

    [HttpPost("ghi-danh")]
    [RequirePermission(ChucNang.GhiDanhKhoaOnline, HanhDong.Them)]
    public async Task<IActionResult> CapQuyenHoc(CapQuyenHocCommand lenh, CancellationToken ct)
    {
        await sender.Send(lenh, ct);
        return NoContent();
    }

    [HttpPut("ghi-danh/{ghiDanhId:guid}")]
    [RequirePermission(ChucNang.GhiDanhKhoaOnline, HanhDong.Sua)]
    public async Task<IActionResult> SuaGhiDanh(
        Guid ghiDanhId, SuaGhiDanhCommand lenh, CancellationToken ct)
    {
        if (ghiDanhId != lenh.Id) return BadRequest();
        await sender.Send(lenh, ct);
        return NoContent();
    }

    [HttpDelete("ghi-danh/{ghiDanhId:guid}")]
    [RequirePermission(ChucNang.GhiDanhKhoaOnline, HanhDong.Xoa)]
    public async Task<IActionResult> ThuQuyenHoc(Guid ghiDanhId, CancellationToken ct)
    {
        await sender.Send(new ThuQuyenHocCommand(ghiDanhId), ct);
        return NoContent();
    }
}
