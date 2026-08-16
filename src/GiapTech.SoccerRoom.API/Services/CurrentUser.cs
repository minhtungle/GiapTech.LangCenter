using System.Security.Claims;
using GiapTech.SoccerRoom.Application.Common.Interfaces;

namespace GiapTech.SoccerRoom.API.Services;

/// <summary>
/// Người dùng của request hiện tại, đọc từ claim.
/// Đặt ở tầng API vì phụ thuộc <see cref="IHttpContextAccessor"/>.
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public bool DaXacThuc => User?.Identity?.IsAuthenticated == true;
}
