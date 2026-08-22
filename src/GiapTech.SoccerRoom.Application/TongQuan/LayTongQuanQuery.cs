using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.TongQuan;

/// <summary>
/// FR-20 — màn Tổng quan (nợ N6, làm 21/08).
///
/// Màn ĐẦU TIÊN người dùng thấy sau khi đăng nhập, nhưng trước đó chỉ có "Xin chào, admin" — 12
/// dòng JSX. Nó không thuộc FR nào nên bị bỏ sót khi liệt kê "đủ FR".
///
/// Nội dung do chủ sản phẩm chọn (21/08): **việc cần làm + trận sắp tới**. Không làm dải 4 số
/// thống kê — thứ đó đã có ở màn Thống kê và Tài chính, lặp lại chỉ để lấp chỗ trống.
///
/// Nguyên tắc: mỗi dòng phải **bấm được để tới đúng chỗ xử lý**. Một con số không kèm đường đi
/// tiếp chỉ làm người dùng biết có việc mà không biết làm ở đâu.
/// </summary>
public record ViecCanLam(
    /// <summary>Mã để frontend dịch và chọn icon — không trả câu tiếng Việt (quy tắc #3).</summary>
    string Ma,
    int SoLuong,
    /// <summary>Đường dẫn frontend để bấm vào. Null = không có chỗ nào cụ thể để tới.</summary>
    string? DuongDan);

/// <summary>Trận sắp tới gần nhất, kèm tiến độ đăng ký nếu đã gửi lời mời.</summary>
public record TranSapToi(
    Guid Id,
    DateTimeOffset ThoiGian,
    string? TenDoiThu,
    /// <summary>Số người đã nhận đá. Null = chưa gửi lời mời đăng ký cho trận này.</summary>
    int? DaNhan,
    int? TongDuocMoi);

public record TongQuanDto(
    string TenDoi,
    /// <summary>Người đang đăng nhập có phải trưởng nhóm — quyết định hiện việc của đội hay của mình.</summary>
    bool LaTruongNhom,
    IReadOnlyList<ViecCanLam> ViecCanLams,
    TranSapToi? TranKeTiep,
    /// <summary>Trận đã đá gần nhất — để biết kết quả vừa rồi mà không phải sang Lịch thi đấu.</summary>
    TranSapToi? TranVuaRoi);

public record LayTongQuanQuery : IRequest<TongQuanDto>;

