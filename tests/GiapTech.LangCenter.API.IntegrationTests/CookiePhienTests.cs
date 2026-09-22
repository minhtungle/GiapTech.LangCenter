using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using GiapTech.LangCenter.API.Authorization;
using GiapTech.LangCenter.API.Controllers.V1;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Refresh token đi bằng cookie `httpOnly`** — ADR-0007 (22/09/2026), mục 7 của đợt rà soát.
///
/// Trước đây cả hai token nằm trong `localStorage`, nên một lỗ XSS — hoặc một gói npm bị chiếm,
/// thực tế hơn nhiều — lấy được refresh token và mạo danh **30 ngày**. Xoay vòng token không
/// cứu được vì kẻ tấn công cũng xoay vòng theo.
///
/// Cookie `httpOnly` thì JavaScript **không đọc được**: XSS vẫn hại được trong lúc phiên đang
/// mở, nhưng **không mang được phiên đi nơi khác**.
/// </summary>
public class CookiePhienTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<HttpResponseMessage> DangNhap(HttpClient c, ApiFactory f)
        => await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = f.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });

    /// <summary>
    /// Điều cốt lõi nhất: **refresh token KHÔNG nằm trong thân phản hồi**.
    ///
    /// Để nó trong body thì JavaScript vẫn đọc được và toàn bộ ADR-0007 thành vô nghĩa — đây
    /// đúng là cách dễ nhất để vô hiệu hoá thay đổi này mà không ai nhận ra.
    /// </summary>
    [Fact]
    public async Task Refresh_token_KHONG_nam_trong_than_phan_hoi()
    {
        var res = await DangNhap(factory.CreateClient(), factory);
        res.EnsureSuccessStatusCode();

        var tho = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("refreshToken", tho, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", tho, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Chốt cứng **danh sách trường** của `PhienDto`.
    ///
    /// Test trên kiểm chuỗi thô nên bắt được trường tên `refreshToken`; test này bắt cả trường
    /// mang refresh token dưới một cái tên khác (`token`, `rt`…). Ai thêm trường vào DTO phiên
    /// sẽ phải dừng lại sửa test và tự hỏi trường đó có nên lộ cho JavaScript không.
    /// </summary>
    [Fact]
    public void PhienDto_chi_co_dung_bon_truong_da_duyet()
    {
        var truong = typeof(PhienDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(
            new[] { "AccessToken", "HetHan", "PhaiDoiMatKhau", "TokenCsrf" },
            truong);
    }

    /// <summary>Cookie phải có `HttpOnly` — thiếu nó là JavaScript đọc được, tức không vá gì cả.</summary>
    [Fact]
    public async Task Cookie_phien_co_HttpOnly_SameSite_va_Path_hep()
    {
        var res = await DangNhap(factory.CreateClient(), factory);

        var cookie = res.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.StartsWith(CookiePhien.Ten + "=", StringComparison.Ordinal));

        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie!, StringComparison.OrdinalIgnoreCase);
        // `Lax`, KHÔNG phải `Strict`: `Strict` chặn cookie ở điều hướng tài liệu (gõ URL, F5,
        // mở link trực tiếp) nên phá luôn luồng khôi phục phiên — xem `CookiePhien`.
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        // Path hẹp: cookie không đi kèm mọi request nghiệp vụ.
        Assert.Contains($"path={CookiePhien.DuongDan}", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cookie CSRF **KHÔNG** `httpOnly` — cố ý, frontend phải đọc được để gửi lại trong header.
    ///
    /// Kiểm chiều này để người sau không "sửa cho nhất quán" bằng cách thêm `HttpOnly` vào —
    /// làm vậy thì double-submit hỏng và mọi lần làm mới token đều 400.
    /// </summary>
    [Fact]
    public async Task Cookie_CSRF_CO_Y_khong_HttpOnly()
    {
        var res = await DangNhap(factory.CreateClient(), factory);

        var cookie = res.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.StartsWith(CookiePhien.TenCsrf + "=", StringComparison.Ordinal));

        Assert.NotNull(cookie);
        Assert.DoesNotContain("httponly", cookie!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **Ở Production, cookie LUÔN `Secure`.**
    ///
    /// `Secure` tắt ở Development vì dev chạy `http://localhost`. Nhánh tắt đó là rủi ro tự
    /// tạo: bật nhầm ở production nghĩa là refresh token đi qua HTTP thuần. Vì vậy nó suy từ
    /// `IWebHostEnvironment` chứ **không** phải biến cấu hình — và test này chốt điều đó.
    /// </summary>
    [Fact]
    public async Task O_Production_cookie_LUON_Secure()
    {
        using var prod = new ApiFactoryProduction();

        var res = await DangNhap(prod.CreateClient(), prod);

        var cookie = res.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith(CookiePhien.Ten + "=", StringComparison.Ordinal));

        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ApiFactoryProduction : ApiFactory
    {
        protected override string MoiTruong => Microsoft.Extensions.Hosting.Environments.Production;
    }

    /// <summary>
    /// Làm mới token đi bằng **cookie**, không cần body — và trả access token mới.
    /// </summary>
    [Fact]
    public async Task Lam_moi_token_di_bang_COOKIE_khong_can_body()
    {
        // `HttpClient` của factory tự giữ cookie giữa các request (CookieContainer mặc định).
        var c = factory.CreateClient();
        var dn = await DangNhap(c, factory);
        var phien = await dn.Content.ReadFromJsonAsync<JsonElement>();
        var csrf = phien.GetProperty("tokenCsrf").GetString();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/lam-moi-token");
        req.Headers.Add(CookiePhien.HeaderCsrf, csrf);
        var res = await c.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var moi = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(moi.GetProperty("accessToken").GetString()));
    }

    /// <summary>
    /// **Thiếu token CSRF thì bị từ chối.**
    ///
    /// Đây là rủi ro MỚI mà việc chuyển sang cookie mang lại: trình duyệt tự gửi cookie kèm mọi
    /// request, kể cả request do trang khác kích hoạt. `SameSite=Strict` chặn gần hết, nhưng
    /// double-submit là lớp chặn tường minh — và phải có test, nếu không thì nó có mà như không.
    /// </summary>
    [Fact]
    public async Task Thieu_token_CSRF_thi_lam_moi_bi_TU_CHOI()
    {
        var c = factory.CreateClient();
        await DangNhap(c, factory);

        // Cookie phiên vẫn được gửi (client giữ cookie), nhưng KHÔNG có header CSRF.
        var res = await c.PostAsync("/api/v1/auth/lam-moi-token", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CSRF_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Token CSRF SAI cũng bị từ chối — không chỉ thiếu.</summary>
    [Fact]
    public async Task Token_CSRF_SAI_cung_bi_tu_choi()
    {
        var c = factory.CreateClient();
        await DangNhap(c, factory);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/lam-moi-token");
        req.Headers.Add(CookiePhien.HeaderCsrf, "gia-mao-khong-khop");
        var res = await c.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// **Đổi mật khẩu KHÔNG được giết phiên của chính mình.**
    ///
    /// `DoiMatKhauCommand` thu hồi **mọi** refresh token — đúng, vì đổi mật khẩu thường là
    /// phản ứng khi nghi bị lộ. Nhưng "mọi" gồm cả token của **người đang đổi**, nên nếu không
    /// cấp lại thì họ mất đường khôi phục phiên: từ ADR-0007 access token nằm trong RAM, tải
    /// lại trang là văng về màn đăng nhập ngay sau khi vừa đổi mật khẩu xong.
    ///
    /// Lỗi này có từ trước ADR-0007 nhưng `localStorage` che đi (access token còn sống 60
    /// phút). Test kiểm **refresh token mới dùng được** — đó mới là thứ chứng minh phiên còn
    /// khôi phục được.
    /// </summary>
    [Fact]
    public async Task Doi_mat_khau_KHONG_giet_phien_cua_chinh_minh()
    {
        // Tài khoản riêng để không ảnh hưởng test khác (đổi mật khẩu là thao tác một chiều).
        using var f = new ApiFactory();
        var c = f.CreateClient();

        var tao = await c.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm đổi mật khẩu" });
        var tt = await tao.Content.ReadFromJsonAsync<JsonElement>();

        var dn = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = tt.GetProperty("maTrungTam").GetString(),
            Username = tt.GetProperty("username").GetString(),
            MatKhau = tt.GetProperty("matKhau").GetString(),
        });
        var phien = await dn.Content.ReadFromJsonAsync<JsonElement>();

        var doi = f.CreateClient();
        doi.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", phien.GetProperty("accessToken").GetString());

        var res = await doi.PostAsJsonAsync("/api/v1/auth/doi-mat-khau", new
        {
            MatKhauCu = tt.GetProperty("matKhau").GetString(),
            MatKhauMoi = "mat-khau-moi-du-dai-123",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Phải có cookie MỚI, và nó phải DÙNG ĐƯỢC để làm mới phiên.
        var rtMoi = TroGiupPhien.LayCookie(res, CookiePhien.Ten);
        Assert.False(string.IsNullOrEmpty(rtMoi), "Đổi mật khẩu phải cấp lại cookie phiên");

        var csrfMoi = (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("tokenCsrf").GetString()!;

        var lamMoi = await TroGiupPhien.LamMoiAsync(f.CreateClient(), rtMoi!, csrfMoi);
        Assert.Equal(HttpStatusCode.OK, lamMoi.StatusCode);
    }

    /// <summary>
    /// Đăng xuất **xoá cookie**. Thiếu bước này thì trình duyệt vẫn gửi refresh token cũ và
    /// người dùng nhận một vòng 400 vô nghĩa mỗi lần mở app.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_XOA_cookie_phien()
    {
        var c = factory.CreateClient();
        var dn = await DangNhap(c, factory);
        var phien = await dn.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", phien.GetProperty("accessToken").GetString());

        var res = await c.PostAsync("/api/v1/auth/dang-xuat", null);

        var cookies = res.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : [];
        var xoa = cookies.FirstOrDefault(x => x.StartsWith(CookiePhien.Ten + "=", StringComparison.Ordinal));

        Assert.NotNull(xoa);
        // Xoá = đặt giá trị rỗng + hạn trong quá khứ.
        Assert.Contains("expires=Thu, 01 Jan 1970", xoa!, StringComparison.OrdinalIgnoreCase);
    }
}
