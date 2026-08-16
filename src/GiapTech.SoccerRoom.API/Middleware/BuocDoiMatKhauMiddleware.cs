using System.Security.Claims;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.API.Middleware;

/// <summary>
/// Chặn mọi endpoint nghiệp vụ khi tài khoản còn cờ <c>PhaiDoiMatKhau</c> (FR-01).
///
/// Vì sao cần ở tầng middleware: đặc tả nói "chuyển hướng đổi mật khẩu TRƯỚC KHI vào hệ
/// thống". Nếu chỉ dựa vào frontend đọc cờ trong response đăng nhập thì token vẫn hợp lệ và
/// gọi thẳng API vẫn qua — tài khoản admin/123456 mà ai cũng biết sẽ dùng được ngay.
/// </summary>
public class BuocDoiMatKhauMiddleware(RequestDelegate next)
{
    /// <summary>Các đường dẫn vẫn cho phép khi đang bị buộc đổi mật khẩu.</summary>
    private static readonly string[] DuongDanDuocPhep =
    [
        "/api/v1/auth/dang-nhap",
        "/api/v1/auth/doi-mat-khau",
        "/api/v1/auth/quen-mat-khau",
        "/swagger",
        "/health"
    ];

    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true || DuocPhep(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await next(context);
            return;
        }

        var phaiDoi = await db.NguoiDungs
            .Where(u => u.Id == userId)
            .Select(u => u.PhaiDoiMatKhau)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (phaiDoi)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { errorCode = MaLoi.PhaiDoiMatKhau, duLieu = (object?)null });
            return;
        }

        await next(context);
    }

    private static bool DuocPhep(PathString path) =>
        DuongDanDuocPhep.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
}
