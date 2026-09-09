using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DaoTao.HocPhi;

/// <summary>Một lần thu tiền.</summary>
public record KhoanThuDto(
    Guid Id,
    Guid HocVienId,
    string TenHocVien,
    Guid LopHocId,
    string TenLopHoc,
    decimal SoTien,
    DateTimeOffset NgayThu,
    PhuongThucThanhToan PhuongThuc,
    string? SoPhieu,
    string? GhiChu,
    string? TenNguoiThu);

/// <summary>
/// Công nợ của một học viên trong một lớp.
///
/// <see cref="ConNo"/> tính động từ <see cref="HocPhiApDung"/> − <see cref="DaThu"/>, không
/// lưu cột — lưu là mở cửa cho sai lệch khi ai đó sửa/xoá một khoản thu.
/// </summary>
public record CongNoDto(
    Guid HocVienId,
    string TenHocVien,
    Guid LopHocId,
    string TenLopHoc,
    decimal HocPhiApDung,
    decimal DaThu,
    DateTimeOffset? NgayKhaiGiang,
    /// <summary>true = còn nợ và đã quá ngưỡng cảnh báo của trung tâm.</summary>
    bool QuaHan)
{
    public decimal ConNo => HocPhiApDung - DaThu;
}

// ---------- Queries ----------

public record LayKhoanThuQuery(
    Guid? LopHocId = null,
    Guid? HocVienId = null,
    ThamSoTrang? Trang = null) : IRequest<KetQuaTrang<KhoanThuDto>>;

public class LayKhoanThuHandler(IAppDbContext db, IPhamViHocPhi phamVi)
    : IRequestHandler<LayKhoanThuQuery, KetQuaTrang<KhoanThuDto>>
{
    public async Task<KetQuaTrang<KhoanThuDto>> Handle(
        LayKhoanThuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();

        var q = await phamVi.LocKhoanThu(db.KhoanThuHocPhis.AsQueryable(), ct);

        if (request.LopHocId is { } lop) q = q.Where(k => k.LopHocId == lop);
        if (request.HocVienId is { } hv) q = q.Where(k => k.HocVienId == hv);

        var tong = await q.CountAsync(ct);

        var duLieu = await q
            .OrderByDescending(k => k.NgayThu)
            .Skip(trang.BoQua)
            .Take(trang.SoDongHopLe)
            .Select(k => new KhoanThuDto(
                k.Id, k.HocVienId, k.HocVien.HoTen, k.LopHocId, k.LopHoc.Ten,
                k.SoTien, k.NgayThu, k.PhuongThuc, k.SoPhieu, k.GhiChu,
                k.NguoiThu != null ? k.NguoiThu.HoTen : null))
            .ToListAsync(ct);

        return new KetQuaTrang<KhoanThuDto>(duLieu, tong, trang.TrangHopLe, trang.SoDongHopLe);
    }
}

/// <summary>Bảng công nợ — mặc định chỉ hiện người CÒN nợ.</summary>
public record LayCongNoQuery(Guid? LopHocId = null, bool ChiConNo = true)
    : IRequest<List<CongNoDto>>;

public class LayCongNoHandler(IAppDbContext db, IPhamViHocPhi phamVi, ICurrentTenant tenant)
    : IRequestHandler<LayCongNoQuery, List<CongNoDto>>
{
    public async Task<List<CongNoDto>> Handle(LayCongNoQuery request, CancellationToken ct)
    {
        var soNgayCanhBao = await db.Tenants
            .Where(t => t.Id == tenant.TenantId)
            .Select(t => t.SoNgayCanhBaoNoHocPhi)
            .FirstOrDefaultAsync(ct);

        var q = await phamVi.LocHocVienTrongLop(db.LopHocHocViens.AsQueryable(), ct);

        if (request.LopHocId is { } lop) q = q.Where(hv => hv.LopHocId == lop);

        var bayGio = DateTimeOffset.UtcNow;

        var ds = await q
            .Select(hv => new
            {
                hv.HocVienId,
                TenHocVien = hv.HocVien.HoTen,
                hv.LopHocId,
                TenLopHoc = hv.LopHoc.Ten,
                hv.HocPhiApDung,
                // Cộng dồn ngay trong SQL — kéo mọi khoản thu về rồi cộng ở C# sẽ chậm dần
                // theo số lần thu.
                DaThu = db.KhoanThuHocPhis
                    .Where(k => k.LopHocId == hv.LopHocId && k.HocVienId == hv.HocVienId)
                    .Sum(k => (decimal?)k.SoTien) ?? 0m,
                hv.LopHoc.NgayKhaiGiang
            })
            .ToListAsync(ct);

        return ds
            .Select(x => new CongNoDto(
                x.HocVienId, x.TenHocVien, x.LopHocId, x.TenLopHoc,
                x.HocPhiApDung, x.DaThu, x.NgayKhaiGiang,
                // Quá hạn = còn nợ VÀ đã qua ngưỡng ngày kể từ khai giảng. Lớp chưa khai
                // giảng thì chưa tính là quá hạn dù chưa đóng đồng nào.
                x.HocPhiApDung - x.DaThu > 0
                && x.NgayKhaiGiang is { } kg
                && bayGio > kg.AddDays(soNgayCanhBao)))
            .Where(x => !request.ChiConNo || x.ConNo > 0)
            .OrderByDescending(x => x.QuaHan)
            .ThenByDescending(x => x.ConNo)
            .ToList();
    }
}

