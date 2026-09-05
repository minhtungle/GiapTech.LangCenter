using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.DaoTao.HocLieu;

/// <summary>FR-13 — tài liệu giảng dạy.</summary>
public record TaiLieuDto(
    Guid Id,
    string TieuDe,
    string? MoTa,
    LoaiTaiLieu Loai,
    string? NguoiTaiLen,
    DateTimeOffset NgayTao,
    List<TepDto> Teps,
    List<Guid> LopHocIds,
    List<string> TenLopHocs);

// ---------- Queries ----------

public record LayDanhSachTaiLieuQuery(
    string? TimKiem = null, Guid? LopHocId = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<TaiLieuDto>>;

public class LayDanhSachTaiLieuHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<LayDanhSachTaiLieuQuery, KetQuaTrang<TaiLieuDto>>
{
    public async Task<KetQuaTrang<TaiLieuDto>> Handle(
        LayDanhSachTaiLieuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.TaiLieus.AsQueryable();

        // Người không xem được mọi lớp chỉ thấy: tài liệu CHUNG (không gắn lớp nào) và tài
        // liệu của lớp họ liên quan. Không lọc thì giáo viên đọc được giáo trình lớp khác.
        if (!await phamVi.ThayMoiLop(HanhDong.Xem, ct))
        {
            var lopDuocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Xem, ct);
            var idLop = lopDuocPhep.Select(l => l.Id);

            q = q.Where(tl => !tl.LopHocs.Any()
                              || tl.LopHocs.Any(x => idLop.Contains(x.LopHocId)));
        }

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(tl => tl.TieuDe.ToLower().Contains(tu));
        }

        if (request.LopHocId is { } lopId)
            q = q.Where(tl => tl.LopHocs.Any(x => x.LopHocId == lopId));

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderByDescending(tl => tl.NgayTao)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(tl => new TaiLieuDto(
                tl.Id, tl.TieuDe, tl.MoTa, tl.Loai,
                tl.NguoiTaiLen != null ? tl.NguoiTaiLen.HoTen : null,
                tl.NgayTao,
                tl.Teps.Select(t => new TepDto(t.Id, t.TenGoc, t.LoaiNoiDung, t.KichThuoc))
                    .ToList(),
                tl.LopHocs.Select(x => x.LopHocId).ToList(),
                tl.LopHocs.Select(x => x.LopHoc.Ten).ToList()))
            .ToListAsync(ct);

        return new KetQuaTrang<TaiLieuDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record TaoTaiLieuCommand(
    string TieuDe, string? MoTa, LoaiTaiLieu Loai, List<Guid> LopHocIds) : IRequest<Guid>;

public class TaoTaiLieuValidator : AbstractValidator<TaoTaiLieuCommand>
{
    public TaoTaiLieuValidator() => RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(200);
}

public class TaoTaiLieuHandler(IAppDbContext db, IPhamViLopHoc phamVi, ICurrentUser currentUser)
    : IRequestHandler<TaoTaiLieuCommand, Guid>
{
    public async Task<Guid> Handle(TaoTaiLieuCommand request, CancellationToken ct)
    {
        await KiemLopHopLe(db, phamVi, request.LopHocIds, ct);

        var tl = new Domain.Entities.TaiLieu
        {
            TieuDe = request.TieuDe.Trim(),
            MoTa = request.MoTa,
            Loai = request.Loai,
            NguoiTaiLenId = currentUser.UserId
        };
        db.TaiLieus.Add(tl);

        GanLop(db, tl.Id, request.LopHocIds);

        await db.SaveChangesAsync(ct);
        return tl.Id;
    }

    /// <summary>
    /// Chỉ gắn được tài liệu vào lớp mình có quyền — nếu không, một giáo viên có thể đẩy tài
    /// liệu vào lớp của người khác.
    /// </summary>
    internal static async Task KiemLopHopLe(
        IAppDbContext db, IPhamViLopHoc phamVi, List<Guid> lopHocIds, CancellationToken ct)
    {
        if (lopHocIds.Count == 0) return;

        var duocPhep = await phamVi.LocTheoPhamVi(db.LopHocs.AsQueryable(), HanhDong.Sua, ct);
        var ids = lopHocIds.Distinct().ToList();

        var soHopLe = await duocPhep.CountAsync(l => ids.Contains(l.Id), ct);
        if (soHopLe != ids.Count) throw new AppException("LOP_HOC_KHONG_HOP_LE");
    }

    internal static void GanLop(IAppDbContext db, Guid taiLieuId, List<Guid> lopHocIds)
    {
        foreach (var id in lopHocIds.Distinct())
        {
            db.TaiLieuLopHocs.Add(new Domain.Entities.TaiLieuLopHoc
            {
                TaiLieuId = taiLieuId,
                LopHocId = id
            });
        }
    }
}

public record CapNhatTaiLieuCommand(
    Guid Id, string TieuDe, LoaiTaiLieu Loai, List<Guid> LopHocIds, string? MoTa = null)
    : IRequest;

public class CapNhatTaiLieuValidator : AbstractValidator<CapNhatTaiLieuCommand>
{
    public CapNhatTaiLieuValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TieuDe).NotEmpty().MaximumLength(200);
    }
}

public class CapNhatTaiLieuHandler(IAppDbContext db, IPhamViLopHoc phamVi)
    : IRequestHandler<CapNhatTaiLieuCommand>
{
    public async Task Handle(CapNhatTaiLieuCommand request, CancellationToken ct)
    {
        var tl = await db.TaiLieus
            .Include(x => x.LopHocs)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TaiLieu {request.Id}");

        await TaoTaiLieuHandler.KiemLopHopLe(db, phamVi, request.LopHocIds, ct);

        tl.TieuDe = request.TieuDe.Trim();
        tl.Loai = request.Loai;
        if (request.MoTa is { } mt) tl.MoTa = string.IsNullOrWhiteSpace(mt) ? null : mt.Trim();

        // Thay thế toàn bộ danh sách lớp — gửi mảng rỗng = chuyển thành tài liệu chung.
        db.TaiLieuLopHocs.RemoveRange(tl.LopHocs);
        TaoTaiLieuHandler.GanLop(db, tl.Id, request.LopHocIds);

        await db.SaveChangesAsync(ct);
    }
}

public record XoaTaiLieuCommand(Guid Id) : IRequest;

public class XoaTaiLieuHandler(IAppDbContext db, ILuuTruTep luuTru)
    : IRequestHandler<XoaTaiLieuCommand>
{
    public async Task Handle(XoaTaiLieuCommand request, CancellationToken ct)
    {
        var tl = await db.TaiLieus.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"TaiLieu {request.Id}");

        var teps = await db.TepDinhKems.Where(t => t.TaiLieuId == tl.Id).ToListAsync(ct);
        await TepDinhKemChung.XoaTeps(db, luuTru, teps, ct);

        db.TaiLieus.Remove(tl);
        await db.SaveChangesAsync(ct);
    }
}
