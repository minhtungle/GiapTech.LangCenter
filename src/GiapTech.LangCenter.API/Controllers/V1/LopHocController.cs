using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.Crm;
using GiapTech.LangCenter.Application.DaoTao.LopHoc;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

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
    /// Sinh THÊM buổi theo tần suất, nối tiếp lịch đang có — **không xoá buổi nào**.
    /// Khác `sinh-lich` vốn thay cả lịch.
    /// </summary>
    [HttpPost("{id:guid}/sinh-them-buoi")]
    [RequirePermission(ChucNang.BuoiHoc, HanhDong.Them)]
    public async Task<ActionResult<List<Application.DaoTao.BuoiHoc.BuoiHocDto>>> SinhThemBuoi(
        Guid id, [FromBody] SinhThemBuoiBody body, CancellationToken ct)
        => Ok(await sender.Send(new Application.DaoTao.BuoiHoc.SinhThemBuoiCommand(
            id, body.TuNgay, body.ThuTrongTuan, body.GioBatDau, body.GioKetThuc,
            body.SoBuoi, body.DenNgay, body.NgayLoaiTru,
            body.LaHocBu, body.GiaoVienId, body.PhongHoc, body.LinkHoc, body.GhiChu), ct));

    public record SinhThemBuoiBody(
        DateOnly TuNgay, List<DayOfWeek> ThuTrongTuan,
        TimeOnly GioBatDau, TimeOnly GioKetThuc,
        int? SoBuoi = null, DateOnly? DenNgay = null, List<DateOnly>? NgayLoaiTru = null,
        bool LaHocBu = false, Guid? GiaoVienId = null,
        string? PhongHoc = null, string? LinkHoc = null, string? GhiChu = null);

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

    // ---------- Danh sách chờ xếp lớp (FR-21) ----------

    /// <summary>
    /// Học viên đã mua khoá nhưng chưa có lớp. Bên đào tạo mở màn này để xếp.
    ///
    /// Không gác bằng <see cref="ChucNang.DoanhThu"/>: DTO cố tình mang số tiền của đơn (thành
    /// học phí khi vào lớp) nên người xếp lớp phải thấy — nhưng đó là người có quyền
    /// <see cref="ChucNang.LopHoc"/> sửa, tức đã được xem học phí lớp.
    /// </summary>
    [HttpGet("cho-xep-lop")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<ActionResult<KetQuaTrang<YeuCauXepLopDto>>> ChoXepLop(
        [FromQuery] TrangThaiYeuCauXepLop? trangThai,
        [FromQuery] Guid? khoaHocId,
        [FromQuery] string? timKiem,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachChoXepLopQuery(
            trangThai, khoaHocId, timKiem, new ThamSoTrang(trang, soDong)), ct));

    /// <summary>
    /// Duyệt học viên đang chờ vào một lớp. Dùng cho cả hai lối vào của UI: từ danh sách chờ
    /// chọn lớp, hoặc từ trong lớp chọn người chờ.
    /// </summary>
    [HttpPost("{id:guid}/duyet-cho-xep-lop")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> DuyetVaoLop(
        Guid id, [FromBody] DuyetVaoLopBody body, CancellationToken ct)
    {
        await sender.Send(
            new DuyetVaoLopCommand(body.YeuCauIds, id, body.BoQuaCanhBaoKhoaHoc), ct);
        return NoContent();
    }

    /// <param name="BoQuaCanhBaoKhoaHoc">
    /// true = người duyệt đã xem cảnh báo lệch khoá học và vẫn muốn tiếp tục (12/09/2026).
    /// Mặc định false nên client cũ vẫn nhận được cảnh báo thay vì bỏ qua âm thầm.
    /// </param>
    public record DuyetVaoLopBody(List<Guid> YeuCauIds, bool BoQuaCanhBaoKhoaHoc = false);

    /// <summary>
    /// Bên đào tạo từ chối xếp lớp — **lý do bắt buộc**, người bán phải trả lời được khách.
    /// Từ chối rồi thì người bán bổ sung thông tin và gửi lại (lần gửi mới).
    /// </summary>
    [HttpPost("cho-xep-lop/{yeuCauId:guid}/tu-choi")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> TuChoiXepLop(
        Guid yeuCauId, [FromBody] TuChoiBody body, CancellationToken ct)
    {
        await sender.Send(new TuChoiXepLopCommand(yeuCauId, body.LyDo), ct);
        return NoContent();
    }

    public record TuChoiBody(string LyDo);

    [HttpDelete("cho-xep-lop/{yeuCauId:guid}")]
    [RequirePermission(ChucNang.LopHoc, HanhDong.Sua)]
    public async Task<IActionResult> HuyYeuCauXepLop(Guid yeuCauId, CancellationToken ct)
    {
        await sender.Send(new HuyYeuCauXepLopCommand(yeuCauId), ct);
        return NoContent();
    }
}