// ---------- Commands ----------

public record ThuHocPhiCommand(
    Guid LopHocId,
    Guid HocVienId,
    decimal SoTien,
    DateTimeOffset? NgayThu,
    PhuongThucThanhToan PhuongThuc,
    string? SoPhieu,
    string? GhiChu) : IRequest<Guid>;

public class ThuHocPhiValidator : AbstractValidator<ThuHocPhiCommand>
{
    public ThuHocPhiValidator()
    {
        RuleFor(x => x.LopHocId).NotEmpty();
        RuleFor(x => x.HocVienId).NotEmpty();
        RuleFor(x => x.SoTien).GreaterThan(0).WithErrorCode("SO_TIEN_PHAI_DUONG");
        RuleFor(x => x.SoPhieu).MaximumLength(100);
    }
}

public class ThuHocPhiHandler(IAppDbContext db, IPhamViHocPhi phamVi, ICurrentUser currentUser)
    : IRequestHandler<ThuHocPhiCommand, Guid>
{
    public async Task<Guid> Handle(ThuHocPhiCommand request, CancellationToken ct)
    {
        // Ghi sổ thu đi qua ĐÚNG cổng với sửa/xoá sổ. Không dùng phạm vi lớp ở đây: giáo viên
        // sửa được lớp mình dạy, nhưng học phí là quan hệ giữa học viên và trung tâm.
        if (!await phamVi.DuocGhiSo(ct))
            throw new KhongDuQuyenException(ChucNang.HocPhi, HanhDong.Them.ToString());

        // Lớp phải tồn tại trong tenant — Query Filter lo phần chéo trung tâm.
        if (!await db.LopHocs.AnyAsync(l => l.Id == request.LopHocId, ct))
            throw new KhongTimThayException($"LopHoc {request.LopHocId}");

        // Học viên phải thực sự trong lớp — thu tiền cho người không học lớp đó là dữ liệu rác
        // và không tính được công nợ.
        var trongLop = await db.LopHocHocViens.AnyAsync(
            hv => hv.LopHocId == request.LopHocId && hv.HocVienId == request.HocVienId, ct);

        if (!trongLop) throw new AppException("HOC_VIEN_KHONG_THUOC_LOP");

        var khoan = new Domain.Entities.KhoanThuHocPhi
        {
            LopHocId = request.LopHocId,
            HocVienId = request.HocVienId,
            SoTien = request.SoTien,
            NgayThu = request.NgayThu ?? DateTimeOffset.UtcNow,
            PhuongThuc = request.PhuongThuc,
            SoPhieu = string.IsNullOrWhiteSpace(request.SoPhieu) ? null : request.SoPhieu.Trim(),
            GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim(),
            NguoiThuId = currentUser.UserId
        };
        db.KhoanThuHocPhis.Add(khoan);

        await db.SaveChangesAsync(ct);
        return khoan.Id;
    }
}

/// <summary>
/// Sửa một khoản thu. Chỉ số tiền, ngày, phương thức, số phiếu, ghi chú.
///
/// KHÔNG cho đổi học viên hay lớp: đó không phải "sửa", đó là chuyển tiền từ sổ này sang sổ
/// khác — phải xoá rồi thu lại để còn dấu vết.
/// </summary>
public record SuaKhoanThuCommand(
    Guid Id,
    decimal SoTien,
    DateTimeOffset NgayThu,
    PhuongThucThanhToan PhuongThuc,
    string? SoPhieu = null,
    string? GhiChu = null) : IRequest;

public class SuaKhoanThuValidator : AbstractValidator<SuaKhoanThuCommand>
{
    public SuaKhoanThuValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SoTien).GreaterThan(0).WithErrorCode("SO_TIEN_PHAI_DUONG");
        RuleFor(x => x.SoPhieu).MaximumLength(100);
    }
}

public class SuaKhoanThuHandler(IAppDbContext db, IPhamViHocPhi phamVi)
    : IRequestHandler<SuaKhoanThuCommand>
{
    public async Task Handle(SuaKhoanThuCommand request, CancellationToken ct)
    {
        var q = await phamVi.LocKhoanThuDuocSua(db.KhoanThuHocPhis.AsQueryable(), ct);

        var khoan = await q.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"KhoanThu {request.Id}");

        khoan.SoTien = request.SoTien;
        khoan.NgayThu = request.NgayThu;
        khoan.PhuongThuc = request.PhuongThuc;

        if (request.SoPhieu is { } sp)
            khoan.SoPhieu = string.IsNullOrWhiteSpace(sp) ? null : sp.Trim();
        if (request.GhiChu is { } gc)
            khoan.GhiChu = string.IsNullOrWhiteSpace(gc) ? null : gc.Trim();

        await db.SaveChangesAsync(ct);
    }
}

public record XoaKhoanThuCommand(Guid Id) : IRequest;

public class XoaKhoanThuHandler(IAppDbContext db, IPhamViHocPhi phamVi)
    : IRequestHandler<XoaKhoanThuCommand>
{
    public async Task Handle(XoaKhoanThuCommand request, CancellationToken ct)
    {
        var q = await phamVi.LocKhoanThuDuocSua(db.KhoanThuHocPhis.AsQueryable(), ct);

        var khoan = await q.FirstOrDefaultAsync(k => k.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"KhoanThu {request.Id}");

        db.KhoanThuHocPhis.Remove(khoan);
        await db.SaveChangesAsync(ct);
    }
}
