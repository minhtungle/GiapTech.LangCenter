using System.Security.Claims;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;

namespace GiapTech.SoccerRoom.API.Authorization;

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
        var nguoiDungId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(tenantId, out var tid) || !Guid.TryParse(nguoiDungId, out var uid))
        {
            logger.LogWarning("Token thiếu claim tenant_id hoặc NameIdentifier");
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
