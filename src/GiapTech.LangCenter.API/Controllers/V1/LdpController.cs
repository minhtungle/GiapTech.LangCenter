using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.API.RateLimit;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Ldp;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// LDP — soạn nội dung trang đích (FR-30). **Sau đăng nhập.**
///
/// Đường công khai nằm ở <see cref="LdpCongKhaiController"/>, tách hẳn file để không ai lỡ
/// thêm một endpoint ẩn danh vào giữa nhóm có gác.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ldp")]
public class LdpController(ISender sender) : ControllerBase
{
    /// <summary>Nội dung trang để soạn — tự tạo trang rỗng nếu tenant chưa có.</summary>
    [HttpGet("trang-dich")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.Xem)]
    [ProducesResponseType<TrangDichDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TrangDichDto>> TrangDich(CancellationToken ct)
        => Ok(await sender.Send(new TrangDichQuery(), ct));

    [HttpPut("khoi/{id:guid}")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SuaKhoi(
        Guid id, [FromBody] SuaKhoiCommand command, CancellationToken ct)
    {
        await sender.Send(command with { KhoiId = id }, ct);
        return NoContent();
    }

    [HttpPost("khoi/{id:guid}/muc")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.Sua)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Guid>> ThemMuc(
        Guid id, [FromBody] ThemMucCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { KhoiId = id }, ct));

    [HttpPut("muc/{id:guid}")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SuaMuc(
        Guid id, [FromBody] SuaMucCommand command, CancellationToken ct)
    {
        await sender.Send(command with { MucId = id }, ct);
        return NoContent();
    }

    [HttpDelete("muc/{id:guid}")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.Sua)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> XoaMuc(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaMucCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Xuất bản / gỡ xuất bản. Quyền RIÊNG `XuatBan`, không phải `Sua` — xem FR-30.
    /// </summary>
    [HttpPut("xuat-ban")]
    [RequirePermission(ChucNang.TrangDich, HanhDong.XuatBan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> XuatBan(
        [FromBody] XuatBanCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    // ---------------------------------------------------------------------------------
    // Liên hệ từ form
    // ---------------------------------------------------------------------------------

    [HttpGet("lien-he")]
    [RequirePermission(ChucNang.LienHeLanding, HanhDong.Xem)]
    [ProducesResponseType<IReadOnlyList<LienHeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LienHeDto>>> DanhSachLienHe(
        [FromQuery] bool? daXuLy, CancellationToken ct)
        => Ok(await sender.Send(new DanhSachLienHeQuery(daXuLy), ct));

    /// <summary>Chuyển liên hệ thành khách hàng CRM — cầu nối LDP → CRM (FR-30).</summary>
    [HttpPost("lien-he/{id:guid}/chuyen-crm")]
    [RequirePermission(ChucNang.LienHeLanding, HanhDong.ChuyenCrm)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Guid>> ChuyenSangCrm(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new ChuyenSangCrmCommand(id), ct));

    [HttpDelete("lien-he/{id:guid}")]
    [RequirePermission(ChucNang.LienHeLanding, HanhDong.Xoa)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> XoaLienHe(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaLienHeCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// LDP — đường CÔNG KHAI (FR-30). Ai cũng gọi được, không cần đăng nhập.
///
/// ## File riêng là cố ý
///
/// Nhóm endpoint ẩn danh để trong một file, tách khỏi nhóm có gác: trộn lẫn thì một
/// `[AllowAnonymous]` thêm nhầm vào giữa danh sách `[RequirePermission]` rất khó thấy khi
/// review. `MoiEndpointPhaiDuocGacTests` vẫn chốt tổng số, nhưng tách file làm con số đó khớp
/// với thứ người đọc nhìn thấy.
///
/// ## Tenant từ đâu
///
/// `TenantMiddleware` giải từ domain (ADR-0008) hoặc từ đường `/t/{mã}` mà frontend gọi kèm.
/// Người gọi không truyền tenant vào được — đó là điều làm nhóm endpoint này an toàn.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ldp")]
public class LdpCongKhaiController(
    ISender sender, IAppDbContext db, ILuuTruAnh luuTru, ICurrentTenant tenant)
    : ControllerBase
{
    /// <summary>
    /// Nội dung trang công khai. 404 khi chưa xuất bản hoặc tenant chưa có trang.
    ///
    /// 404 chứ không 200 với thân rỗng: "trung tâm này chưa có trang" và "trang tồn tại nhưng
    /// trống" là hai chuyện khác nhau, và trình duyệt/Google cần biết đúng cái nào.
    /// </summary>
    [HttpGet("cong-khai")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.TraCuu)]
    [ProducesResponseType<TrangCongKhaiDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrangCongKhaiDto>> CongKhai(CancellationToken ct)
        => await sender.Send(new TrangCongKhaiQuery(), ct) is { } dto
            ? Ok(dto)
            : NotFound();

    /// <summary>
    /// Ảnh của một khối — ENDPOINT ẨN DANH.
    ///
    /// Nhận **id khối**, không nhận khoá ảnh: khoá mang `tenantId` ở đầu, đưa ra ngoài là tặng
    /// người lạ một GUID thật. Server tự tra khoá, và **chỉ tra trong trang đã xuất bản của
    /// tenant hiện tại** — đúng khuôn `AuthController.Logo` (22/09/2026).
    /// </summary>
    [HttpGet("anh/khoi/{id:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.TraCuu)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnhKhoi(Guid id, CancellationToken ct)
    {
        var khoa = await db.KhoiLdps
            .AsNoTracking()
            .Where(k => k.Id == id && k.Hien && k.TrangDich.DaXuatBan && k.KhoaAnh != null)
            .Select(k => k.KhoaAnh)
            .FirstOrDefaultAsync(ct);

        return await TraAnh(khoa, ct);
    }

    /// <summary>Ảnh của một mục — cùng nguyên tắc với <see cref="AnhKhoi"/>.</summary>
    [HttpGet("anh/muc/{id:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.TraCuu)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnhMuc(Guid id, CancellationToken ct)
    {
        var khoa = await db.MucLdps
            .AsNoTracking()
            .Where(m => m.Id == id && m.KhoiLdp.Hien && m.KhoiLdp.TrangDich.DaXuatBan
                        && m.KhoaAnh != null)
            .Select(m => m.KhoaAnh)
            .FirstOrDefaultAsync(ct);

        return await TraAnh(khoa, ct);
    }

    private async Task<IActionResult> TraAnh(string? khoa, CancellationToken ct)
    {
        if (khoa is null || tenant.TenantId is null) return NotFound();

        var anh = await luuTru.TaiVe(khoa, ct);
        if (anh is null) return NotFound();

        // `public`: ảnh landing là nội dung công khai, để proxy/CDN cache được. Khoá chứa
        // GUID nên đổi ảnh là đổi khoá — cache dài vẫn đúng.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(anh.NoiDung, anh.LoaiNoiDung);
    }

    /// <summary>
    /// Khách vãng lai gửi form đăng ký tư vấn — ENDPOINT ẨN DANH **GHI DỮ LIỆU**.
    ///
    /// Loại nguy hiểm nhất. Bốn lớp bảo vệ: rate limit riêng (5/phút, chặt hơn `TraCuu` vì mỗi
    /// request tạo một hàng DB), honeypot, giới hạn độ dài ở validator, và không trả dữ liệu
    /// gì về.
    ///
    /// Luôn trả **204 kể cả khi bỏ qua vì honeypot**: nói cho bot biết nó bị phát hiện chỉ
    /// khiến người viết bot sửa lại.
    /// </summary>
    [HttpPost("lien-he")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.GuiLienHe)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuiLienHe(
        [FromBody] GuiLienHeCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
