using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.API.RateLimit;
using GiapTech.SoccerRoom.Application.DangKyNhanh;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// FR-19 — đăng ký đá trận qua link/QR, không cần đăng nhập.
///
/// Hai nhóm endpoint rất khác nhau về mức tin cậy:
/// - `link/*` — trưởng nhóm sinh/thu hồi link. Cần đăng nhập + là trưởng nhóm.
/// - `xem` và `tra-loi` — **ẩn danh**, xác thực bằng token trong body.
///
/// Token trong **body** chứ không trong URL: URL vào access log, vào history trình duyệt, và vào
/// header Referer khi trang nạp tài nguyên ngoài. Cùng lý do với FR-18.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dang-ky-nhanh")]
public class DangKyNhanhController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Trưởng nhóm sinh link/QR. Token thô trả về **đúng một lần** — DB chỉ lưu hash.
    ///
    /// Cần quyền THÊM trên Lịch thi đấu: sinh link là mở đường cho người ngoài ghi vào phản hồi
    /// của trận, tương đương thêm dữ liệu vào lịch.
    /// </summary>
    [HttpPost("link/{loiMoiId:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<LinkDangKyDaTao>> TaoLink(
        Guid loiMoiId, [FromBody] TaoLinkBody body, CancellationToken ct)
        => Ok(await sender.Send(new TaoLinkDangKyCommand(loiMoiId, body.Han), ct));

    /// <summary>Thu hồi link — token chết ngay dù chưa hết hạn.</summary>
    [HttpPost("link/{loiMoiId:guid}/thu-hoi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<IActionResult> ThuHoiLink(Guid loiMoiId, CancellationToken ct)
    {
        await sender.Send(new ThuHoiLinkDangKyCommand(loiMoiId), ct);
        return NoContent();
    }

    /// <summary>
    /// Sửa danh sách người được mời sau khi đã gửi.
    ///
    /// Bỏ ai đã trả lời sẽ xoá câu trả lời của họ → lệnh bị từ chối với mã
    /// `XOA_SE_MAT_CAU_TRA_LOI` nếu thiếu `dongYXoaCauTraLoi` (quy tắc #1). Xác nhận ở tầng API
    /// chứ không phó mặc UI có hiện hộp thoại hay không.
    /// </summary>
    [HttpPut("link/{loiMoiId:guid}/danh-sach")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> SuaDanhSach(
        Guid loiMoiId, [FromBody] SuaDanhSachBody body, CancellationToken ct)
    {
        await sender.Send(
            new SuaDanhSachMoiCommand(loiMoiId, body.CauThuIds, body.DongYXoaCauTraLoi), ct);
        return NoContent();
    }

    /// <summary>Xem trước ai sẽ mất câu trả lời nếu bỏ tick — không sửa gì.</summary>
    [HttpPost("link/{loiMoiId:guid}/xem-truoc-mat-du-lieu")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<IReadOnlyList<CanhBaoMatCauTraLoi>>> XemTruocMatDuLieu(
        Guid loiMoiId, [FromBody] SuaDanhSachBody body, CancellationToken ct)
        => Ok(await sender.Send(new XemAiSeMatCauTraLoiQuery(loiMoiId, body.CauThuIds), ct));

    // ----- Ẩn danh: người mở link -----

    /// <summary>
    /// Nội dung trang đăng ký nhanh. **Ẩn danh** — người dùng chưa có tài khoản.
    ///
    /// POST chứ không GET vì token nằm trong body. Không RESTful lắm, nhưng đúng về bảo mật, và
    /// cùng cách với `POST /moi-qua-link/xem`.
    /// </summary>
    [HttpPost("xem")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.LoiMoiTheoToken)]
    public async Task<ActionResult<TrangDangKyNhanh>> Xem(
        [FromBody] TokenBody body, CancellationToken ct)
        => Ok(await sender.Send(new XemTrangDangKyQuery(body.Token), ct));

    /// <summary>
    /// Chọn tên và trả lời. **Ẩn danh** và **GHI dữ liệu** — endpoint nhạy cảm nhất của FR-19.
    ///
    /// Trả lời lần hai = ĐỔI câu trả lời, không bị chặn (quyết định 21/08: khoá cứng thì người mở
    /// link đầu tiên chọn hộ người khác rồi khoá luôn họ). Mỗi lần đổi tăng `SoLanSua` để trưởng
    /// nhóm thấy dấu vết.
    /// </summary>
    [HttpPost("tra-loi")]
    [AllowAnonymous]
    [EnableRateLimiting(GioiHanTanSuat.LoiMoiTheoToken)]
    public async Task<IActionResult> TraLoi([FromBody] TraLoiQuaLinkBody body, CancellationToken ct)
    {
        await sender.Send(
            new TraLoiQuaLinkCommand(body.Token, body.CauThuId, body.TraLoi, body.GhiChu), ct);
        return NoContent();
    }
}

public record TaoLinkBody(HanLinkDangKy Han);

public record SuaDanhSachBody(IReadOnlyList<Guid> CauThuIds, bool DongYXoaCauTraLoi = false);

public record TokenBody(string Token);

public record TraLoiQuaLinkBody(
    string Token, Guid CauThuId, TraLoiThamGia TraLoi, string? GhiChu);
