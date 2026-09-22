using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GiapTech.LangCenter.API.Middleware;

/// <summary>
/// **Một phiên mỗi tài khoản** — chặn token của phiên đã bị đăng nhập nơi khác đẩy ra
/// (20/09/2026).
///
/// Yêu cầu chủ sản phẩm: *"chỉ cho phép 1 người đăng nhập tài khoản cùng lúc"*. Chốt phương án
/// **đẩy phiên cũ ra** (người vừa đăng nhập được vào, như Facebook/Zalo) và **có hiệu lực
/// ngay**.
///
/// ## Vì sao phải ở middleware, không chỉ thu hồi refresh token
///
/// JWT là stateless: server không tra DB mỗi request, nên thu hồi refresh token **không** đá
/// phiên cũ ra — họ vẫn gọi API bình thường cho tới khi access token hết hạn (**60 phút**).
/// Một tiếng hai người dùng song song thì không còn là "chỉ 1 người cùng lúc".
///
/// ## So `jti`, không thêm claim mới
///
/// `jti` đã có sẵn trong mọi token từ trước. Thêm claim mới là **thay đổi phá vỡ tương thích**
/// (quy tắc #1): token đang lưu hành không có claim đó, và mọi người đang mở app sẽ bị đá ra
/// ngay lúc triển khai.
///
/// ## Trường hợp cho qua
///
/// - **Đường dẫn xác thực** (`/auth/*`) — chặn ở đây thì chính lệnh đăng nhập cũng bị chặn, và
///   người bị đẩy ra không có đường quay lại.
/// - **`PhienHienTai` rỗng** = tài khoản **chưa từng đăng nhập** kể từ 20/09/2026. Cho qua.
///   Đăng xuất KHÔNG ghi `null` mà ghi `TaiKhoan.DaDangXuat` — nếu không thì token vừa đăng
///   xuất sẽ đi lọt qua đúng nhánh này.
///
/// ## Đã GỠ: nhánh cho qua khi token thiếu `jti` (22/09/2026)
///
/// Bản 20/09 cho qua token thiếu `jti`/`TaiKhoanId` để không đá hàng loạt người đang mở app
/// lúc triển khai. Access token chỉ sống **60 phút** nên những token đó đã chết từ lâu; nhánh
/// này giờ chỉ còn là một đường vòng **fail-open**: ai gửi được token không có `jti` sẽ bỏ qua
/// được toàn bộ cơ chế một-phiên, kể cả sau khi đăng xuất.
///
/// Nay **chặn** (401 `PHIEN_DA_BI_DAY_RA`). Đợt rà soát bảo mật 22/09/2026 nêu đúng rủi ro
/// này, và nó bắt buộc phải gỡ **cùng lúc** với việc thêm endpoint đăng xuất.
///
/// ## Cache
///
/// Tra DB mỗi request là đắt. Cache <see cref="ThoiGianCache"/> giây theo tài khoản, cùng cách
/// với cache phân quyền. Đánh đổi: phiên cũ có thể sống thêm tối đa chừng ấy giây sau khi bị
/// đẩy ra — chấp nhận được, và vẫn hơn 60 phút rất nhiều.
/// </summary>
public class PhienDuyNhatMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Ngắn có chủ ý: đây là cửa chặn bảo mật, không phải dữ liệu tra cứu. Dài hơn thì người
    /// bị đẩy ra còn thao tác được lâu hơn.
    /// </summary>
    internal const int ThoiGianCache = 10;

    /// <summary>
    /// `/auth/*` phải qua được, nếu không thì chính lệnh đăng nhập bị chặn và người vừa bị đẩy
    /// ra không có đường quay lại. `lam-moi-token` KHÔNG cần ngoại lệ ở đây vì nó tự kiểm
    /// refresh token đã bị thu hồi chưa — nhưng vẫn để trong nhóm `/auth/` cho nhất quán.
    /// </summary>
    private static bool DuocPhep(PathString duongDan) =>
        duongDan.StartsWithSegments("/api/v1/auth")
        || duongDan.StartsWithSegments("/swagger")
        || duongDan.StartsWithSegments("/health");

    public async Task InvokeAsync(HttpContext context, IAppDbContext db, IMemoryCache cache)
    {
        if (context.User.Identity?.IsAuthenticated != true || DuocPhep(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTenant.TaiKhoanId), out var taiKhoanId)
            || !Guid.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Jti), out var jti))
        {
            // CHẶN, không cho qua (đổi 22/09/2026 — xem "Đã GỠ" ở chú thích đầu lớp). Token
            // thiếu claim là token phát trước 20/09/2026, mà access token chỉ sống 60 phút nên
            // chúng đã chết từ lâu. Để ngỏ thì đây là đường vòng bỏ qua cả cơ chế một-phiên
            // lẫn đăng xuất.
            await TraLoiBiDayRa(context);
            return;
        }

        // Khoá sinh từ `IPhienService.Khoa` — cùng một chỗ với bên xoá cache, để hai bên
        // không thể lệch nhau khi ai đó đổi cách đặt tên khoá.
        var phienHienTai = await cache.GetOrCreateAsync(IPhienService.Khoa(taiKhoanId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ThoiGianCache);
            return await db.TaiKhoans
                .Where(t => t.Id == taiKhoanId)
                .Select(t => t.PhienHienTai)
                .FirstOrDefaultAsync(context.RequestAborted);
        });

        // `null` = tài khoản chưa từng đăng nhập kể từ khi có tính năng này. Cho qua.
        //
        // Đã ĐĂNG XUẤT thì cột mang `TaiKhoan.DaDangXuat` — một Guid hằng, không `jti` nào
        // trùng được — nên rơi đúng vào nhánh chặn dưới đây mà không cần kiểm riêng.
        if (phienHienTai is { } phien && phien != jti)
        {
            await TraLoiBiDayRa(context);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// 401 kèm mã lỗi để frontend biết **không được thử làm mới token** (làm mới cũng vô ích,
    /// refresh token đã bị thu hồi) và hiện đúng câu giải thích thay vì màn đăng nhập trắng.
    /// </summary>
    private static Task TraLoiBiDayRa(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(
            new { errorCode = MaLoi.PhienDaBiDayRa, duLieu = (object?)null });
    }
}
