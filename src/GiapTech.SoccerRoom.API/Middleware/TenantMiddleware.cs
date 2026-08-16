using System.Security.Claims;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Infrastructure.MultiTenancy;

namespace GiapTech.SoccerRoom.API.Middleware;

/// <summary>
/// Đọc claim tenant từ JWT và nạp vào <see cref="CurrentTenant"/> của request.
///
/// Đây là mắt xích biến JWT thành bộ lọc dữ liệu: thiếu nó thì Global Query Filter không
/// biết tenant nào và mọi truy vấn nghiệp vụ trả về rỗng. Phải chạy SAU UseAuthentication
/// (cần claim đã được giải mã) và TRƯỚC UseAuthorization.
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentTenant currentTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var giaTri = context.User.FindFirstValue(ClaimTenant.TenantId);

            if (Guid.TryParse(giaTri, out var tenantId))
            {
                currentTenant.Gan(tenantId);
            }
            else
            {
                // Token hợp lệ về chữ ký nhưng thiếu/hỏng claim tenant. Không thể phục vụ
                // an toàn: bỏ qua sẽ khiến filter chạy ở chế độ "không tenant" và lộ dữ liệu
                // của mọi CLB. Chặn tại đây.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { errorCode = "TOKEN_THIEU_TENANT" });
                return;
            }
        }

        await next(context);
    }
}
