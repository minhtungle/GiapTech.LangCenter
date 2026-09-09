using System.Security.Claims;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;

namespace GiapTech.LangCenter.API.Services;

/// <summary>
/// Người dùng của request hiện tại, đọc từ claim.
/// Đặt ở tầng API vì phụ thuộc <see cref="IHttpContextAccessor"/>.
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Null với token phát hành trước khi tách bảng (07/09/2026) — chủ ý: thà từ chối thao
    /// tác trên tài khoản còn hơn đoán nhầm sang id người.
    /// </summary>
    public Guid? TaiKhoanId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTenant.TaiKhoanId), out var id) ? id : null;

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public bool DaXacThuc => User?.Identity?.IsAuthenticated == true;
}
