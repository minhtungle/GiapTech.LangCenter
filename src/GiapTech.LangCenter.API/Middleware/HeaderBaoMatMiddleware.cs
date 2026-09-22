namespace GiapTech.LangCenter.API.Middleware;

/// <summary>
/// **Header bảo mật ở tầng ứng dụng** — lớp đáy, không thay Nginx (22/09/2026).
///
/// ## Vấn đề nó chữa
///
/// Trước thay đổi này, toàn bộ header bảo mật chỉ tồn tại trong `deploy/nginx/langcenter.conf`
/// — một tệp **phải copy tay lên VPS**, và certbot còn tự chèn thêm block 443. Cài sai, quên
/// reload, hay dựng môi trường mới mà bỏ sót tệp đó thì production chạy **trần trụi**: không
/// HSTS, không `nosniff`, không `X-Frame-Options`.
///
/// Không phải rủi ro lý thuyết: chú thích trong chính `langcenter.conf` và `Caddyfile.dev` xác
/// nhận việc này **đã từng xảy ra** và kéo dài. Rà soát bảo mật 22/09/2026 xếp là mục 5.
///
/// ## Không thay Nginx, mà chồng thêm
///
/// Nginx vẫn là nơi đặt header cho **mọi** phản hồi, kể cả tệp tĩnh mà API không đụng tới.
/// Middleware này chỉ bảo đảm phần API **không bao giờ** thiếu header, dù đứng sau proxy nào.
///
/// Vì vậy nó dùng <see cref="Microsoft.AspNetCore.Http.IHeaderDictionary"/> kiểu **chỉ đặt khi
/// chưa có**: nếu Nginx đã đặt rồi thì giữ nguyên giá trị của Nginx, không ghi đè. Ghi đè sẽ
/// làm cấu hình ở hai nơi âm thầm đá nhau, và người sửa Nginx sẽ không hiểu vì sao thay đổi
/// của mình không có tác dụng.
///
/// ## Vì sao KHÔNG có Content-Security-Policy ở đây
///
/// CSP phụ thuộc thứ frontend thật sự tải (`blob:` cho ảnh qua axios, `data:` cho favicon…),
/// nên nó thuộc về nơi phục vụ **trang**, không phải nơi phục vụ **API**. Đặt một CSP đoán mò
/// ở đây sẽ hoặc quá chặt (ảnh biến mất, không lỗi nào hiện ra) hoặc quá lỏng (vô nghĩa).
/// Nginx giữ CSP; xem `langcenter.conf`.
///
/// ## HSTS chỉ khi thật sự chạy HTTPS
///
/// Gửi HSTS qua HTTP thuần là vô nghĩa (trình duyệt bỏ qua theo chuẩn), còn gửi ở môi trường
/// dev chạy `http://localhost` thì **khoá cả localhost sang HTTPS** trong trình duyệt của lập
/// trình viên — một lỗi rất khó chẩn đoán vì nó nằm trong cache của trình duyệt, không nằm
/// trong mã. Nên chỉ gửi khi request thật sự là HTTPS, hoặc khi proxy khai `X-Forwarded-Proto:
/// https`.
/// </summary>
public class HeaderBaoMatMiddleware(RequestDelegate next)
{
    /// <summary>1 năm — giá trị khuyến nghị, khớp với `langcenter.conf`.</summary>
    private const string GiaTriHsts = "max-age=31536000; includeSubDomains";

    public async Task InvokeAsync(HttpContext context)
    {
        /*
          Đặt TRƯỚC khi chạy tiếp, qua `OnStarting`: một khi phản hồi đã bắt đầu gửi thì không
          còn thêm header được nữa (ASP.NET ném `InvalidOperationException`). Endpoint trả
          stream — ảnh qua MinIO chẳng hạn — bắt đầu gửi rất sớm.
        */
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            // `TryAdd` chứ không gán: Nginx đã đặt thì giữ của Nginx — xem chú thích đầu lớp.
            h.TryAdd("X-Content-Type-Options", "nosniff");
            h.TryAdd("X-Frame-Options", "DENY");
            h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            h.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

            if (LaHttps(context))
                h.TryAdd("Strict-Transport-Security", GiaTriHsts);

            return Task.CompletedTask;
        });

        await next(context);
    }

    /// <summary>
    /// Request có thật sự đi qua HTTPS không.
    ///
    /// Sau reverse proxy thì `IsHttps` là false (proxy nói chuyện HTTP với API), nên phải đọc
    /// `X-Forwarded-Proto`. Chỉ tin header đó khi đứng sau proxy — chạy trực tiếp mà tin thì
    /// ai cũng tự đặt được, và ở đây hậu quả là bật HSTS cho một kết nối không mã hoá.
    /// </summary>
    private static bool LaHttps(HttpContext context)
    {
        if (context.Request.IsHttps) return true;

        var sauProxy = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue("SAU_REVERSE_PROXY", false);

        return sauProxy
               && context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto)
               && proto.ToString().Equals("https", StringComparison.OrdinalIgnoreCase);
    }
}
