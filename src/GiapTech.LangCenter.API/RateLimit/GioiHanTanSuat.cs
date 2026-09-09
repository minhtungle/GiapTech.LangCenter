using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GiapTech.LangCenter.API.RateLimit;

/// <summary>
/// Giới hạn tần suất cho các endpoint **ẩn danh** (nợ kỹ thuật N3).
///
/// Vì sao ở tầng API chứ không ở Caddy như kế hoạch ban đầu ghi:
///
/// 1. `caddy:2-alpine` **không có** module rate limit — nó là plugin của bên thứ ba, muốn dùng
///    phải tự build image Caddy bằng `xcaddy`. Thêm một image tự build vào chuỗi triển khai là
///    thêm một thứ phải bảo trì và phải rebuild mỗi lần Caddy ra bản vá.
/// 2. Caddy chỉ thấy IP và đường dẫn, **không đọc được body**. Mà token của lời mời nằm trong
///    body (cố ý — URL vào log, vào history, vào Referer). Nên Caddy không phân biệt được
///    "một người dò 500 token khác nhau" với "500 người mở link của mình".
/// 3. .NET 8 có `Microsoft.AspNetCore.RateLimiting` sẵn trong framework, không thêm dependency.
///
/// Đánh đổi: giới hạn ở API nghĩa là request rác vẫn tới được API (Caddy sẽ chặn sớm hơn, nhẹ
/// hơn). Chấp nhận được ở quy mô này; nếu sau bị tấn công thật thì thêm một lớp ở Caddy **bên
/// trên** lớp này, không thay thế nó.
///
/// Chống được gì: dò token/mã trung tâm theo kiểu vét cạn. **Không** chống được DDoS phân tán — việc
/// đó cần Cloudflare hoặc tương đương, xem `docs/ha-tang/`.
/// </summary>
public static class GioiHanTanSuat
{
    /// <summary>Endpoint tra mã trung tâm ở trang đăng nhập — chỉ ĐỌC.</summary>
    public const string TraCuu = "tra-cuu";

    /// <summary>Đăng nhập và quên mật khẩu — chống dò mật khẩu.</summary>
    public const string XacThuc = "xac-thuc";

    /// <summary>Độ dài một đoạn của cửa sổ trượt, tính bằng giây (1 phút / 6 đoạn).</summary>
    private const int GiaySauMotDoan = 10;

    // Hạn mức đặt thành hằng số công khai để TEST đọc được. Test chỉ so TÊN policy thì không
    // bắt được việc ai đó nới số — phản chứng 21/08 lọt đúng kiểu đó: đổi 10 thành 3000 mà 5/5
    // vẫn xanh. Con số phải kiểm được, không chỉ cái tên.
    public const int HanMucTraCuu = 30;
    public const int HanMucXacThuc = 10;

    /// <summary>
    /// Bật/tắt qua cấu hình <c>GIOI_HAN_TAN_SUAT</c> (mặc định BẬT).
    ///
    /// Cần cờ này vì `TestServer` không có kết nối TCP thật: `RemoteIpAddress` là null nên MỌI
    /// test rơi vào chung một phân vùng, và 10 request/phút bị đốt hết trong vài test đầu — 112
    /// test đỏ vì nhận `QUA_NHIEU_YEU_CAU` thay vì dữ liệu. Đã xảy ra thật 21/08.
    ///
    /// Tắt bằng cấu hình chứ không bằng `#if DEBUG`: bản Release chạy trong CI cũng cần tắt, và
    /// `GioiHanTanSuatTests` cần **bật** để kiểm chính nó — nên phải là thứ đổi được theo từng
    /// factory, không phải theo kiểu build.
    /// </summary>
    public const string CauHinhBat = "GIOI_HAN_TAN_SUAT";

