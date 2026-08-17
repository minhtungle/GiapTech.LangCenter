using Asp.Versioning;
using GiapTech.SoccerRoom.API.Authorization;
using GiapTech.SoccerRoom.Application.LichThiDau.ChiTietTran;
using GiapTech.SoccerRoom.Application.LichThiDau.HomThu;
using GiapTech.SoccerRoom.Application.LichThiDau.LoiMoi;
using GiapTech.SoccerRoom.Application.LichThiDau.Video;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.SoccerRoom.API.Controllers.V1;

/// <summary>
/// Hòm thư — lời mời giao hữu từ đối thủ (FR-09) và lời mời đăng ký thi đấu từ trưởng nhóm.
///
/// Route cũ `/loi-moi` đổi thành `/hom-thu`. **Không tăng version API** (quy tắc #11 chỉ yêu
/// cầu tăng version khi breaking change với client ngoài): frontend là client duy nhất và
/// được cập nhật cùng lúc.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hom-thu")]
public class HomThuController(ISender sender) : ControllerBase
{
    // ----- Hòm thư tổng hợp -----

    /// <summary>
    /// Nội dung hòm thư của NGƯỜI ĐANG ĐĂNG NHẬP.
    ///
    /// Chỉ cần quyền **Xem** lịch thi đấu: cầu thủ phải trả lời được lời mời mà không cần
    /// quyền sửa trận — cùng lý do với vote MVP.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<HomThuDto>> Xem(CancellationToken ct)
        => Ok(await sender.Send(new LayHomThuQuery(), ct));

    // ----- Lời mời đăng ký thi đấu -----

    /// <summary>Trưởng nhóm gửi lời mời đăng ký cho một trận. Quyền trưởng nhóm kiểm ở handler.</summary>
    [HttpPost("dang-ky")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<Guid>> GuiLoiMoiDangKy(
        [FromBody] GuiLoiMoiDangKyCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    /// <summary>Cầu thủ trả lời — chỉ sửa được phản hồi của chính mình.</summary>
    [HttpPost("dang-ky/{loiMoiId:guid}/tra-loi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<IActionResult> TraLoi(
        Guid loiMoiId, [FromBody] TraLoiThamGiaCommand command, CancellationToken ct)
    {
        await sender.Send(command with { LoiMoiId = loiMoiId }, ct);
        return NoContent();
    }

    /// <summary>Bảng tổng hợp ai đã trả lời gì.</summary>
    [HttpGet("dang-ky/{loiMoiId:guid}/phan-hoi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<PhanHoiDto>>> PhanHoi(Guid loiMoiId, CancellationToken ct)
        => Ok(await sender.Send(new LayPhanHoiQuery(loiMoiId), ct));

    [HttpPost("dang-ky/{loiMoiId:guid}/dong")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<IActionResult> DongLoiMoi(
        Guid loiMoiId, [FromQuery] bool dong = true, CancellationToken ct = default)
    {
        await sender.Send(new DongLoiMoiCommand(loiMoiId, dong), ct);
        return NoContent();
    }

    [HttpDelete("dang-ky/{loiMoiId:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<IActionResult> XoaLoiMoiDangKy(Guid loiMoiId, CancellationToken ct)
    {
        await sender.Send(new XoaLoiMoiDangKyCommand(loiMoiId), ct);
        return NoContent();
    }

    // ----- Lời mời giao hữu từ đối thủ (FR-09) -----

    [HttpGet("giao-huu")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<LoiMoiDto>>> DanhSachGiaoHuu(
        [FromQuery] TrangThaiLoiMoi? trangThai, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachLoiMoiQuery(trangThai), ct));

    [HttpPost("giao-huu")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> TaoGiaoHuu(
        [FromBody] LuuLoiMoiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("giao-huu/{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhatGiaoHuu(
        Guid id, [FromBody] LuuLoiMoiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    /// <summary>Chấp nhận → tự sinh trận đấu "đã lên lịch". Trả về id trận vừa tạo.</summary>
    [HttpPost("giao-huu/{id:guid}/chap-nhan")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Them)]
    public async Task<ActionResult<Guid>> ChapNhanGiaoHuu(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new ChapNhanLoiMoiCommand(id), ct));

    [HttpPost("giao-huu/{id:guid}/tu-choi")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> TuChoiGiaoHuu(Guid id, CancellationToken ct)
    {
        await sender.Send(new TuChoiLoiMoiCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("giao-huu/{id:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xoa)]
    public async Task<IActionResult> XoaGiaoHuu(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaLoiMoiCommand(id), ct);
        return NoContent();
    }
}

/// <summary>FR-10 — chi tiết trận: đội hình (a), sơ đồ (b), đánh giá + vote MVP (c).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tran-dau/{tranDauId:guid}")]
public class ChiTietTranController(ISender sender) : ControllerBase
{
    // ----- Tab (a): đội hình -----

    [HttpGet("doi-hinh")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<DoiHinhDto>>> DoiHinh(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LayDoiHinhQuery(tranDauId), ct));

    [HttpPut("doi-hinh")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuDoiHinh(
        Guid tranDauId, [FromBody] LuuDoiHinhCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    // ----- Tab (b): sơ đồ chiến thuật -----

    [HttpGet("so-do")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<SoDoDto>> SoDo(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LaySoDoQuery(tranDauId), ct));

    [HttpPut("so-do")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuSoDo(
        Guid tranDauId, [FromBody] LuuSoDoCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    // ----- Video (tab c) -----

    [HttpGet("video")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<VideoDto>>> Video(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LayVideoTranQuery(tranDauId), ct));

    [HttpPut("video")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuVideo(
        Guid tranDauId, [FromBody] LuuVideoTranCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    // ----- Tab (c): đánh giá + vote MVP -----

    [HttpGet("danh-gia")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<ActionResult<List<DanhGiaDto>>> DanhGia(Guid tranDauId, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhGiaQuery(tranDauId), ct));

    [HttpPut("danh-gia")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Sua)]
    public async Task<IActionResult> LuuDanhGia(
        Guid tranDauId, [FromBody] LuuDanhGiaCommand command, CancellationToken ct)
    {
        await sender.Send(command with { TranDauId = tranDauId }, ct);
        return NoContent();
    }

    /// <summary>
    /// Thả tim MVP. Chỉ cần quyền **Xem** lịch thi đấu: cầu thủ (Player) phải bình chọn được
    /// mà không cần quyền sửa trận — xem docs/nghiep-vu/lich-thi-dau.md.
    /// </summary>
    [HttpPost("vote-mvp/{cauThuId:guid}")]
    [RequirePermission(ChucNang.LichThiDau, HanhDong.Xem)]
    public async Task<IActionResult> VoteMvp(
        Guid tranDauId, Guid cauThuId, CancellationToken ct)
    {
        await sender.Send(new VoteMvpCommand(tranDauId, cauThuId), ct);
        return NoContent();
    }
}
