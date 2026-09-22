using System.Net.Http.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Header bảo mật ở tầng ứng dụng** (22/09/2026) — mục 5 của đợt rà soát bảo mật.
///
/// Trước đây toàn bộ header chỉ có trong `deploy/nginx/langcenter.conf` — tệp **phải copy tay
/// lên VPS**. Cài sai, quên reload, hay dựng môi trường mới mà bỏ sót thì production chạy
/// không HSTS, không `nosniff`, không `X-Frame-Options`. Chú thích trong chính tệp nginx xác
/// nhận việc này **đã từng xảy ra**.
///
/// Middleware không thay Nginx mà là **lớp đáy**: phần API không bao giờ thiếu header, dù đứng
/// sau proxy nào, dù ai quên cấu hình gì.
/// </summary>
public class HeaderBaoMatTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    public async Task Moi_phan_hoi_deu_co_header_bao_mat(string ten, string giaTri)
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/tinh-nang");

        Assert.True(res.Headers.TryGetValues(ten, out var gt), $"Thiếu header {ten}");
        Assert.Equal(giaTri, gt!.First());
    }

    /// <summary>
    /// Header phải có **cả trên phản hồi LỖI**.
    ///
    /// Đây là nửa hay bị quên, và là nửa quan trọng hơn: trang lỗi chính là thứ kẻ tấn công
    /// muốn nhúng vào iframe. Nginx phải dùng `always` vì cùng lý do — không có nó thì Nginx
    /// bỏ header với 4xx/5xx.
    /// </summary>
    [Fact]
    public async Task Header_co_ca_tren_phan_hoi_LOI()
    {
        // 401: chưa đăng nhập mà gọi endpoint cần quyền.
        var res = await factory.CreateClient().GetAsync("/api/v1/toi/quyen");

        Assert.True(res.Headers.Contains("X-Content-Type-Options"),
            "Phản hồi lỗi thiếu header bảo mật — xem chú thích trong HeaderBaoMatMiddleware.");
    }

    /// <summary>
    /// **KHÔNG gửi HSTS qua HTTP thuần.**
    ///
    /// Gửi qua HTTP là vô nghĩa (trình duyệt bỏ qua theo chuẩn), nhưng ở môi trường dev chạy
    /// `http://localhost` thì nó **khoá cả localhost sang HTTPS** trong trình duyệt của lập
    /// trình viên — lỗi rất khó chẩn đoán vì nó nằm trong cache trình duyệt, không nằm trong mã.
    /// </summary>
    [Fact]
    public async Task KHONG_gui_HSTS_qua_HTTP_thuan()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/tinh-nang");

        Assert.False(res.Headers.Contains("Strict-Transport-Security"),
            "Gửi HSTS qua HTTP thuần sẽ khoá localhost sang HTTPS trong trình duyệt dev.");
    }
}
