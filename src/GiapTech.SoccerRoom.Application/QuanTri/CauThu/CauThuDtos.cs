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
    int? SoAo, string? ViTriSoTruong,
    bool DaNghi = false, DateOnly? NgayNghi = null);

/// <summary>Lọc theo trạng thái hoạt động (21/08).</summary>
public enum LocTrangThaiCauThu
{
    /// <summary>Chỉ người đang đá — **mặc định** cho danh sách hằng ngày.</summary>
    DangDa = 0,

    /// <summary>Chỉ người đã nghỉ, để rà lại hoặc cho ai đó đá lại.</summary>
    DaNghi = 1,

    Tatca = 2,
}

// ---------- Queries ----------

/// <param name="Loc">
/// Mặc định <see cref="LocTrangThaiCauThu.DangDa"/> — danh sách hằng ngày phải gọn.
///
/// Mặc định là "đang đá" chứ không "tất cả" vì đây là API dùng chung: mọi chỗ chọn người (mời
/// đăng ký, xếp đội hình, thu quỹ) đều gọi nó, và không chỗ nào trong số đó muốn thấy người đã
/// nghỉ. Để mặc định "tất cả" thì mỗi chỗ gọi phải nhớ truyền tham số — quên một chỗ là người
/// nghỉ lại xuất hiện trong ô chọn.
/// </param>
public record LayDanhSachCauThuQuery(
    string? TimKiem = null,
    ThamSoTrang? Trang = null,
    LocTrangThaiCauThu Loc = LocTrangThaiCauThu.DangDa)
    : IRequest<KetQuaTrang<CauThuDto>>;

public class LayDanhSachCauThuHandler(IAppDbContext db)
    : IRequestHandler<LayDanhSachCauThuQuery, KetQuaTrang<CauThuDto>>
{
    public async Task<KetQuaTrang<CauThuDto>> Handle(
        LayDanhSachCauThuQuery request, CancellationToken ct)
    {
        var trang = request.Trang ?? new ThamSoTrang();
        var q = db.CauThus.AsQueryable();

        q = request.Loc switch
        {
            LocTrangThaiCauThu.DangDa => q.Where(c => !c.DaNghi),
            LocTrangThaiCauThu.DaNghi => q.Where(c => c.DaNghi),
            _ => q,
        };

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
                db.NguoiDungs.Any(u => u.CauThuId == c.Id), c.SoAo, c.ViTriSoTruong,
                c.DaNghi, c.NgayNghi))
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
                   db.NguoiDungs.Any(u => u.CauThuId == c.Id), c.SoAo, c.ViTriSoTruong,
                   c.DaNghi, c.NgayNghi))
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

        // Trận nào cầu thủ này có đánh giá — phải TÍNH LẠI tỷ số sau khi xoá.
        //
        // Bàn thắng đội nhà là TỔNG bàn trong đánh giá (xem TranDau.DongBoTySoNha). Đánh giá của
        // cầu thủ bị xoá sẽ Cascade theo, nên nếu không tính lại thì trận vẫn giữ tỷ số cũ —
        // "thắng 2-1" với 0 bàn trong đánh giá, một con số không giải thích được từ dữ liệu.
        //
        // Lỗi này IM LẶNG: không ai biết cho tới khi mở chi tiết trận và thấy tỷ số không khớp
        // bàn thắng cầu thủ. Nó phá đúng nguyên tắc "một nguồn sự thật cho tỷ số" của FR-10, và
        // con số sai lan sang thống kê, biểu đồ, bảng xếp hạng.
        var idTranAnhHuong = await db.DanhGiaCauThus
            .Where(d => d.CauThuId == request.Id)
            .Select(d => d.TranDauId)
            .Distinct()
            .ToListAsync(ct);

        db.CauThus.Remove(cauThu);
        await db.SaveChangesAsync(ct);

        if (idTranAnhHuong.Count == 0) return;

        // Đọc SAU khi xoá: lúc này đánh giá của cầu thủ đó đã biến mất khỏi DB, nên tổng cộng
        // được là tổng còn lại thật.
        var trans = await db.TranDaus
            .Where(t => idTranAnhHuong.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var tran in trans)
        {
            var tongBanThang = await db.DanhGiaCauThus
                .Where(d => d.TranDauId == tran.Id)
                .SumAsync(d => (int?)d.SoBanGhiDuoc, ct) ?? 0;

            tran.DongBoTySoNha(tongBanThang);
        }

        await db.SaveChangesAsync(ct);
    }
}
