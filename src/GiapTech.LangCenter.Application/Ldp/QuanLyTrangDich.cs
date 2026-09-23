using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Ldp;

/// <summary>
/// Đọc trang đích để SOẠN (FR-30) — khác truy vấn công khai ở chỗ trả cả khối đang tắt và
/// khối chưa có nội dung.
///
/// **Tự tạo trang nếu chưa có.** Mỗi tenant đúng một trang, và nó không có ý nghĩa gì khi
/// vắng mặt — bắt người dùng bấm "Tạo trang" một lần rồi không bao giờ bấm lại là thêm một
/// bước vô ích. Tạo kèm đủ 10 khối rỗng theo thứ tự mặc định.
/// </summary>
public record TrangDichQuery : IRequest<TrangDichDto>;

public class TrangDichHandler(IAppDbContext db) : IRequestHandler<TrangDichQuery, TrangDichDto>
{
    /// <summary>Thứ tự khối mặc định — theo hình dạng landing thường gặp.</summary>
    private static readonly LoaiKhoiLdp[] ThuTuMacDinh =
    [
        LoaiKhoiLdp.Hero, LoaiKhoiLdp.GioiThieu, LoaiKhoiLdp.KhoaHoc, LoaiKhoiLdp.QuyTrinh,
        LoaiKhoiLdp.GiaoVien, LoaiKhoiLdp.CamNhan, LoaiKhoiLdp.DoiTac, LoaiKhoiLdp.TinTuc,
        LoaiKhoiLdp.CoSo, LoaiKhoiLdp.LienHe
    ];

    public async Task<TrangDichDto> Handle(TrangDichQuery request, CancellationToken ct)
    {
        var trang = await db.TrangDiches
            .Include(t => t.Khois).ThenInclude(k => k.Mucs)
            .FirstOrDefaultAsync(ct);

        if (trang is null)
        {
            trang = new TrangDich { DaXuatBan = false };
            for (var i = 0; i < ThuTuMacDinh.Length; i++)
            {
                trang.Khois.Add(new KhoiLdp
                {
                    Loai = ThuTuMacDinh[i],
                    ThuTu = i,
                    // Bật sẵn: trang mới tạo mà mọi khối đều tắt thì người dùng soạn xong
                    // vẫn thấy trang trắng và không hiểu vì sao.
                    Hien = true
                });
            }

            db.TrangDiches.Add(trang);
            await db.SaveChangesAsync(ct);
        }

        return Chuyen(trang);
    }

    internal static TrangDichDto Chuyen(TrangDich t) => new(
        t.Id, t.DaXuatBan, t.TieuDeSeo, t.MoTaSeo,
        t.Khois.OrderBy(k => k.ThuTu).Select(k => new KhoiDto(
                k.Id, k.Loai, k.Hien, k.ThuTu, k.TieuDe, k.MoTa, k.KhoaAnh,
                k.NhanNut, k.DuongDanNut,
                k.Mucs.OrderBy(m => m.ThuTu).Select(m => new MucDto(
                        m.Id, m.ThuTu, m.TieuDe, m.PhuDe, m.MoTa, m.KhoaAnh,
                        m.GiaNiemYet, m.DuongDan))
                    .ToList()))
            .ToList());
}

/// <summary>Sửa một khối: nội dung, bật/tắt, thứ tự.</summary>
public record SuaKhoiCommand(
    Guid KhoiId,
    bool Hien,
    int ThuTu,
    string? TieuDe,
    string? MoTa,
    string? KhoaAnh,
    string? NhanNut,
    string? DuongDanNut) : IRequest;

