using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.DaoTao.BuoiHoc;
using GiapTech.LangCenter.Application.DaoTao.DiemDanh;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>FR-09 — buổi học và lịch dạy.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/buoi-hoc")]
public class BuoiHocController(ISender sender) : ControllerBase
{
    /// <summary>Lịch dạy trong một khoảng — dùng cho "hôm nay" và lịch tuần.</summary>
    [HttpGet]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Xem)]
    public async Task<ActionResult<List<BuoiHocDto>>> Lich(
        [FromQuery] DateTimeOffset tu, [FromQuery] DateTimeOffset den, CancellationToken ct)
        => Ok(await sender.Send(new LayLichTheoKhoangQuery(tu, den), ct));

    /// <summary>Chi tiết một buổi — cho view chi tiết buổi học.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Xem)]
    public async Task<ActionResult<BuoiHocDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayBuoiHocQuery(id), ct));

    // ---------- Nhận xét của học viên về buổi ----------

    /// <summary>
    /// Nhận xét học viên gửi về buổi này. Học viên chỉ thấy nhận xét CỦA MÌNH — lọc trong
    /// handler, xem `LayNhanXetBuoiHocHandler`.
    /// </summary>
    /// <summary>
    /// Nhận xét về buổi học. Gác bằng thao tác HẸP NHẤT (`TuLam` — ai gửi được thì đọc lại
    /// được), còn "đọc nhận xét của MỌI người" do handler quyết bằng `NhanXetBuoiHoc.Xem`.
    ///
    /// Vì sao không gác bằng `Xem`: endpoint phục vụ cả hai vai trò, mà `[RequirePermission]`
    /// chỉ nhận MỘT quyền. Gác bằng quyền rộng thì học viên nhận 403; gác bằng quyền hẹp rồi
    /// lọc ở handler thì cả hai vào được và mỗi người thấy đúng phần của mình — cùng khuôn đã
    /// dùng cho sổ học phí (`IPhamViHocPhi`).
    /// </summary>
    [HttpGet("{id:guid}/nhan-xet")]
    [RequirePermission(ChucNang.NhanXetBuoiHoc, HanhDong.TuLam)]
    public async Task<ActionResult<List<Application.DaoTao.NhanXet.NhanXetBuoiHocDto>>> NhanXet(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(
            new Application.DaoTao.NhanXet.LayNhanXetBuoiHocQuery(id), ct));

    /// <summary>
    /// Học viên gửi nhận xét. KHÔNG nhận id học viên — lấy từ token, cùng cách với tự điểm
    /// danh. Gửi lần thứ hai là sửa nhận xét cũ.
    ///
    /// Gác bằng `DiemDanh.Them` — quyền mà nhóm Học viên có (để tự điểm danh), còn giáo viên
    /// thì bị chặn ở handler vì họ không phải học viên đang học của lớp.
    /// </summary>
    [HttpPost("{id:guid}/nhan-xet")]
    [RequirePermission(ChucNang.NhanXetBuoiHoc, HanhDong.TuLam)]
    public async Task<ActionResult<Guid>> GuiNhanXet(
        Guid id, [FromBody] GuiNhanXetBody body, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.NhanXet.GuiNhanXetBuoiHocCommand(
            id, body.NoiDung, body.MucHaiLong), ct));

    public record GuiNhanXetBody(string NoiDung, int? MucHaiLong = null);

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatBuoiHocCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>
    /// Xoá hẳn một buổi lên nhầm. Khác `huy` — huỷ giữ bản ghi để lịch sử còn nguyên.
    /// Chặn với buổi đã chốt hoặc đã có điểm danh.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaBuoiHocCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/huy")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Huy)]
    public async Task<IActionResult> Huy(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyBuoiHocCommand(id), ct);
        return NoContent();
    }

    // ---------- Điểm danh ----------

    [HttpGet("{id:guid}/diem-danh")]
    [RequirePermission(ChucNang.DiemDanh, HanhDong.Xem)]
    public async Task<ActionResult<List<DiemDanhDto>>> BangDiemDanh(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayBangDiemDanhQuery(id), ct));

    [HttpPost("{id:guid}/diem-danh")]
    [RequirePermission(ChucNang.DiemDanh, HanhDong.Sua)]
    public async Task<IActionResult> GhiDiemDanh(
        Guid id, [FromBody] GhiDiemDanhBody body, CancellationToken ct)
    {
        await sender.Send(new GhiDiemDanhCommand(id, body.DanhSach), ct);
        return NoContent();
    }

    public record GhiDiemDanhBody(List<GhiDiemDanhItem> DanhSach);

    /// <summary>Chốt buổi: sinh đủ bản ghi cho người chưa điểm danh, mặc định Vắng.</summary>
    [HttpPost("{id:guid}/chot")]
    [RequirePermission(ChucNang.DiemDanh, HanhDong.Chot)]
    public async Task<IActionResult> Chot(Guid id, CancellationToken ct)
    {
        await sender.Send(new ChotBuoiHocCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Học viên tự điểm danh. KHÔNG nhận id học viên — lấy từ token, nên không có đường điểm
    /// danh hộ người khác dù có quyền.
    /// </summary>
    [HttpPost("{id:guid}/tu-diem-danh")]
    [RequirePermission(ChucNang.DiemDanh, HanhDong.TuLam)]
    public async Task<IActionResult> TuDiemDanh(Guid id, CancellationToken ct)
    {
        await sender.Send(new TuDiemDanhCommand(id), ct);
        return NoContent();
    }
}
