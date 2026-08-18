using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.Common.Models;
using GiapTech.SoccerRoom.Application.CongDong;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// CỘNG ĐỒNG — xem các CLB khác trong hệ thống và gửi lời mời thách đấu.
///
/// ⚠️ Toàn bộ controller này đọc dữ liệu NGOÀI tenant hiện tại. Xem `CongDongDtos` để biết
/// những gì cố ý lộ và những gì cố ý không.
///
/// Dùng chung quyền `LichThiDau`: thách đấu là việc của người sắp lịch. Tách thành chức năng
/// riêng sẽ khiến mọi nhóm quyền đang có mất quyền vào sàn cho tới khi admin đi cấp lại —
/// thêm chức năng vào `QUYEN_CHUC_NANG` không tự gán cho nhóm nào.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cong-dong")]
public class CongDongController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<ClbCongDongDto>>> DanhSach(
        [FromQuery] string? tuKhoa,
        [FromQuery] string? khuVuc,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachCongDongQuery(tuKhoa, khuVuc, trang, soDong), ct));

    /// <summary>
    /// Chi tiết công khai một CLB: mô tả, lịch sử đấu gần đây, đối đầu với ta.
    ///
    /// Tra theo **mã đội** chứ không phải id — cộng đồng không trả id tenant ra ngoài. 404 cho
    /// cả mã sai định dạng, mã không tồn tại, và mã của chính mình.
    /// </summary>
    // Ràng buộc `length(7)` tường minh, KHÔNG dựa vào việc ASP.NET Core ưu tiên route literal
    // hơn route tham số: `{maDoi}` cũng khớp "khu-vuc" và "loi-moi", và nếu ai đó đổi thứ tự
    // action hay thêm endpoint mới thì hành vi ngầm đó im lặng đổi theo.
    [HttpGet("{maDoi:length(7)}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<ChiTietClbDto>> ChiTiet(string maDoi, CancellationToken ct)
        => await sender.Send(new LayChiTietClbQuery(maDoi), ct) is { } clb
            ? Ok(clb)
            : NotFound();

    [HttpGet("khu-vuc")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<string>>> KhuVuc(CancellationToken ct)
        => Ok(await sender.Send(new LayKhuVucQuery(), ct));

    /// <summary>Lời mời thách đấu — cả ta gửi lẫn ta nhận, trong một danh sách.</summary>
    [HttpGet("loi-moi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<ThuThachDauDto>>> LoiMoi(CancellationToken ct)
        => Ok(await sender.Send(new LayThuThachDauQuery(), ct));

    /// <summary>Gửi lời mời tới CLB khác. Cần quyền THÊM: nó tạo dữ liệu ở hòm thư bên kia.</summary>
    [HttpPost("loi-moi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Gui(
        [FromBody] GuiLoiMoiThachDauCommand body, CancellationToken ct)
        => Ok(await sender.Send(body, ct));

    /// <summary>
    /// Đồng ý hoặc từ chối. Cần quyền THÊM vì đồng ý sẽ TẠO trận trong lịch của cả hai bên —
    /// người chỉ có quyền Xem không được tự ý thêm trận vào lịch đội.
    /// </summary>
    [HttpPost("loi-moi/{id:guid}/tra-loi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<IActionResult> TraLoi(
        Guid id, [FromBody] TraLoiThachDauBody body, CancellationToken ct)
    {
        await sender.Send(new TraLoiThachDauCommand(id, body.ChapNhan, body.PhanHoi), ct);
        return NoContent();
    }

    public record TraLoiThachDauBody(bool ChapNhan, string? PhanHoi);

    /// <summary>Bên gửi rút lại lời mời chưa được trả lời.</summary>
    [HttpDelete("loi-moi/{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> Huy(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyLoiMoiThachDauCommand(id), ct);
        return NoContent();
    }
}
