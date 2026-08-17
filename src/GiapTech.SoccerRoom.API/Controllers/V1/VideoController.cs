using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.LichThiDau.Video;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Thư viện video — xem tổng hợp video của mọi trận.
///
/// Đọc thẳng từ `VIDEO_TRAN`, không có bảng riêng: sửa video ở màn trận thì thư viện đổi
/// theo ngay, không có bản sao nào để lệch.
///
/// Nằm dưới quyền <see cref="ChucNang.LichThiDau"/> chứ không tạo chức năng mới — thêm chức
/// năng vào QUYEN_CHUC_NANG sẽ bắt mọi CLB đang chạy phải cấp lại quyền.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/thu-vien-video")]
public class ThuVienVideoController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<VideoThuVienDto>>> DanhSach(
        [FromQuery] Guid? tranDauId, [FromQuery] Guid? doiThuId, [FromQuery] string? timKiem,
        [FromQuery] int trang = 1, [FromQuery] int soDong = 20, CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayThuVienVideoQuery(tranDauId, doiThuId, timKiem, new ThamSoTrang(trang, soDong)), ct));
}
