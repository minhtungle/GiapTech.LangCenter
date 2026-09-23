namespace GiapTech.LangCenter.API.Middleware;

/// <summary>
/// Đọc domain của request theo cách KHÔNG tin client (ADR-0008, 23/09/2026).
///
/// ## Vì sao không đọc thẳng `Request.Host`
///
/// `Host` là thứ **client gửi lên**, không phải thứ server biết. Tra tenant thẳng từ nó thì
/// kẻ tấn công gửi `Host: vietgeneducation.edu.vn` tới bất kỳ đường nào cũng **tự chọn được
/// tenant**.
///
/// Nguy hiểm gấp đôi vì nginx của dự án `include /etc/nginx/proxy_params`, mà bản mặc định
/// của file đó trên Debian/Ubuntu có `proxy_set_header Host $http_host` — chuyển tiếp nguyên
/// Host của client xuống API.
///
/// ## Cách làm
///
/// Nginx đặt header <see cref="TenHeader"/> bằng `$server_name` — GIÁ TRỊ TRONG CẤU HÌNH,
/// không phải giá trị client gửi. API chỉ đọc header đó khi request đến từ proxy tin cậy;
/// mọi trường hợp khác thì **xoá nó đi** trước khi ai kịp đọc.
///
/// Ở local không có proxy nên header này không bao giờ có — và đó là lý do local chạy đường
/// mã trung tâm, đúng như thiết kế.
/// </summary>
public static class DomainRequest
{
    /// <summary>
    /// Header do NGINX đặt, không phải client. Cấu hình phía nginx:
    /// <code>proxy_set_header X-Tenant-Domain $server_name;</code>
    /// Dùng `$server_name` chứ KHÔNG dùng `$host`/`$http_host` — hai cái sau là của client.
    /// </summary>
    public const string TenHeader = "X-Tenant-Domain";

    /// <summary>
    /// Cấu hình: đứng sau reverse proxy hay không. Dùng lại đúng khoá mà
    /// <c>Program.cs</c> đã dùng cho `UseHttpsRedirection`, để hai nơi không thể lệch nhau —
    /// lệch thì hoặc là bỏ lọt header giả, hoặc là bỏ qua header thật.
    /// </summary>
    public const string KhoaSauProxy = "SAU_REVERSE_PROXY";

    /// <summary>
    /// Mã trung tâm cho đường CÔNG KHAI DỰ PHÒNG `/t/{mã}` (FR-30).
    ///
    /// Trung tâm chưa trỏ domain riêng vẫn phải xem được landing của mình — đó là lý do
    /// ADR-0008 giữ hai đường vào. Frontend ở `/t/{mã}` gửi mã qua header này.
    ///
    /// **Client TỰ ĐẶT được header này, và điều đó CHẤP NHẬN ĐƯỢC** — khác hẳn
    /// <see cref="TenHeader"/>. Lý do: mã trung tâm không phải bí mật (nó nằm trên màn đăng
    /// nhập của chính trung tâm đó), và thứ mở ra được chỉ là **nội dung đã xuất bản công
    /// khai** — thứ vốn dành cho cả Internet.
    ///
    /// Nhưng nó CHỈ được dùng cho nhóm endpoint công khai của LDP. Dùng nó ở chỗ khác là để
    /// client tự chọn tenant, nên middleware giới hạn theo đường dẫn.
    /// </summary>
    public const string TenHeaderMaTrungTam = "X-Ma-Trung-Tam";

    /// <summary>Tiền tố đường dẫn duy nhất chấp nhận <see cref="TenHeaderMaTrungTam"/>.</summary>
    public const string DuongDanCongKhai = "/api/v1/ldp";

    /// <summary>
    /// Lấy domain đáng tin của request, hoặc `null` nếu không có.
    ///
    /// **Có tác dụng phụ có chủ ý:** khi không đứng sau proxy, hàm này XOÁ header khỏi
    /// request. Để nguyên thì một đoạn mã khác đọc phải nó sẽ tin nhầm — thà xoá hẳn còn
    /// hơn dựa vào việc mọi người đọc đều nhớ kiểm.
    /// </summary>
    public static string? LayDomain(HttpContext ctx, bool sauProxy)
    {
        if (!sauProxy)
        {
            // Không có proxy ⇒ header này chỉ có thể do client tự đặt ⇒ giả.
            ctx.Request.Headers.Remove(TenHeader);
            return null;
        }

        var giaTri = ctx.Request.Headers[TenHeader].ToString();
        return string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();
    }
}
