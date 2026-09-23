using GiapTech.LangCenter.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GiapTech.LangCenter.API.Authorization;

/// <summary>
/// Chỉ cho **tài khoản chủ hệ thống** gọi endpoint này (ADR-0009).
///
/// Đây là chiều NGƯỢC LẠI của hàng rào chính. Hai chiều, mỗi chiều một cơ chế:
///
/// | Chiều | Ai chặn |
/// |---|---|
/// | Token chủ → API nghiệp vụ | `TenantMiddleware` (token chủ thiếu `tenant_id` ⇒ 401) |
/// | Token tenant → API site chủ | **attribute này** (token có `tenant_id` ⇒ 403) |
///
/// Không gộp được vào `[RequirePermission]`: cơ chế đó đọc bảng `QUYEN_CHUC_NANG` của một
/// tenant, mà tài khoản chủ không thuộc tenant nào nên không có hàng nào để đọc.
///
/// Dùng `[AllowAnonymous]` + attribute này cho endpoint đăng nhập chủ; các endpoint còn lại
/// của site chủ dùng attribute này một mình (vẫn cần token hợp lệ).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ChiChuHeThongAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(
                new { errorCode = "CHUA_DANG_NHAP" });
            return;
        }

        var loai = user.FindFirst(ClaimTenant.LoaiDanhTinh)?.Value;
        var coTenant = user.FindFirst(ClaimTenant.TenantId) is not null;

        // Kiểm CẢ HAI: phải đúng loại chủ VÀ không mang tenant.
        //
        // Không chỉ kiểm `loai == "chu"`: nếu sau này có đường nào phát token vừa mang loại
        // chủ vừa mang tenant thì token đó sẽ đi lọt cả hai chiều. Kiểm thêm vế thứ hai làm
        // cho một lỗi như vậy thành vô hại thay vì thành lỗ hổng.
        if (loai != ClaimTenant.LoaiChuHeThong || coTenant)
        {
            context.Result = new ObjectResult(new { errorCode = "KHONG_PHAI_CHU_HE_THONG" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
