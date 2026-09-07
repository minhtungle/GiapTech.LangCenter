using Asp.Versioning;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.LMS.API.Controllers.V1;

/// <summary>
/// Thông tin về CHÍNH người đang đăng nhập.
///
/// Không gác `[RequirePermission]`: ai đăng nhập được cũng phải biết mình có quyền gì, nếu
/// không thì frontend không dựng nổi menu. Endpoint chỉ trả quyền của **chính** phiên hiện
/// tại — không nhận tham số id nên không có đường dò quyền người khác.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/toi")]
public class ToiController(
    IQuyenService quyenService,
    ICurrentTenant tenant,
    ICurrentUser currentUser,
    IMuiGioTrungTam muiGio)
    : ControllerBase
{
    /// <summary>Cấu hình trung tâm mà MỌI vai trò cần để hiển thị đúng.</summary>
    public record CauHinhCuaToi(string MuiGio);

    /// <summary>
    /// Múi giờ của trung tâm — frontend cần để vẽ lịch đúng ô ngày.
    ///
    /// Không lấy từ `/thiet-lap` vì endpoint đó gác bằng `ThietLapChung.Xem`: giáo viên và học
    /// viên không gọi được, mà họ chính là người xem lịch nhiều nhất.
    ///
    /// Vì sao không dùng múi giờ của MÁY người xem: buổi 18:00 giờ Việt Nam mở trên máy đặt
    /// UTC+9 sẽ hiện 20:00, và với lịch thì lệch giờ còn làm buổi **nhảy sang ô ngày khác** —
    /// sai rõ hơn nhiều so với bảng.
    /// </summary>
    [HttpGet("cau-hinh")]
    public async Task<ActionResult<CauHinhCuaToi>> CauHinh(CancellationToken ct)
    {
        var tz = await muiGio.LayMuiGio(ct);
        return Ok(new CauHinhCuaToi(tz.Id));
    }

    /// <summary>Một dòng quyền: chức năng + thao tác.</summary>
    public record QuyenCuaToi(string ChucNang, string HanhDong);

    /// <summary>Hệ thống nào người này vào được — để dựng bộ chuyển hệ thống.</summary>
    public record HeThongCuaToi(List<string> Ma);

    /// <summary>
    /// Danh sách hệ thống (HRM · CRM · LMS) mà người đang đăng nhập có ít nhất một quyền.
    ///
    /// Tính ở BACKEND chứ không để frontend suy từ danh sách quyền: quy tắc "vào được hệ thống
    /// nào" phải giống nhau ở mọi nơi, và frontend suy sai thì bộ chuyển đưa người dùng tới
    /// một sidebar trống.
    ///
    /// **Chức năng dùng chung không tính** (`TaiKhoan`, `PhanQuyen`…): nếu tính thì người chỉ
    /// có quyền quản trị tài khoản sẽ "vào được" cả ba hệ thống, mà cả ba đều chỉ hiện đúng
    /// nhóm quản trị — ba lối vào giống hệt nhau, bộ chuyển thành vô nghĩa.
    ///
    /// Không sắp xếp lại: thứ tự enum (Hrm · Crm · Lms) là thứ tự hiển thị.
    /// </summary>
    [HttpGet("he-thong")]
    public async Task<ActionResult<HeThongCuaToi>> HeThongs(CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return Ok(new HeThongCuaToi([]));

        var quyen = await quyenService.LayTatCaQuyenAsync(tid, tkId, ct);
        var coQuyen = quyen.Select(x => x.ChucNang).ToHashSet();

        return Ok(new HeThongCuaToi(
            Enum.GetValues<HeThong>()
                .Where(ht => ChucNang.ChucNangCua(ht).Any(coQuyen.Contains))
                .Select(ht => ht.ToString())
                .ToList()));
    }

    /// <summary>
    /// Toàn bộ quyền hiệu lực, để frontend ẩn menu và nút.
    ///
    /// **Ẩn ở frontend là tiện lợi, không phải bảo vệ** — mọi endpoint vẫn tự gác quyền của
    /// nó. Danh sách này chỉ để người dùng không phải bấm vào rồi mới biết mình không được
    /// phép.
    /// </summary>
    [HttpGet("quyen")]
    public async Task<ActionResult<List<QuyenCuaToi>>> Quyen(CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid || currentUser.TaiKhoanId is not { } tkId)
            return Ok(new List<QuyenCuaToi>());

        var ds = await quyenService.LayTatCaQuyenAsync(tid, tkId, ct);

        return Ok(ds
            .Select(x => new QuyenCuaToi(x.ChucNang, x.HanhDong.ToString()))
            .OrderBy(x => x.ChucNang).ThenBy(x => x.HanhDong)
            .ToList());
    }
}