public class LayTongQuanHandler(IAppDbContext db, ICurrentUser currentUser, ICurrentTenant tenant)
    : IRequestHandler<LayTongQuanQuery, TongQuanDto>
{
    public async Task<TongQuanDto> Handle(LayTongQuanQuery request, CancellationToken ct)
    {
        var bayGio = DateTimeOffset.UtcNow;

        var nguoiDung = await db.NguoiDungs
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => new { u.LaTruongNhom, u.CauThuId })
            .FirstOrDefaultAsync(ct);

        var laTruongNhom = nguoiDung?.LaTruongNhom ?? false;

        var tenDoi = await db.Tenants
            .Where(t => t.Id == tenant.TenantId)
            .Select(t => t.TenDoi)
            .FirstOrDefaultAsync(ct) ?? "";

        var viec = new List<ViecCanLam>();

        // ----- Việc của TRƯỞNG NHÓM: của cả đội -----
        if (laTruongNhom)
        {
            // Lời mời thách đấu đang chờ ta trả lời. `toiGui == false` mới là việc của ta —
            // lời mời ta gửi thì đang chờ bên kia.
            var loiMoiChoTaTraLoi = await db.LoiMoiThachDaus
                .CountAsync(l => l.TenantNhanId == tenant.TenantId
                                 && l.TrangThai == TrangThaiLoiMoi.ChoPhanHoi, ct);
            if (loiMoiChoTaTraLoi > 0)
                viec.Add(new ViecCanLam("LOI_MOI_THACH_DAU", loiMoiChoTaTraLoi, "/hom-thu"));

            // Trận sắp tới CHƯA gửi lời mời đăng ký — dễ quên nhất, và quên thì tới sát giờ mới
            // biết thiếu người.
            var tranChuaMoi = await db.TranDaus
                .CountAsync(t => t.ThoiGian > bayGio
                                 && t.TrangThai == TrangThaiTranDau.DaLenLich
                                 && !db.LoiMoiThamGias.Any(l => l.TranDauId == t.Id), ct);
            if (tranChuaMoi > 0)
                viec.Add(new ViecCanLam("TRAN_CHUA_MOI_DANG_KY", tranChuaMoi, "/lich-thi-dau"));

            // Người còn nợ quỹ ở các đợt CHƯA đóng. Đếm theo NGƯỜI-ĐỢT, không gộp theo người:
            // "4 khoản chưa thu" đúng hơn "3 người còn nợ" khi một người nợ hai đợt.
            var conNoQuy = await db.DongGopQuys
                .CountAsync(d => d.SoTienDaDong < d.SoTienCanDong
                                 && d.Quy.TrangThai != TrangThaiQuy.DaDong, ct);
            if (conNoQuy > 0)
                viec.Add(new ViecCanLam("CON_NO_QUY", conNoQuy, "/tai-chinh"));
        }

        // ----- Việc của CHÍNH MÌNH: ai cũng có, kể cả trưởng nhóm -----
        if (nguoiDung?.CauThuId is { } cauThuId)
        {
            // Lời mời đăng ký chưa trả lời, chỉ tính trận CHƯA đá — trận qua rồi thì trả lời
            // cũng vô nghĩa.
            var chuaTraLoi = await db.PhanHoiThamGias
                .CountAsync(p => p.CauThuId == cauThuId
                                 && p.TraLoi == TraLoiThamGia.ChuaTraLoi
                                 && !p.LoiMoi.DaDong
                                 && p.LoiMoi.TranDau.ThoiGian > bayGio, ct);
            if (chuaTraLoi > 0)
                viec.Add(new ViecCanLam("TOI_CHUA_TRA_LOI", chuaTraLoi, "/hom-thu"));

            var toiConNo = await db.DongGopQuys
                .CountAsync(d => d.CauThuId == cauThuId
                                 && d.SoTienDaDong < d.SoTienCanDong
                                 && d.Quy.TrangThai != TrangThaiQuy.DaDong, ct);
            if (toiConNo > 0)
                viec.Add(new ViecCanLam("TOI_CON_NO_QUY", toiConNo, "/tai-chinh"));
        }

        // ----- Trận kế tiếp + trận vừa rồi -----
        var tranKeTiep = await LayTran(db, ct, sapToi: true, bayGio);
        var tranVuaRoi = await LayTran(db, ct, sapToi: false, bayGio);

        return new TongQuanDto(tenDoi, laTruongNhom, viec, tranKeTiep, tranVuaRoi);
    }

    /// <summary>
    /// Trận gần nhất theo hướng thời gian, kèm tiến độ đăng ký.
    ///
    /// Bỏ trận `DaHuy` và `LuuTru`: chúng không phải việc sắp tới, và hiện lên chỉ gây nhầm.
    /// </summary>
    private static async Task<TranSapToi?> LayTran(
        IAppDbContext db, CancellationToken ct, bool sapToi, DateTimeOffset bayGio)
    {
        var q = db.TranDaus.Where(t =>
            t.TrangThai != TrangThaiTranDau.DaHuy && t.TrangThai != TrangThaiTranDau.LuuTru);

        q = sapToi
            ? q.Where(t => t.ThoiGian > bayGio).OrderBy(t => t.ThoiGian)
            : q.Where(t => t.ThoiGian <= bayGio).OrderByDescending(t => t.ThoiGian);

        return await q
            .Select(t => new TranSapToi(
                t.Id,
                t.ThoiGian,
                t.DoiThu != null ? t.DoiThu.TenDoi : null,
                // Null khi chưa gửi lời mời — khác hẳn với "đã gửi mà chưa ai nhận" (0).
                db.LoiMoiThamGias.Any(l => l.TranDauId == t.Id)
                    ? db.PhanHoiThamGias.Count(p =>
                        p.LoiMoi.TranDauId == t.Id && p.TraLoi == TraLoiThamGia.ThamGia)
                    : null,
                db.LoiMoiThamGias.Any(l => l.TranDauId == t.Id)
                    ? db.PhanHoiThamGias.Count(p => p.LoiMoi.TranDauId == t.Id)
                    : null))
            .FirstOrDefaultAsync(ct);
    }
}
