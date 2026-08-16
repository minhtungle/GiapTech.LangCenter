using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>FR-04 — hồ sơ cầu thủ.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cau-thu")]
public class CauThuController(IAppDbContext db) : ControllerBase
{
    public record CauThuDto(Guid Id, string HoTen, DateOnly? NgaySinh);

    /// <summary>
    /// Danh sách cầu thủ của CLB đang đăng nhập.
    /// Không cần lọc tenant thủ công — Global Query Filter đã làm việc đó.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.CauThu, HanhDong.Xem)]
    public async Task<ActionResult<List<CauThuDto>>> DanhSach(CancellationToken ct)
        => Ok(await db.CauThus
            .OrderBy(c => c.HoTen)
            .Select(c => new CauThuDto(c.Id, c.HoTen, c.NgaySinh))
            .ToListAsync(ct));
}
