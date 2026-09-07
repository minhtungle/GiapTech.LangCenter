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
/// LMS — hồ sơ học viên. Đối xứng với <see cref="NhanSuController"/>: cùng một bảng
/// `NGUOI_DUNG`, khác phạm vi vai trò và khác quyền.
///
/// Vì sao học viên **không** sang HRM (chốt 08/09/2026): học viên là KHÁCH, không phải nhân sự.
/// Người phụ trách tuyển sinh cần thêm học viên nhưng không nên thấy hợp đồng và lương của giáo
/// viên — mà nếu hai nhóm dùng chung một màn thì họ buộc phải cùng một quyền.
///
/// **Gác bằng `TaiKhoan`, KHÔNG phải `LopHoc`.** Đây là lỗi tôi đã mắc và sửa trong lượt kiểm
/// tay 08/09/2026: `LopHoc.Xem` là quyền **học viên cũng có** (để xem lớp mình học), nên gác
/// bằng nó thì học viên đọc được danh sách MỌI học viên — họ tên, số điện thoại, địa chỉ, tên
/// và số điện thoại phụ huynh. Kiểm bằng tài khoản `hv1` thật mới lộ ra; test tôi viết lúc đó
/// chỉ dùng admin nên xanh hết.
///
/// `TaiKhoan.Xem` phân biệt đúng cái cần phân biệt: nhóm Giáo viên **có** nó (đã sẵn trong ma
/// trận mặc định, kèm chú thích "xem học viên lớp mình"), nhóm Học viên **không có** chức năng
/// `TaiKhoan` nào. Đây cũng là lý do không dùng `LopHocToanTrungTam`: giáo viên cố ý KHÔNG có
/// chức năng đó — nó là thứ giới hạn họ trong lớp được phân công — nên gác bằng nó sẽ chặn oan
/// chính người cần dùng màn này nhất.
///
/// Không thêm hằng `ChucNang` mới: mỗi hằng mới lại làm admin của trung tâm đã tồn tại bị 403
/// cho tới khi chạy bổ khuyết quyền (bẫy đã gặp ở giai đoạn 0).
///
/// Lưu ý phạm vi: quyền này cho đọc danh sách học viên **toàn trung tâm**, không giới hạn "học
/// viên lớp mình". Thu hẹp được thì tốt hơn, nhưng cần `IPhamViLopHoc` cho hồ sơ con người —
/// ghi vào nợ kỹ thuật, chưa làm trong lượt này.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hoc-vien")]
public class HocVienController(ISender sender) : ControllerBase
{
    private static readonly LoaiNguoiDung[] ChiHocVien = [LoaiNguoiDung.HocVien];

    [HttpGet]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<NguoiDungDto>>> DanhSach(
        [FromQuery] string? timKiem,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachNguoiDungQuery(
            timKiem, null, null, new ThamSoTrang(trang, soDong), ChiHocVien), ct));

    [HttpPost]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] TaoNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaHocVien(command.LoaiNguoiDung);
        return Ok(await sender.Send(command, ct));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatNguoiDungCommand command, CancellationToken ct)
    {
        BaoDamLaHocVien(command.LoaiNguoiDung);
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.TaiKhoan, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaNguoiDungCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Chặn dùng endpoint học viên để tạo/sửa NHÂN SỰ — chiều ngược của
    /// <c>NhanSuController.BaoDamLaNhanSu</c>. Thiếu nó thì người phụ trách tuyển sinh tự
    /// tạo được tài khoản giáo viên bằng cách gọi API trực tiếp.
    /// </summary>
    private static void BaoDamLaHocVien(LoaiNguoiDung loai)
    {
        if (loai != LoaiNguoiDung.HocVien)
            throw new Application.Common.Exceptions.AppException("KHONG_PHAI_HOC_VIEN");
    }
}
