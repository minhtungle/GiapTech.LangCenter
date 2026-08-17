using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.HomThu;

/// <summary>
/// Hòm thư — nơi tập trung mọi thứ cần người dùng phản hồi.
///
/// Hai loại thư:
/// 1. **Lời mời giao hữu** từ đội bạn (FR-09) — chỉ trưởng nhóm xử lý được.
/// 2. **Lời mời đăng ký thi đấu** do trưởng nhóm gửi — cầu thủ trả lời.
///
/// Gộp một màn thay vì hai vì với người dùng đó là cùng một việc: "có gì cần tôi trả lời
/// không". Tách ra thì cầu thủ phải nhớ vào hai chỗ.
/// </summary>
public record ThuLoiMoiDangKyDto(
    Guid Id,
    Guid TranDauId,
    DateTimeOffset ThoiGianTran,
    string? TenDoiThu,
    string? LoiNhan,
    DateTimeOffset? HanTraLoi,
    bool DaDong,
    /// <summary>Câu trả lời của CHÍNH người đang đăng nhập. Null nếu họ không có hồ sơ cầu thủ.</summary>
    TraLoiThamGia? TraLoiCuaToi,
    int SoThamGia,
    int SoKhongThamGia,
    int SoChuaChac,
    int SoChuaTraLoi);

/// <summary>Một dòng trong bảng tổng hợp phản hồi — chỉ trưởng nhóm xem.</summary>
public record PhanHoiDto(
    Guid CauThuId, string HoTen, TraLoiThamGia TraLoi, string? GhiChu,
    DateTimeOffset? ThoiGianTraLoi);

public record HomThuDto(
    /// <summary>Người đang đăng nhập có phải trưởng nhóm không — quyết định UI hiện gì.</summary>
    bool LaTruongNhom,
    List<ThuLoiMoiDangKyDto> LoiMoiDangKy);

// ---------- Queries ----------

public record LayHomThuQuery : IRequest<HomThuDto>;

