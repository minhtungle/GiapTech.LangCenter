using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Crm;

/// <summary>Một sản phẩm bán kèm: sách, học cụ (FR-20).</summary>
public record SanPhamDto(
    Guid Id,
    string Ten,
    string? GhiChu,
    decimal GiaTien,
    DonViTien DonViTien,
    string? DonViTinh,
    bool DangBan,
    /// <summary>Số đơn đã bán — để UI biết sản phẩm nào không xoá được.</summary>
    int SoDonHang,
    /// <summary>Tổng số lượng đã bán ra, để biết mặt hàng nào chạy.</summary>
    int TongSoLuongBan);

public record LayDanhSachSanPhamQuery(
    string? TimKiem = null,
    bool? DangBan = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<SanPhamDto>>;

public class LayDanhSachSanPhamHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachSanPhamQuery, KetQuaTrang<SanPhamDto>>
{
    public async Task<KetQuaTrang<SanPhamDto>> Handle(
        LayDanhSachSanPhamQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.SanPhams.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(s => s.Ten.ToLower().Contains(tu));
        }

        if (request.DangBan is { } db_) q = q.Where(s => s.DangBan == db_);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(s => s.Ten)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(s => new SanPhamDto(
                s.Id, s.Ten, s.GhiChu, s.GiaTien, s.DonViTien, s.DonViTinh, s.DangBan,
                s.DonHangs.Count,
                // `Sum` trên tập rỗng trả 0 trong SQL, không cần xử lý riêng.
                s.DonHangs.Sum(d => d.SoLuong)))
            .ToListAsync(ct);

        return new KetQuaTrang<SanPhamDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

public record LuuSanPhamCommand(
    Guid? Id,
    string Ten,
    string? GhiChu,
    decimal GiaTien,
    DonViTien DonViTien,
    string? DonViTinh,
    bool DangBan = true) : IRequest<Guid>;

public class LuuSanPhamValidator : AbstractValidator<LuuSanPhamCommand>
{
    public LuuSanPhamValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GhiChu).MaximumLength(1000);
        RuleFor(x => x.DonViTinh).MaximumLength(50);
        RuleFor(x => x.GiaTien).GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_KHONG_HOP_LE");
    }
}

public class LuuSanPhamHandler(IAppDbContext db) : IRequestHandler<LuuSanPhamCommand, Guid>
{
    public async Task<Guid> Handle(LuuSanPhamCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // Kiểm ở đây để có mã lỗi đọc được; UNIQUE ở tầng DB mới chặn được hai request song
        // song (quy tắc #8).
        var trung = await db.SanPhams
            .AnyAsync(s => s.Ten.ToLower() == ten.ToLower() && s.Id != request.Id, ct);
        if (trung) throw new AppException("SAN_PHAM_TRUNG_TEN");

        Domain.Entities.SanPham sp;
        if (request.Id is { } id)
        {
            sp = await db.SanPhams.FirstOrDefaultAsync(s => s.Id == id, ct)
                 ?? throw new KhongTimThayException($"SanPham {id}");
        }
        else
        {
            sp = new Domain.Entities.SanPham();
            db.SanPhams.Add(sp);
        }

        // Ghi đủ mọi trường của form (quy tắc #1).
        sp.Ten = ten;
        sp.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();
        sp.GiaTien = request.GiaTien;
        sp.DonViTien = request.DonViTien;
        sp.DonViTinh = string.IsNullOrWhiteSpace(request.DonViTinh)
            ? null : request.DonViTinh.Trim();
        sp.DangBan = request.DangBan;

        await db.SaveChangesAsync(ct);
        return sp.Id;
    }
}

public record XoaSanPhamCommand(Guid Id) : IRequest;

public class XoaSanPhamHandler(IAppDbContext db) : IRequestHandler<XoaSanPhamCommand>
{
    public async Task Handle(XoaSanPhamCommand request, CancellationToken ct)
    {
        var sp = await db.SanPhams.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"SanPham {request.Id}");

        // FK Restrict — thiếu kiểm ở đây thì nổ ở tầng DB thành 500, người dùng không biết rằng
        // việc cần làm là NGỪNG BÁN. Đúng bẫy đã gặp với xoá buổi học có nhận xét (07/09).
        if (await db.DangKyKhoaHocs.AnyAsync(d => d.SanPhamId == sp.Id, ct))
            throw new AppException("SAN_PHAM_DA_CO_DON_HANG");

        db.SanPhams.Remove(sp);
        await db.SaveChangesAsync(ct);
    }
}
