using System.Security.Claims;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;

namespace GiapTech.LangCenter.API.Authorization;

/// <summary>
/// Kiểm tra quyền động lúc chạy — quy tắc bất di bất dịch #9.
///
/// Cố tình KHÔNG có nhánh "nếu là admin thì cho qua": admin có toàn quyền nhờ được gán nhóm
/// quyền đầy đủ lúc seed, nên chỉ một cơ chế duy nhất quyết định mọi truy cập. Ngoại lệ
/// hard-code sẽ là con đường vòng không ai kiểm tra được bằng dữ liệu.
/// </summary>
public class QuyenAuthorizationHandler(IQuyenService quyenService, ILogger<QuyenAuthorizationHandler> logger)
    : AuthorizationHandler<QuyenRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, QuyenRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return; // chưa xác thực → không Succeed, trả 401

        var tenantId = context.User.FindFirstValue(ClaimTenant.TenantId);

        // Quyền gán cho TÀI KHOẢN, không cho người — nên tra bằng claim tài khoản, không phải
        // NameIdentifier (vốn mang id người). Token phát hành trước 07/09/2026 không có claim
        // này: từ chối, người dùng đăng nhập lại là xong. Thà 403 rõ ràng còn hơn tra nhầm id
        // và trả về tập quyền của một hàng dữ liệu khác.
        var taiKhoanId = context.User.FindFirstValue(ClaimTenant.TaiKhoanId);

        if (!Guid.TryParse(tenantId, out var tid) || !Guid.TryParse(taiKhoanId, out var uid))
        {
            logger.LogWarning("Token thiếu claim tenant_id hoặc tai_khoan_id");
            return;
        }

        if (await quyenService.CoQuyenAsync(tid, uid, requirement.ChucNang, requirement.HanhDong))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Từ chối {NguoiDung} thao tác {HanhDong} trên {ChucNang}",
                uid, requirement.HanhDong, requirement.ChucNang);
        }
    }
}
