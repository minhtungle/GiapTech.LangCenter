namespace GiapTech.LangCenter.API.Authorization;

/// <summary>
/// **Cookie mang refresh token** — ADR-0007 (22/09/2026).
///
/// Một chỗ duy nhất quyết định mọi thuộc tính cookie phiên. Rải `Append`/`Delete` ở từng
/// endpoint là cách chắc chắn để sớm muộn có một chỗ thiếu `HttpOnly` hoặc thiếu `Secure` mà
/// không ai thấy.
///
/// ## Vì sao từng thuộc tính
///
/// | Thuộc tính | Lý do |
/// |---|---|
/// | `HttpOnly` | **Cốt lõi**: JavaScript không đọc được ⇒ XSS không mang phiên đi nơi khác |
/// | `Secure` | Không đi qua HTTP thuần. Tắt ở Development vì `http://localhost` — xem dưới |
/// | `SameSite=Lax` | Xem mục riêng bên dưới — `Strict` phá luồng khôi phục phiên |
/// | `Path=/api/v1/auth` | Chỉ gửi kèm nhóm endpoint xác thực, không kèm mọi request nghiệp vụ |
///
/// ## Vì sao `Lax` chứ KHÔNG phải `Strict`
///
/// Bản đầu dùng `SameSite=Strict` vì frontend và API cùng origin nên tưởng `Strict` không phá
/// gì. **Sai, và E2E bắt được**: `Strict` chặn cookie ở request phát sinh từ **điều hướng tài
/// liệu** — gõ URL, bấm F5, mở link trực tiếp, hay bất kỳ `page.goto` nào.
///
/// Đúng là những thao tác người dùng làm suốt ngày. Hệ quả đo được: đăng nhập xong, gõ thẳng
/// một URL khác là cookie **không được gửi**, `lam-moi-token` trả 400, và app đá người dùng về
/// màn đăng nhập. Tức `Strict` biến "khôi phục phiên sau F5" — thứ ADR-0007 phải có vì access
/// token nằm trong RAM — thành không dùng được.
///
/// `Lax` gửi cookie ở điều hướng cấp cao nhất bằng phương thức an toàn (GET), và **vẫn chặn**
/// request chéo origin kiểu POST do trang khác kích hoạt — tức vẫn chặn đúng ca CSRF nguy hiểm.
/// Phần còn lại do double-submit token lo, và đó mới là lớp chống CSRF chính ở đây.
///
/// ## `Secure` suy từ MÔI TRƯỜNG, không từ cấu hình
///
/// Cần tắt `Secure` ở dev (`http://localhost:5173`), nhưng **không** làm nó thành biến cấu hình
/// người vận hành đặt tay được: đặt nhầm ở production nghĩa là refresh token đi qua HTTP thuần,
/// tức phá đúng thứ ADR-0007 dựng lên. Suy từ `IWebHostEnvironment` thì môi trường Production
/// **không có cách nào** tắt được. Canh bởi `CookiePhienTests`.
/// </summary>
public static class CookiePhien
{
    /// <summary>Tên cookie refresh token.</summary>
    public const string Ten = "lms_rt";

    /// <summary>
    /// Đường dẫn hẹp: cookie chỉ đi kèm `/api/v1/auth/*`.
    ///
    /// Phải khớp route của `AuthController`. Lệch thì cookie không được gửi và người dùng bị
    /// đăng xuất mỗi lần tải lại trang — hỏng theo kiểu im lặng, nên có test chốt giá trị này.
    /// </summary>
    public const string DuongDan = "/api/v1/auth";

    /// <summary>
    /// Tên cookie CSRF — **KHÔNG** `httpOnly`, vì frontend phải đọc được để gửi lại trong header.
    /// </summary>
    public const string TenCsrf = "lms_csrf";

    /// <summary>Header mà frontend gửi token CSRF trở lại.</summary>
    public const string HeaderCsrf = "X-CSRF-Token";

