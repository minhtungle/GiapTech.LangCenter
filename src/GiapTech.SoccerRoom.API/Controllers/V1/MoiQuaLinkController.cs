using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.MoiQuaLink;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// FR-18 — lời mời thách đấu qua link/QR.
///
/// ⚠️ Endpoint **XEM** là công khai (`AllowAnonymous`): người nhận chưa có tài khoản, bắt đăng
/// nhập trước khi xem là yêu cầu họ tạo đội cho một thứ họ chưa biết nội dung.
///
/// Token là thứ DUY NHẤT bảo vệ endpoint đó — nên nó phải nằm trong body, không trong URL: token
/// trên URL đi vào log truy cập của Caddy, vào history trình duyệt, và vào header `Referer` khi
/// người dùng bấm bất kỳ link nào trên trang.
///
/// **Nợ kỹ thuật:** cần rate limit ở tầng Caddy cho `POST xem` — nó là endpoint ẩn danh nhận
/// input do người gọi kiểm soát. Gộp cùng nợ N3.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/moi-qua-link")]
public class MoiQuaLinkController(ISender sender) : ControllerBase
{
    /// <summary>Sinh link/QR mời một đối thủ chưa liên kết. Trả token thô ĐÚNG MỘT LẦN.</summary>
    [HttpPost]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<TaoLoiMoiLinkKetQua>> Tao(
        [FromBody] TaoLoiMoiLinkCommand body, CancellationToken ct)
        => Ok(await sender.Send(body, ct));

    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<LoiMoiLinkDaGuiDto>>> DanhSach(CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachLoiMoiLinkQuery(), ct));

    public record XemBody(string Token);

    /// <summary>
    /// Xem lời mời — KHÔNG cần đăng nhập.
    ///
    /// Dùng POST dù về ngữ nghĩa là đọc: token trong body không bị ghi vào log/history/Referer
    /// như khi đặt trên URL. Đánh đổi ngữ nghĩa REST để không rò token là đúng chỗ.
    /// </summary>
    [HttpPost("xem")]
    [AllowAnonymous]
    public async Task<ActionResult<XemLoiMoiLinkDto>> Xem(
        [FromBody] XemBody body, CancellationToken ct)
        => await sender.Send(new XemLoiMoiLinkQuery(body.Token), ct) is { } dto
            ? Ok(dto)
            : NotFound();

    public record TraLoiBody(string Token, bool ChapNhan, string? PhanHoi);

    /// <summary>
    /// Chấp nhận / từ chối. CẦN đăng nhập — nó ghi vào lịch của cả hai CLB.
    ///
    /// Quyền `LichThiDau.Them`: chấp nhận sẽ TẠO trận trong lịch đội, người chỉ có quyền Xem
    /// không được tự ý thêm trận.
    /// </summary>
    [HttpPost("tra-loi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<TraLoiLinkKetQua>> TraLoi(
        [FromBody] TraLoiBody body, CancellationToken ct)
        => Ok(await sender.Send(
            new TraLoiLoiMoiLinkCommand(body.Token, body.ChapNhan, body.PhanHoi), ct));

    /// <summary>Thu hồi link chưa ai trả lời (ca 7).</summary>
    [HttpPost("{id:guid}/thu-hoi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> ThuHoi(Guid id, CancellationToken ct)
    {
        await sender.Send(new ThuHoiLoiMoiLinkCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Gỡ liên kết khi sai người nhận (ca 5). Cần quyền Sửa vì nó đổi dữ liệu đối thủ.
    /// </summary>
    [HttpPost("{id:guid}/huy-lien-ket")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> HuyLienKet(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyLienKetCommand(id), ct);
        return NoContent();
    }
}
