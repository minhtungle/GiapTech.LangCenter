using System.Collections.Concurrent;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.LMS.Infrastructure.Identity;

/// <summary>
/// Cài đặt <see cref="IMuiGioTrungTam"/> — đọc `TENANT.mui_gio`, cache theo id chuỗi.
///
/// **Không bao giờ ném**: lịch học là chức năng chính, một id múi giờ gõ sai không được làm
/// sập cả module. Xấu nhất rơi về giờ Việt Nam rồi UTC, kèm log mức Error để còn phát hiện.
/// </summary>
public class MuiGioTrungTam(
    AppDbContext db, ICurrentTenant tenant, ILogger<MuiGioTrungTam> logger) : IMuiGioTrungTam
{
    private const string MacDinh = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// Cache tĩnh theo id múi giờ: `FindSystemTimeZoneById` đọc file hệ thống mỗi lần gọi trên
    /// Linux. Số múi giờ hữu hạn và không đổi lúc chạy nên cache vĩnh viễn là an toàn.
    /// </summary>
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new();

    public async Task<TimeZoneInfo> LayMuiGio(CancellationToken ct = default)
    {
        var id = MacDinh;

        if (tenant.TenantId is { } tid)
        {
            var tuDb = await db.Tenants
                .Where(t => t.Id == tid)
                .Select(t => t.MuiGio)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(tuDb)) id = tuDb;
        }

        return Tim(id, logger);
    }

    private static TimeZoneInfo Tim(string id, ILogger logger)
    {
        if (Cache.TryGetValue(id, out var daCo)) return daCo;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            Cache[id] = tz;
            return tz;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Nếu chính múi giờ mặc định cũng không nạp được thì hệ thống thiếu tzdata/icu-libs
            // — xem Dockerfile. Rơi về UTC để không sập, nhưng log Error vì lịch sẽ lệch giờ.
            if (id == MacDinh)
            {
                logger.LogError(ex,
                    "Không nạp được múi giờ mặc định {Id} — thiếu tzdata/icu-libs? Dùng UTC.", id);
                Cache[id] = TimeZoneInfo.Utc;
                return TimeZoneInfo.Utc;
            }

            logger.LogError(ex, "Múi giờ {Id} không hợp lệ, dùng {MacDinh}.", id, MacDinh);
            return Tim(MacDinh, logger);
        }
    }
}
