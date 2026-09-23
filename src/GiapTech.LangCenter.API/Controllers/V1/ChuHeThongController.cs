using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.API.RateLimit;
using GiapTech.LangCenter.Application.ChuHeThong;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// Site của **chủ sản phẩm** — quản vòng đời tenant (ADR-0009).
///
/// Thay endpoint tự đăng ký ẩn danh `/dang-ky-trung-tam` (đã tắt mặc định từ 22/09/2026 vì
/// chủ sản phẩm chốt đóng hẳn — nợ N3) bằng đường **có xác thực**.
///
/// ## Cố ý rất hẹp
///
/// Chỉ quản **vòng đời tenant**: xem danh sách, tạo mới, gắn domain. **Không** đọc dữ liệu
/// nghiệp vụ bên trong tenant (học viên, học phí, lớp học) — muốn đọc phải chọc thủng Global
/// Query Filter, và nếu thật sự cần thì phải là một ADR mới có lý do viết ra.
///
/// Cũng **không có xoá tenant**: xoá hàng loạt dữ liệu thật thuộc quy tắc #1, phải làm tay có
/// backup.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chu-he-thong")]
public class ChuHeThongController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Đăng nhập chủ hệ thống. Không có mã trung tâm — tài khoản này không thuộc trung tâm nào.
    ///
    /// `[AllowAnonymous]` vì chưa có token; hạn mức dùng chung policy `XacThuc` với đăng nhập
    /// tenant. Đây là endpoint ẩn danh **thứ 10**, đã cập nhật bound trong
    /// `MoiEndpointPhaiDuocGacTests` kèm lý do.
    /// </summary>
    [HttpPost("dang-nhap")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.XacThuc)]
    [ProducesResponseType<DangNhapChuResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DangNhapChuResult>> DangNhap(
        [FromBody] DangNhapChuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    /// <summary>
    /// Danh sách trung tâm — tên, mã, domain, ngày tạo.
    ///
    /// Đây là endpoint duy nhất trong hệ thống đọc **mọi** tenant. An toàn được vì
    /// `TENANT` không phải `ITenantEntity` nên vốn không bị Query Filter, và vì
    /// `[ChiChuHeThong]` chặn mọi token có `tenant_id`.
    /// </summary>
    [HttpGet("trung-tam")]
    [ChiChuHeThong]
    [ProducesResponseType<IReadOnlyList<TrungTamTomTatDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrungTamTomTatDto>>> DanhSachTrungTam(
        CancellationToken ct)
        => Ok(await sender.Send(new DanhSachTrungTamQuery(), ct));

    /// <summary>
    /// Tạo trung tâm mới — thay endpoint tự đăng ký ẩn danh (đóng nợ N3).
    ///
    /// Trả **mật khẩu admin ở dạng thô, đúng một lần này**. Server chỉ giữ bản băm, nên không
    /// có đường nào lấy lại — người tạo phải chép ngay. Đó là đánh đổi có chủ ý: phương án
    /// còn lại là mật khẩu cố định ai cũng biết, đúng lỗ hổng đã vá 22/09/2026.
    /// </summary>
    [HttpPost("trung-tam")]
    [ChiChuHeThong]
    [ProducesResponseType<TrungTamVuaTaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrungTamVuaTaoDto>> TaoTrungTam(
        [FromBody] TaoTrungTamCommand command, CancellationToken ct)
    {
        var dto = await sender.Send(command, ct);
        return CreatedAtAction(nameof(DanhSachTrungTam), new { }, dto);
    }

    /// <summary>
    /// Gắn / đổi / gỡ domain của một trung tâm (ADR-0008).
    ///
    /// Gỡ domain (truyền `null`) là thao tác an toàn: trung tâm rơi về đường mã trung tâm,
    /// không mất quyền truy cập. Đó chính là lý do ADR-0008 giữ hai đường cùng sống.
    /// </summary>
    [HttpPut("trung-tam/{id:guid}/domain")]
    [ChiChuHeThong]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GanDomain(
        Guid id, [FromBody] GanDomainCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TrungTamId = id }, ct);
        return NoContent();
    }
}
