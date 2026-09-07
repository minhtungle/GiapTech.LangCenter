using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.DaoTao.LopHoc;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// FR-07 — lớp học.
///
/// Lưu ý về quyền: `[RequirePermission]` chỉ quyết định CÓ ĐƯỢC GỌI endpoint hay không, nó
/// không lọc dữ liệu. Việc giới hạn "chỉ lớp mình phụ trách" nằm ở `IPhamViLopHoc` trong từng
/// handler — bỏ nó đi thì giáo viên đọc được mọi lớp của trung tâm dù attribute vẫn đúng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lop-hoc")]
public class LopHocController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<LopHocDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] TrangThaiLopHoc? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayDanhSachLopHocQuery(timKiem, trangThai, new ThamSoTrang(trang, soDong)), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<LopHocDto>> ChiTiet(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayLopHocQuery(id), ct));

    /// <summary>Bước 1 của wizard — lưu nháp, lớp chưa hiện với người khác.</summary>
    [HttpPost]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoLopHocCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatLopHocCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Hoàn tất wizard: lớp rời trạng thái nháp và hiện với mọi người liên quan.</summary>
    [HttpPost("{id:guid}/hoan-tat")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> HoanTat(
        Guid id, [FromBody] HoanTatBody body, CancellationToken ct)
    {
        await sender.Send(new HoanTatLopHocCommand(id, body.NgayKhaiGiang), ct);
        return NoContent();
    }

    public record HoanTatBody(DateTimeOffset NgayKhaiGiang);

    /// <summary>Huỷ lớp — giữ toàn bộ lịch sử, khác hẳn xoá.</summary>
    [HttpPost("{id:guid}/huy")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> Huy(Guid id, CancellationToken ct)
    {
        await sender.Send(new HuyLopHocCommand(id), ct);
        return NoContent();
    }

    /// <summary>Xoá cứng — chỉ áp dụng cho lớp còn ở trạng thái nháp.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaLopHocNhapCommand(id), ct);
        return NoContent();
    }

    // ---------- Buổi học (bước 2 của wizard) ----------

    [HttpGet("{id:guid}/buoi-hoc")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Xem)]
    public async Task<ActionResult<List<Application.DaoTao.BuoiHoc.BuoiHocDto>>> BuoiHoc(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.BuoiHoc.LayBuoiHocCuaLopQuery(id), ct));

    /// <summary>
    /// Sinh toàn bộ lịch theo tần suất. Gọi lại sẽ SINH LẠI từ đầu — chỉ cho phép khi lớp
    /// chưa có buổi nào được điểm danh.
    ///
    /// Gác bằng `LopHoc.Sua` chứ không `BuoiHoc.Them`: sinh lịch là một phần không tách rời
    /// của việc lập lớp. Đòi thêm `BuoiHoc.Them` sẽ làm người có nhóm Trợ giảng kẹt giữa
    /// wizard — tạo được lớp nhưng không sinh được lịch.
    /// </summary>
    [HttpPost("{id:guid}/sinh-lich")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<ActionResult<List<Application.DaoTao.BuoiHoc.BuoiHocDto>>> SinhLich(
        Guid id, [FromBody] SinhLichBody body, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.BuoiHoc.SinhLichChoLopCommand(
            id, body.NgayKhaiGiang, body.ThuTrongTuan, body.GioBatDau, body.GioKetThuc,
            body.SoBuoi, body.DenNgay, body.NgayLoaiTru), ct));

    public record SinhLichBody(
        DateOnly NgayKhaiGiang,
        List<DayOfWeek> ThuTrongTuan,
        TimeOnly GioBatDau,
        TimeOnly GioKetThuc,
        int? SoBuoi = null,
        DateOnly? DenNgay = null,
        List<DateOnly>? NgayLoaiTru = null);

    // ---------- Học viên trong lớp (bước 3 của wizard) ----------

    /// <summary>
    /// Thêm MỘT buổi lẻ — dạy bù, ôn tập. Không đụng buổi nào đang có.
    /// </summary>
    [HttpPost("{id:guid}/buoi-hoc")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Them)]
    public async Task<ActionResult<Application.DaoTao.BuoiHoc.BuoiHocDto>> ThemBuoiHoc(
        Guid id, [FromBody] ThemBuoiHocBody body, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.BuoiHoc.ThemBuoiHocCommand(
            id, body.Ngay, body.GioBatDau, body.GioKetThuc, body.LaHocBu,
            body.GiaoVienId, body.PhongHoc, body.LinkHoc, body.GhiChu), ct));

    public record ThemBuoiHocBody(
        DateOnly Ngay, TimeOnly GioBatDau, TimeOnly GioKetThuc,
        bool LaHocBu = false, Guid? GiaoVienId = null,
        string? PhongHoc = null, string? LinkHoc = null, string? GhiChu = null);

    /// <summary>
    /// Sinh THÊM buổi theo tần suất, nối tiếp lịch đang có — **không xoá buổi nào**.
    /// Khác `sinh-lich` vốn thay cả lịch.
    /// </summary>
    [HttpPost("{id:guid}/sinh-them-buoi")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Them)]
    public async Task<ActionResult<List<Application.DaoTao.BuoiHoc.BuoiHocDto>>> SinhThemBuoi(
        Guid id, [FromBody] SinhThemBuoiBody body, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.BuoiHoc.SinhThemBuoiCommand(
            id, body.TuNgay, body.ThuTrongTuan, body.GioBatDau, body.GioKetThuc,
            body.SoBuoi, body.DenNgay, body.NgayLoaiTru), ct));

    public record SinhThemBuoiBody(
        DateOnly TuNgay, List<DayOfWeek> ThuTrongTuan,
        TimeOnly GioBatDau, TimeOnly GioKetThuc,
        int? SoBuoi = null, DateOnly? DenNgay = null, List<DateOnly>? NgayLoaiTru = null);

    [HttpGet("{id:guid}/hoc-vien")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Xem)]
    public async Task<ActionResult<List<HocVienTrongLopDto>>> HocVien(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayHocVienTrongLopQuery(id), ct));

    [HttpPost("{id:guid}/hoc-vien")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> ThemHocVien(
        Guid id, [FromBody] ThemHocVienBody body, CancellationToken ct)
    {
        await sender.Send(
            new ThemHocVienVaoLopCommand(id, body.HocVienIds, body.HocPhiApDung), ct);
        return NoContent();
    }

    public record ThemHocVienBody(List<Guid> HocVienIds, decimal? HocPhiApDung = null);

    [HttpDelete("{id:guid}/hoc-vien/{hocVienId:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> GoHocVien(Guid id, Guid hocVienId, CancellationToken ct)
    {
        await sender.Send(new GoHocVienKhoiLopCommand(id, hocVienId), ct);
        return NoContent();
    }
}
