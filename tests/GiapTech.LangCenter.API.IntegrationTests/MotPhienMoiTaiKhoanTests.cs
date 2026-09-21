using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Một phiên mỗi tài khoản** (20/09/2026) — yêu cầu chủ sản phẩm *"chỉ cho phép 1 người đăng
/// nhập tài khoản cùng lúc"*.
///
/// Chốt phương án: **đẩy phiên CŨ ra** (người vừa đăng nhập được vào, như Facebook/Zalo) và
/// **có hiệu lực ngay**, không chờ access token hết hạn.
///
/// ## Vì sao không chỉ thu hồi refresh token
///
/// JWT là stateless — server không tra DB mỗi request. Thu hồi refresh token thôi thì phiên cũ
/// vẫn gọi API bình thường tới **60 phút** (hạn access token). Một tiếng hai người dùng song
/// song thì không còn là "chỉ 1 người cùng lúc". Nên phải chặn ở middleware.
/// </summary>
public class MotPhienMoiTaiKhoanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Đăng nhập và trả về CẢ HAI token — test cần cả access lẫn refresh.</summary>
    private async Task<(string Access, string Refresh)> DangNhap(string user, string mk)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("accessToken").GetString()!,
                body.GetProperty("refreshToken").GetString()!);
    }

    private HttpClient Voi(string accessToken)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

    /// <summary>
    /// Phiên thứ hai đẩy phiên thứ nhất ra — và người MỚI vào được ngay.
    ///
    /// Chiều thứ hai quan trọng không kém: bản đầu của tôi cache `phien_hien_tai` mà quên xoá
    /// lúc đăng nhập, nên **chính người vừa đăng nhập cũng nhận 401** cho tới khi cache hết
    /// hạn. Chỉ kiểm "A bị đẩy ra" thì lỗi đó vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Dang_nhap_moi_day_phien_cu_ra_va_nguoi_moi_vao_duoc_ngay()
    {
        var (accessA, _) = await DangNhap("manager", "manager123");
        Assert.Equal(HttpStatusCode.OK, (await Voi(accessA).GetAsync("/api/v1/toi/quyen")).StatusCode);

        var (accessB, _) = await DangNhap("manager", "manager123");

        // KHÔNG chờ: người vừa đăng nhập phải dùng được ngay, không sau vài giây.
        Assert.Equal(HttpStatusCode.OK, (await Voi(accessB).GetAsync("/api/v1/toi/quyen")).StatusCode);

        var resA = await Voi(accessA).GetAsync("/api/v1/toi/quyen");
        Assert.Equal(HttpStatusCode.Unauthorized, resA.StatusCode);
        // Mã RIÊNG, không dùng lại `CHUA_XAC_THUC`: người dùng cần biết tài khoản mình vừa được
        // dùng ở máy khác — đó có thể là dấu hiệu lộ mật khẩu.
        Assert.Contains("PHIEN_DA_BI_DAY_RA", await resA.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Phiên cũ KHÔNG tự sống lại bằng refresh token.
    ///
    /// Thiếu chốt này thì máy cũ âm thầm gia hạn và hai người dùng song song mãi — đúng cái
    /// tính năng sinh ra để chặn.
    /// </summary>
    [Fact]
    public async Task Phien_cu_khong_lam_moi_token_de_song_lai_duoc()
    {
        var (_, refreshA) = await DangNhap("manager", "manager123");
        await DangNhap("manager", "manager123");   // B đăng nhập, A bị đẩy ra

        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = refreshA });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Hai tài khoản KHÁC NHAU không ảnh hưởng nhau — chốt chặn phải hẹp đúng một tài khoản.
    ///
    /// Chiều ngược của mọi test trên: một bản sửa quá tay (đẩy ra theo tenant, hoặc so nhầm
    /// khoá cache) sẽ làm người này đăng nhập đá văng người kia. Không có test này thì lỗi đó
    /// lọt, và nó sẽ hỏng cả hệ thống chứ không chỉ một tính năng.
    /// </summary>
    [Fact]
    public async Task Tai_khoan_khac_nhau_khong_day_nhau_ra()
    {
        var (accessQuanLy, _) = await DangNhap("manager", "manager123");
        var (accessKhac, _) = await DangNhap("player", "player123");

        // Cả hai phải cùng dùng được — khác tài khoản thì khác phiên.
        Assert.Equal(HttpStatusCode.OK,
            (await Voi(accessQuanLy).GetAsync("/api/v1/toi/quyen")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Voi(accessKhac).GetAsync("/api/v1/toi/quyen")).StatusCode);
    }
}
