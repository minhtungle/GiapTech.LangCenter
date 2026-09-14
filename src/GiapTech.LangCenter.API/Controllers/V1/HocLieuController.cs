using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.DaoTao.HocLieu;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>FR-11 — bài tập giao trong buổi học.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bai-tap")]
public class BaiTapController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.BaiTap, HanhDong.Xem)]
    public async Task<ActionResult<List<BaiTapDto>>> TheoLop(
        [FromQuery] Guid lopHocId,
        [FromQuery] Guid? buoiHocId,
        CancellationToken ct)
        => Ok(await sender.Send(new LayBaiTapCuaLopQuery(lopHocId, buoiHocId), ct));

    [HttpPost]
    [RequirePermission(ChucNang.BaiTap, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoBaiTapCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.BaiTap, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatBaiTapCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.BaiTap, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaBaiTapCommand(id), ct);
        return NoContent();
    }

    // ---------- Bài nộp ----------

    [HttpGet("{id:guid}/bai-nop")]
    [RequirePermission(ChucNang.BaiNopBaiTap, HanhDong.Xem)]
    public async Task<ActionResult<List<BaiNopDto>>> BaiNop(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayBaiNopQuery(id), ct));

    /// <summary>
    /// Học viên nộp bài. Không nhận id học viên — lấy từ token, nên không nộp hộ được.
    /// </summary>
    [HttpPost("{id:guid}/nop")]
    [RequirePermission(ChucNang.BaiNopBaiTap, HanhDong.TuLam)]
    public async Task<ActionResult<Guid>> Nop(
        Guid id, [FromBody] NopBaiBody body, CancellationToken ct)
        => Ok(await sender.Send(new NopBaiCommand(id, body.NoiDung), ct));

    public record NopBaiBody(string? NoiDung);
}

/// <summary>Chấm bài — endpoint riêng, tách khỏi việc sửa nội dung bài nộp.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bai-nop")]
public class BaiNopController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Chấm điểm. Command cố ý KHÔNG có trường nội dung: giáo viên có quyền `Sua` trên bài nộp
    /// là để chấm, không phải để sửa bài của học viên.
    /// </summary>
    [HttpPost("{id:guid}/cham")]
    [RequirePermission(ChucNang.BaiNopBaiTap, HanhDong.Cham)]
    public async Task<IActionResult> Cham(
        Guid id, [FromBody] ChamBody body, CancellationToken ct)
    {
        await sender.Send(new ChamBaiNopCommand(id, body.Diem, body.NhanXet), ct);
        return NoContent();
    }

    public record ChamBody(decimal? Diem, string? NhanXet);
}

/// <summary>FR-13 — tài liệu giảng dạy.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tai-lieu")]
public class TaiLieuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.TaiLieu, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<TaiLieuDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] Guid? lopHocId,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachTaiLieuQuery(timKiem, lopHocId, new ThamSoTrang(trang, soDong)), ct));

    [HttpPost]
    [RequirePermission(ChucNang.TaiLieu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoTaiLieuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.TaiLieu, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatTaiLieuCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.TaiLieu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaTaiLieuCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// Tệp đính kèm — dùng chung cho bài tập, bài nộp, bài kiểm tra, bài làm, tài liệu.
///
/// Gác bằng <see cref="ChucNang.Anh"/> vì đây là endpoint dùng CHUNG cho mọi module: gác bằng
/// quyền của một module cụ thể sẽ khiến người thiếu quyền đó không tải được tệp ở nơi khác —
/// đúng cái bẫy đã gặp với endpoint đọc ảnh.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tep")]
public class TepController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequirePermission(ChucNang.Anh, HanhDong.Them)]
    public async Task<ActionResult<TepDto>> TaiLen(
        [FromQuery] LoaiDinhKem loai,
        [FromQuery] Guid doiTuongId,
        IFormFile tep,
        CancellationToken ct)
        => Ok(await sender.Send(new TaiTepCommand(
            loai, doiTuongId, tep.OpenReadStream(), tep.ContentType, tep.FileName), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.Anh, HanhDong.Xem)]
    public async Task<IActionResult> TaiVe(Guid id, CancellationToken ct)
    {
        var tep = await sender.Send(new TaiTepVeQuery(id), ct);

        // Đặt tên tệp khi tải về theo TÊN GỐC, không phải khoá GUID — người dùng tải "de-thi
        // -giua-ky.pdf" chứ không phải "a3f9....pdf".
        return File(tep.NoiDung, tep.LoaiNoiDung, tep.TenGoc);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.Anh, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaTepCommand(id), ct);
        return NoContent();
    }
}
