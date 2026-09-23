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
        var chuan = ChuanHoa(domain);
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

    public void XoaCache(string domain)
    {
        var chuan = ChuanHoa(domain);
        if (!string.IsNullOrEmpty(chuan))
            cache.Remove(Khoa(chuan));
    }

    /// <summary>
    /// Chuẩn hoá domain về dạng lưu trong DB: chữ thường, bỏ scheme, bỏ dấu chấm cuối.
    ///
    /// **Giữ nguyên cổng.** Lúc đầu tôi định cắt cổng đi cho gọn, nhưng như vậy
    /// `localhost:5173` và `localhost:9999` thành một — ở local hai cổng là hai ứng dụng
    /// khác nhau. Cổng là một phần của định danh, cắt đi là gộp nhầm.
    ///
    /// Dấu chấm cuối (`abc.com.`) hợp lệ về mặt DNS và trình duyệt gửi được, nhưng DB lưu
    /// dạng không chấm — không bỏ thì cùng một domain lại tra trượt.
    /// </summary>
    internal static string ChuanHoa(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return string.Empty;

        var s = domain.Trim().ToLowerInvariant();

        if (s.StartsWith("http://", StringComparison.Ordinal))
            s = s[7..];
        else if (s.StartsWith("https://", StringComparison.Ordinal))
            s = s[8..];

        // Bỏ đường dẫn nếu lỡ lọt vào (vd "abc.com/xyz")
        var gach = s.IndexOf('/');
        if (gach >= 0)
            s = s[..gach];

        // Dấu chấm cuối của FQDN
        s = s.TrimEnd('.');

        return s;
    }

    private static string Khoa(string domain) => $"tenant-domain:{domain}";
}
