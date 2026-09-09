using Asp.Versioning;
using GiapTech.LangCenter.LMS.API.Authorization;
using GiapTech.LangCenter.LMS.Application.NhanSu;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// HRM — cơ cấu tổ chức dạng cây (FR-22).
///
/// Gác bằng <see cref="ChucNang.PhongBan"/>, tách khỏi `GiaoVienNhanSu`: sửa cơ cấu tổ chức là
/// việc của người quản trị hoặc trưởng phòng nhân sự, còn xem/sửa hồ sơ một người là việc
/// thường ngày của người trực nhân sự.
///
/// **Không gác theo `PHONG_BAN.nguoi_quan_ly_id`** — cột đó là thông tin tổ chức, không phải
/// quyền (quy tắc #9).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/phong-ban")]
public class PhongBanController(ISender sender) : ControllerBase
{
    /// <summary>Cây phòng ban lồng nhau, kèm sĩ số từng phòng và cả nhánh.</summary>
    [HttpGet]
    [RequirePermission(ChucNang.PhongBan, HanhDong.Xem)]
    public async Task<ActionResult<List<PhongBanDto>>> Cay(CancellationToken ct)
        => Ok(await sender.Send(new LayCayPhongBanQuery(), ct));

    [HttpPost]
    [RequirePermission(ChucNang.PhongBan, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuPhongBanCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.PhongBan, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuPhongBanCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.PhongBan, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaPhongBanCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Xếp nhân sự vào phòng ban — **cách 2** của FR-22 (cách 1 là chọn ngay trên form hồ sơ).
    ///
    /// `phongBanId = null` trong thân = gỡ khỏi cơ cấu. Gác bằng `Sua` chứ không `Them`: không
    /// tạo ra bản ghi mới nào, chỉ đổi chỗ người đã có.
    /// </summary>
    [HttpPost("xep-nhan-su")]
    [RequirePermission(ChucNang.PhongBan, HanhDong.Sua)]
    public async Task<IActionResult> XepNhanSu(
        [FromBody] XepNhanSuVaoPhongBanCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
