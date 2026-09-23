using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.ChuHeThong;

/// <summary>Tóm tắt một trung tâm cho site chủ — KHÔNG có dữ liệu nghiệp vụ bên trong.</summary>
public record TrungTamTomTatDto(
    Guid Id,
    string MaTrungTam,
    string TenTrungTam,
    string? DomainQuanTri,
    string? DomainLanding,
    DateTimeOffset NgayTao);

/// <summary>
/// Danh sách mọi trung tâm (ADR-0009).
///
/// Endpoint duy nhất trong hệ thống đọc **mọi** tenant. Không lọc theo tenant vì đó chính là
/// mục đích — và `TENANT` vốn không phải `ITenantEntity` nên không có Query Filter để bỏ qua.
/// Hàng rào nằm ở `[ChiChuHeThong]` trên controller.
/// </summary>
public record DanhSachTrungTamQuery : IRequest<IReadOnlyList<TrungTamTomTatDto>>;

public class DanhSachTrungTamHandler(IAppDbContext db)
    : IRequestHandler<DanhSachTrungTamQuery, IReadOnlyList<TrungTamTomTatDto>>
{
    public async Task<IReadOnlyList<TrungTamTomTatDto>> Handle(
        DanhSachTrungTamQuery request, CancellationToken ct)
        => await db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrungTamTomTatDto(
                t.Id, t.MaTrungTam, t.TenTrungTam,
                t.DomainQuanTri, t.DomainLanding, t.CreatedAt))
            .ToListAsync(ct);
}

/// <summary>
/// Gắn / đổi / gỡ domain của một trung tâm (ADR-0008 + ADR-0009).
///
/// `null` = gỡ domain. Gỡ là thao tác **an toàn**: trung tâm rơi về đường mã trung tâm chứ
/// không mất quyền truy cập — đúng lý do ADR-0008 giữ hai đường cùng sống.
/// </summary>
/// <param name="TrungTamId">Do controller điền từ route, không phải từ body.</param>
public record GanDomainCommand(
    Guid TrungTamId,
    string? DomainQuanTri,
    string? DomainLanding) : IRequest;

public class GanDomainHandler(IAppDbContext db, IGiaiTenantTheoDomain giaiTenant)
    : IRequestHandler<GanDomainCommand>
{
    public async Task Handle(GanDomainCommand request, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TrungTamId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Tenant {request.TrungTamId}");

        var quanTriMoi = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainQuanTri);
        var landingMoi = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainLanding);

        if (quanTriMoi is not null && quanTriMoi == landingMoi)
            throw new AppException(
                MaLoi.DuLieuKhongHopLe,
                "Domain quản trị và domain landing không được trùng nhau");

        // Xoá cache của domain CŨ lẫn MỚI.
        //
        // Cũ: không xoá thì domain vừa gỡ vẫn tra ra tenant này tới 5 phút — tức là vẫn vào
        // được sau khi đã gỡ.
        // Mới: domain vừa gắn có thể đang nằm trong cache "không khớp" (TTL 30 giây), nên
        // không xoá thì người vận hành gắn xong thử ngay lại thấy 404 và tưởng mình làm sai.
        foreach (var d in new[]
                 {
                     tenant.DomainQuanTri, tenant.DomainLanding, quanTriMoi, landingMoi
                 })
        {
            if (!string.IsNullOrEmpty(d)) giaiTenant.XoaCache(d);
        }

        tenant.DomainQuanTri = quanTriMoi;
        tenant.DomainLanding = landingMoi;

        // UNIQUE ở tầng DB là thứ chặn thật khi hai người gắn cùng một domain cùng lúc
        // (quy tắc #8). Ở đây không kiểm trùng bằng `AnyAsync` rồi ghi — đó đúng là cái bẫy
        // race condition mà `DongThoiTests` canh.
        await db.SaveChangesAsync(ct);
    }

}
