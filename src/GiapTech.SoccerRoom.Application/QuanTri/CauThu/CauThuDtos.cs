using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.CauThu;

/// <summary>FR-04 — hồ sơ cầu thủ. Độc lập với tài khoản đăng nhập.</summary>
public record CauThuDto(
    Guid Id, string HoTen, string? AnhDaiDien, DateOnly? NgaySinh,
    DateOnly? NgayThamGia, string? GhiChu, bool CoTaiKhoan,
    int? SoAo, string? ViTriSoTruong);

// ---------- Queries ----------

public record LayDanhSachCauThuQuery(string? TimKiem = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<CauThuDto>>;

public class LayDanhSachCauThuHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachCauThuQuery, KetQuaTrang<CauThuDto>>
{
    public async Task<KetQuaTrang<CauThuDto>> Handle(
        LayDanhSachCauThuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.CauThus.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            // Dùng ToLower().Contains() thay vì EF.Functions.ILike: ILike là hàm riêng của
            // Npgsql, mà Application không được phụ thuộc provider (xem LuatPhuThuocTests).
            // EF dịch được cả hai sang SQL; đổi DB sau này cũng không phải sửa handler.
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(c => c.HoTen.ToLower().Contains(tu));
        }

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(c => c.HoTen)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(c => new CauThuDto(
                c.Id, c.HoTen, c.AnhDaiDien, c.NgaySinh, c.NgayThamGia, c.GhiChu,
                db.NguoiDungs.Any(u => u.CauThuId == c.Id), c.SoAo, c.ViTriSoTruong))
            .ToListAsync(ct);

        return new KetQuaTrang<CauThuDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

public record LayCauThuQuery(Guid Id) : IRequest<CauThuDto>;

public class LayCauThuHandler(IAppDbContext db) : IRequestHandler<LayCauThuQuery, CauThuDto>
{
    public async Task<CauThuDto> Handle(LayCauThuQuery request, CancellationToken ct)
        => await db.CauThus
               .Where(c => c.Id == request.Id)
               .Select(c => new CauThuDto(
                   c.Id, c.HoTen, c.AnhDaiDien, c.NgaySinh, c.NgayThamGia, c.GhiChu,
                   db.NguoiDungs.Any(u => u.CauThuId == c.Id), c.SoAo, c.ViTriSoTruong))
               .FirstOrDefaultAsync(ct)
           ?? throw new KhongTimThayException($"CauThu {request.Id}");
}

// ---------- Commands ----------

public record TaoCauThuCommand(
    string HoTen, string? AnhDaiDien, DateOnly? NgaySinh,
    DateOnly? NgayThamGia, string? GhiChu,
    int? SoAo = null, string? ViTriSoTruong = null) : IRequest<Guid>;

public class TaoCauThuValidator : AbstractValidator<TaoCauThuCommand>
{
    public TaoCauThuValidator()
    {
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NgaySinh)
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.NgaySinh.HasValue)
            .WithErrorCode("NGAY_SINH_TUONG_LAI");
        RuleFor(x => x.SoAo).InclusiveBetween(1, 99).When(x => x.SoAo.HasValue)
            .WithErrorCode("SO_AO_KHONG_HOP_LE");
        RuleFor(x => x.ViTriSoTruong).MaximumLength(8);
    }
}

public class TaoCauThuHandler(IAppDbContext db) : IRequestHandler<TaoCauThuCommand, Guid>
{
    public async Task<Guid> Handle(TaoCauThuCommand request, CancellationToken ct)
    {
        // TenantId do SaveChanges tự gán — không gán thủ công ở đây.
        var cauThu = new Domain.Entities.CauThu
        {
            HoTen = request.HoTen.Trim(),
            AnhDaiDien = request.AnhDaiDien,
            NgaySinh = request.NgaySinh,
            NgayThamGia = request.NgayThamGia,
            GhiChu = request.GhiChu,
            SoAo = request.SoAo,
            ViTriSoTruong = ChuanHoaCauThu.ViTri(request.ViTriSoTruong)
        };

        db.CauThus.Add(cauThu);
        await db.SaveChangesAsync(ct);
        return cauThu.Id;
    }
}

public record CapNhatCauThuCommand(
    Guid Id, string HoTen, string? AnhDaiDien, DateOnly? NgaySinh,
    DateOnly? NgayThamGia, string? GhiChu,
    int? SoAo = null, string? ViTriSoTruong = null) : IRequest;

public class CapNhatCauThuValidator : AbstractValidator<CapNhatCauThuCommand>
{
    public CapNhatCauThuValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SoAo).InclusiveBetween(1, 99).When(x => x.SoAo.HasValue)
            .WithErrorCode("SO_AO_KHONG_HOP_LE");
        RuleFor(x => x.ViTriSoTruong).MaximumLength(8);
    }
}

public class CapNhatCauThuHandler(IAppDbContext db) : IRequestHandler<CapNhatCauThuCommand>
{
    public async Task Handle(CapNhatCauThuCommand request, CancellationToken ct)
    {
        var cauThu = await db.CauThus.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"CauThu {request.Id}");

        cauThu.HoTen = request.HoTen.Trim();
        cauThu.AnhDaiDien = request.AnhDaiDien;
        cauThu.NgaySinh = request.NgaySinh;
        cauThu.NgayThamGia = request.NgayThamGia;
        cauThu.GhiChu = request.GhiChu;
        cauThu.SoAo = request.SoAo;
        cauThu.ViTriSoTruong = ChuanHoaCauThu.ViTri(request.ViTriSoTruong);

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Mã vị trí luôn viết HOA (GK, CB) — không thì "gk" và "GK" thành hai vị trí khác nhau.</summary>
internal static class ChuanHoaCauThu
{
    public static string? ViTri(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
}

public record XoaCauThuCommand(Guid Id) : IRequest;

public class XoaCauThuHandler(IAppDbContext db) : IRequestHandler<XoaCauThuCommand>
{
    public async Task Handle(XoaCauThuCommand request, CancellationToken ct)
    {
        var cauThu = await db.CauThus.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"CauThu {request.Id}");

        // Dữ liệu tài chính phải giữ vết: FK DONGGOP_QUY -> CAU_THU đặt Restrict, xóa cứng
        // sẽ ném lỗi DB khó hiểu. Chặn sớm với mã lỗi rõ ràng để frontend hướng dẫn được.
        if (await db.DongGopQuys.AnyAsync(d => d.CauThuId == request.Id, ct))
            throw new AppException("CAU_THU_DA_CO_DU_LIEU_QUY",
                $"CauThu {request.Id} còn bản ghi đóng quỹ");

        db.CauThus.Remove(cauThu);
        await db.SaveChangesAsync(ct);
    }
}
