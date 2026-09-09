using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.NhanSu;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// HRM — danh mục chức vụ (FR-24): "Ban quản lý", "Trưởng phòng", "Kế toán"…
///
/// **Khác `LoaiNguoiDung`**: đây là chức danh do admin tự quản, còn `LoaiNguoiDung` là loại
/// nghiệp vụ cố định trong code (quyết định ai gán được vào lớp, hồ sơ con nào áp dụng).
///
/// **Không gác quyền theo chức vụ** — quyền vẫn đọc từ `QUYEN_CHUC_NANG` (quy tắc #9).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chuc-vu")]
public class ChucVuController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Danh mục chức vụ. `chiDangDung=true` cho form chọn (bỏ chức vụ đã ngừng dùng).
    ///
    /// Gác bằng <see cref="ChucNang.NhanSu"/> chứ không <see cref="ChucNang.ChucVu"/>: người
    /// trực nhân sự cần ĐỌC danh mục để gán cho hồ sơ, mà không cần quyền sửa danh mục.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChucNang.NhanSu, HanhDong.Xem)]
    public async Task<ActionResult<List<ChucVuDto>>> DanhSach(
        [FromQuery] bool? chiDangDung, CancellationToken ct = default)
        => Ok(await sender.Send(new LayDanhSachChucVuQuery(chiDangDung), ct));

    [HttpPost]
    [RequirePermission(ChucNang.ChucVu, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Tao(
        [FromBody] LuuChucVuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.ChucVu, HanhDong.Sua)]
    public async Task<ActionResult<Guid>> CapNhat(
        Guid id, [FromBody] LuuChucVuCommand command, CancellationToken ct)
        => Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.ChucVu, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaChucVuCommand(id), ct);
        return NoContent();
    }
}
