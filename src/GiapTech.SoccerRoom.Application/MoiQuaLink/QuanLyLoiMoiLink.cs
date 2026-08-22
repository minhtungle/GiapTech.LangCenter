using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.MoiQuaLink;

/// <summary>Một link đã gửi, theo góc nhìn NGƯỜI GỬI.</summary>
public record LoiMoiLinkDaGuiDto(
    Guid Id,
    Guid DoiThuId,
    string TenDoiThu,
    Guid? TranDauId,
    DateTimeOffset? ThoiGianDeXuat,
    string? DiaDiem,
    TrangThaiLoiMoi TrangThai,
    TinhTrangLink TinhTrang,
    string? PhanHoi,
    DateTimeOffset HetHan,
    DateTimeOffset NgayTao,
    /// <summary>Mã đội của CLB đã chấp nhận — null nếu chưa ai nhận.</summary>
    string? MaDoiDaNhan,
    string? TenDoiDaNhan,
    /// <summary>Đã liên kết và CHƯA huỷ → cho phép huỷ liên kết nếu sai người (ca 5).</summary>
    bool CoTheHuyLienKet);

public record LayDanhSachLoiMoiLinkQuery : IRequest<List<LoiMoiLinkDaGuiDto>>;

public class LayDanhSachLoiMoiLinkHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayDanhSachLoiMoiLinkQuery, List<LoiMoiLinkDaGuiDto>>
{
    public async Task<List<LoiMoiLinkDaGuiDto>> Handle(
        LayDanhSachLoiMoiLinkQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is null) throw new AppException(MaLoi.ChuaXacThuc);

        // Global Query Filter tự lọc theo tenant người gửi — bảng này CÓ tenant_id.
        var ds = await db.LoiMoiLinks
            .OrderByDescending(l => l.NgayTao)
            .Select(l => new
            {
                l.Id, l.DoiThuId, l.TranDauId, l.ThoiGianDeXuat, l.DiaDiem, l.TrangThai,
                l.PhanHoi, l.HetHan, l.NgayTao, l.ThuHoiLuc, l.TenantNhanId, l.DaHuyLienKet,
                TenDoiThu = l.DoiThu.TenDoi,
                MaDoiHeThong = l.DoiThu.MaDoiHeThong,
            })
            .ToListAsync(ct);

        // Tên CLB đã nhận: đọc ngoài tenant, nên chỉ lấy mã + tên (không id, không gì khác).
        var idNhan = ds.Where(x => x.TenantNhanId != null).Select(x => x.TenantNhanId!.Value)
            .Distinct().ToList();
        var tenNhan = idNhan.Count == 0
            ? []
            : await db.Tenants.IgnoreQueryFilters()
                .Where(t => idNhan.Contains(t.Id))
                .Select(t => new { t.Id, t.MaDoi, t.TenDoi })
                .ToDictionaryAsync(t => t.Id, t => (t.MaDoi, t.TenDoi), ct);

        return ds.Select(x =>
        {
            var nhan = x.TenantNhanId is { } idn && tenNhan.TryGetValue(idn, out var v)
                ? v
                : (MaDoi: (string?)null, TenDoi: (string?)null);

            return new LoiMoiLinkDaGuiDto(
                x.Id, x.DoiThuId, x.TenDoiThu, x.TranDauId, x.ThoiGianDeXuat, x.DiaDiem,
                x.TrangThai,
                XemLoiMoiLinkHandler.XacDinhTinhTrang(
                    x.TrangThai, x.HetHan, x.ThuHoiLuc, x.TranDauId != null, x.TranDauId),
                x.PhanHoi, x.HetHan, x.NgayTao,
                nhan.MaDoi, nhan.TenDoi,
                // Huỷ liên kết chỉ có nghĩa khi ĐÃ liên kết và chưa huỷ.
                CoTheHuyLienKet: x.TrangThai == TrangThaiLoiMoi.DaChapNhan
                                 && !x.DaHuyLienKet
                                 && x.MaDoiHeThong != null);
        }).ToList();
    }
}

// ---------- Thu hồi (ca 7) ----------

public record ThuHoiLoiMoiLinkCommand(Guid Id) : IRequest;

public class ThuHoiLoiMoiLinkHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<ThuHoiLoiMoiLinkCommand>
{
    public async Task Handle(ThuHoiLoiMoiLinkCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is null) throw new AppException(MaLoi.ChuaXacThuc);

        // Query Filter lo phần cách ly: chỉ người gửi thấy lời mời của mình.
        var l = await db.LoiMoiLinks.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LoiMoiLink {request.Id}");

        if (l.TrangThai != TrangThaiLoiMoi.ChoPhanHoi)
            throw new AppException(MaLoiLink.DaTraLoi);

        // Đánh dấu chứ KHÔNG xoá hàng: người nhận đang mở link cần thấy "đã được thu hồi" thay
        // vì một trang lỗi không giải thích gì.
        l.ThuHoiLuc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

// ---------- Huỷ liên kết vì sai người nhận (ca 5) ----------

public record HuyLienKetCommand(Guid Id) : IRequest;

/// <summary>
/// Gỡ liên kết khi link bị chuyển tiếp và người lạ chấp nhận.
///
/// Link chia sẻ được là bản chất — không chống tuyệt đối được. Nhưng phải **phát hiện và hoàn
/// tác** được: người gửi thấy CLB nào đã nhận, và gỡ nếu sai.
///
/// Gỡ nghĩa là: xoá `MaDoiHeThong` khỏi đối thủ (về lại dạng tên gõ tay). **Không** xoá trận —
/// bạn vẫn đá với ai đó hôm đó, và xoá trận là làm mất dữ liệu bạn không yêu cầu (quy tắc #1).
/// </summary>
public class HuyLienKetHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<HuyLienKetCommand>
{
    public async Task Handle(HuyLienKetCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is null) throw new AppException(MaLoi.ChuaXacThuc);

        var l = await db.LoiMoiLinks
            .Include(x => x.DoiThu)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KhongTimThayException($"LoiMoiLink {request.Id}");

        if (l.TrangThai != TrangThaiLoiMoi.DaChapNhan || l.DaHuyLienKet)
            throw new AppException("CHUA_LIEN_KET_KHONG_HUY_DUOC");

        l.DoiThu.MaDoiHeThong = null;
        l.DaHuyLienKet = true;

        await db.SaveChangesAsync(ct);
    }
}
