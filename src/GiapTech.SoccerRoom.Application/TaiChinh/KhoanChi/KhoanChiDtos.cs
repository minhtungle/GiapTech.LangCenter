using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.TaiChinh.KhoanChi;

public record KhoanChiDto(
    Guid Id,
    Guid? QuyId,
    string? TenQuy,
    string NoiDung,
    decimal SoTien,
    DateOnly NgayChi,
    string? NguoiChi,
    string? GhiChu);

/// <summary>
/// Số dư quỹ đội — tổng thu trừ tổng chi.
///
/// "Đã thu" là tiền **thực nhận** (<c>SoTienDaDong</c>), không phải tiền phải thu: quỹ chỉ
/// tiêu được số đã vào túi. Số còn phải thu tách riêng để thủ quỹ biết còn bao nhiêu đang nợ.
/// </summary>
public record TongQuanTaiChinhDto(
    decimal TongDaThu,
    decimal TongConPhaiThu,
    decimal TongDaChi,
    decimal SoDu,
    int SoDotQuyDangMo,
    int SoNguoiConNo);

// ---------- Queries ----------

public record LayTongQuanQuery : IRequest<TongQuanTaiChinhDto>;

public class LayTongQuanHandler(IAppDbContext db)
    : IRequestHandler<LayTongQuanQuery, TongQuanTaiChinhDto>
{
    public async Task<TongQuanTaiChinhDto> Handle(LayTongQuanQuery request, CancellationToken ct)
    {
        // Ba truy vấn tổng hợp riêng thay vì một join: join sẽ nhân bản hàng (mỗi khoản chi ×
        // mỗi khoản đóng của cùng đợt quỹ) và tổng bị thổi phồng.
        var daThu = await db.DongGopQuys.SumAsync(d => (decimal?)d.SoTienDaDong, ct) ?? 0m;
        var canThu = await db.DongGopQuys.SumAsync(d => (decimal?)d.SoTienCanDong, ct) ?? 0m;
        var daChi = await db.KhoanChis.SumAsync(k => (decimal?)k.SoTien, ct) ?? 0m;

        var soDotDangMo = await db.Quys
            .CountAsync(q => q.TrangThai == Domain.Enums.TrangThaiQuy.DangMo, ct);

        var soNguoiConNo = await db.DongGopQuys
            .Where(d => d.SoTienDaDong < d.SoTienCanDong)
            // Đếm theo NGƯỜI, không theo dòng: một người nợ ba đợt quỹ vẫn là một người.
            .Select(d => d.CauThuId)
            .Distinct()
            .CountAsync(ct);

        return new TongQuanTaiChinhDto(
            daThu,
            // Kẹp ở 0: người đóng dư (làm tròn lên khi góp tiền mặt) không được làm số còn
            // phải thu thành âm.
            Math.Max(0m, canThu - daThu),
            daChi,
            daThu - daChi,
            soDotDangMo,
            soNguoiConNo);
    }
}

public record LayDanhSachChiQuery(Guid? QuyId = null, ThamSoTrang? Trang = null)
    : IRequest<KetQuaTrang<KhoanChiDto>>;

public class LayDanhSachChiHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachChiQuery, KetQuaTrang<KhoanChiDto>>
{
    public async Task<KetQuaTrang<KhoanChiDto>> Handle(
        LayDanhSachChiQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.KhoanChis.AsQueryable();

        if (request.QuyId is { } quyId) q = q.Where(k => k.QuyId == quyId);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderByDescending(k => k.NgayChi)
            .ThenByDescending(k => k.NgayTao)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(k => new KhoanChiDto(
                k.Id, k.QuyId, k.Quy != null ? k.Quy.TenQuy : null,
                k.NoiDung, k.SoTien, k.NgayChi, k.NguoiChi, k.GhiChu))
            .ToListAsync(ct);

        return new KetQuaTrang<KhoanChiDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record LuuKhoanChiCommand(
    Guid? Id,
    Guid? QuyId,
    string NoiDung,
    decimal SoTien,
    DateOnly NgayChi,
    string? NguoiChi,
    string? GhiChu) : IRequest<Guid>;

public class LuuKhoanChiValidator : AbstractValidator<LuuKhoanChiCommand>
{
    public LuuKhoanChiValidator()
    {
        RuleFor(x => x.NoiDung).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NguoiChi).MaximumLength(200);
        RuleFor(x => x.GhiChu).MaximumLength(1000);

        // Chi 0 đồng là dòng rác; chi âm là cách viết sai của một khoản thu.
        RuleFor(x => x.SoTien).GreaterThan(0).WithErrorCode("SO_TIEN_PHAI_DUONG");
        RuleFor(x => x.NgayChi).NotEmpty();
    }
}

public class LuuKhoanChiHandler(IAppDbContext db) : IRequestHandler<LuuKhoanChiCommand, Guid>
{
    public async Task<Guid> Handle(LuuKhoanChiCommand request, CancellationToken ct)
    {
        if (request.QuyId is { } quyId && !await db.Quys.AnyAsync(q => q.Id == quyId, ct))
            throw new KhongTimThayException($"Quy {quyId}");

        Domain.Entities.KhoanChi chi;

        if (request.Id is { } id)
        {
            chi = await db.KhoanChis.FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KhongTimThayException($"KhoanChi {id}");
        }
        else
        {
            chi = new Domain.Entities.KhoanChi();
            db.KhoanChis.Add(chi);
        }

        chi.QuyId = request.QuyId;
        chi.NoiDung = request.NoiDung.Trim();
        chi.SoTien = request.SoTien;
        chi.NgayChi = request.NgayChi;
        chi.NguoiChi = request.NguoiChi;
        chi.GhiChu = request.GhiChu;

        await db.SaveChangesAsync(ct);
        return chi.Id;
    }
}

public record XoaKhoanChiCommand(Guid Id) : IRequest;

public class XoaKhoanChiHandler(IAppDbContext db) : IRequestHandler<XoaKhoanChiCommand>
{
    public async Task Handle(XoaKhoanChiCommand request, CancellationToken ct)
    {
        var chi = await db.KhoanChis.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"KhoanChi {request.Id}");

        // Khoản chi xoá được (khác đợt quỹ đã thu tiền): thủ quỹ gõ nhầm một dòng chi là
        // chuyện thường, mà không xoá được thì họ phải sửa nó thành "0 đồng" — bẩn hơn.
        db.KhoanChis.Remove(chi);
        await db.SaveChangesAsync(ct);
    }
}
