using Asp.Versioning;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.QuanTri.ThietLap;
using GiapTech.LangCenter.Application.DaoTao.ThongKe;
using GiapTech.LangCenter.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiapTech.LangCenter.API.Controllers.V1;

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
    IMuiGioTrungTam muiGio,
    ISender sender)
    : ControllerBase
{
    /// <summary>
    /// Cấu hình trung tâm mà MỌI vai trò cần để hiển thị đúng.
    ///
    /// Thêm nhận diện (logo, tên viết tắt) ngày 22/09/2026 — sidebar phải hiện đúng logo trung
    /// tâm chứ không phải ô chữ cái suy từ tên.
    /// </summary>
    /// <param name="MuiGio">Múi giờ trung tâm, để vẽ lịch đúng ô ngày.</param>
    /// <param name="TenTrungTam">Tên đầy đủ — sidebar hiện cái này, không lấy từ JWT nữa.</param>
    /// <param name="TenVietTat">Tên viết tắt do trung tâm tự đặt ở Thiết lập; null = tự suy từ tên.</param>
    /// <param name="KhoaLogo">
    /// Khoá ảnh logo để gọi `GET /anh/{khoa}`; null = chưa tải logo, UI dùng ô chữ cái.
    ///
    /// Ở đây trả **khoá thật** (khác endpoint ẩn danh `/auth/ten-trung-tam` chỉ trả cờ
    /// boolean): người gọi đã đăng nhập và đã thuộc tenant này, khoá không lộ thêm gì.
    /// </param>
    public record CauHinhCuaToi(
        string MuiGio, string TenTrungTam, string? TenVietTat, string? KhoaLogo);

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
        var nhanDien = await sender.Send(new LayNhanDienTrungTamQuery(), ct);

        return Ok(new CauHinhCuaToi(
            tz.Id, nhanDien.TenTrungTam, nhanDien.TenVietTat, nhanDien.KhoaLogo));
    }

    /// <summary>
    /// FR-15 — số liệu cho màn Tổng quan, đã lọc theo phạm vi của chính người đang đăng nhập.
    ///
    /// Đặt ở `ToiController` chứ không tạo `ThongKeController` riêng: đây là *"việc của tôi
    /// hôm nay"*, không phải báo cáo toàn trung tâm. Cùng lý lẽ với `/toi/cau-hinh`.
    ///
    /// **Không gác `[RequirePermission]`** — cùng lẽ với các endpoint khác trong controller
    /// này: ai đăng nhập được cũng thấy màn Tổng quan. `IPhamViLopHoc` đã lọc dữ liệu về đúng
    /// phạm vi từng người, và số hàng chờ tự về 0 với ai không có `LopHoc.Sua`. Gác thêm bằng
    /// `ThongKe.Xem` sẽ làm giáo viên và học viên thấy màn đầu tiên trống trơn.
    /// </summary>
    [HttpGet("tong-quan")]
    public async Task<ActionResult<TongQuanDto>> TongQuan(CancellationToken ct)
        => Ok(await sender.Send(new LayTongQuanQuery(), ct));

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

        // Bỏ những thao tác KHÔNG mở lối vào hệ thống của chúng — ví dụ
        // `TieuChiDanhGia.TuLam`: học viên giữ nó để chấm giáo viên trên phiếu nhận xét ở LMS,
        // không phải để vào module nhân sự. Xem `ChucNang.MoLoiVaoHeThong`.
        var coQuyen = quyen
            .Where(x => ChucNang.MoLoiVaoHeThong(x.ChucNang, x.HanhDong))
            .Select(x => x.ChucNang)
            .ToHashSet();

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
