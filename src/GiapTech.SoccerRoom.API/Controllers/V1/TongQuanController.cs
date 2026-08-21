using Asp.Versioning;
using GiapTech.SoccerRoom.Application.TongQuan;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Màn Tổng quan — việc cần làm + trận sắp tới (nợ N6).
///
/// **Không** dùng `[RequirePermission]`: đây là màn đầu tiên sau khi đăng nhập, mọi tài khoản đều
/// phải vào được. Nếu bắt quyền thì người không có quyền nào rơi vào trang trắng ngay sau khi
/// đăng nhập — tệ hơn hẳn việc thấy một danh sách rỗng.
///
/// An toàn vẫn được giữ: handler chỉ đọc trong tenant hiện tại (Global Query Filter), và việc của
/// cả đội chỉ trả cho trưởng nhóm — cầu thủ thường chỉ thấy việc của chính mình.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tong-quan")]
public class TongQuanController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TongQuanDto>> Lay(CancellationToken ct)
        => Ok(await sender.Send(new LayTongQuanQuery(), ct));
}
