using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Crm;

/// <summary>Một khách hàng (FR-17).</summary>
public record KhachHangDto(
    Guid Id,
    string HoTen,
    string? Email,
    string? SoDienThoai,
    string? LinkFacebook,
    string? GhiChu,
    PhuongThucThanhToan PhuongThucThanhToan,
    /// <summary>Có giá trị = khách đã thành học viên, hồ sơ học tập nằm ở `NGUOI_DUNG`.</summary>
    Guid? NguoiDungId,
    string? TenHocVien,
    /// <summary>Số khoá đã đăng ký. 0 = chưa mua, chưa xuất hiện ở màn Doanh thu.</summary>
    int SoDangKy,
    /// <summary>
    /// Tổng đã mua, **quy về VND** bằng tỷ giá đã chụp lúc đăng ký.
    ///
    /// Để ở đây chứ không bắt UI tự cộng: mỗi đăng ký một đơn vị tiền khác nhau, cộng ở client
    /// thì hoặc sai hoặc phải nhân đôi công thức quy đổi.
    /// </summary>
    decimal TongMuaVnd);

// ---------- Queries ----------

public record LayDanhSachKhachHangQuery(
    string? TimKiem = null,
    /// <summary>true = chỉ khách ĐÃ mua; false = chỉ khách chưa mua; null = tất cả.</summary>
    bool? DaMua = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<KhachHangDto>>;

public class LayDanhSachKhachHangHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachKhachHangQuery, KetQuaTrang<KhachHangDto>>
{
    public async Task<KetQuaTrang<KhachHangDto>> Handle(
        LayDanhSachKhachHangQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.KhachHangs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TimKiem))
        {
            var tu = request.TimKiem.Trim().ToLower();
            q = q.Where(k => k.HoTen.ToLower().Contains(tu)
                             || (k.SoDienThoai != null && k.SoDienThoai.Contains(tu))
                             || (k.Email != null && k.Email.ToLower().Contains(tu)));
        }

        if (request.DaMua is { } daMua)
            q = daMua ? q.Where(k => k.DangKys.Any()) : q.Where(k => !k.DangKys.Any());

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderBy(k => k.HoTen)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(k => new KhachHangDto(
                k.Id, k.HoTen, k.Email, k.SoDienThoai, k.LinkFacebook, k.GhiChu,
                k.PhuongThucThanhToan,
                k.NguoiDungId,
                k.NguoiDung == null ? null : k.NguoiDung.HoTen,
                k.DangKys.Count,
                // Quy đổi ngay trong SQL: cả hai cột đã chụp nên phép nhân này bất biến.
                k.DangKys.Sum(d => d.SoTien * d.TyGiaVeVnd)))
            .ToListAsync(ct);

        return new KetQuaTrang<KhachHangDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

// ---------- Commands ----------

public record LuuKhachHangCommand(
    Guid? Id,
    string HoTen,
    string? Email,
    string? SoDienThoai,
    string? LinkFacebook,
    string? GhiChu,
    PhuongThucThanhToan PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
    /// <summary>Nối với hồ sơ học viên khi khách đã vào học. null = chưa nối / bỏ nối.</summary>
    Guid? NguoiDungId = null) : IRequest<Guid>;

public class LuuKhachHangValidator : AbstractValidator<LuuKhachHangCommand>
{
    public LuuKhachHangValidator()
    {
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.SoDienThoai).MaximumLength(30);
        RuleFor(x => x.LinkFacebook).MaximumLength(500);
        RuleFor(x => x.GhiChu).MaximumLength(1000);
    }
}

public class LuuKhachHangHandler(IAppDbContext db) : IRequestHandler<LuuKhachHangCommand, Guid>
{
    public async Task<Guid> Handle(LuuKhachHangCommand request, CancellationToken ct)
    {
        var sdt = string.IsNullOrWhiteSpace(request.SoDienThoai)
            ? null
            : request.SoDienThoai.Trim();

        // Trùng số điện thoại: kiểm ở đây để có mã lỗi đọc được; UNIQUE partial index ở tầng DB
        // mới là thứ chặn hai request song song (quy tắc #8). Số rỗng thì KHÔNG chặn — khách chỉ
        // để lại Facebook là chuyện thường.
        if (sdt is not null)
        {
            var trung = await db.KhachHangs
                .AnyAsync(k => k.SoDienThoai == sdt && k.Id != request.Id, ct);
            if (trung) throw new AppException("KHACH_HANG_TRUNG_SO_DIEN_THOAI");
        }

        if (request.NguoiDungId is { } ndId)
        {
            // Global Query Filter lo cách ly tenant, nhưng vẫn phải kiểm TỒN TẠI: gán id của
            // tenant khác sẽ lọt qua đây và nổ ở FK với thông báo khó hiểu.
            var co = await db.NguoiDungs.AnyAsync(u => u.Id == ndId, ct);
            if (!co) throw new AppException("NGUOI_DUNG_KHONG_HOP_LE");
        }

        Domain.Entities.KhachHang kh;
        if (request.Id is { } id)
        {
            kh = await db.KhachHangs.FirstOrDefaultAsync(k => k.Id == id, ct)
                 ?? throw new KhongTimThayException($"KhachHang {id}");
        }
        else
        {
            kh = new Domain.Entities.KhachHang();
            db.KhachHangs.Add(kh);
        }

        // Ghi đủ mọi trường của form (quy tắc #1).
        kh.HoTen = request.HoTen.Trim();
        kh.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        kh.SoDienThoai = sdt;
        kh.LinkFacebook = string.IsNullOrWhiteSpace(request.LinkFacebook)
            ? null : request.LinkFacebook.Trim();
        kh.GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();
        kh.PhuongThucThanhToan = request.PhuongThucThanhToan;
        kh.NguoiDungId = request.NguoiDungId;

        await db.SaveChangesAsync(ct);
        return kh.Id;
    }
}

public record XoaKhachHangCommand(Guid Id) : IRequest;

public class XoaKhachHangHandler(IAppDbContext db) : IRequestHandler<XoaKhachHangCommand>
{
    public async Task Handle(XoaKhachHangCommand request, CancellationToken ct)
    {
        var kh = await db.KhachHangs.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
                 ?? throw new KhongTimThayException($"KhachHang {request.Id}");

        // FK Restrict — đăng ký là dữ liệu tiền, không được biến mất theo khách.
        if (await db.DangKyKhoaHocs.AnyAsync(d => d.KhachHangId == kh.Id, ct))
            throw new AppException("KHACH_HANG_DA_CO_DANG_KY");

        db.KhachHangs.Remove(kh);
        await db.SaveChangesAsync(ct);
    }
}