    private static CookieOptions TuyChon(IWebHostEnvironment moiTruong, DateTimeOffset? hetHan) => new()
    {
        HttpOnly = true,
        // KHÔNG đọc từ IConfiguration — xem chú thích đầu lớp.
        Secure = !moiTruong.IsDevelopment(),
        // `Lax` chứ không `Strict` — xem chú thích đầu lớp.
        SameSite = SameSiteMode.Lax,
        Path = DuongDan,
        Expires = hetHan,
    };

    /// <summary>Đặt cookie refresh token. Gọi sau đăng nhập và sau mỗi lần làm mới (xoay vòng).</summary>
    public static void Dat(
        HttpResponse res, IWebHostEnvironment moiTruong, string refreshToken, DateTimeOffset hetHan)
        => res.Cookies.Append(Ten, refreshToken, TuyChon(moiTruong, hetHan));

    /// <summary>
    /// Xoá cookie refresh token (đăng xuất).
    ///
    /// Phải truyền **đúng** `Path` và các thuộc tính như lúc đặt: trình duyệt coi cookie khác
    /// `Path` là cookie khác, nên xoá sai đường dẫn thì cookie cũ **vẫn còn** và người dùng
    /// tưởng đã đăng xuất. Đi qua cùng một `TuyChon` để không thể lệch.
    /// </summary>
    public static void Xoa(HttpResponse res, IWebHostEnvironment moiTruong)
        => res.Cookies.Delete(Ten, TuyChon(moiTruong, null));

    /// <summary>Đọc refresh token từ cookie. `null` khi không có.</summary>
    public static string? Doc(HttpRequest req)
        => req.Cookies.TryGetValue(Ten, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    /// <summary>
    /// Đặt cookie CSRF (**đọc được** bằng JS) cùng lúc với cookie phiên.
    ///
    /// Double-submit: server phát giá trị này trong cookie, frontend đọc rồi gửi lại trong
    /// header <see cref="HeaderCsrf"/>; server so hai bên. Trang khác **không đọc được** cookie
    /// của origin này nên không dựng được header khớp, dù trình duyệt vẫn tự gửi cookie đi.
    ///
    /// `Path` để mặc định `/` vì frontend chạy ở gốc phải đọc được nó.
    /// </summary>
    public static void DatCsrf(
        HttpResponse res, IWebHostEnvironment moiTruong, string giaTri, DateTimeOffset hetHan)
        => res.Cookies.Append(TenCsrf, giaTri, new CookieOptions
        {
            HttpOnly = false, // CỐ Ý: frontend phải đọc được — xem trên
            Secure = !moiTruong.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = hetHan,
        });

    /// <summary>Xoá cookie CSRF. Cùng thuộc tính như lúc đặt, vì lý do đã nêu ở <see cref="Xoa"/>.</summary>
    public static void XoaCsrf(HttpResponse res, IWebHostEnvironment moiTruong)
        => res.Cookies.Delete(TenCsrf, new CookieOptions
        {
            HttpOnly = false,
            Secure = !moiTruong.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });

    /// <summary>
    /// Kiểm double-submit: header phải khớp cookie.
    ///
    /// So bằng <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>
    /// — so chuỗi thường sẽ thoát sớm ở byte đầu khác nhau, về lý thuyết cho biết đoán đúng
    /// được bao nhiêu ký tự đầu.
    /// </summary>
    public static bool CsrfHopLe(HttpRequest req)
    {
        if (!req.Cookies.TryGetValue(TenCsrf, out var cookie) || string.IsNullOrEmpty(cookie))
            return false;

        var header = req.Headers[HeaderCsrf].ToString();
        if (string.IsNullOrEmpty(header)) return false;

        var a = System.Text.Encoding.UTF8.GetBytes(cookie);
        var b = System.Text.Encoding.UTF8.GetBytes(header);
        return a.Length == b.Length
               && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Sinh token CSRF bằng CSPRNG — 32 byte, base64url cho an toàn khi nằm trong cookie.</summary>
    public static string SinhCsrf()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
