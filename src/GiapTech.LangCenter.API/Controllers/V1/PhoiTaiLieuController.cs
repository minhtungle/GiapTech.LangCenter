using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.QuanTri.Phoi;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// Phôi tài liệu — mẫu .docx có biến `{{key}}`, điền giá trị rồi xuất bản in (09/10/2026).
/// Nhãn trên giao diện: "Thiết lập file".
///
/// Tải tệp dùng `multipart/form-data` chứ không JSON base64: phôi tới 20 MB, base64 phình
/// thêm 33% và nằm trọn trong bộ nhớ ở cả hai đầu.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/phoi-tai-lieu")]
public class PhoiTaiLieuController(ISender sender, IAppDbContext db, ILuuTruTep luuTru)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<ActionResult<IReadOnlyList<PhoiTaiLieuDto>>> DanhSach(
        [FromQuery] bool? dangDung, CancellationToken ct)
        => Ok(await sender.Send(new LayDanhSachPhoiQuery(dangDung), ct));

    [HttpGet("{id:guid}/ban-xuat")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<ActionResult<IReadOnlyList<BanXuatDto>>> BanXuat(
        Guid id, CancellationToken ct)
        => Ok(await sender.Send(new LayBanXuatQuery(id), ct));

    /// <summary>
    /// Mọi bản đã xuất gần đây — để chọn đính kèm khi gửi mail cho khách.
    ///
    /// Gác bằng `PhoiTaiLieu.Xem`: người gửi mail phải được xem phôi mới đính kèm được bản
    /// xuất từ nó. Quyền gửi mail (`EmailKhachHang.GuiThu`) không tự mở quyền đọc tài liệu.
    /// </summary>
    [HttpGet("ban-xuat")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<ActionResult<IReadOnlyList<BanXuatKemPhoiDto>>> BanXuatGanDay(
        CancellationToken ct)
        => Ok(await sender.Send(new LayBanXuatGanDayQuery(), ct));

    /// <summary>Tải phôi .docx lên; hệ thống đọc luôn danh sách key `{{...}}` trong tệp.</summary>
    [HttpPost]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Them)]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> Tao(
        IFormFile tep, [FromForm] string ten, [FromForm] string? moTa, CancellationToken ct)
    {
        await using var s = tep.OpenReadStream();
        return Ok(await sender.Send(
            new TaoPhoiCommand(ten, moTa, s, tep.ContentType, tep.FileName), ct));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Sua)]
    public async Task<IActionResult> CapNhat(
        Guid id, [FromBody] CapNhatPhoiCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        await sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>
    /// Thay tệp .docx. Danh sách key đọc lại từ tệp mới; giá trị mặc định của key còn tồn
    /// tại được giữ, key biến mất thì giá trị cũng đi.
    /// </summary>
    [HttpPut("{id:guid}/tep")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Sua)]
    public async Task<IActionResult> ThayTep(Guid id, IFormFile tep, CancellationToken ct)
    {
        await using var s = tep.OpenReadStream();
        await sender.Send(new ThayTepPhoiCommand(id, s, tep.ContentType, tep.FileName), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaPhoiCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Xuất bản .docx đã điền giá trị. Trả id bản xuất — tải về qua
    /// <see cref="TaiBanXuat"/>.
    ///
    /// Gác bằng `Them` chứ không `Xem`: xuất file sinh ra một hàng `BAN_XUAT_PHOI` và một tệp
    /// trong kho. Người chỉ được xem phôi không nên tạo ra dữ liệu mới.
    /// </summary>
    [HttpPost("{id:guid}/xuat")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Xuat(
        Guid id, [FromBody] Dictionary<string, string?> giaTri, CancellationToken ct)
        => Ok(await sender.Send(new XuatPhoiCommand(id, giaTri), ct));

    /// <summary>
    /// Bản .docx đã điền giá trị, **không lưu** — frontend dựng HTML xem trước bằng Mammoth.
    ///
    /// Gác bằng `Xem` chứ không `Them`: xem trước không tạo ra dữ liệu nào, nên người chỉ
    /// được đọc phôi vẫn kiểm được nội dung trước khi nhờ người khác xuất.
    /// </summary>
    [HttpPost("{id:guid}/xem-truoc")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<IActionResult> XemTruoc(
        Guid id, [FromBody] Dictionary<string, string?> giaTri, CancellationToken ct)
    {
        var s = await sender.Send(new XemTruocPhoiQuery(id, giaTri), ct);
        return File(s, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    /// <summary>Tải tệp phôi GỐC (chưa điền) — để người dùng xem lại mình đã tải lên gì.</summary>
    [HttpGet("{id:guid}/tep")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<IActionResult> TaiTepGoc(Guid id, CancellationToken ct)
    {
        var phoi = await db.PhoiTaiLieus.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (phoi is null) return NotFound();

        var tep = await luuTru.TaiVe(phoi.KhoaTep, ct);
        if (tep is null) throw new AppException("PHOI_MAT_TEP");
        return File(tep.NoiDung, tep.LoaiNoiDung, phoi.TenTepGoc);
    }

    /// <summary>
    /// Tải một bản đã xuất.
    ///
    /// Tra `BAN_XUAT_PHOI` để lấy khoá thay vì nhận khoá từ client: khoá là đường dẫn trong
    /// kho, nhận từ client là mở đường đọc tệp của tenant khác dù `MinioLuuTruTep` có kiểm
    /// tiền tố — đi qua bảng thì Global Query Filter kiểm hộ (quy tắc #2).
    /// </summary>
    [HttpGet("ban-xuat/{banXuatId:guid}/tep")]
    [RequirePermission(ChucNang.PhoiTaiLieu, HanhDong.Xem)]
    public async Task<IActionResult> TaiBanXuat(Guid banXuatId, CancellationToken ct)
    {
        var ban = await db.BanXuatPhois.FirstOrDefaultAsync(x => x.Id == banXuatId, ct);
        if (ban is null) return NotFound();

        var tep = await luuTru.TaiVe(ban.KhoaTep, ct);
        if (tep is null) throw new AppException("PHOI_MAT_TEP");
        return File(tep.NoiDung, tep.LoaiNoiDung, ban.TenTep);
    }
}
