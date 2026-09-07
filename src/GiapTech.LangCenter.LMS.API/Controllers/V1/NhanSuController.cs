using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Application.QuanTri.NguoiDung;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// HRM — nhân sự: nhân viên, giáo viên, trợ giảng. **Không gồm học viên.**
///
/// Vì sao là controller RIÊNG chứ không thêm tham số vào `/nguoi-dung`:
///
/// `/nguoi-dung` gác bằng `ChucNang.TaiKhoan` — chức năng **dùng chung** — và giáo viên cũng
/// gọi nó để chọn học viên vào lớp. Nếu đưa màn Nhân sự lên đúng endpoint đó thì trưởng phòng
/// nhân sự phải được cấp quyền `TaiKhoan`, tức là thấy luôn cả tài khoản đăng nhập của mọi
/// người — việc tách hai màn hình sẽ chẳng đổi được gì ở tầng API, nơi duy nhất chặn thật.
///
/// Endpoint này gác bằng `GiaoVienNhanSu` (HRM). Người quản lý nhân sự cần đúng quyền HRM,
/// không cần quyền quản trị tài khoản.
///
/// Hồ sơ con người vẫn là **một bảng `NGUOI_DUNG` duy nhất** — HRM và LMS nhìn cùng dữ liệu,
/// khác góc nhìn. Không có bảng nhân sự thứ hai (chốt 08/09/2026): hai nguồn sự thật cho cùng
/// một con người thì sửa tên một bên là bên kia sai, đúng lỗi đã gặp với tài khoản/người dùng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nhan-su")]
public class NhanSuController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Ba vai trò nhân sự — phạm vi CỐ ĐỊNH của màn hình, người dùng không mở rộng được.
    ///
    /// Trợ giảng đi cùng giáo viên vì họ dùng chung hồ sơ `HO_SO_GIAO_VIEN`.
    /// </summary>
    private static readonly LoaiNguoiDung[] VaiTroNhanSu =
        [LoaiNguoiDung.NhanVien, LoaiNguoiDung.GiaoVien, LoaiNguoiDung.TroGiang];

    /// <summary>
    /// Danh sách nhân sự. Lọc ba vai trò ở **server** — lọc trên trang đã tải thì phân trang
    /// và tổng số bản ghi đều sai.
    ///
    /// `loaiNguoiDung` là bộ lọc người dùng chọn trong ô "Vai trò"; nó **không** phá được phạm
    /// vi ba vai trò ở trên, nên gõ thẳng `?loaiNguoiDung=HocVien` vào URL vẫn không ra học
    /// viên nào.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.GiaoVienNhanSu, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<NguoiDungDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] LoaiNguoiDung? loaiNguoiDung,
        [FromQuery] TrangThaiNhanSu? trangThaiNhanSu,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
    {
        // Bộ lọc chọn ra học viên thì bỏ qua — phạm vi màn hình thắng bộ lọc.
        var loc = loaiNguoiDung is { } l && VaiTroNhanSu.Contains(l) ? l : (LoaiNguoiDung?)null;

        return Ok(await sender.Send(new LayDanhSachNguoiDungQuery(
            timKiem, loc, trangThaiNhanSu, new ThamSoTrang(trang, soDong),
            VaiTroNhanSu), ct));
    }

    [HttpPost]
    [RequirePermission(ChucNang.GiaoVienNhanSu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaNhanSu(command.LoaiNguoiDung);
        return Ok(await sender.Send(command, ct));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.GiaoVienNhanSu, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaNhanSu(command.LoaiNguoiDung);
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.GiaoVienNhanSu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaNguoiDungCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Chặn dùng endpoint HRM để tạo/sửa HỌC VIÊN.
    ///
    /// Không kiểm thì người có quyền HRM tạo được học viên qua đây — lách đúng ranh giới vừa
    /// dựng, và bằng cách gọi API trực tiếp thì UI ẩn gì cũng vô nghĩa.
    /// </summary>
    private static void BaoDamLaNhanSu(LoaiNguoiDung loai)
    {
        if (!VaiTroNhanSu.Contains(loai))
            throw new Application.Common.Exceptions.AppException("KHONG_PHAI_NHAN_SU");
    }
}
