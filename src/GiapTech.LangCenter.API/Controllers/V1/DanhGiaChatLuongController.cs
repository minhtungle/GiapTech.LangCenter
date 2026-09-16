using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.NhanSu;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// HRM — **danh mục tiêu chí đánh giá** thang 5 (FR-29, 16/09/2026).
///
/// Module cấu hình: mỗi trung tâm tự đặt bộ tiêu chí cho hai nhóm — nhân viên kinh doanh (quản lý
/// chấm theo kỳ) và giảng dạy (học viên chấm từng buổi học).
///
/// Gác bằng `TieuChiDanhGia` chứ không `NhanSu`: đổi bộ tiêu chí là đổi **cách cả trung tâm được
/// đánh giá**, không cùng mức với sửa một hồ sơ.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tieu-chi-danh-gia")]
public class TieuChiDanhGiaController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Danh mục tiêu chí. `chiDangDung=true` cho FORM CHẤM ĐIỂM — phiếu mới không được hiện
    /// tiêu chí đã ngừng dùng; màn quản lý danh mục vẫn thấy đủ để bật lại.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.TieuChiDanhGia, HanhDong.Xem)]
    public async Task<ActionResult<List<TieuChiDto>>> DanhSach(
        [FromQuery] NhomTieuChi? nhom, [FromQuery] bool chiDangDung = false,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayTieuChiQuery(nhom, chiDangDung), ct));

    [HttpPost]
    [RequirePermission(ChucNang.TieuChiDanhGia, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuTieuChiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.TieuChiDanhGia, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuTieuChiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    // KHÔNG có DELETE: tiêu chí đã có điểm mà xoá thì mọi kỳ đã chấm đổi số một cách im lặng
    // (quy tắc #1). Ngừng dùng bằng `DangDung = false` — phiếu cũ vẫn đọc được.
}

/// <summary>
/// HRM — **thống kê nhân sự** (FR-29): xếp hạng nhân viên kinh doanh · giáo viên · trợ giảng.
///
/// Gác bằng `ThongKeNhanSu` chứ không `NhanSu`, cùng lý do như `ThongKeDoanhThu` tách khỏi
/// `DoanhThu`: đây là bảng xếp hạng cả trung tâm kèm doanh số và điểm chất lượng của từng đồng
/// nghiệp, khác hẳn việc xem một hồ sơ.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/thong-ke-nhan-su")]
public class ThongKeNhanSuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.ThongKeNhanSu, HanhDong.Xem)]
    public async Task<ActionResult<ThongKeNhanSuDto>> ThongKe(
        [FromQuery] DateTimeOffset? tuNgay, [FromQuery] DateTimeOffset? denNgay,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayThongKeNhanSuQuery(tuNgay, denNgay), ct));

    /// <summary>Phiếu đánh giá của một nhân viên trong một kỳ — null nếu chưa chấm.</summary>
    [HttpGet("phieu")]
    [RequirePermission(ChucNang.ThongKeNhanSu, HanhDong.Cham)]
    public async Task<ActionResult<PhieuDanhGiaDto?>> Phieu(
        [FromQuery] Guid nhanVienId, [FromQuery] string ky, CancellationToken ct)
        => Ok(await sender.Send(new LayPhieuDanhGiaQuery(nhanVienId, ky), ct));

    /// <summary>
    /// Quản lý chấm điểm một nhân viên kinh doanh theo kỳ.
    ///
    /// Gác bằng `Cham`, tách khỏi `Xem`: xem bảng xếp hạng là việc của nhiều người, chấm điểm
    /// đồng nghiệp là việc của quản lý.
    /// </summary>
    [HttpPost("phieu")]
    [RequirePermission(ChucNang.ThongKeNhanSu, HanhDong.Cham)]
    public async Task<ActionResult<Guid>> LuuPhieu(
        [FromBody] LuuPhieuDanhGiaCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));
}
