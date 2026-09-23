using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GiapTech.LangCenter.Infrastructure.MultiTenancy;

/// <summary>
/// Tra tenant từ domain, có cache ngắn hạn (ADR-0008).
///
/// Cache là bắt buộc chứ không phải tối ưu sớm: tra domain chạy ở MỌI request, kể cả
/// request ẩn danh của trang landing. Không cache thì mỗi lượt tải trang là thêm một
/// truy vấn — và landing là chỗ chịu tải cao nhất vì khách vãng lai không bị giới hạn gì.
/// </summary>
public class GiaiTenantTheoDomain(AppDbContext db, IMemoryCache cache) : IGiaiTenantTheoDomain
{
    /// <summary>
    /// TTL ngắn. Gắn/đổi domain đều gọi <see cref="XoaCache"/>, nên TTL chỉ là lưới an toàn
    /// cho trường hợp sửa dữ liệu thẳng trong DB (việc hay làm lúc dựng VPS).
    /// </summary>
    private static readonly TimeSpan ThoiGianSong = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Cache CẢ kết quả rỗng, với TTL ngắn hơn.
    ///
    /// Vì sao cần: domain không khớp là trường hợp một kẻ quét tự động tạo ra hàng loạt
    /// (gửi Host ngẫu nhiên). Không cache thì mỗi request rác là một truy vấn DB — biến
    /// việc tra domain thành đường khuếch đại tải.
    ///
    /// TTL ngắn hơn vì đây là trạng thái "chưa có", hay đổi hơn: người vận hành vừa gắn
    /// domain xong thì muốn thấy hiệu lực ngay, không phải chờ 5 phút.
    /// </summary>
    private static readonly TimeSpan ThoiGianSongRong = TimeSpan.FromSeconds(30);

    public async Task<ThongTinTenantTheoDomain?> TraAsync(
        string domain, CancellationToken ct = default)
    {
        var chuan = Domain.Common.DomainTrungTam.ChuanHoa(domain) ?? string.Empty;
        if (string.IsNullOrEmpty(chuan))
            return null;

        var khoa = Khoa(chuan);

        // Dùng TryGetValue chứ không dùng `cache.Get(...) is null`: hai thứ đó khác nhau khi
        // giá trị đã cache CHÍNH LÀ null (domain không khớp). Nhầm chỗ này thì kết quả rỗng
        // không bao giờ được cache và lá chắn chống quét ở trên mất tác dụng.
        if (cache.TryGetValue<ThongTinTenantTheoDomain?>(khoa, out var daCo))
            return daCo;

        // Một tenant có thể gắn cả hai domain, và chúng có thể trùng nhau (dev, hoặc trung
        // tâm dùng chung một domain cho cả landing lẫn quản trị). Ưu tiên QUẢN TRỊ: nếu
        // trùng thì coi là cửa vào quản trị, vì đó là nhánh chặt hơn — landing không cần
        // quyền gì, còn quản trị thì cần.
        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.DomainQuanTri == chuan || t.DomainLanding == chuan)
            .Select(t => new
            {
                t.Id,
                t.MaTrungTam,
                t.TenTrungTam,
                LaQuanTri = t.DomainQuanTri == chuan
            })
            .OrderByDescending(t => t.LaQuanTri)
            .FirstOrDefaultAsync(ct);

        var ketQua = tenant is null
            ? null
            : new ThongTinTenantTheoDomain(
                tenant.Id, tenant.MaTrungTam, tenant.TenTrungTam, tenant.LaQuanTri);

        cache.Set(khoa, ketQua, ketQua is null ? ThoiGianSongRong : ThoiGianSong);
        return ketQua;
    }

    public async Task<Guid?> TraTheoMaAsync(string maTrungTam, CancellationToken ct = default)
    {
        if (!Domain.Common.MaTrungTam.HopLe(maTrungTam)) return null;

        var ma = Domain.Common.MaTrungTam.ChuanHoa(maTrungTam);
        var khoa = $"tenant-ma:{ma}";

        if (cache.TryGetValue<Guid?>(khoa, out var daCo)) return daCo;

        var id = await db.Tenants
            .AsNoTracking()
            .Where(t => t.MaTrungTam == ma)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

        // Cache cả kết quả rỗng với TTL ngắn — cùng lý do chống quét như `TraAsync`.
        cache.Set(khoa, id, id is null ? ThoiGianSongRong : ThoiGianSong);
        return id;
    }

    public void XoaCache(string domain)
    {
        var chuan = Domain.Common.DomainTrungTam.ChuanHoa(domain) ?? string.Empty;
        if (!string.IsNullOrEmpty(chuan))
            cache.Remove(Khoa(chuan));
    }

    private static string Khoa(string domain) => $"tenant-domain:{domain}";
}