public class LayHomThuHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<LayHomThuQuery, HomThuDto>
{
    public async Task<HomThuDto> Handle(LayHomThuQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var nguoiDung = await db.NguoiDungs
            .Where(u => u.Id == userId)
            .Select(u => new { u.LaTruongNhom, u.CauThuId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KhongTimThayException($"NguoiDung {userId}");

        var loiMois = await db.LoiMoiThamGias
            .OrderByDescending(l => l.TranDau.ThoiGian)
            .Select(l => new
            {
                l.Id,
                l.TranDauId,
                l.TranDau.ThoiGian,
                TenDoiThu = l.TranDau.DoiThu != null ? l.TranDau.DoiThu.TenDoi : null,
                l.LoiNhan,
                l.HanTraLoi,
                l.DaDong,
                // Đếm ở DB thay vì kéo cả danh sách phản hồi về rồi đếm trong bộ nhớ.
                SoThamGia = l.PhanHois.Count(p => p.TraLoi == TraLoiThamGia.ThamGia),
                SoKhongThamGia = l.PhanHois.Count(p => p.TraLoi == TraLoiThamGia.KhongThamGia),
                SoChuaChac = l.PhanHois.Count(p => p.TraLoi == TraLoiThamGia.ChuaChac),
                SoChuaTraLoi = l.PhanHois.Count(p => p.TraLoi == TraLoiThamGia.ChuaTraLoi),
                // Câu trả lời của chính người đang xem — null khi họ không gắn hồ sơ cầu thủ
                // (tài khoản quản lý thuần tuý).
                TraLoiCuaToi = nguoiDung.CauThuId == null
                    ? (TraLoiThamGia?)null
                    : l.PhanHois
                        .Where(p => p.CauThuId == nguoiDung.CauThuId)
                        .Select(p => (TraLoiThamGia?)p.TraLoi)
                        .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return new HomThuDto(
            nguoiDung.LaTruongNhom,
            loiMois
                .Select(l => new ThuLoiMoiDangKyDto(
                    l.Id, l.TranDauId, l.ThoiGian, l.TenDoiThu, l.LoiNhan, l.HanTraLoi, l.DaDong,
                    l.TraLoiCuaToi,
                    l.SoThamGia, l.SoKhongThamGia, l.SoChuaChac, l.SoChuaTraLoi))
                .ToList());
    }
}

public record LayPhanHoiQuery(Guid LoiMoiId) : IRequest<List<PhanHoiDto>>;

public class LayPhanHoiHandler(IAppDbContext db)
    : IRequestHandler<LayPhanHoiQuery, List<PhanHoiDto>>
{
    public async Task<List<PhanHoiDto>> Handle(LayPhanHoiQuery request, CancellationToken ct)
        => await db.PhanHoiThamGias
            .Where(p => p.LoiMoiId == request.LoiMoiId)
            // Người đã nhận lời lên đầu — trưởng nhóm xếp đội hình từ danh sách này.
            .OrderBy(p => p.TraLoi == TraLoiThamGia.ThamGia ? 0
                : p.TraLoi == TraLoiThamGia.ChuaChac ? 1
                : p.TraLoi == TraLoiThamGia.ChuaTraLoi ? 2 : 3)
            .ThenBy(p => p.CauThu.HoTen)
            .Select(p => new PhanHoiDto(
                p.CauThuId, p.CauThu.HoTen, p.TraLoi, p.GhiChu, p.ThoiGianTraLoi))
            .ToListAsync(ct);
}

// ---------- Commands ----------

/// <summary>
/// Trưởng nhóm gửi lời mời đăng ký cho một trận.
///
/// Tạo sẵn hàng phản hồi "chưa trả lời" cho **mọi cầu thủ** của CLB: trưởng nhóm cần thấy ai
/// chưa trả lời, không chỉ ai đã đồng ý.
/// </summary>
public record GuiLoiMoiDangKyCommand(
    Guid TranDauId, string? LoiNhan, DateTimeOffset? HanTraLoi) : IRequest<Guid>;

public class GuiLoiMoiDangKyValidator : AbstractValidator<GuiLoiMoiDangKyCommand>
{
    public GuiLoiMoiDangKyValidator()
    {
        RuleFor(x => x.TranDauId).NotEmpty();
        RuleFor(x => x.LoiNhan).MaximumLength(1000);
    }
}

public class GuiLoiMoiDangKyHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GuiLoiMoiDangKyCommand, Guid>
{
    public async Task<Guid> Handle(GuiLoiMoiDangKyCommand request, CancellationToken ct)
    {
        var nguoiGui = await XacThucTruongNhom.Lay(db, currentUser, ct);

        if (!await db.TranDaus.AnyAsync(t => t.Id == request.TranDauId, ct))
            throw new KhongTimThayException($"TranDau {request.TranDauId}");

        if (await db.LoiMoiThamGias.AnyAsync(l => l.TranDauId == request.TranDauId, ct))
            throw new AppException("TRAN_DA_CO_LOI_MOI");

        var loiMoi = new Domain.Entities.LoiMoiThamGia
        {
            TranDauId = request.TranDauId,
            NguoiGuiId = nguoiGui,
            LoiNhan = request.LoiNhan,
            HanTraLoi = request.HanTraLoi,
        };
        db.LoiMoiThamGias.Add(loiMoi);

        var cauThuIds = await db.CauThus.Select(c => c.Id).ToListAsync(ct);
        if (cauThuIds.Count == 0) throw new AppException("CHUA_CO_CAU_THU_NAO");

        foreach (var cauThuId in cauThuIds)
            db.PhanHoiThamGias.Add(new Domain.Entities.PhanHoiThamGia
            {
                LoiMoi = loiMoi,
                CauThuId = cauThuId,
            });

        await db.SaveChangesAsync(ct);
        return loiMoi.Id;
    }
}

/// <summary>Cầu thủ trả lời lời mời — chỉ sửa được phản hồi của CHÍNH mình.</summary>
public record TraLoiThamGiaCommand(Guid LoiMoiId, TraLoiThamGia TraLoi, string? GhiChu)
    : IRequest;

public class TraLoiThamGiaValidator : AbstractValidator<TraLoiThamGiaCommand>
{
    public TraLoiThamGiaValidator()
    {
        RuleFor(x => x.LoiMoiId).NotEmpty();
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}

public class TraLoiThamGiaHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<TraLoiThamGiaCommand>
{
    public async Task Handle(TraLoiThamGiaCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var cauThuId = await db.NguoiDungs
            .Where(u => u.Id == userId)
            .Select(u => u.CauThuId)
            .FirstOrDefaultAsync(ct);

        // Tài khoản quản lý thuần tuý không gắn hồ sơ cầu thủ thì không có gì để đăng ký.
        if (cauThuId is not { } ctId)
            throw new AppException("TAI_KHOAN_CHUA_GAN_CAU_THU");

        var loiMoi = await db.LoiMoiThamGias
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        if (loiMoi.DaDong) throw new AppException("LOI_MOI_DA_DONG");

        var phanHoi = await db.PhanHoiThamGias
            .FirstOrDefaultAsync(p => p.LoiMoiId == request.LoiMoiId && p.CauThuId == ctId, ct)
            ?? throw new AppException("KHONG_NAM_TRONG_DANH_SACH_MOI");

        phanHoi.TraLoi = request.TraLoi;
        phanHoi.GhiChu = request.GhiChu;
        phanHoi.ThoiGianTraLoi = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Trưởng nhóm đóng lời mời khi đã chốt đội hình.</summary>
public record DongLoiMoiCommand(Guid LoiMoiId, bool Dong) : IRequest;

public class DongLoiMoiHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DongLoiMoiCommand>
{
    public async Task Handle(DongLoiMoiCommand request, CancellationToken ct)
    {
        await XacThucTruongNhom.Lay(db, currentUser, ct);

        var loiMoi = await db.LoiMoiThamGias
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        loiMoi.DaDong = request.Dong;
        await db.SaveChangesAsync(ct);
    }
}

public record XoaLoiMoiDangKyCommand(Guid LoiMoiId) : IRequest;

public class XoaLoiMoiDangKyHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<XoaLoiMoiDangKyCommand>
{
    public async Task Handle(XoaLoiMoiDangKyCommand request, CancellationToken ct)
    {
        await XacThucTruongNhom.Lay(db, currentUser, ct);

        var loiMoi = await db.LoiMoiThamGias
            .FirstOrDefaultAsync(l => l.Id == request.LoiMoiId, ct)
            ?? throw new KhongTimThayException($"LoiMoiThamGia {request.LoiMoiId}");

        // Phản hồi xoá theo (Cascade) — chúng vô nghĩa khi không còn lời mời.
        db.LoiMoiThamGias.Remove(loiMoi);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Kiểm quyền trưởng nhóm.
///
/// Đặt ở tầng Application chứ không dùng <c>[RequirePermission]</c>: trưởng nhóm là **cờ trên
/// tài khoản**, không phải chức năng trong QUYEN_CHUC_NANG. Thêm nó vào bảng quyền sẽ bắt mọi
/// CLB đang chạy phải cấp lại quyền.
/// </summary>
internal static class XacThucTruongNhom
{
    public static async Task<Guid> Lay(
        IAppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var laTruongNhom = await db.NguoiDungs
            .Where(u => u.Id == userId)
            .Select(u => u.LaTruongNhom)
            .FirstOrDefaultAsync(ct);

        if (!laTruongNhom) throw new AppException("CHI_TRUONG_NHOM_DUOC_LAM");

        return userId;
    }
}