    public static IServiceCollection ThemGioiHanTanSuat(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // 429 kèm mã lỗi để frontend dịch được (quy tắc #3), không phải body rỗng.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                // Retry-After giúp client biết chờ bao lâu thay vì thử lại ngay và bị chặn tiếp.
                //
                // Cửa sổ TRƯỢT không cấp metadata `RetryAfter` (nó không có một thời điểm nạp
                // lại xác định như cửa sổ cố định), nên phải tự đặt. Không có header này thì
                // client thử lại ngay và bị chặn tiếp — vòng lặp vô nghĩa.
                var sauBaoLau = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var sau)
                    ? (int)sau.TotalSeconds
                    // Một đoạn của cửa sổ: 1 phút / 6 đoạn = 10 giây. Chờ đúng một đoạn là có
                    // lại quota, không cần chờ cả phút.
                    : GiaySauMotDoan;

                context.HttpContext.Response.Headers.RetryAfter =
                    sauBaoLau.ToString(NumberFormatInfo.InvariantInfo);

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"errorCode":"QUA_NHIEU_YEU_CAU","duLieu":null}""", ct);
            };

            // Cửa sổ trượt chứ không cố định: cửa sổ cố định cho phép gấp đôi hạn mức ở chỗ
            // giáp ranh (30 request cuối phút 1 + 30 đầu phút 2 = 60 trong một giây).
            options.AddPolicy(TraCuu, KhoaTheoIp(HanMucTraCuu, phut: 1));

            // Chặt hơn: 10/phút. Người dùng thật mở link một lần rồi trả lời một lần — 10 là
            // đã rất rộng, còn người dò cần hàng nghìn lần mới có hy vọng.

            options.AddPolicy(XacThuc, KhoaTheoIp(HanMucXacThuc, phut: 1));
        });

        return services;
    }

    private static Func<HttpContext, RateLimitPartition<string>> KhoaTheoIp(
        int soRequest, int phut) =>
        context => RateLimitPartition.GetSlidingWindowLimiter(
            LayIp(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = soRequest,
                Window = TimeSpan.FromMinutes(phut),
                SegmentsPerWindow = 6,
                // Không xếp hàng: request thứ 31 phải bị TỪ CHỐI ngay, không phải chờ. Xếp hàng
                // biến rate limit thành nguồn treo request và giữ luôn tài nguyên server.
                QueueLimit = 0,
            });

    /// <summary>
    /// IP thật của client.
    ///
    /// Sau Caddy thì `RemoteIpAddress` là IP của container Caddy — **mọi** người dùng chung một
    /// khoá và hạn mức thành hạn mức cho cả hệ thống. Nên phải đọc `X-Forwarded-For`.
    ///
    /// **Đã ĐO hành vi Caddy 2.11 thay vì suy đoán (21/08):** Caddy **ghi đè** header này bằng
    /// IP nó thấy, **không** nối thêm vào danh sách. Dựng một Caddy thăm dò trước một server
    /// echo: client gửi `X-Forwarded-For: 9.9.9.9` thì server nhận đúng `192.168.65.1` — giá trị
    /// client tự đặt bị xoá sạch.
    ///
    /// Hệ quả: header chỉ có **một** giá trị, nên `LastOrDefault` và `FirstOrDefault` cho cùng
    /// kết quả ở cấu hình hiện tại. Dùng **phần tử cuối** vì nó đúng trong cả hai trường hợp:
    /// nếu sau này chèn thêm một proxy phía trước (Cloudflare chẳng hạn) thì chuỗi thành
    /// `client, proxy1` và phần tử cuối là cái *gần ta nhất mà ta tin được* — lấy phần tử đầu
    /// lúc đó là tin giá trị client tự đặt, tức mở đường nhảy khoá vô hạn lần.
    ///
    /// Chỉ đọc header khi <c>SAU_REVERSE_PROXY=true</c>: chạy trực tiếp mà tin nó thì ai cũng tự
    /// đặt để vượt giới hạn.
    /// </summary>
    private static string LayIp(HttpContext context)
    {
        var sauProxy = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue("SAU_REVERSE_PROXY", false);

        if (sauProxy &&
            context.Request.Headers.TryGetValue("X-Forwarded-For", out var xff))
        {
            var cuoi = xff.ToString().Split(',', StringSplitOptions.TrimEntries)
                .LastOrDefault(s => s.Length > 0);
            if (cuoi is not null) return cuoi;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro";
    }
}
