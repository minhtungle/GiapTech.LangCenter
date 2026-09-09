using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Anh;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// Logo / ảnh bìa / mã QR của trung tâm (FR-06).
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
    /// Gác bằng <see cref="ChucNang.Anh"/> chứ không bằng quyền của một module cụ thể: ảnh
    /// hiện ở nhiều màn, gác theo module sẽ khiến ảnh biến mất ở nửa số màn với người thiếu
    /// đúng quyền đó — một cách âm thầm, không có thông báo lỗi nào người dùng hiểu được.
    /// </summary>
    [HttpGet("{*khoa}")]
    [RequirePermission(ChucNang.Anh, HanhDong.Xem)]
    public async Task<IActionResult> Doc(string khoa, CancellationToken ct)
    {
        var anh = await luuTru.TaiVe(khoa, ct);
        if (anh is null) return NotFound();

        // Ảnh bất biến: khoá chứa GUID nên đổi ảnh là đổi khoá. Cache dài để trình duyệt
        // không hỏi lại — không có bước này thì mỗi lần mở danh sách là ngần ấy request.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";

        return File(anh.NoiDung, anh.LoaiNoiDung);
    }

    [HttpPost("trung-tam/logo")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiLogo(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.Logo, tep.OpenReadStream(), tep.ContentType), ct));

    [HttpPost("trung-tam/anh-bia")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiAnhBia(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.AnhBia, tep.OpenReadStream(), tep.ContentType), ct));

    /// <summary>
    /// Mã QR chuyển khoản. Quyền `ThietLapChung.Sua` như logo — nó là thông tin trung tâm.
    /// </summary>
    [HttpPost("trung-tam/qr-chuyen-khoan")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<ActionResult<string>> TaiAnhQr(IFormFile tep, CancellationToken ct)
        => Ok(await sender.Send(
            new TaiAnhLenCommand(LoaiAnh.AnhQr, tep.OpenReadStream(), tep.ContentType), ct));

    [HttpDelete("trung-tam/qr-chuyen-khoan")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaAnhQr(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.AnhQr), ct);
        return NoContent();
    }

    [HttpDelete("trung-tam/logo")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaLogo(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.Logo), ct);
        return NoContent();
    }

    [HttpDelete("trung-tam/anh-bia")]
    [RequirePermission(ChucNang.ThietLapChung, HanhDong.Sua)]
    public async Task<IActionResult> XoaAnhBia(CancellationToken ct)
    {
        await sender.Send(new XoaAnhCommand(LoaiAnh.AnhBia), ct);
        return NoContent();
    }
}
