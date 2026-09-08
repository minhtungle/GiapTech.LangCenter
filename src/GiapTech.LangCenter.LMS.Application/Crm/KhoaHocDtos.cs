using FluentValidation;
using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Application.Common.Models;
using GiapTech.LangCenter.LMS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Crm;

/// <summary>Một khoá học trong danh mục bán ra (FR-19).</summary>
public record KhoaHocDto(
    Guid Id,
    string Ten,
    string? GhiChu,
    decimal GiaTien,
    DonViTien DonViTien,
    int SoBuoi,
    bool DangBan,
    /// <summary>Số đăng ký đã bán — để UI biết khoá nào không xoá được.</summary>
    int SoDangKy);

// ---------- Queries ----------

public record LayDanhSachKhoaHocQuery(
    string? TimKiem = null,
    bool? DangBan = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<KhoaHocDto>>;

public class LayDanhSachKhoaHocHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachKhoaHocQuery, KetQuaTrang<KhoaHocDto>>
{
    public async Task<KetQuaTrang<KhoaHocDto>> Handle(
        LayDanhSachKhoaHocQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.KhoaHocs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(k => k.Ten.ToLower().Contains(tu));
        }

        if (request.DangBan is { } db_) q = q.Where(k => k.DangBan == db_);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(k => k.Ten)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(k => new KhoaHocDto(
                k.Id, k.Ten, k.GhiChu, k.GiaTien, k.DonViTien, k.SoBuoi, k.DangBan,
                k.DangKys.Count))
            .ToListAsync(ct);

        return new KetQuaTrang<KhoaHocDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record LuuKhoaHocCommand(
    Guid? Id,
    string Ten,
    string? GhiChu,
    decimal GiaTien,
    DonViTien DonViTien,
    int SoBuoi,
    bool DangBan = true) : IRequest<Guid>;

public class LuuKhoaHocValidator : AbstractValidator<LuuKhoaHocCommand>
{
    public LuuKhoaHocValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GhiChu).MaximumLength(1000);
        RuleFor(x => x.GiaTien).GreaterThanOrEqualTo(0).WithErrorCode("SO_TIEN_KHONG_HOP_LE");
        // 0 buổi là khoá không có nội dung; 500 khớp chặn trên của sinh lịch.
        RuleFor(x => x.SoBuoi).InclusiveBetween(1, 500);
    }
}

public class LuuKhoaHocHandler(IAppDbContext db) : IRequestHandler<LuuKhoaHocCommand, Guid>
{
    public async Task<Guid> Handle(LuuKhoaHocCommand request, CancellationToken ct)
    {
        var ten = request.Ten.Trim();

        // Kiểm ở đây để trả MÃ LỖI rõ ràng; UNIQUE ở tầng DB mới là thứ chặn thật khi hai
        // request song song (quy tắc #8).
        var trung = await db.KhoaHocs
            .AnyAsync(k => k.Ten.ToLower() == ten.ToLower() && k.Id != request.Id, ct);
        if (trung) throw new AppException("KHOA_HOC_TRUNG_TEN");

        Domain.Entities.KhoaHoc khoa;
        if (request.Id is { } id)
        {
            khoa = await db.KhoaHocs.FirstOrDefaultAsync(k => k.Id == id, ct)
                   ?? throw new KhongTimThayException($"KhoaHoc {id}");
        }
        else
        {
            khoa = new Domain.Entities.KhoaHoc();
            db.KhoaHocs.Add(khoa);
        }

        // Ghi ĐỦ mọi trường của form (quy tắc #1): thiếu một trường ở đây là âm thầm xoá nó
        // mỗi lần lưu — đúng lỗi 16/08 với ô địa chỉ.
        khoa.Ten = ten;
        khoa.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();
        khoa.GiaTien = request.GiaTien;
        khoa.DonViTien = request.DonViTien;
        khoa.SoBuoi = request.SoBuoi;
        khoa.DangBan = request.DangBan;

        await db.SaveChangesAsync(ct);
        return khoa.Id;
    }
}

public record XoaKhoaHocCommand(Guid Id) : IRequest;

public class XoaKhoaHocHandler(IAppDbContext db) : IRequestHandler<XoaKhoaHocCommand>
{
    public async Task Handle(XoaKhoaHocCommand request, CancellationToken ct)
    {
        var khoa = await db.KhoaHocs.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
                   ?? throw new KhongTimThayException($"KhoaHoc {request.Id}");

        // FK là Restrict — thiếu kiểm ở đây thì nổ ở tầng DB thành 500 LOI_HE_THONG, người dùng
        // không hiểu vì sao và cũng không biết nên NGỪNG BÁN thay vì xoá. Đúng bẫy đã gặp với
        // xoá buổi học có nhận xét (07/09/2026).
        if (await db.DangKyKhoaHocs.AnyAsync(d => d.KhoaHocId == khoa.Id, ct))
            throw new AppException("KHOA_HOC_DA_CO_DANG_KY");

        db.KhoaHocs.Remove(khoa);
        await db.SaveChangesAsync(ct);
    }
}
