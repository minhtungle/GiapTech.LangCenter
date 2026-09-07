using Asp.Versioning;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// Thông tin về CHÍNH người đang đăng nhập.
///
/// Không gác `[RequirePermission]`: ai đăng nhập được cũng phải biết mình có quyền gì, nếu
/// không thì frontend không dựng nổi menu. Endpoint chỉ trả quyền của **chính** phiên hiện
/// tại — không nhận tham số id nên không có đường dò quyền người khác.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/toi")]
public class ToiController(
    IQuyenService quyenService, ICurrentTenant tenant, ICurrentUser currentUser)
    : ControllerBase
{
    /// <summary>Một dòng quyền: chức năng + thao tác.</summary>
    public record QuyenCuaToi(string ChucNang, string HanhDong);

    /// <summary>
    /// Toàn bộ quyền hiệu lực, để frontend ẩn menu và nút.
    ///
    /// **Ẩn ở frontend là tiện lợi, không phải bảo vệ** — mọi endpoint vẫn tự gác quyền của
    /// nó. Danh sách này chỉ để người dùng không phải bấm vào rồi mới biết mình không được
    /// phép.
    /// </summary>
    [HttpGet("quyen")]
    public async Task<ActionResult<List<QuyenCuaToi>>> Quyen(CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return Ok(new List<QuyenCuaToi>());

        var ds = await quyenService.LayTatCaQuyenAsync(tid, tkId, ct);

        return Ok(ds
            .Select(x => new QuyenCuaToi(x.ChucNang, x.HanhDong.ToString()))
            .OrderBy(x => x.ChucNang).ThenBy(x => x.HanhDong)
            .ToList());
    }
}
