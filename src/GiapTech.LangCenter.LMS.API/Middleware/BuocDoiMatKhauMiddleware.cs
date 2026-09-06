using System.Security.Claims;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.API.Middleware;

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
        "/api/v1/auth/dat-lai-mat-khau",
        "/api/v1/auth/lam-moi-token",
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

        // Claim tài khoản, không phải NameIdentifier: cờ buộc đổi mật khẩu thuộc về TÀI KHOẢN.
        // Token cũ (trước 07/09/2026) không có claim này → cho qua, vì chặn ở đây sẽ khoá cứng
        // mọi phiên đang mở mà không có đường ra.
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTenant.TaiKhoanId), out var taiKhoanId))
        {
            await next(context);
            return;
        }

        var phaiDoi = await db.TaiKhoans
            .Where(u => u.Id == taiKhoanId)
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
