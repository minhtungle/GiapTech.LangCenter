using Asp.Versioning;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Application.DaoTao.HocPhi;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

/// <summary>
/// FR-14 — học phí.
///
/// **Dữ liệu tài chính**: `[RequirePermission]` chỉ quyết định có gọi được endpoint hay không.
/// Việc học viên chỉ thấy sổ của chính mình, và chỉ người quản lý tài chính sửa được sổ, nằm ở
/// `IPhamViHocPhi` trong từng handler.
///
/// Hệ thống KHÔNG xử lý tiền — không cổng thanh toán, không đối chiếu sao kê. Đây là sổ ghi
/// tay điện tử.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hoc-phi")]
public class HocPhiController(ISender sender) : ControllerBase
{
    /// <summary>Danh sách khoản thu. Học viên chỉ thấy khoản của chính mình.</summary>
    [HttpGet]
    [RequirePermission(ChucNang.HocPhi, HanhDong.Xem)]
    public async Task<ActionResult<KetQuaTrang<KhoanThuDto>>> DanhSach(
        [FromQuery] Guid? lopHocId,
        [FromQuery] Guid? hocVienId,
        [FromQuery] int trang = 1,
        [FromQuery] int soDong = 20,
        CancellationToken ct = default)
        => Ok(await sender.Send(
            new LayKhoanThuQuery(lopHocId, hocVienId, new ThamSoTrang(trang, soDong)), ct));

    /// <summary>Bảng công nợ — mặc định chỉ hiện người còn nợ.</summary>
    [HttpGet("cong-no")]
    [RequirePermission(ChucNang.HocPhi, HanhDong.Xem)]
    public async Task<ActionResult<List<CongNoDto>>> CongNo(
        [FromQuery] Guid? lopHocId,
        [FromQuery] bool chiConNo = true,
        CancellationToken ct = default)
        => Ok(await sender.Send(new LayCongNoQuery(lopHocId, chiConNo), ct));

    [HttpPost]
    [RequirePermission(ChucNang.HocPhi, HanhDong.Them)]
    public async Task<ActionResult<Guid>> Thu(
        [FromBody] ThuHocPhiCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(ChucNang.HocPhi, HanhDong.Sua)]
    public async Task<IActionResult> Sua(
        Guid id, [FromBody] SuaKhoanThuCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { errorCode = "ID_KHONG_KHOP" });
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(ChucNang.HocPhi, HanhDong.Xoa)]
    public async Task<IActionResult> Xoa(Guid id, CancellationToken ct)
    {
        await sender.Send(new XoaKhoanThuCommand(id), ct);
        return NoContent();
    }
}
