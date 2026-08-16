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
public record DoiThuDto(Guid Id, string TenDoi, string? LienHe, string? GhiChu, int SoTranDaDau);

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
                db.TranDaus.Count(t => t.DoiThuId == d.Id)))
            .ToListAsync(ct);

        return new KetQuaTrang<DoiThuDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record LuuDoiThuCommand(Guid? Id, string TenDoi, string? LienHe, string? GhiChu)
    : IRequest<Guid>;

public class LuuDoiThuValidator : AbstractValidator<LuuDoiThuCommand>
{
    public LuuDoiThuValidator()
    {
        RuleFor(x => x.TenDoi).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LienHe).MaximumLength(200);
    }
}

public class LuuDoiThuHandler(IAppDbContext db) : IRequestHandler<LuuDoiThuCommand, Guid>
{
    public async Task<Guid> Handle(LuuDoiThuCommand request, CancellationToken ct)
    {
        var ten = request.TenDoi.Trim();

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
