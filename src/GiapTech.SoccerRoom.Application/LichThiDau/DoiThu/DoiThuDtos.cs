using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.LichThiDau.DoiThu;

/// <summary>
/// Sổ đối thủ của CLB — nền cho FR-09 (lời mời) và FR-10 (trận đấu).
///
/// DTO phải chứa đủ mọi trường mà lệnh cập nhật ghi đè (quy tắc #1).
/// </summary>
public record DoiThuDto(
    Guid Id, string TenDoi, string? LienHe, string? GhiChu, int SoTranDaDau,
    /// <summary>Mã đội nếu CLB này cũng dùng hệ thống. Null khi chỉ có trong sổ của ta.</summary>
    string? MaDoiHeThong);

// ---------- Queries ----------

public record LayDanhSachDoiThuQuery(string? TimKiem = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<DoiThuDto>>;

public class LayDanhSachDoiThuHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachDoiThuQuery, KetQuaTrang<DoiThuDto>>
{
    public async Task<KetQuaTrang<DoiThuDto>> Handle(
        LayDanhSachDoiThuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.DoiThus.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(d => d.TenDoi.ToLower().Contains(tu));
        }

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(d => d.TenDoi)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(d => new DoiThuDto(
                d.Id, d.TenDoi, d.LienHe, d.GhiChu,
                db.TranDaus.Count(t => t.DoiThuId == d.Id),
                d.MaDoiHeThong))
            .ToListAsync(ct);

        return new KetQuaTrang<DoiThuDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record LuuDoiThuCommand(
    Guid? Id, string TenDoi, string? LienHe, string? GhiChu,
    string? MaDoiHeThong = null) : IRequest<Guid>;

public class LuuDoiThuValidator : AbstractValidator<LuuDoiThuCommand>
{
    public LuuDoiThuValidator()
    {
        RuleFor(x => x.TenDoi).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LienHe).MaximumLength(200);

        // Định dạng mã đội KHÔNG kiểm ở đây mà ở handler: pipeline validation gộp mọi lỗi
        // thành một mã DU_LIEU_KHONG_HOP_LE duy nhất, nên `WithErrorCode` bị mất và frontend
        // không nói được "mã đội sai" thay vì "dữ liệu sai". Xem LuuDoiThuHandler.
    }
}

public class LuuDoiThuHandler(IAppDbContext db) : IRequestHandler<LuuDoiThuCommand, Guid>
{
    public async Task<Guid> Handle(LuuDoiThuCommand request, CancellationToken ct)
    {
        var ten = request.TenDoi.Trim();

        // Mã sai định dạng chặn ở đây, trước khi chạm DB: để lọt xuống thì cột char(7) ném
        // lỗi Npgsql thô mà frontend không dịch được. Ở handler chứ không ở validator để giữ
        // được mã lỗi riêng (xem LuuDoiThuValidator).
        var coMa = !string.IsNullOrWhiteSpace(request.MaDoiHeThong);
        if (coMa && !Domain.Common.MaDoi.HopLe(request.MaDoiHeThong!))
            throw new AppException("MA_DOI_KHONG_HOP_LE");

        // Trùng tên đối thủ trong cùng CLB gần như luôn là nhập nhầm hai lần — chặn sớm để
        // lịch sử đối đầu không bị chia làm hai bản ghi.
        var daCo = await db.DoiThus
            .AnyAsync(d => d.TenDoi.ToLower() == ten.ToLower() && d.Id != request.Id, ct);
        if (daCo) throw new AppException("DOI_THU_DA_TON_TAI");

        Domain.Entities.DoiThu doiThu;

        if (request.Id is { } id)
        {
            doiThu = await db.DoiThus.FirstOrDefaultAsync(d => d.Id == id, ct)
                ?? throw new KhongTimThayException($"DoiThu {id}");
        }
        else
        {
            doiThu = new Domain.Entities.DoiThu();
            db.DoiThus.Add(doiThu);
        }

        doiThu.TenDoi = ten;
        doiThu.LienHe = request.LienHe;
        doiThu.GhiChu = request.GhiChu;
        doiThu.MaDoiHeThong = string.IsNullOrWhiteSpace(request.MaDoiHeThong)
            ? null
            : Domain.Common.MaDoi.ChuanHoa(request.MaDoiHeThong);

        await db.SaveChangesAsync(ct);
        return doiThu.Id;
    }
}

public record XoaDoiThuCommand(Guid Id) : IRequest;

public class XoaDoiThuHandler(IAppDbContext db) : IRequestHandler<XoaDoiThuCommand>
{
    public async Task Handle(XoaDoiThuCommand request, CancellationToken ct)
    {
        var doiThu = await db.DoiThus.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"DoiThu {request.Id}");

        // FK TRAN_DAU -> DOI_THU đặt SetNull nên xóa được, nhưng lịch sử trận sẽ mất tên đối
        // thủ và không khôi phục được. Chặn lại thay vì âm thầm làm hỏng dữ liệu (quy tắc #1).
        var soTran = await db.TranDaus.CountAsync(t => t.DoiThuId == request.Id, ct);
        if (soTran > 0)
            throw new AppException("DOI_THU_DA_CO_TRAN_DAU")
            {
                DuLieu = new Dictionary<string, object> { ["soTran"] = soTran }
            };

        db.DoiThus.Remove(doiThu);
        await db.SaveChangesAsync(ct);
    }
}