public class SuaKhoiHandler(IAppDbContext db) : IRequestHandler<SuaKhoiCommand>
{
    public async Task Handle(SuaKhoiCommand r, CancellationToken ct)
    {
        var khoi = await db.KhoiLdps.FirstOrDefaultAsync(k => k.Id == r.KhoiId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Khối {r.KhoiId}");

        /*
          Gán MỌI trường, kể cả khi client gửi null (quy tắc #1).

          Lệnh này ghi đè trường nào thì trường đó phải có trong DTO trả về VÀ trong form —
          màn soạn hiện đủ cả tám ô, nên gán đủ tám là đúng. Xoá nội dung một ô là thao tác
          hợp lệ và người dùng phải làm được.
        */
        khoi.Hien = r.Hien;
        khoi.ThuTu = r.ThuTu;
        khoi.TieuDe = r.TieuDe;
        khoi.MoTa = r.MoTa;
        khoi.KhoaAnh = r.KhoaAnh;
        khoi.NhanNut = r.NhanNut;
        khoi.DuongDanNut = r.DuongDanNut;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Thêm mục vào một khối nhiều mục.</summary>
public record ThemMucCommand(Guid KhoiId, string TieuDe) : IRequest<Guid>;

public class ThemMucHandler(IAppDbContext db) : IRequestHandler<ThemMucCommand, Guid>
{
    public async Task<Guid> Handle(ThemMucCommand r, CancellationToken ct)
    {
        var khoi = await db.KhoiLdps
            .Include(k => k.Mucs)
            .FirstOrDefaultAsync(k => k.Id == r.KhoiId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Khối {r.KhoiId}");

        // Bảy loại khối có nhiều mục; ba loại còn lại (Hero, GioiThieu, LienHe) chỉ có nội
        // dung của chính khối. Chặn ở đây chứ không tin giao diện — giao diện không hiện nút
        // "Thêm mục" cho ba loại kia, nhưng API vẫn phải tự bảo vệ.
        if (khoi.Loai is not (LoaiKhoiLdp.KhoaHoc or LoaiKhoiLdp.GiaoVien
            or LoaiKhoiLdp.CamNhan or LoaiKhoiLdp.TinTuc
            or LoaiKhoiLdp.DoiTac or LoaiKhoiLdp.QuyTrinh or LoaiKhoiLdp.CoSo))
        {
            throw new AppException(
                MaLoi.DuLieuKhongHopLe, $"Khối {khoi.Loai} không chứa mục con");
        }

        var muc = new MucLdp
        {
            KhoiLdpId = khoi.Id,
            TieuDe = r.TieuDe.Trim(),
            ThuTu = khoi.Mucs.Count == 0 ? 0 : khoi.Mucs.Max(m => m.ThuTu) + 1
        };

        db.MucLdps.Add(muc);
        await db.SaveChangesAsync(ct);
        return muc.Id;
    }
}

/// <summary>Sửa một mục.</summary>
public record SuaMucCommand(
    Guid MucId,
    int ThuTu,
    string TieuDe,
    string? PhuDe,
    string? MoTa,
    string? KhoaAnh,
    decimal? GiaNiemYet,
    string? DuongDan) : IRequest;

public class SuaMucHandler(IAppDbContext db) : IRequestHandler<SuaMucCommand>
{
    public async Task Handle(SuaMucCommand r, CancellationToken ct)
    {
        var muc = await db.MucLdps.FirstOrDefaultAsync(m => m.Id == r.MucId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Mục {r.MucId}");

        // Gán đủ mọi trường — xem chú thích ở `SuaKhoiHandler`.
        muc.ThuTu = r.ThuTu;
        muc.TieuDe = r.TieuDe.Trim();
        muc.PhuDe = r.PhuDe;
        muc.MoTa = r.MoTa;
        muc.KhoaAnh = r.KhoaAnh;
        muc.GiaNiemYet = r.GiaNiemYet;
        muc.DuongDan = r.DuongDan;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Xoá một mục.</summary>
public record XoaMucCommand(Guid MucId) : IRequest;

public class XoaMucHandler(IAppDbContext db) : IRequestHandler<XoaMucCommand>
{
    public async Task Handle(XoaMucCommand r, CancellationToken ct)
    {
        var muc = await db.MucLdps.FirstOrDefaultAsync(m => m.Id == r.MucId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Mục {r.MucId}");

        db.MucLdps.Remove(muc);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Xuất bản / gỡ xuất bản trang (FR-30).
///
/// Thao tác riêng `HanhDong.XuatBan`, không nằm trong `Sua`: sửa sai thì sửa lại, còn xuất
/// bản sai là người ngoài đã đọc được.
/// </summary>
public record XuatBanCommand(bool XuatBan, string? TieuDeSeo, string? MoTaSeo) : IRequest;

public class XuatBanHandler(IAppDbContext db) : IRequestHandler<XuatBanCommand>
{
    public async Task Handle(XuatBanCommand r, CancellationToken ct)
    {
        var trang = await db.TrangDiches.FirstOrDefaultAsync(ct)
            ?? throw new AppException(MaLoi.KhongTimThay, "Chưa có trang đích");

        trang.DaXuatBan = r.XuatBan;
        trang.TieuDeSeo = r.TieuDeSeo;
        trang.MoTaSeo = r.MoTaSeo;

        await db.SaveChangesAsync(ct);
    }
}
