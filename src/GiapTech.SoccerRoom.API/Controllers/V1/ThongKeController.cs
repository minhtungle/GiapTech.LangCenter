using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.LichThiDau.TranDau;
using GiapTech.SoccerRoom.Application.ThongKe;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// FR-12 → FR-14 — thống kê.
///
/// Dùng **POST** cho một endpoint đọc: bộ lọc là object lồng (danh sách kết quả, danh sách
/// trạng thái) mà querystring diễn đạt vụng — cùng lý do và cùng kiểu body với
/// <c>POST /tran-dau/tim-kiem</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/thong-ke")]
public class ThongKeController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequirePermission(ChucNang.ThongKe, HanhDong.Xem)]
    public async Task<ActionResult<ThongKeDto>> Xem(
        [FromBody] BoLocTranDau? loc, CancellationToken ct)
        => Ok(await sender.Send(new LayThongKeQuery(loc), ct));
}
