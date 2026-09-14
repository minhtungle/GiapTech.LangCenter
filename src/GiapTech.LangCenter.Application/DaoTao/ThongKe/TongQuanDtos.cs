using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.ThongKe;

/// <summary>
/// FR-15 — số liệu cho màn Tổng quan.
///
/// **Mỗi con số ở đây phải dẫn tới một màn xử lý.** Nguyên tắc rút từ dự án trước và đã ghi
/// trong `TongQuan.tsx`: *"một con số không kèm đường đi tiếp chỉ làm người dùng biết có việc
/// mà không biết làm ở đâu"*. Nên DTO này cố tình KHÔNG có những số như "tổng số học viên" —
/// đó là thông tin để ngắm, không phải việc để làm.
///
/// **Không có số tiền nào** (chốt 12/09/2026): LMS không quản lý và không hiển thị tiền học;
/// chỉ CRM nắm số tiền. Kế hoạch giai đoạn 5 từng ghi "cảnh báo nợ học phí" — mục đó nay không
/// còn áp dụng.
///
/// **Một DTO cho mọi vai trò.** Không tách ba dashboard Admin/Giáo viên/Học viên như kế hoạch
/// cũ: `IPhamViLopHoc` đã lọc đúng phạm vi mỗi người (admin thấy toàn trung tâm, giáo viên thấy
/// lớp mình dạy, học viên thấy lớp mình học), nên cùng một truy vấn cho ra con số đúng với
/// từng người. Ba bản sao là ba chỗ phải sửa khi đổi, và trái với cách dự án làm phân quyền —
/// suy từ dữ liệu quyền chứ không hard-code vai trò.
/// </summary>
public record TongQuanDto(
    /// <summary>Buổi học hôm nay trong phạm vi người này — dẫn tới `/lms/lop-hoc`.</summary>
    int BuoiHomNay,

    /// <summary>
    /// Buổi ĐÃ QUA mà chưa chốt điểm danh — việc tồn đọng rõ ràng nhất của giáo viên.
    ///
    /// "Chưa chốt" = buổi `DaLenLich` có thời điểm kết thúc đã trôi qua. Không tính buổi
    /// `DaHoanThanh` (đã chốt) hay `DaHuy`.
    /// </summary>
    int BuoiQuaHanChuaDiemDanh,

    /// <summary>Bài nộp chưa chấm (`Diem == null`) — dẫn tới lớp có bài đó.</summary>
    int BaiNopChuaCham,

    /// <summary>
    /// Học viên đã mua khoá, đang chờ xếp lớp (FR-21) — dẫn tới tab Chờ xếp lớp.
    ///
    /// **0 với người không có `LopHoc.Sua`**: hàng chờ là việc của người điều phối, và endpoint
    /// chờ xếp lớp cũng gác bằng quyền đó. Trả số cho người không bấm được là mời họ đi vào ngõ
    /// cụt — đúng lỗi menu "Chờ xếp lớp" đã gặp 10/09/2026.
    /// </summary>
    int ChoXepLop,

    /// <summary>Lớp đang hoạt động trong phạm vi — bối cảnh cho các số trên.</summary>
    int LopDangHoatDong);

public record LayTongQuanQuery : IRequest<TongQuanDto>;

public class LayTongQuanHandler(
    IAppDbContext db,
    IPhamViLopHoc phamVi,
    IQuyenService quyenService,
    ICurrentTenant tenant,
    ICurrentUser currentUser,
    IMuiGioTrungTam muiGio)
    : IRequestHandler<LayTongQuanQuery, TongQuanDto>
{
    public async Task<TongQuanDto> Handle(LayTongQuanQuery request, CancellationToken ct)
    {
        // Phạm vi lọc MỘT LẦN rồi dùng lại cho mọi số: admin nhận nguyên truy vấn, giáo viên
        // chỉ lớp mình dạy, học viên chỉ lớp mình học. Đây là lý do một DTO phục vụ được mọi
        // vai trò mà không cần biết vai trò là gì.
        var lopTrongPhamVi = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xem, ct);
        var idLop = lopTrongPhamVi.Select(l => l.Id);

        var bayGio = DateTimeOffset.UtcNow;

        /*
          "Hôm nay" theo MÚI GIỜ TRUNG TÂM, không phải UTC.

          Buổi 7h sáng giờ Việt Nam là 0h UTC **cùng ngày**, nhưng buổi 6h sáng là 23h UTC
          **hôm trước** — lấy ngày theo UTC sẽ đẩy các buổi sáng sớm sang hôm trước và đếm
          thiếu. `BUOI_HOC.bat_dau` lưu thời điểm tuyệt đối nên phải quy về khoảng [0h, 24h)
          của múi giờ trung tâm rồi mới so.
        */
        var tz = await muiGio.LayMuiGio(ct);
        var homNay = TimeZoneInfo.ConvertTime(bayGio, tz).Date;
        var dauNgay = new DateTimeOffset(homNay, tz.GetUtcOffset(homNay));
        var cuoiNgay = dauNgay.AddDays(1);

        var buoiHomNay = await db.BuoiHocs
            .Where(b => idLop.Contains(b.LopHocId)
                        && b.TrangThai != TrangThaiBuoiHoc.DaHuy
                        && b.BatDau >= dauNgay && b.BatDau < cuoiNgay)
            .CountAsync(ct);

        var buoiQuaHan = await db.BuoiHocs
            .Where(b => idLop.Contains(b.LopHocId)
                        && b.TrangThai == TrangThaiBuoiHoc.DaLenLich
                        && b.KetThuc < bayGio)
            .CountAsync(ct);

        var baiNopChuaCham = await db.BaiNops
            // `BAI_TAP` gắn BUỔI HỌC (không gắn lớp trực tiếp) nên phải đi qua hai chặng.
            .Where(n => n.Diem == null && idLop.Contains(n.BaiTap.BuoiHoc.LopHocId))
            .CountAsync(ct);

        var lopHoatDong = await lopTrongPhamVi
            .CountAsync(l => l.TrangThai != TrangThaiLopHoc.Nhap
                             && l.TrangThai != TrangThaiLopHoc.DaHuy
                             && l.TrangThai != TrangThaiLopHoc.DaKetThuc, ct);

        // Hàng chờ chỉ có nghĩa với người ĐIỀU PHỐI được. Xem chú thích ở `ChoXepLop`.
        var duocXepLop = tenant.TenantId is { } tid && currentUser.TaiKhoanId is { } tkId
                         // `XepLop.Duyet` chứ không `LopHoc.Sua` (14/09/2026): hàng chờ nay
                         // là chức năng riêng. Dùng quyền cũ thì người có quyền sửa lớp nhưng
                         // không được duyệt đơn vẫn thấy số chờ — rồi bấm vào nhận 403.
                         && await quyenService.CoQuyenAsync(
                             tid, tkId, ChucNang.XepLop, HanhDong.Duyet, ct);

        var choXepLop = duocXepLop
            ? await db.YeuCauXepLops.CountAsync(
                y => y.TrangThai == TrangThaiYeuCauXepLop.DangCho, ct)
            : 0;

        return new TongQuanDto(
            buoiHomNay, buoiQuaHan, baiNopChuaCham, choXepLop, lopHoatDong);
    }
}
