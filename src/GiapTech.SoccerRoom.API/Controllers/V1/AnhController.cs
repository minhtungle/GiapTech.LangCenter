using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Anh;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Ảnh đại diện cầu thủ (FR-04) và logo / ảnh bìa CLB (FR-06).
///
/// **API làm proxy**, không dùng presigned URL: MinIO không expose ra Internet (quy tắc #6),
/// và đi qua API thì mỗi lần đọc đều kiểm được tenant. Đánh đổi là mỗi ảnh tốn một vòng qua
/// API — bù lại bằng cache header ở endpoint đọc.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/anh")]
public class AnhController(ISender sender, ILuuTruAnh luuTru) : ControllerBase
{
    /// <summary>
    /// Đọc ảnh theo khoá.
    ///
    /// Khoá chứa dấu `/` nên dùng `{*khoa}` để bắt cả đường dẫn. Cách ly tenant kiểm ở tầng
    /// lưu trữ (`MinioLuuTruAnh.TaiVe`) — kho lưu trữ không có Global Query Filter.
    ///
    /// Chỉ cần quyền **Xem cầu thủ**: ảnh hiện ở nhiều màn (đội hình, xếp hạng, sidebar), bắt
    /// quyền riêng cho từng loại ảnh sẽ khiến avatar biến mất ở nửa số màn.
    /// </summary>
    [HttpGet("{*khoa}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xem)]
    public async Task<IActionResult> Doc(string khoa, CancellationToken ct)
    {
        var anh = await luuTru.TaiVe(khoa, ct);
        if (anh is null) return NotFound();

        // Ảnh bất biến: khoá chứa GUID nên đổi ảnh là đổi khoá. Cache dài để trình duyệt
        // không hỏi lại — không có bước này thì mỗi lần mở danh sách là ngần ấy request.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";

        return File(anh.NoiDung, anh.LoaiNoiDung);
    }

    [HttpPost("cau-thu/{cauThuId:guid}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiAnhCauThu(
        Guid cauThuId, IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.CauThu, cauThuId, tep.OpenReadStream(), tep.ContentType),
            ct));

    [HttpDelete("cau-thu/{cauThuId:guid}")]
    [RequirePermission(ChucNang.CauThu, HanhDong.Sua)]
    public async Task<IActionResult> XoaAnhCauThu(Guid cauThuId, CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.CauThu, cauThuId), ct);
        return NoContent();
    }

    [HttpPost("clb/logo")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiLogo(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.Logo, null, tep.OpenReadStream(), tep.ContentType), ct));

    [HttpPost("clb/anh-bia")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiAnhBia(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.AnhBia, null, tep.OpenReadStream(), tep.ContentType), ct));

    /// <summary>
    /// Mã QR chuyển khoản quỹ. Quyền `ThietLapChung.Sua` như logo — nó là thông tin CLB.
    ///
    /// Ảnh này KHÔNG lên Cộng đồng (khác logo và ảnh bìa): số tài khoản quỹ là dữ liệu nội bộ.
    /// </summary>
    [HttpPost("clb/qr-chuyen-khoan")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiAnhQr(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.AnhQr, null, tep.OpenReadStream(), tep.ContentType), ct));

    [HttpDelete("clb/qr-chuyen-khoan")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaAnhQr(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.AnhQr, null), ct);
        return NoContent();
    }

    [HttpDelete("clb/logo")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaLogo(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.Logo, null), ct);
        return NoContent();
    }

    [HttpDelete("clb/anh-bia")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaAnhBia(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.AnhBia, null), ct);
        return NoContent();
    }
}
